using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Aori.Graph.Dependencies
{
    /// <summary>
    /// Defines a zone telling <see cref="GraphSystem"/> to effectively ignore
    /// any <see cref="GraphEntry"/> within the zone. Note that this zone only
    /// ignores the graph entries before building the actual graph. Connections
    /// between two graph entries can still cut through this zone.
    /// </summary>
    /// <remarks>This component is used as a cleanup step after the graph is built.
    /// This effectively removes all the nodes inside its zone and all related
    /// connections. Use this if you want to clear nodes outside a map bounding.</remarks>
    [DisallowMultipleComponent]
    public sealed class GraphDeadZone : MonoBehaviour
    {
        [Header("- Nodes")]
        [SerializeField]
        private List<Transform> m_nodeEntries = new();

#if UNITY_EDITOR
        [Header("- Debug")]
        [SerializeField]
        private bool m_showZone;
#endif

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var epsilon = Vector3.up * Mathf.Epsilon;
            Gizmos.color = Color.indianRed;
            for (var i = 0; i < m_nodeEntries.Count; i++)
            {
                var current = m_nodeEntries[i];
                var next = m_nodeEntries[(i + 1) % m_nodeEntries.Count];
                Gizmos.DrawLine(
                    current.position + epsilon,
                    next.position + epsilon
                );
            }
        }

        internal bool IsNodeWithinDeadZone(GraphNode node)
        {
            var polygon
                = m_nodeEntries
                    .Select(entry => new Vector2(entry.position.x, entry.position.z))
                    .ToArray();
            return Math.IsPointInsidePolygon(
                new Vector2(node.Position.x, node.Position.z),
                polygon
            );
        }
#endif
    }
}