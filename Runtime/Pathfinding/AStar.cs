using System.Collections.Generic;
using Aori.Graph.Dependencies;
using Aori.Graph.Utils;
using UnityEngine;

namespace Aori.Graph.Pathfinding
{
    /// <summary>
    /// Graph-based A* pathfinder operating on <see cref="GraphContext"/> node topology.
    /// </summary>
    internal sealed class AStar
    {
        private readonly GraphContext _context;

        /// <summary>
        /// Creates a pathfinder bound to a graph context snapshot.
        /// </summary>
        public AStar(GraphContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Attempts to navigate from world-space <paramref name="start"/> to
        /// world-space <paramref name="end"/>.
        /// </summary>
        /// <param name="start">Navigation start in world space.</param>
        /// <param name="end">Navigation end in world space.</param>
        /// <param name="path">Resulting waypoint queue when successful.</param>
        /// <returns>True when a valid path is found.</returns>
        public bool TryNavigate(Vector3 start, Vector3 end, out Queue<Vector3> path)
        {
            path = null;

            if (_context == null || _context.NodeSet.Count == 0)
            {
                return false;
            }

            if (!TryGetNearestNode(start, out var startNode) || 
                !TryGetNearestNode(end, out var endNode))
            {
                return false;
            }

            if (startNode == endNode)
            {
                path = new Queue<Vector3>();
                path.Enqueue(start);
                if ((end - start).sqrMagnitude > 0f)
                {
                    path.Enqueue(end);
                }

                return true;
            }

            if (!TryFindNodePath(startNode, endNode, out var nodePath))
            {
                return false;
            }

            path = new Queue<Vector3>();
            path.Enqueue(start);

            foreach (var node in nodePath)
            {
                path.Enqueue(node.Position);
            }

            path.Enqueue(end);
            return true;
        }

        /// <summary>
        /// Finds the nearest graph node to the given world position.
        /// </summary>
        private bool TryGetNearestNode(Vector3 position, out GraphNode nearestNode)
        {
            nearestNode = null;

            if (_context == null || _context.NodeSet.Count == 0)
            {
                return false;
            }

            var bestDistanceSquared = float.PositiveInfinity;
            foreach (var node in _context.NodeSet)
            {
                var distanceSquared = (node.Position - position).sqrMagnitude;
                if (distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                nearestNode = node;
            }

            return nearestNode != null;
        }

        /// <summary>
        /// Executes A* over the graph's adjacency data and reconstructs an ordered node path.
        /// </summary>
        private static bool TryFindNodePath(
            GraphNode startNode,
            GraphNode endNode,
            out List<GraphNode> nodePath)
        {
            nodePath = null;

            var openSet = new HashSet<GraphNode> { startNode };
            var closedSet = new HashSet<GraphNode>();
            var cameFrom = new Dictionary<GraphNode, GraphNode>();
            var gScore = new Dictionary<GraphNode, float> { [startNode] = 0f };
            var fScore = new Dictionary<GraphNode, float>
            {
                [startNode] = GetSquaredEuclideanDistance(startNode, endNode)
            };

            while (openSet.Count > 0)
            {
                var currentNode = GetNodeWithLowestScore(openSet, fScore);
                if (currentNode == endNode)
                {
                    nodePath = ReconstructNodePath(cameFrom, currentNode);
                    return true;
                }

                openSet.Remove(currentNode);
                closedSet.Add(currentNode);

                foreach (var neighbor in currentNode.Neighbors)
                {
                    if (closedSet.Contains(neighbor))
                    {
                        continue;
                    }

                    var currentGScore
                        = gScore.GetValueOrDefault(currentNode, float.PositiveInfinity);
                    var tentativeGScore
                        = currentGScore + GetSquaredEuclideanDistance(currentNode, neighbor);
                    var neighborGScore
                        = gScore.GetValueOrDefault(neighbor, float.PositiveInfinity);

                    if (tentativeGScore >= neighborGScore)
                    {
                        continue;
                    }

                    cameFrom[neighbor] = currentNode;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor]
                        = tentativeGScore + GetSquaredEuclideanDistance(neighbor, endNode);
                    openSet.Add(neighbor);
                }
            }

            return false;
        }

        /// <summary>
        /// Selects the node in the open set with the smallest recorded F-score.
        /// </summary>
        private static GraphNode GetNodeWithLowestScore(
            HashSet<GraphNode> openSet,
            IReadOnlyDictionary<GraphNode, float> fScore
        )
        {
            GraphNode bestNode = null;
            var bestScore = float.PositiveInfinity;

            foreach (var node in openSet)
            {
                var score
                    = fScore.GetValueOrDefault(node, float.PositiveInfinity);
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestNode = node;
            }

            return bestNode;
        }

        /// <summary>
        /// Reconstructs a start-to-goal node path from A* predecessor mapping.
        /// </summary>
        private static List<GraphNode> ReconstructNodePath(
            IReadOnlyDictionary<GraphNode, GraphNode> cameFrom,
            GraphNode currentNode
        )
        {
            var path = new List<GraphNode> { currentNode };

            while (cameFrom.TryGetValue(currentNode, out var previousNode))
            {
                currentNode = previousNode;
                path.Add(currentNode);
            }

            path.Reverse();
            return path;
        }

        /// <summary>
        /// Returns squared Euclidean distance between two graph nodes.
        /// </summary>
        private static float GetSquaredEuclideanDistance(
            GraphNode firstNode,
            GraphNode secondNode)
        {
            return (firstNode.Position - secondNode.Position).sqrMagnitude;
        }
    }
}