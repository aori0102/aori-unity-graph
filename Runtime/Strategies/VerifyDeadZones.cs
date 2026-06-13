using System.Collections.Generic;
using System.Linq;
using Aori.Graph.Dependencies;
using Aori.Graph.Utils;

namespace Aori.Graph.Strategies
{
    internal sealed class VerifyDeadZones : BuildPhaseStrategy
    {
        public VerifyDeadZones(GraphContext context, GraphSystem system)
            : base(context, system)
        { }

        public override void Execute()
        {
            var removalSet = new HashSet<GraphNode>();
            foreach (var node in _context.NodeSet
                         .Where(node =>
                             _context.DeadZoneList.Any(zone =>
                                 zone.IsNodeWithinDeadZone(node))))
            {
                removalSet.Add(node);
            }

            foreach (var node in removalSet)
            {
                foreach (var neighbor in node.Neighbors)
                {
                    neighbor.RemoveNeighbor(node);
                }

                _context.NodeSet.Remove(node);
            }
        }
    }
}