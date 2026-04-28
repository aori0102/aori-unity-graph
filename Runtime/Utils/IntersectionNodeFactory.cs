using System.Collections.Generic;
using Aori.Graph.Dependencies;
using UnityEngine;

namespace Aori.Graph.Utils
{
    internal sealed class IntersectionNodeFactory
    {
        private readonly Dictionary<QuantizedVectorKey, GraphNode> m_nodeMap = new();

        /// <summary>
        /// Gets an existing node at the quantized point or creates a new one.
        /// </summary>
        /// <param name="point">Intersection point in XZ space.</param>
        /// <returns>Graph node mapped to the provided point.</returns>
        public GraphNode GetOrCreate(Vector2 point)
        {
            var key = new QuantizedVectorKey(point);
            if (m_nodeMap.TryGetValue(key, out var node))
            {
                return node;
            }

            node = new GraphNode(new Vector3(point.x, 0f, point.y));
            m_nodeMap[key] = node;
            return node;
        }

        private readonly struct QuantizedVectorKey : System.IEquatable<QuantizedVectorKey>
        {
            private const float QUANTIZATION_SCALE = 10000f;

            public QuantizedVectorKey(Vector2 point)
            {
                X = Mathf.RoundToInt(point.x * QUANTIZATION_SCALE);
                Y = Mathf.RoundToInt(point.y * QUANTIZATION_SCALE);
            }

            private int X { get; }
            private int Y { get; }

            public bool Equals(QuantizedVectorKey other)
            {
                return X == other.X && Y == other.Y;
            }

            public override bool Equals(object obj)
            {
                return obj is QuantizedVectorKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (X * 397) ^ Y;
            }
        }
    }
}

