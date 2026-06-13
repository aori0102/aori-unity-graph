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

                switch (firstIsSplitPoint)
                {
                    // Neither is an intersecting node.
                    case false when !secondIsSplitPoint:
                        continue;

                    // Both are intersecting nodes.
                    case true when secondIsSplitPoint:
                    {
                        var midpoint
                            = (edge.First.Position + edge.Second.Position) / 2f;
                        if (IsInsideAnyCluster(Math.ToXZ(midpoint), cluster))
                        {
                            edgesToRemove.Add(edge);
                        }

                        continue;
                    }

                    // The first node of the edge is an intersecting node.
                    case true when IsInsideAnyCluster(edge.Second, cluster):
                        edgesToRemove.Add(edge);
                        nodesToRemove.Add(edge.Second);

                        continue;

                    // The second node of the edge is an intersecting node.
                    case false when IsInsideAnyCluster(edge.First, cluster):
                        edgesToRemove.Add(edge);
                        nodesToRemove.Add(edge.First);

                        continue;
                }
            }
        }

        private bool IsInsideAnyCluster(GraphNode node, Cluster currentCluster)
        {
            return IsInsideAnyCluster(
                point: Math.ToXZ(node.Position),
                currentCluster: currentCluster
            );
        }

        private bool IsInsideAnyCluster(Vector2 point, Cluster currentCluster)
        {
            return _context.ClusterList.Any(otherCluster =>
                !ReferenceEquals(currentCluster, otherCluster) &&
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