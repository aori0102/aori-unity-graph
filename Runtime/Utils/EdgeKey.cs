using Aori.Graph.Dependencies;

namespace Aori.Graph.Utils
{
    internal readonly struct EdgeKey : System.IEquatable<EdgeKey>
    {
        public EdgeKey(GraphNode first, GraphNode second)
        {
            First = first;
            Second = second;
        }

        public GraphNode First { get; }
        public GraphNode Second { get; }

        public bool Equals(EdgeKey other)
        {
            return
                (ReferenceEquals(First, other.First) && ReferenceEquals(Second, other.Second)) ||
                (ReferenceEquals(First, other.Second) && ReferenceEquals(Second, other.First));
        }

        public override bool Equals(object obj)
        {
            return obj is EdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            var firstHash = First != null ? First.GetHashCode() : 0;
            var secondHash = Second != null ? Second.GetHashCode() : 0;
            return firstHash ^ secondHash;
        }
    }
}

