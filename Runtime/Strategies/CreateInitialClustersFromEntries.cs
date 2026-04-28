using System.Linq;
using Aori.Graph.Dependencies;
using Aori.Graph.Utils;
using UnityEngine;

namespace Aori.Graph.Strategies
{
    /// <summary>
    /// Creates and assigned <see cref="GraphNode"/>, then creates clusters for each
    /// entry in on <see cref="GraphContext.EntryList"/>.
    /// </summary>
    internal sealed class CreateInitialClustersFromEntries : BuildPhaseStrategy
    {
        public CreateInitialClustersFromEntries(
            GraphContext context,
            GraphSystem system
        ) : base(context, system)
        { }

        public override void Execute()
        {
            foreach (var entry in _context.EntryList)
            {
                // Create nodes from each point entry
                var nodes = entry.GraphNodePoints
                    .Where(point => point)
                    .Select(point => new GraphNode(point.position))
                    .ToList();

                if (nodes.Count == 0)
                {
                    continue;
                }

                // Register all nodes
                foreach (var node in nodes)
                {
                    _context.NodeSet.Add(node);
                }

                // Define a new cluster with the created nodes
                var cluster = new Cluster();
                cluster.SetOrderedNodes(nodes);

                Debug.Log(
                    $"Cluster number {_context.ClusterList.Count} corresponding to entry {entry.GetInstanceID()} of {entry.transform.parent?.name}");

                _context.ClusterList.Add(cluster);
            }
        }
    }
}