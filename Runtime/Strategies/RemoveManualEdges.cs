using System.Collections.Generic;
using System.Linq;
using Aori.Graph.Utils;

namespace Aori.Graph.Strategies
{
    internal sealed class RemoveManualEdges : BuildPhaseStrategy
    {
        public RemoveManualEdges(GraphContext context, GraphSystem system)
            : base(context, system)
        { }

        public override void Execute()
        {
            var edgesToRemove = new HashSet<EdgeKey>();
            foreach (var node in _context.NodeSet)
            {
                foreach (var neighbor in node.Neighbors)
                {
                    if (_context.EdgeRemoverList.Any(remover =>
                            remover.DoesEdgeIntersects(node.Position, neighbor.Position)))
                    {
                        edgesToRemove.Add(new EdgeKey(node, neighbor));
                    }
                }
            }

            foreach (var edge in edgesToRemove)
            {
                edge.First.RemoveNeighbor(edge.Second);
                edge.Second.RemoveNeighbor(edge.First);
            }
        }
    }
}