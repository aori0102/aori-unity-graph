using Aori.Graph.Utils;

namespace Aori.Graph.Strategies
{
    internal sealed class BuildClusterShell : BuildPhaseStrategy
    {
        public BuildClusterShell(
            GraphContext context,
            GraphSystem system
        ) : base(context, system)
        { }

        public override void Execute()
        {
            foreach (var cluster in _context.ClusterList)
            {
                for (var i = 0; i < cluster.OrderedNodes.Count; i++)
                {
                    var first
                        = cluster.OrderedNodes[i];
                    var second
                        = cluster.OrderedNodes[(i + 1) % cluster.OrderedNodes.Count];
                    
                    first.AddNeighbor(second);
                    second.AddNeighbor(first);

                    _context.ClusterShellEdgeSet.Add(new EdgeKey(first, second));
                }
            }
        }
    }
}