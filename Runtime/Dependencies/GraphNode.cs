using System.Collections.Generic;
using UnityEngine;

namespace Aori.Graph.Dependencies
{
    /// <summary>
    /// Represents a node in the navigation graph.
    /// Each node holds references to neighboring nodes that can be traversed to.
    /// </summary>
    internal sealed class GraphNode
    {
        private readonly HashSet<GraphNode> m_neighbors;

        public Vector3 Position { get; }

        public IReadOnlyCollection<GraphNode> Neighbors => m_neighbors;

        public GraphNode(Vector3 position)
        {
            Position = position;
            m_neighbors = new HashSet<GraphNode>();
        }

        /// <summary>
        /// Adds a neighbor node to this node's neighbor list.
        /// </summary>
        public void AddNeighbor(GraphNode neighbor)
        {
            if (neighbor != null && neighbor != this)
            {
                m_neighbors.Add(neighbor);
            }
        }

        /// <summary>
        /// Removes a neighbor node from this node's neighbor list.
        /// </summary>
        public void RemoveNeighbor(GraphNode neighbor)
        {
            if (neighbor != null)
            {
                m_neighbors.Remove(neighbor);
            }
        }

        /// <summary>
        /// Gets the distance to another node.
        /// </summary>
        public float GetDistance(GraphNode other)
        {
            return other == null
                ? float.MaxValue
                : Vector3.Distance(Position, other.Position);
        }

        /// <summary>
        /// Checks if a specific edge exists between this node and another.
        /// </summary>
        public bool HasEdgeTo(GraphNode other)
        {
            return m_neighbors.Contains(other);
        }
    }
}