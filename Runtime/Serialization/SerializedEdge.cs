using UnityEngine;

namespace Aori.Graph.Serialization
{
    [System.Serializable]
    internal struct SerializedEdge
    {
        [SerializeField]
        private int m_firstIndex;

        [SerializeField]
        private int m_secondIndex;

        public int FirstIndex
        {
            get => m_firstIndex;
            set => m_firstIndex = value;
        }

        public int SecondIndex
        {
            get => m_secondIndex;
            set => m_secondIndex = value;
        }
    }
}