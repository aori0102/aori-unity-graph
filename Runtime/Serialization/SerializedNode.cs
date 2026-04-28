using UnityEngine;

namespace Aori.Graph.Serialization
{
    [System.Serializable]
    internal struct SerializedNode
    {
        [SerializeField]
        private Vector3 m_position;

        public Vector3 Position
        {
            get => m_position;
            set => m_position = value;
        }
    }
}