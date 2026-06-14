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
            var query = _context.IntersectingNodeSet
                .SelectMany(intersection => intersection.Neighbors)
                .Where(IsInsideAnyCluster)
                .ToArray();
            foreach (var candidate in query)
            {
                nodesToRemove.Add(candidate);
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
    }
}