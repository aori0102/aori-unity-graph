using System.Collections.Generic;
using Aori.Graph.Dependencies;
using Aori.Graph.Utils;
using UnityEngine;

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
            var edgesToBuild = new HashSet<EdgeKey>();
            foreach (var cluster in _context.ClusterList)
            {
                for (var i = 0; i < cluster.OrderedNodes.Count; i++)
                {
                    var first
                        = cluster.OrderedNodes[i];
                    var second
                        = cluster.OrderedNodes[(i + 1) % cluster.OrderedNodes.Count];

                    edgesToBuild.Add(new EdgeKey(first, second));
                }

                if (!cluster.AllowIntraConnections)
                {
                    continue;
                }

                for (var firstIndex = 0;
                     firstIndex < cluster.OrderedNodes.Count - 1;
                     firstIndex++)
                {
                    for (var secondIndex = firstIndex + 1;
                         secondIndex < cluster.OrderedNodes.Count;
                         secondIndex++)
                    {
                        var first = cluster.OrderedNodes[firstIndex];
                        var second = cluster.OrderedNodes[secondIndex];
                        var edgeKey = new EdgeKey(first, second);

                        if (PassesConnectionAngleThreshold(first, second))
                        {
                            edgesToBuild.Add(edgeKey);
                        }
                    }
                }
            }

            foreach (var edge in edgesToBuild)
            {
                edge.First.AddNeighbor(edge.Second);
                edge.Second.AddNeighbor(edge.First);
                _context.ClusterShellEdgeSet.Add(edge);
            }
        }

        /// <summary>
        /// Applies per-node angle threshold checks for a candidate cross-cluster connection.
        /// </summary>
        /// <param name="firstNode">First endpoint.</param>
        /// <param name="secondNode">Second endpoint.</param>
        /// <returns>True when both endpoints satisfy minimum allowed angle constraints.</returns>
        private bool PassesConnectionAngleThreshold(GraphNode firstNode, GraphNode secondNode)
        {
            var minAllowedAngle = _context.Config.DissolveThreshold * Mathf.Deg2Rad;
            if (minAllowedAngle <= 0f)
            {
                return true;
            }

            return
                PassesNodeAngleThreshold(firstNode, secondNode, minAllowedAngle) &&
                PassesNodeAngleThreshold(secondNode, firstNode, minAllowedAngle);
        }

        /// <summary>
        /// Checks whether adding a candidate neighbor at a node violates minimum angle constraints.
        /// Shell edges are ignored so boundary edges do not block new inter-cluster links.
        /// </summary>
        /// <param name="centerNode">Node receiving the candidate connection.</param>
        /// <param name="candidateNeighbor">Candidate neighbor to validate.</param>
        /// <param name="minAllowedAngle">Minimum allowed angle in radians.</param>
        /// <returns>True if all considered angles satisfy the threshold.</returns>
        private bool PassesNodeAngleThreshold(
            GraphNode centerNode,
            GraphNode candidateNeighbor,
            float minAllowedAngle
        )
        {
            foreach (var existingNeighbor in centerNode.Neighbors)
            {
                // Shell edges define cluster boundaries; do not use them to block inter-cluster links.
                if (_context.ClusterShellEdgeSet.Contains(new EdgeKey(centerNode, existingNeighbor)))
                {
                    continue;
                }

                var angle = Math.CalculateAngleBetweenEdges(
                    center: centerNode.Position,
                    firstNeighbor: existingNeighbor.Position,
                    secondNeighbor: candidateNeighbor.Position
                );

                // dissolveThreshold is the minimum allowed angle.
                if (angle < minAllowedAngle)
                {
                    return false;
                }
            }

            return true;
        }
    }
}