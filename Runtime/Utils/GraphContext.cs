using System.Collections.Generic;
using Aori.Graph.Dependencies;

namespace Aori.Graph.Utils
{
    internal sealed class GraphContext
    {
        public List<GraphEntry> EntryList { get; }
        public List<GraphDeadZone> DeadZoneList { get; }
        public HashSet<GraphNode> NodeSet { get; }
        public List<Cluster> ClusterList { get; }
        public HashSet<EdgeKey> ClusterShellEdgeSet { get; }
        public HashSet<GraphNode> IntersectingNodeSet { get; }
        public GraphConfig Config { get; }
        public List<EdgeRemover>  EdgeRemoverList { get; }

        public GraphContext(GraphConfig config)
        {
            Config = config;

            DeadZoneList = new List<GraphDeadZone>();
            EntryList = new List<GraphEntry>();
            NodeSet = new HashSet<GraphNode>();
            ClusterList = new List<Cluster>();
            ClusterShellEdgeSet = new HashSet<EdgeKey>();
            IntersectingNodeSet = new HashSet<GraphNode>();
            EdgeRemoverList = new List<EdgeRemover>();
        }

        public void ResetContext()
        {
            DeadZoneList.Clear();
            EntryList.Clear();
            NodeSet.Clear();
            ClusterList.Clear();
            ClusterShellEdgeSet.Clear();
            IntersectingNodeSet.Clear();
            EdgeRemoverList.Clear();
        }
    }
}