using UnityEngine;

namespace Aori.Graph.Dependencies
{
    [CreateAssetMenu(
        fileName = "GraphConfig",
        menuName = "Configuration/Graph"
    )]
    [System.Serializable]
    public sealed class GraphConfig : ScriptableObject
    {
        [Header("- Graph Simplification")]
        [SerializeField]
        [Tooltip(
            "Minimum allowed angle in degrees between two edges. " +
            "Edges below this angle will be dissolved for graph simplification."
        )]
        [Range(0f, 180f)]
        private float m_dissolveThreshold = 45f;

        [Header("- Vertex Blending")]
        [SerializeField]
        [Tooltip(
            "Maximum distance at which vertices from different furniture" +
            " can be blended together."
        )]
        [Range(0.01f, 10f)]
        private float m_vertexBlendDistance = 0.1f;

        [SerializeField]
        [Tooltip(
            "Maximum distance at which edges can be" +
            " considered overlapping and blended."
        )]
        [Range(0.01f, 10f)]
        private float m_edgeBlendDistance = 0.05f;

        public float DissolveThreshold => m_dissolveThreshold;
        public float VertexBlendDistance => m_vertexBlendDistance;
        public float EdgeBlendDistance => m_edgeBlendDistance;
    }
}