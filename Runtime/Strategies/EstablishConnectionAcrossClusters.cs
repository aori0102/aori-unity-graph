using System.Collections.Generic;
using System.Linq;
using Aori.DSA;
using Aori.Exception;
using Aori.Graph.Dependencies;
using Aori.Graph.Utils;
using UnityEngine;

namespace Aori.Graph.Strategies
{
    /// <summary>
    /// Connects all nodes between every pair of clusters. Connection that trespass
    /// any cluster will be ignored.
    /// </summary>
    internal sealed class EstablishConnectionAcrossClusters : BuildPhaseStrategy
    {
        public EstablishConnectionAcrossClusters(
            GraphContext context,
            GraphSystem system
        ) : base(context, system)
        { }

        public override void Execute()
        {
            var clusterList = _context.ClusterList;
            var clusterUnionFind = BuildMergedClusterUnionFind();

            for (var firstIndex = 0;
                 firstIndex < clusterList.Count;
                 firstIndex++)
            {
                // Get first cluster's nodes.
                var firstCluster = clusterList[firstIndex].OrderedNodes;

                if (firstCluster.Count == 0)
                {
                    continue;
                }

                var firstRoot = clusterUnionFind.Find(firstIndex);

                for (var secondIndex = firstIndex + 1;
                     secondIndex < clusterList.Count;
                     secondIndex++)
                {
                    // Get second cluster's nodes.
                    var secondCluster = clusterList[secondIndex].OrderedNodes;
                    if (secondCluster.Count == 0)
                    {
                        continue;
                    }

                    var secondRoot = clusterUnionFind.Find(secondIndex);
                    if (firstRoot == secondRoot)
                    {
                        // Skip if this cluster pair is merged.
                        continue;
                    }

                    ConnectClusterPair(firstCluster, secondCluster);
                }
            }
        }

        /// <summary>
        /// Connects two clusters by finding candidate edges between their nodes and creating connections
        /// that don't cross negative zones and respect angle thresholds.
        /// Each node in the first cluster connects to its closest node in the second cluster, and vice versa.
        /// </summary>
        /// <param name="firstCluster">Nodes from the first cluster.</param>
        /// <param name="secondCluster">Nodes from the second cluster.</param>
        private void ConnectClusterPair(
            IReadOnlyList<GraphNode> firstCluster,
            IReadOnlyList<GraphNode> secondCluster
        )
        {
            // Get all possible edges between two clusters.
            var allEdgeSet = GetAllEdges(firstCluster, secondCluster);
            var mutualNodeList
                = firstCluster.Where(secondCluster.Contains).ToArray();

            // Fetch valid edges and form connections.
            var validCandidateQuery
                = allEdgeSet.Where(candidate =>
                    !mutualNodeList.Contains(candidate.First) &&
                    !mutualNodeList.Contains(candidate.Second) &&
                    !ReferenceEquals(candidate.First, candidate.Second) &&
                    PassesConnectionAngleThreshold(candidate.First, candidate.Second) &&
                    !DoesConnectionCrossAnyNegativeZone(candidate.First, candidate.Second));
            foreach (var candidate in validCandidateQuery)
            {
                candidate.First.AddNeighbor(candidate.Second);
                candidate.Second.AddNeighbor(candidate.First);
            }
        }

        private UnionFind BuildMergedClusterUnionFind()
        {
            var clusterCount = _context.ClusterList.Count;
            var clustersUnionFind = new UnionFind(clusterCount);

            for (var firstIndex = 0;
                 firstIndex < clusterCount;
                 firstIndex++)
            {
                var firstCluster = _context.ClusterList[firstIndex];

                for (var secondIndex = firstIndex + 1;
                     secondIndex < clusterCount;
                     secondIndex++)
                {
                    var secondCluster = _context.ClusterList[secondIndex];

                    var hasMutualNode
                        = firstCluster.Nodes
                            .Any(node => secondCluster.Nodes.Contains(node));

                    if (hasMutualNode)
                    {
                        clustersUnionFind.Union(firstIndex, secondIndex);
                    }
                }
            }

            return clustersUnionFind;
        }

        /// <summary>
        /// Rejects candidate links that cross any cluster edge.
        /// </summary>
        private bool DoesConnectionCrossAnyNegativeZone(
            GraphNode firstNode,
            GraphNode secondNode
        )
        {
            // Find the cluster the first node belongs to
            var firstOrigin
                = _context.ClusterList.FirstOrDefault(cluster =>
                    cluster.Nodes.Contains(firstNode));
            if (firstOrigin == null)
            {
                throw new MismatchDataException(
                    $"Node {firstNode} does not belong to any cluster.");
            }

            if (IsInsideAnyNegativeZone(firstNode, firstOrigin))
            {
                return true;
            }

            // Find the cluster the first node belongs to
            var secondOrigin
                = _context.ClusterList.FirstOrDefault(cluster =>
                    cluster.Nodes.Contains(secondNode));
            if (secondOrigin == null)
            {
                throw new MismatchDataException(
                    $"Node {secondNode} does not belong to any cluster.");
            }

            if (IsInsideAnyNegativeZone(secondNode, secondOrigin))
            {
                return true;
            }

            // Fetch all cluster shell edges. Skip ones whose endpoints are the
            // same as candidate points.
            return _context.ClusterShellEdgeSet
                .Where(shellEdge =>
                    !ReferenceEquals(shellEdge.First, firstNode) &&
                    !ReferenceEquals(shellEdge.Second, firstNode) &&
                    !ReferenceEquals(shellEdge.First, secondNode) &&
                    !ReferenceEquals(shellEdge.Second, secondNode))
                .Any(shellEdge =>
                    Math.SegmentsIntersect(
                        firstStart: Math.ToXZ(firstNode.Position),
                        firstEnd: Math.ToXZ(secondNode.Position),
                        secondStart: Math.ToXZ(shellEdge.First.Position),
                        secondEnd: Math.ToXZ(shellEdge.Second.Position)));
        }

        /// <summary>
        /// Checks if a node's position is inside or on the boundary of any cluster's negative zone.
        /// A negative zone is defined as the interior of the shell cycle polygon.
        /// </summary>
        /// <param name="testingNode">Node to test.</param>
        /// <param name="origin">The cluster the node belongs to</param>
        /// <returns>True if the node is inside any cluster's negative zone.</returns>
        private bool IsInsideAnyNegativeZone(GraphNode testingNode, Cluster origin)
        {
            return _context.ClusterList
                .Where(cluster => !ReferenceEquals(cluster, origin))
                .Select(cluster => cluster.OrderedNodes)
                .Any(clusterShellCycle =>
                    clusterShellCycle.Count >= 3 &&
                    Math.IsPointInsidePolygon(
                        point: Math.ToXZ(testingNode.Position),
                        vertices: clusterShellCycle
                            .Select(node => Math.ToXZ(node.Position))
                            .ToList()
                    )
                );
        }


        private HashSet<EdgeKey> GetAllEdges(
            IReadOnlyList<GraphNode> firstCluster,
            IReadOnlyList<GraphNode> secondCluster
        )
        {
            var edgeSet = new HashSet<EdgeKey>();
            foreach (var first in firstCluster)
            {
                foreach (var second in secondCluster)
                {
                    var edge = new EdgeKey(first, second);
                    edgeSet.Add(edge);
                }
            }

            return edgeSet;
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