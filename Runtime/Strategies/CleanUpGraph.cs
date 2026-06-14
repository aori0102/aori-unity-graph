using System.Collections.Generic;
using System.Linq;
using Aori.Graph.Dependencies;
using Aori.Graph.Utils;

namespace Aori.Graph.Strategies
{
    internal sealed class CleanUpGraph : BuildPhaseStrategy
    {
        public CleanUpGraph(GraphContext context, GraphSystem system)
            : base(context, system)
        { }

        public override void Execute()
        {
            CleanUpIntraConnections();
            CleanUpIntraNodes();
        }

        private void CleanUpIntraConnections()
        {
            var edgesToRemove = new HashSet<EdgeKey>();
            foreach (var first in _context.NodeSet)
            {
                foreach (var second in first.Neighbors)
                {
                    var edge = new EdgeKey(first, second);
                    if (IsIntersection(first) &&
                        IsIntersection(second) &&
                        IsEdgeIntraConnected(first, second))
                    {
                        edgesToRemove.Add(edge);
                    }
                }
            }

            foreach (var edge in edgesToRemove)
            {
                edge.First.RemoveNeighbor(edge.Second);
                edge.Second.RemoveNeighbor(edge.First);
            }
        }

        private bool IsIntersection(GraphNode node)
        {
            return _context.IntersectingNodeSet.Contains(node);
        }

        private void CleanUpIntraNodes()
        {
            var nodesToRemove = new HashSet<GraphNode>();
            foreach (var node in _context.NodeSet.Where(IsInsideAnyCluster))
            {
                nodesToRemove.Add(node);
            }

            foreach (var node in nodesToRemove)
            {
                foreach (var neighbor in node.Neighbors)
                {
                    neighbor.RemoveNeighbor(node);
                }

                _context.NodeSet.Remove(node);
            }
        }

        private bool IsInsideAnyCluster(GraphNode node)
        {
            var ignoreList = _context.ClusterList.Where(cluster => cluster.Nodes.Contains(node));
            return _context.ClusterList.Any(otherCluster =>
                !ignoreList.Contains(otherCluster) &&
                Math.IsPointInsidePolygon(
                    point: Math.ToXZ(node.Position),
                    vertices: otherCluster.OrderedNodes
                        .Select(candidate => Math.ToXZ(candidate.Position))
                        .ToList()
                )
            );
        }

        private bool IsEdgeIntraConnected(GraphNode first, GraphNode second)
        {
            var midpoint = (first.Position + second.Position) / 2f;
            return _context.ClusterList.Any(otherCluster =>
                Math.IsPointInsidePolygon(
                    point: Math.ToXZ(midpoint),
                    vertices: otherCluster.OrderedNodes
                        .Select(candidate => Math.ToXZ(candidate.Position))
                        .ToList()
                )
            );
        }
    }
}