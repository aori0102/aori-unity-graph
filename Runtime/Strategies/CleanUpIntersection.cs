using System.Collections.Generic;
using System.Linq;
using Aori.Graph.Dependencies;
using Aori.Graph.Utils;
using UnityEngine;

namespace Aori.Graph.Strategies
{
    internal sealed class CleanUpIntersection : BuildPhaseStrategy
    {
        public CleanUpIntersection(
            GraphContext context,
            GraphSystem system
        ) : base(context, system)
        { }

        public override void Execute()
        {
            foreach (var cluster in _context.ClusterList)
            {
                if (cluster.OrderedNodes.Count < 2)
                {
                    continue;
                }

                var clusterNodes = cluster.OrderedNodes.ToList();
                var nodesToRemove = new HashSet<GraphNode>();
                var edgesToRemove = new HashSet<EdgeKey>();
                var edgesToRebuild = new HashSet<EdgeKey>();

                ProcessCluster(
                    cluster,
                    clusterNodes,
                    edgesToRemove,
                    nodesToRemove
                );

                CalculateRebuildEdges(
                    clusterNodes,
                    nodesToRemove,
                    edgesToRebuild
                );

                foreach (var removeEdge in edgesToRemove)
                {
                    removeEdge.First.RemoveNeighbor(removeEdge.Second);
                    removeEdge.Second.RemoveNeighbor(removeEdge.First);

                    _context.ClusterShellEdgeSet.Remove(removeEdge);
                }

                foreach (var rebuildEdge in edgesToRebuild)
                {
                    rebuildEdge.First.AddNeighbor(rebuildEdge.Second);
                    rebuildEdge.Second.AddNeighbor(rebuildEdge.First);
                }

                foreach (var removeNode in nodesToRemove)
                {
                    foreach (var neighbor in removeNode.Neighbors)
                    {
                        neighbor.RemoveNeighbor(removeNode);
                    }

                    clusterNodes.Remove(removeNode);
                    _context.NodeSet.Remove(removeNode);
                }

                cluster.SetOrderedNodes(clusterNodes);
            }
        }

        private void CalculateRebuildEdges(
            List<GraphNode> clusterNodes,
            HashSet<GraphNode> nodesToRemove,
            HashSet<EdgeKey> edgesToRebuild)
        {
            var nodeCount = clusterNodes.Count;
            foreach (var node in clusterNodes.Where(nodesToRemove.Contains))
            {
                var index = clusterNodes.IndexOf(node);

                GraphNode previous = null;
                GraphNode next = null;

                for (var i = 0; i < nodeCount - 1; i++)
                {
                    var previousIndex = (nodeCount + index - i - 1) % nodeCount;
                    var nextIndex = (index + i + 1) % nodeCount;

                    previous ??= nodesToRemove.Contains(clusterNodes[previousIndex])
                        ? null
                        : clusterNodes[previousIndex];

                    next ??= nodesToRemove.Contains(clusterNodes[nextIndex])
                        ? null
                        : clusterNodes[nextIndex];
                }

                if (previous != null && next != null && !ReferenceEquals(previous, next))
                {
                    edgesToRebuild.Add(new EdgeKey(previous, next));
                }
            }
        }

        private void ProcessCluster(
            Cluster cluster,
            IReadOnlyList<GraphNode> clusterNodes,
            HashSet<EdgeKey> edgesToRemove,
            HashSet<GraphNode> nodesToRemove
        )
        {
            for (var edgeIndex = 0; edgeIndex < clusterNodes.Count; edgeIndex++)
            {
                var first = clusterNodes[edgeIndex];
                var second = clusterNodes[(edgeIndex + 1) % clusterNodes.Count];
                var edge = new EdgeKey(first, second);

                var firstIsSplitPoint
                    = _context.IntersectingNodeSet.Contains(edge.First);
                var secondIsSplitPoint
                    = _context.IntersectingNodeSet.Contains(edge.Second);

                if (firstIsSplitPoint)
                {
                    var ignoreList = new List<Cluster>
                    {
                        cluster, GetIntersectedCluster(edge.First, cluster)
                    };
                    if (IsInsideAnyCluster(Math.ToXZ(edge.First.Position), ignoreList))
                    {
                        nodesToRemove.Add(edge.First);
                    }
                }

                if (secondIsSplitPoint)
                {
                    var ignoreList = new List<Cluster>
                    {
                        cluster, GetIntersectedCluster(edge.Second, cluster)
                    };
                    if (IsInsideAnyCluster(Math.ToXZ(edge.Second.Position), ignoreList))
                    {
                        nodesToRemove.Add(edge.Second);
                    }
                }

                switch (firstIsSplitPoint)
                {
                    case true when secondIsSplitPoint:
                    {
                        var ignoreList = new List<Cluster>
                        {
                            cluster,
                            GetIntersectedCluster(edge.First, cluster),
                            GetIntersectedCluster(edge.Second, cluster)
                        };

                        var anyIntra = false; // If any nodes are inside any clusters.
                        if (IsInsideAnyCluster(Math.ToXZ(edge.First.Position), ignoreList))
                        {
                            anyIntra = true;
                            nodesToRemove.Add(edge.First);
                        }

                        if (IsInsideAnyCluster(Math.ToXZ(edge.Second.Position), ignoreList))
                        {
                            anyIntra = true;
                            nodesToRemove.Add(edge.Second);
                        }

                        if (!anyIntra)
                        {
                            edgesToRemove.Add(edge);
                        }

                        break;
                    }
                    case false when !secondIsSplitPoint:
                    {
                        if (IsInsideAnyCluster(Math.ToXZ(edge.First.Position), cluster))
                        {
                            nodesToRemove.Add(edge.First);
                        }

                        if (IsInsideAnyCluster(Math.ToXZ(edge.Second.Position), cluster))
                        {
                            nodesToRemove.Add(edge.Second);
                        }

                        break;
                    }
                }
            }
        }

        private Cluster GetIntersectedCluster(GraphNode intersection, Cluster firstCluster)
        {
            return _context.ClusterList.FirstOrDefault(otherCluster =>
                !ReferenceEquals(otherCluster, firstCluster) &&
                otherCluster.Nodes.Contains(intersection)
            );
        }

        private bool IsInsideAnyCluster(Vector2 point, Cluster ignoredCluster)
        {
            return _context.ClusterList.Any(otherCluster =>
                !ReferenceEquals(ignoredCluster, otherCluster) &&
                Math.IsPointInsidePolygon(
                    point: point,
                    vertices: otherCluster.OrderedNodes
                        .Select(node => Math.ToXZ(node.Position))
                        .ToList()
                )
            );
        }

        private bool IsInsideAnyCluster(Vector2 point, List<Cluster> ignoredClusters)
        {
            return _context.ClusterList.Any(otherCluster =>
                !ignoredClusters.Contains(otherCluster) &&
                Math.IsPointInsidePolygon(
                    point: point,
                    vertices: otherCluster.OrderedNodes
                        .Select(node => Math.ToXZ(node.Position))
                        .ToList()
                )
            );
        }
    }
}