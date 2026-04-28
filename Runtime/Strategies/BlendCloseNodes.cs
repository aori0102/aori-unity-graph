using System.Collections.Generic;
using System.Linq;
using Aori.Graph.Dependencies;
using Aori.Graph.Utils;
using UnityEngine;

namespace Aori.Graph.Strategies
{
    /// <summary>
    /// Blends every pair of nodes whose distance is less than a certain threshold,
    /// represented by <see cref="GraphConfig.VertexBlendDistance"/>.
    /// </summary>
    internal sealed class BlendCloseNodes : BuildPhaseStrategy
    {
        public BlendCloseNodes(GraphContext context, GraphSystem system) : base(context, system)
        { }

        public override void Execute()
        {
            var blendDistance = _context.Config.VertexBlendDistance;
            if (blendDistance <= 0f)
            {
                return;
            }

            var needCheck = true;
            while (needCheck)
            {
                needCheck = false;

                var nodeList = _context.NodeSet.ToList();
                var nodeCount = nodeList.Count;

                var replacementMap = new Dictionary<GraphNode, GraphNode>();

                for (var firstIndex = 0; firstIndex < nodeCount; firstIndex++)
                {
                    var firstNode = nodeList[firstIndex];
                    if (replacementMap.ContainsKey(firstNode))
                    {
                        continue;
                    }

                    for (var secondIndex = firstIndex + 1; secondIndex < nodeCount; secondIndex++)
                    {
                        var secondNode = nodeList[secondIndex];
                        if (replacementMap.ContainsKey(secondNode))
                        {
                            continue;
                        }

                        var distance
                            = Vector3.Distance(firstNode.Position, secondNode.Position);
                        if (!(distance < blendDistance))
                        {
                            continue;
                        }

                        needCheck = true;

                        var midPoint = (firstNode.Position + secondNode.Position) / 2f;
                        var replacement = new GraphNode(midPoint);
                        replacementMap[firstNode] = replacement;
                        replacementMap[secondNode] = replacement;
                    }
                }

                foreach (var (removal, replacement) in replacementMap)
                {
                    foreach (var neighbor in removal.Neighbors)
                    {
                        neighbor.RemoveNeighbor(removal);

                        replacement.AddNeighbor(neighbor);
                        neighbor.AddNeighbor(replacement);
                    }

                    _context.NodeSet.Remove(removal);
                    _context.NodeSet.Add(replacement);
                }
            }
        }
    }
}