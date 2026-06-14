using UnityEngine;

namespace Aori.Graph.Dependencies
{
    [DisallowMultipleComponent]
    public sealed class EdgeRemover : MonoBehaviour
    {
        [SerializeField]
        private Transform m_from;

        [SerializeField]
        private Transform m_to;

        public bool DoesEdgeIntersects(Vector3 from, Vector3 to)
        {
            return Math.SegmentsIntersect(
                Math.ToXZ(from),
                Math.ToXZ(to),
                Math.ToXZ(m_from.position),
                Math.ToXZ(m_to.position)
            );
        }
    }
}