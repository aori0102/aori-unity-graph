using System.Collections.Generic;
using System.Linq;
using Aori.Graph.Dependencies;
using Aori.Graph.Utils;

namespace Aori.Graph.Strategies
{
    internal sealed class SnapNonShellEdgesToNearbyVertices : BuildPhaseStrategy
    {
        public SnapNonShellEdgesToNearbyVertices(
            GraphContext context,
            GraphSystem system
        ) : base(context, system)
        { }

        public override void Execute()
        {
            var snapDistance = _context.Config.EdgeBlendDistance;
            if (snapDistance <= 0f)
            {
                return;
            }

            var snapDistanceSquared = snapDistance * snapDistance;
            var nonShellEdgeList = CollectNonShellEdges();

            foreach (var edge in nonShellEdgeList)
            {
                if (!edge.First.HasEdgeTo(edge.Second))
                {
                    continue;
                }

                var snapNodeList = FindSnapCandidateNodes(edge, snapDistanceSquared);
                if (snapNodeList.Count == 0)
                {
                    continue;
                }

                edge.First.RemoveNeighbor(edge.Second);
                edge.Second.RemoveNeighbor(edge.First);

                var chainNodeList = new List<GraphNode>(snapNodeList.Count + 2) { edge.First };
                chainNodeList.AddRange(snapNodeList);
                chainNodeList.Add(edge.Second);

                for (var i = 0; i < chainNodeList.Count - 1; i++)
                {
                    var current = chainNodeList[i];
                    var next = chainNodeList[i + 1];
                    if (ReferenceEquals(current, next))
                    {
                        continue;
                    }

                    current.AddNeighbor(next);
                    next.AddNeighbor(current);
                }
            }
        }

        /// <summary>
        /// Collects all non-shell edges currently present in the graph.
        /// </summary>
        /// <returns>List of undirected edges that are not part of cluster shells.</returns>
        private List<EdgeKey> CollectNonShellEdges()
        {
            var edgeSet = new HashSet<EdgeKey>();
            var edgeToAdd
                = _context.NodeSet
                    .SelectMany(node => node.Neighbors, (node, neighbor) => new EdgeKey(node, neighbor))
                    .Where(edge => !_context.ClusterShellEdgeSet.Contains(edge));
            foreach (var edge in edgeToAdd)
            {
                edgeSet.Add(edge);
            }

            return edgeSet.ToList();
        }

        /// <summary>
        /// Finds vertices that lie close to the specified non-shell edge and can be used to split it.
        /// Candidates are ordered by projected parameter along the edge.
        /// </summary>
        /// <param name="edge">Edge to test for nearby vertices.</param>
        /// <param name="snapDistanceSquared">Maximum squared distance from edge projection to qualify.</param>
        /// <returns>Ordered distinct list of snap candidate nodes.</returns>
        private List<GraphNode> FindSnapCandidateNodes(
            EdgeKey edge,
            float snapDistanceSquared
        )
        {
            var projectedCandidateList = new List<ProjectedSnapCandidate>();

            foreach (var candidateNode in _context.NodeSet)
            {
                if (ReferenceEquals(candidateNode, edge.First) ||
                    ReferenceEquals(candidateNode, edge.Second))
                {
                    continue;
                }

                if (!Math.TryProjectPointToSegmentXZ(
                        point: candidateNode.Position,
                        segmentStart: edge.First.Position,
                        segmentEnd: edge.Second.Position,
                        out var t,
                        out var distanceSquared
                    ))
                {
                    continue;
                }

                if (distanceSquared > snapDistanceSquared)
                {
                    continue;
                }

                projectedCandidateList.Add(new ProjectedSnapCandidate(candidateNode, t, distanceSquared));
            }

            return projectedCandidateList
                .OrderBy(candidate => candidate.Parameter)
                .ThenBy(candidate => candidate.DistanceSquared)
                .Select(candidate => candidate.Node)
                .Distinct()
                .ToList();
        }
    }
}