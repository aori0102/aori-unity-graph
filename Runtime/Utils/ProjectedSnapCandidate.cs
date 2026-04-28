using Aori.Graph.Dependencies;

namespace Aori.Graph.Utils
{
    internal readonly struct ProjectedSnapCandidate
    {
        public GraphNode Node { get; }
        public float Parameter { get; }
        public float DistanceSquared { get; }

        public ProjectedSnapCandidate(GraphNode node, float parameter, float distanceSquared)
        {
            Node = node;
            Parameter = parameter;
            DistanceSquared = distanceSquared;
        }
    }
}