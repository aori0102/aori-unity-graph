using System;
using System.Collections.Generic;
using Aori.Graph.Dependencies;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Aori.Graph.Utils
{
    /// <summary>
    /// Represents a cluster of graph nodes forming a single negative zone boundary.
    /// 
    /// A cluster maintains:
    /// - An ordered cycle of nodes that define its shell (boundary).
    /// - A set of the same nodes for fast membership lookups.
    /// - A serialized mesh for visualization of the negative zone.
    /// 
    /// Nodes are stored both as an ordered list (for geometry operations) and a set (for fast queries).
    /// The mesh is rebuilt during the finalization phase and persisted for later rendering.
    /// </summary>
    [System.Serializable]
    internal sealed class Cluster
    {
        /// <summary>
        /// The visualization mesh representing the cluster's negative zone.
        /// Serialized and persisted across domain reloads.
        /// </summary>
        [SerializeField]
        [HideInInspector]
        private Mesh m_mesh;

        /// <summary>
        /// Runtime set of nodes for membership checking. Not serialized.
        /// </summary>
        [NonSerialized]
        private HashSet<GraphNode> m_nodeSet = new();

        /// <summary>
        /// Runtime ordered list of nodes forming the shell cycle. Not serialized.
        /// </summary>
        [NonSerialized]
        private List<GraphNode> m_orderedNodeList = new();

        [NonSerialized]
        private bool m_allowIntraConnections;

        /// <summary>
        /// Gets a read-only collection of all nodes in this cluster.
        /// </summary>
        public IReadOnlyCollection<GraphNode> Nodes
        {
            get
            {
                EnsureRuntimeState();
                return m_nodeSet;
            }
        }

        /// <summary>
        /// Gets the ordered list of nodes forming the cluster's shell cycle.
        /// The order matters for geometry calculations and mesh triangulation.
        /// </summary>
        public IReadOnlyList<GraphNode> OrderedNodes
        {
            get
            {
                EnsureRuntimeState();
                return m_orderedNodeList;
            }
        }

        /// <summary>
        /// Gets the serialized mesh representing this cluster's negative zone.
        /// May be null if the cluster mesh has not been finalized yet.
        /// </summary>
        public Mesh Mesh => m_mesh;
        public bool AllowIntraConnections => m_allowIntraConnections;

        public void SetAllowIntraConnections(bool allow)
        {
            m_allowIntraConnections = allow;
        }

        /// <summary>
        /// Sets the ordered nodes for this cluster, updating both the set and ordered list.
        /// Duplicate nodes are automatically skipped.
        /// </summary>
        /// <param name="nodes">Enumerable of nodes to set, in desired order.</param>
        public void SetOrderedNodes(IEnumerable<GraphNode> nodes)
        {
            EnsureRuntimeState();
            m_nodeSet.Clear();
            m_orderedNodeList.Clear();

            foreach (var node in nodes)
            {
                if (node == null || !m_nodeSet.Add(node))
                {
                    continue;
                }

                m_orderedNodeList.Add(node);
            }
        }

        /// <summary>
        /// Clears all nodes from this cluster, leaving the mesh intact.
        /// </summary>
        public void ClearNodes()
        {
            EnsureRuntimeState();
            m_nodeSet.Clear();
            m_orderedNodeList.Clear();
        }

        /// <summary>
        /// Sets or replaces the mesh for this cluster.
        /// If a mesh was previously assigned, it is destroyed before the new one is assigned.
        /// Safe to call with null to clear the mesh.
        /// </summary>
        /// <param name="mesh">New mesh to assign, or null to clear.</param>
        public void SetMesh(Mesh mesh)
        {
            if (ReferenceEquals(m_mesh, mesh))
            {
                return;
            }

            if (m_mesh)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(m_mesh);
                }
                else
                {
                    Object.DestroyImmediate(m_mesh);
                }
            }

            m_mesh = mesh;
        }

        /// <summary>
        /// Ensures runtime state is initialized (for deserialization scenarios).
        /// Called automatically by properties that access runtime collections.
        /// </summary>
        private void EnsureRuntimeState()
        {
            m_nodeSet ??= new HashSet<GraphNode>();
            m_orderedNodeList ??= new List<GraphNode>();
        }
    }
}