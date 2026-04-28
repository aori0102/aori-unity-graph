using Aori.Graph.Dependencies;

namespace Aori.Graph.Utils
{
    /// <summary>
    /// Represents edge splitting information.
    /// </summary>
    internal readonly struct SplitPoint
    {
        /// <summary>
        /// The splitting node.
        /// </summary>
        public GraphNode Node { get; }

        /// <summary>
        /// The normalized value indicating the ratio in the edge
        /// segment where it is splitted.
        /// </summary>
        public float Parameter { get; }

        public SplitPoint(GraphNode node, float parameter)
        {
            Node = node;
            Parameter = parameter;
        }
    }
}