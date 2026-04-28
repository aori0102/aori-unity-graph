using System.Collections.Generic;
using UnityEngine;

namespace Aori.Graph.Serialization
{
    [System.Serializable]
    internal sealed class SerializedCluster
    {
        [SerializeField]
        private List<int> m_orderedNodeIndexList = new();

        [SerializeField]
        private Mesh m_mesh;

        public List<int> OrderedNodeIndexList
        {
            get => m_orderedNodeIndexList;
            set => m_orderedNodeIndexList = value;
        }

        public Mesh Mesh
        {
            get => m_mesh;
            set => m_mesh = value;
        }
    }
}