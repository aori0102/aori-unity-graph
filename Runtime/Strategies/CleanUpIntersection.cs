using System.Collections.Generic;
using System.Linq;
using Aori.Graph.Dependencies;
using Aori.Graph.Utils;

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
            var nodesToRemove = new HashSet<GraphNode>();
            foreach (var intersection in _context.IntersectingNodeSet)
            {
                foreach (var neighbor in intersection.Neighbors)
                {
                    if(IsInsideAnyCluster(neighbor) || IsInsideAnyCluster(intersection, neighbor))
                    {
                        nodesToRemove.Add(intersection);
                    }
                }
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

        private bool IsInsideAnyCluster(GraphNode first, GraphNode second)
        {
            return _context.ClusterList.Any(cluster =>
                Math.IsPointInsidePolygon(
                    point: Math.ToXZ((first.Position + second.Position) / 2f),
                    vertices: cluster.OrderedNodes
                        .Select(candidate => Math.ToXZ(candidate.Position))
                        .ToList()
                )
            );
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
    }
}