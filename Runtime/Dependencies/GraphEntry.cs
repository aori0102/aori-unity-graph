using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Aori.Graph.Dependencies
{
    /// <summary>
    /// Defines a set of waypoints that form the boundary of a furniture or obstacle.
    /// These waypoints are used by the Graph system to construct navigation nodes.
    /// </summary>
    public class GraphEntry : MonoBehaviour
    {
        [Header("- Entry Points")]
        [SerializeField]
        private List<Transform> m_graphNodePoints = new();

#if UNITY_EDITOR
        [Header("- Debug Visualization")]
        [SerializeField]
        private bool m_drawDebugVisualization = true;
#endif

        public List<Transform> GraphNodePoints => m_graphNodePoints;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var skipDraw = !m_drawDebugVisualization ||
                          m_graphNodePoints == null ||
                          m_graphNodePoints.Count == 0;
            if (skipDraw)
            {
                return;
            }

            // Draw all node points
            Gizmos.color = Color.yellow;
            foreach (var point in m_graphNodePoints
                         .Where(point => point))
            {
                Gizmos.DrawSphere(point.position, 0.1f);
            }

            // Draw connections between sequential points
            Gizmos.color = Color.yellow;
            for (var i = 0; i < m_graphNodePoints.Count; i++)
            {
                var current = m_graphNodePoints[i];
                var next = m_graphNodePoints[(i + 1) % m_graphNodePoints.Count];

                if (current != null && next != null)
                {
                    Gizmos.DrawLine(current.position, next.position);
                }
            }
        }
#endif
    }
}