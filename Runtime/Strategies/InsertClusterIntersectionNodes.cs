using System.Collections.Generic;
using System.Linq;
using Aori.Graph.Dependencies;
using Aori.Graph.Utils;
using UnityEngine;

namespace Aori.Graph.Strategies
{
    /// <summary>
    /// Adds <see cref="GraphNode"/>s based on the intersection between clusters.
    /// </summary>
    internal sealed class InsertClusterIntersectionNodes : BuildPhaseStrategy
    {
        public InsertClusterIntersectionNodes(
            GraphContext context,
            GraphSystem system
        ) : base(context, system)
        { }

        public override void Execute()
        {
            // Get all clusters.
            var clusterList = _context.ClusterList.ToList();
            if (clusterList.Count < 2)
            {
                return;
            }

            // Get a list where the ith element represents the list of
            // nodes of the ith cluster.
            var clusterSnapshotList = clusterList
                .Select(cluster => cluster.OrderedNodes.ToList())
                .ToList();

            var intersectionNodeFactory = new IntersectionNodeFactory();

            // Maps splitting information for each edge
            // [
            //  Key: index of the cluster,
            //  Value: [
            //          Key: index of the edge within that cluster
            //          Value: the list of points to be split for intersecting
            //         ]
            // ]
            var clusterEdgeSplitMap =
                new Dictionary<int, Dictionary<int, List<SplitPoint>>>();

            for (var firstClusterIndex = 0;
                 firstClusterIndex < clusterSnapshotList.Count;
                 firstClusterIndex++)
            {
                // First cluster's nodes.
                var firstCluster = clusterSnapshotList[firstClusterIndex];
                if (firstCluster.Count < 2)
                {
                    continue;
                }

                for (var secondClusterIndex = firstClusterIndex + 1;
                     secondClusterIndex < clusterSnapshotList.Count;
                     secondClusterIndex++)
                {
                    // Second cluster's nodes.
                    var secondCluster = clusterSnapshotList[secondClusterIndex];
                    if (secondCluster.Count < 2)
                    {
                        continue;
                    }

                    // Collect intersection points between two clusters.
                    CollectInterClusterEdgeIntersections(
                        firstCluster,
                        secondCluster,
                        firstClusterIndex,
                        secondClusterIndex,
                        clusterEdgeSplitMap,
                        intersectionNodeFactory
                    );
                }
            }

            if (clusterEdgeSplitMap.Count == 0)
            {
                return;
            }

            foreach (var (clusterIndex, splitMap) in clusterEdgeSplitMap)
            {
                // Rebuild the cluster after processing intersection.
                var rebuiltCluster
                    = InsertIntersectionNodesIntoCluster(
                        clusterSnapshotList[clusterIndex],
                        splitMap
                    );

                if (rebuiltCluster.Count == 0)
                {
                    continue;
                }

                clusterList[clusterIndex].SetOrderedNodes(rebuiltCluster);
            }

            // Update all nodes after intersection processing.
            foreach (var node in clusterList.SelectMany(cluster => cluster.Nodes))
            {
                _context.NodeSet.Add(node);
            }
        }

        /// <summary>
        /// Collects edge intersections between two clusters and registers split points per source edge.
        /// Creates shared intersection nodes through a factory to keep overlapping intersection points deduplicated.
        /// </summary>
        /// <param name="firstCluster">Ordered shell nodes of the first cluster.</param>
        /// <param name="secondCluster">Ordered shell nodes of the second cluster.</param>
        /// <param name="firstClusterIndex">Index of the first cluster in the outer cluster list.</param>
        /// <param name="secondClusterIndex">Index of the second cluster in the outer cluster list.</param>
        /// <param name="clusterEdgeSplitMap">Accumulator mapping cluster index to edge split definitions.</param>
        /// <param name="intersectionNodeFactory">Factory used to reuse nodes at identical intersection positions.</param>
        private void CollectInterClusterEdgeIntersections(
            IReadOnlyList<GraphNode> firstCluster,
            IReadOnlyList<GraphNode> secondCluster,
            int firstClusterIndex,
            int secondClusterIndex,
            Dictionary<int, Dictionary<int, List<SplitPoint>>> clusterEdgeSplitMap,
            IntersectionNodeFactory intersectionNodeFactory
        )
        {
            const float intersectionEpsilon = 0.0001f;

            for (var firstEdgeIndex = 0;
                 firstEdgeIndex < firstCluster.Count;
                 firstEdgeIndex++)
            {
                var firstStart
                    = firstCluster[firstEdgeIndex];
                var firstEnd
                    = firstCluster[(firstEdgeIndex + 1) % firstCluster.Count];
                var firstStart2D
                    = Math.ToXZ(firstStart.Position);
                var firstEnd2D
                    = Math.ToXZ(firstEnd.Position);

                for (var secondEdgeIndex = 0;
                     secondEdgeIndex < secondCluster.Count;
                     secondEdgeIndex++)
                {
                    var secondStart
                        = secondCluster[secondEdgeIndex];
                    var secondEnd
                        = secondCluster[(secondEdgeIndex + 1) % secondCluster.Count];
                    var secondStart2D
                        = Math.ToXZ(secondStart.Position);
                    var secondEnd2D
                        = Math.ToXZ(secondEnd.Position);

                    if (!TryGetSegmentIntersectionPoint(
                            firstStart2D,
                            firstEnd2D,
                            secondStart2D,
                            secondEnd2D,
                            out var intersectionPoint,
                            out var firstT
                        ))
                    {
                        continue;
                    }

                    if (!TryGetSegmentIntersectionPoint(
                            secondStart2D,
                            secondEnd2D,
                            firstStart2D,
                            firstEnd2D,
                            out _,
                            out var secondT
                        ))
                    {
                        continue;
                    }

                    if (firstT <= intersectionEpsilon || firstT >= 1f - intersectionEpsilon ||
                        secondT <= intersectionEpsilon || secondT >= 1f - intersectionEpsilon)
                    {
                        continue;
                    }

                    var intersectionNode
                        = intersectionNodeFactory.GetOrCreate(intersectionPoint);
                    RegisterSplitPoint(
                        clusterEdgeSplitMap,
                        firstClusterIndex,
                        firstEdgeIndex,
                        intersectionNode,
                        firstT
                    );
                    RegisterSplitPoint(
                        clusterEdgeSplitMap,
                        secondClusterIndex,
                        secondEdgeIndex,
                        intersectionNode,
                        secondT
                    );
                }
            }
        }

        /// <summary>
        /// Computes the intersection point between two segments in 2D.
        /// </summary>
        /// <param name="firstStart">Start point of first segment.</param>
        /// <param name="firstEnd">End point of first segment.</param>
        /// <param name="secondStart">Start point of second segment.</param>
        /// <param name="secondEnd">End point of second segment.</param>
        /// <param name="intersectionPoint">Resolved intersection point when segments intersect.</param>
        /// <param name="firstSegmentT">Normalized parameter of the intersection along first segment.</param>
        /// <returns>True if finite segment intersection exists within tolerance.</returns>
        private bool TryGetSegmentIntersectionPoint(
            Vector2 firstStart,
            Vector2 firstEnd,
            Vector2 secondStart,
            Vector2 secondEnd,
            out Vector2 intersectionPoint,
            out float firstSegmentT
        )
        {
            intersectionPoint = default;
            firstSegmentT = 0f;

            var r = firstEnd - firstStart;
            var s = secondEnd - secondStart;
            var denominator = Math.Cross2D(r, s);
            var difference = secondStart - firstStart;

            if (Mathf.Abs(denominator) <= 0.0001f)
            {
                return false;
            }

            var t = Math.Cross2D(difference, s) / denominator;
            var u = Math.Cross2D(difference, r) / denominator;

            if (t < -0.0001f || t > 1f + 0.0001f || u < -0.0001f || u > 1f + 0.0001f)
            {
                return false;
            }

            firstSegmentT = Mathf.Clamp01(t);
            intersectionPoint = firstStart + (r * firstSegmentT);
            return true;
        }

        /// <summary>
        /// Registers a split point for a specific edge within a cluster.
        /// </summary>
        /// <param name="clusterEdgeSplitMap">Accumulator mapping cluster and edge indices to split points.</param>
        /// <param name="clusterIndex">Cluster index owning the edge being split.</param>
        /// <param name="edgeIndex">Index of the edge in the cluster's ordered shell sequence.</param>
        /// <param name="intersectionNode">Node created or reused at the split position.</param>
        /// <param name="parameter">Normalized edge parameter (0..1) where the split occurs.</param>
        private static void RegisterSplitPoint(
            Dictionary<int, Dictionary<int, List<SplitPoint>>> clusterEdgeSplitMap,
            int clusterIndex,
            int edgeIndex,
            GraphNode intersectionNode,
            float parameter
        )
        {
            if (!clusterEdgeSplitMap.TryGetValue(clusterIndex, out var edgeSplitMap))
            {
                edgeSplitMap = new Dictionary<int, List<SplitPoint>>();
                clusterEdgeSplitMap[clusterIndex] = edgeSplitMap;
            }

            if (!edgeSplitMap.TryGetValue(edgeIndex, out var splitPointList))
            {
                splitPointList = new List<SplitPoint>();
                edgeSplitMap[edgeIndex] = splitPointList;
            }

            splitPointList.Add(new SplitPoint(intersectionNode, parameter));
        }

        /// <summary>
        /// Rebuilds a cluster node sequence by inserting registered split nodes along each shell edge.
        /// </summary>
        /// <param name="clusterNodeList">Original ordered cluster nodes.</param>
        /// <param name="edgeSplitMap">Per-edge split points sorted and inserted between edge endpoints.</param>
        /// <returns>New ordered node list containing original and inserted intersection nodes.</returns>
        // ReSharper disable once MemberCanBeMadeStatic.Global
        private List<GraphNode> InsertIntersectionNodesIntoCluster(
            IReadOnlyList<GraphNode> clusterNodeList,
            IReadOnlyDictionary<int, List<SplitPoint>> edgeSplitMap
        )
        {
            if (clusterNodeList.Count == 0)
            {
                return new List<GraphNode>();
            }

            var rebuiltCluster
                = new List<GraphNode>(clusterNodeList.Count + edgeSplitMap.Count);

            for (var edgeIndex = 0; edgeIndex < clusterNodeList.Count; edgeIndex++)
            {
                var currentNode = clusterNodeList[edgeIndex];
                var nextNode = clusterNodeList[(edgeIndex + 1) % clusterNodeList.Count];
                rebuiltCluster.Add(currentNode);

                if (!edgeSplitMap.TryGetValue(edgeIndex, out var splitPointList)
                    || splitPointList.Count == 0)
                {
                    continue;
                }

                // Group the splitting list by the node they represent. For each group, order
                // by parameter and choose the first one. This ensures no duplication and 
                // the closest of each split node is always chosen. Then order all the
                // chosen split nodes by the parameter, exclude the ones that are either the
                // current or next node of the edge.
                var splitPointQuery
                    = splitPointList
                        .GroupBy(splitPoint => splitPoint.Node)
                        .Select(group =>
                            group.OrderBy(point => point.Parameter).First())
                        .OrderBy(point => point.Parameter)
                        .Where(splitPoint =>
                            !ReferenceEquals(splitPoint.Node, currentNode) &&
                            !ReferenceEquals(splitPoint.Node, nextNode))
                        .Select(splitPoint => splitPoint.Node)
                        .ToArray();
                if (splitPointQuery.Length == 0)
                {
                    continue;
                }

                // Remove the previous connection to the other edge endpoint.
                currentNode.RemoveNeighbor(nextNode);
                nextNode.RemoveNeighbor(currentNode);
                _context.ClusterShellEdgeSet.Remove(new EdgeKey(currentNode, nextNode));
                
                var currentTraverse = currentNode;

                // Register intersection points
                foreach (var splitNode in splitPointQuery)
                {
                    rebuiltCluster.Add(splitNode);
                    _context.IntersectingNodeSet.Add(splitNode);

                    currentTraverse.AddNeighbor(splitNode);
                    splitNode.AddNeighbor(currentTraverse);
                    _context.ClusterShellEdgeSet.Add(new EdgeKey(currentTraverse, splitNode));

                    currentTraverse = splitNode;
                }

                // Connect the last split point to the other edge endpoint.
                currentTraverse.AddNeighbor(nextNode);
                nextNode.AddNeighbor(currentTraverse);
                _context.ClusterShellEdgeSet.Add(new EdgeKey(currentTraverse, nextNode));
            }

            return rebuiltCluster;
        }
    }
}