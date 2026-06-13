using System.Collections.Generic;
using System.Linq;
using Aori.EditorUtility;
using Aori.Exception;
using Aori.Graph.Dependencies;
using Aori.Graph.Pathfinding;
using Aori.Graph.Serialization;
using Aori.Graph.Strategies;
using Aori.Graph.Utils;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Aori.Graph
{
    /// <summary>
    /// Main graph system that manages navigation nodes and their connections.
    /// 
    /// Graph construction is cluster-driven: each GraphEntry starts as a cluster,
    /// then blending can merge clusters into larger negative zones. The build process
    /// follows a multiphase algorithm:
    /// 
    /// Phase 1: Create initial clusters from GraphEntry components in the scene.
    /// Phase 2: Insert intersection nodes where cluster edges cross.
    /// Phase 3: Blend close vertices and merge linked clusters.
    /// Phase 4: Build shell boundaries for current cluster geometry.
    /// Phase 5: Remove nodes that fall inside any negative zone.
    /// Phase 6: Rebuild shell data after blend/merge.
    /// Phase 7: Establish connections between clusters.
    /// Phase 8: Simplify the graph by removing unnecessary edges.
    /// Phase 9: Ensure connectivity by adding bridges between disconnected components.
    /// Phase 10: Snap non-shell edges to nearby vertices for further simplification.
    /// Phase 11: Finalize cluster meshes for visualization.
    /// 
    /// Serialization is handled through OnBeforeSerialize/OnAfterDeserialize to preserve
    /// the built graph across domain reloads.
    /// </summary>
    public sealed class GraphSystem : MonoBehaviour,
        ISerializationCallbackReceiver
    {
        /// <summary>
        /// Configuration object holding graph construction parameters like dissolve thresholds
        /// and blend distances.
        /// </summary>
        [Header("- Configuration")]
        [SerializeField]
        private GraphConfig m_graphConfig;

#if UNITY_EDITOR
        /// <summary>
        /// When enabled, draws all graph nodes as blue spheres in the scene view.
        /// </summary>
        [Header("- Debug Visualization")]
        [SerializeField]
        private bool m_drawNodes = true;

        /// <summary>
        /// When enabled, draws all graph edges as green lines in the scene view.
        /// </summary>
        [SerializeField]
        private bool m_drawConnection = true;

        [SerializeField]
        private bool m_drawNodeBounding = true;

        [SerializeField]
        [ShowIf(nameof(m_drawNodeBounding))]
        [Range(0.1f, 5f)]
        private float m_nodeBoundSize = 0.2f;

        /// <summary>
        /// Phase index (0-11) at which to terminate the graph build process.
        /// Set to 0 to run all phases. Useful for debugging intermediate build states.
        /// </summary>
        [Header("- Debug Build")]
        [SerializeField]
        [Range(0, 8)]
        [Tooltip("Editor-only: terminate BuildGraph after the selected phase. 0 means do not terminate early.")]
        private int m_terminateAfterPhase;
#endif

        [SerializeField]
        [HideInInspector]
        private List<SerializedNode> m_serializedNodeList = new();

        [SerializeField]
        [HideInInspector]
        private List<SerializedEdge> m_serializedEdgeList = new();

        [SerializeField]
        [HideInInspector]
        private List<SerializedCluster> m_serializedClusterList = new();

        [SerializeField]
        [HideInInspector]
        private bool m_hasSerializedGraph;

        [System.NonSerialized]
        private bool m_isSnapshotCaptureRequested;

        private GraphContext m_context;
        private AStar m_pathFinder;

        /// <summary>
        /// List of graph building strategies in sequence.
        /// </summary>
        private readonly List<BuildPhaseStrategy> _strategyList = new();

        /// <summary>
        /// Gets the total number of build phases in the pipeline.
        /// </summary>
        public int PhaseCount => _strategyList.Count;

        /// <summary>
        /// Singleton instance of the GraphSystem. Only one can exist at a time.
        /// </summary>
        public static GraphSystem Instance { get; private set; }

        /// <summary>
        /// Initializes the singleton instance during Awake.
        /// </summary>
        private void Awake()
        {
            InitializeSingleton();
            InitializeRuntimePathfindingState();
        }

        /// <summary>
        /// Cleans up the singleton instance when destroyed.
        /// </summary>
        private void OnDestroy()
        {
            DestroySingleton();
        }

        /// <summary>
        /// Sets up the singleton instance. Throws if an instance already exists.
        /// </summary>
        private void InitializeSingleton()
        {
            if (Instance)
            {
                throw new ReinitializedSingletonException(this);
            }

            Instance = this;
        }

        /// <summary>
        /// Clears the singleton reference.
        /// </summary>
        private void DestroySingleton()
        {
            Instance = null;
        }

        /// <summary>
        /// Unity serialization callback that captures the current runtime graph snapshot.
        /// </summary>
        public void OnBeforeSerialize()
        {
            if (!m_isSnapshotCaptureRequested)
            {
                return;
            }

            CaptureGraphSnapshot();
            m_isSnapshotCaptureRequested = false;
        }

        /// <summary>
        /// Unity serialization callback that restores runtime graph state from serialized snapshot data.
        /// </summary>
        public void OnAfterDeserialize()
        {
            RestoreGraphSnapshot();
            _strategyList.Clear();
        }

        /// <summary>
        /// Builds the graph from all GraphEntry components in the scene.
        /// Executes all build phases in sequence unless early termination is enabled via editor settings.
        /// After building, the graph is automatically serialized so it persists across domain reloads.
        /// </summary>
        internal void BuildGraph()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                Debug.LogWarning("BuildGraph is editor-only and should not be called in play mode.");
                return;
            }
#else
            return;
#endif

            // BuildGraph is the single data-flow entry point: scene GraphEntry data is read,
            // transformed through the phase pipeline, then captured again for serialization.
            if (!m_graphConfig)
            {
                // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
                Debug.LogError(
                    "GraphConfig is not assigned. Please assign a GraphConfig to " +
                    "the GraphSystem component.");
                return;
            }

            ValidateContextAndStrategies();

            // Clear graph beforehand.
            ClearGraph();

            // Fetch all GraphEntry from the scene.
            var graphEntries = FindObjectsByType<GraphEntry>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );
            if (graphEntries.Length == 0)
            {
                // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
                Debug.LogWarning("No GraphEntry components found in the scene.");
                SaveSerializedGraphState();
                return;
            }

            m_context.EntryList.AddRange(graphEntries);

            // Fetch all dead zones from scene
            var deadZones = FindObjectsByType<GraphDeadZone>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );
            m_context.DeadZoneList.AddRange(deadZones);

#if UNITY_EDITOR
            if (TryTerminateBuildAtPhase(0))
            {
                SaveSerializedGraphState();
                return;
            }
#endif

            for (var i = 0; i < _strategyList.Count; i++)
            {
                var phaseIndex = i + 1;
                var phaseDefinition = _strategyList[i];
                // Each phase mutates the shared graph state in place, so later phases always
                // see the latest cluster list, node set, and shell-edge set.
                phaseDefinition.Execute();

#if UNITY_EDITOR
                if (!TryTerminateBuildAtPhase(phaseIndex))
                {
                    continue;
                }

                SaveSerializedGraphState();
                return;
#endif
            }

            SaveSerializedGraphState();

            // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
            Debug.Log(
                $"Graph built successfully with {m_context.NodeSet.Count} nodes" +
                $" across {m_context.ClusterList.Count} clusters.");
        }

        /// <summary>
        /// Validates <see cref="m_context"/> is valid and verifies
        /// <see cref="_strategyList"/>.
        /// </summary>
        private void ValidateContextAndStrategies()
        {
            m_context ??= new GraphContext(m_graphConfig);

            if (_strategyList.Count > 0)
            {
                return;
            }

            _strategyList.Clear();

            // Phase 1
            _strategyList.Add(new CreateInitialClustersFromEntries(
                context: m_context,
                system: this
            ));

            // Phase 2
            _strategyList.Add(new BuildClusterShell(
                context: m_context,
                system: this
            ));

            // Phase 3
            _strategyList.Add(new InsertClusterIntersectionNodes(
                context: m_context,
                system: this
            ));

            // Phase 4
            _strategyList.Add(new CleanUpIntersection(
                context: m_context,
                system: this
            ));
            // Phase 5
            _strategyList.Add(new EstablishConnectionAcrossClusters(
                context: m_context,
                system: this
            ));

            // Phase 6
            _strategyList.Add(new BlendCloseNodes(
                context: m_context,
                system: this
            ));

            // Phase 7
            _strategyList.Add(new SnapNonShellEdgesToNearbyVertices(
                context: m_context,
                system: this
            ));

            // Phase 8
            _strategyList.Add(new VerifyDeadZones(
                context: m_context,
                system: this
            ));
        }

        /// <summary>
        /// Initializes or refreshes the graph pathfinder binding to the active graph context.
        /// </summary>
        private void EnsurePathFinder()
        {
            if (m_context == null)
            {
                m_pathFinder = null;
                return;
            }

            m_pathFinder = new AStar(m_context);
        }

        /// <summary>
        /// Runtime bootstrap for pathfinding. Rehydrates graph context from serialized data,
        /// then binds the runtime A* pathfinder to the restored context.
        /// </summary>
        private void InitializeRuntimePathfindingState()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureRuntimeContextForNavigation();
        }

        /// <summary>
        /// Ensures runtime navigation has a valid graph context. If no live context exists,
        /// it restores one from serialized snapshot data captured by editor BuildGraph.
        /// </summary>
        private void EnsureRuntimeContextForNavigation()
        {
            if (!Application.isPlaying)
            {
                m_pathFinder = null;
                return;
            }

            if (m_context == null || m_context.NodeSet.Count == 0)
            {
                RestoreGraphSnapshot();
            }

            if (m_context == null || m_context.NodeSet.Count == 0)
            {
                m_pathFinder = null;
                return;
            }

            EnsurePathFinder();
        }

        /// <summary>
        /// Attempts to navigate across the built graph from <paramref name="start"/> to
        /// <paramref name="end"/>.
        /// </summary>
        /// <param name="start">World-space start position.</param>
        /// <param name="end">World-space destination position.</param>
        /// <param name="path">Resulting world-space waypoint queue when successful.</param>
        /// <returns>True when a path can be found between the nearest nodes to the input points.</returns>
        public bool TryNavigate(Vector3 start, Vector3 end, out Queue<Vector3> path)
        {
            path = null;

            if (!Application.isPlaying)
            {
                return false;
            }

            EnsureRuntimeContextForNavigation();

            if (m_context == null || m_context.NodeSet.Count == 0)
            {
                return false;
            }

            return m_pathFinder != null &&
                   m_pathFinder.TryNavigate(start, end, out path);
        }

        /// <summary>
        /// Retrieves the step name (method name) for a given phase index (1-based).
        /// </summary>
        /// <param name="phaseIndex">1-based phase index</param>
        /// <returns>The name of the phase method, or an empty string if index is out of range.</returns>
        internal string GetPhaseStepString(int phaseIndex)
        {
            var listIndex = phaseIndex - 1;
            if (listIndex < 0 || listIndex >= _strategyList.Count)
            {
                return string.Empty;
            }

            return _strategyList[listIndex].StepName;
        }

        /// <summary>
        /// Clears all graph state and serialized snapshots.
        /// </summary>
        private void ClearGraph()
        {
            DisposeClusterMeshes();
            m_context?.ResetContext();
        }

        /// <summary>
        /// Destroys all cluster meshes currently owned by the runtime context.
        /// This prevents mesh leaks across repeated rebuilds.
        /// </summary>
        private void DisposeClusterMeshes()
        {
            if (m_context == null)
            {
                return;
            }

            foreach (var cluster in m_context.ClusterList)
            {
                cluster.SetMesh(null);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Checks if the build should terminate at the given phase index.
        /// Used for debugging to inspect intermediate build states.
        /// </summary>
        /// <param name="phaseIndex">1-based phase index to check.</param>
        /// <returns>True if termination is requested and build should stop.</returns>
        private bool TryTerminateBuildAtPhase(int phaseIndex)
        {
            return m_terminateAfterPhase == phaseIndex;
        }

        /// <summary>
        /// Draws debug visualization gizmos in the scene view.
        /// Shows nodes as blue spheres, edges as green lines, and negative zones as red filled meshes.
        /// Visualization is controlled by editor-only serialized boolean flags.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (m_context == null)
            {
                return;
            }

            if (m_context.NodeSet.Count == 0)
            {
                return;
            }

            // Gizmo drawing is read-only: it reflects the already-built graph state and does
            // not regenerate nodes, edges, or meshes during render.
            if (m_drawNodes)
            {
                foreach (var node in m_context.NodeSet)
                {
                    Gizmos.color
                        = m_context.IntersectingNodeSet.Contains(node)
                            ? Color.red
                            : Color.blue;
                    Gizmos.DrawSphere(node.Position, 0.08f);

                    if (!m_drawNodeBounding)
                    {
                        continue;
                    }

                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(node.Position, m_nodeBoundSize);
                }
            }

            if (!m_drawConnection)
            {
                return;
            }

            {
                var connectionQuery
                    = m_context.NodeSet
                        .SelectMany(node =>
                            node.Neighbors, (node, neighbor) => new EdgeKey(node, neighbor))
                        .Distinct();
                foreach (var edge in connectionQuery)
                {
                    var firstIsIntersection
                        = m_context.IntersectingNodeSet.Contains(edge.First);
                    var secondIsIntersection
                        = m_context.IntersectingNodeSet.Contains(edge.Second);
                    switch (firstIsIntersection)
                    {
                        case true when secondIsIntersection:
                            Gizmos.color = Color.darkOrchid;
                            break;

                        case true:
                        case false when secondIsIntersection:
                            Gizmos.color = Color.yellow;
                            break;

                        default:
                            Gizmos.color = Color.green;
                            break;
                    }

                    Gizmos.DrawLine(edge.First.Position, edge.Second.Position);
                }
            }
        }

#endif

        /// <summary>
        /// Captures the runtime graph into serialized DTO lists.
        /// </summary>
        private void CaptureGraphSnapshot()
        {
            m_serializedNodeList ??= new List<SerializedNode>();
            m_serializedEdgeList ??= new List<SerializedEdge>();
            m_serializedClusterList ??= new List<SerializedCluster>();

            m_serializedNodeList.Clear();
            m_serializedEdgeList.Clear();
            m_serializedClusterList.Clear();
            m_hasSerializedGraph = false;

            if (m_context == null || m_context.NodeSet.Count == 0)
            {
                return;
            }

            var nodeList = m_context.NodeSet.ToList();
            var indexByNode = new Dictionary<GraphNode, int>(nodeList.Count);
            for (var i = 0; i < nodeList.Count; i++)
            {
                indexByNode[nodeList[i]] = i;
                m_serializedNodeList.Add(new SerializedNode
                {
                    Position = nodeList[i].Position
                });
            }

            var undirectedEdgeSet = new HashSet<(int First, int Second)>();
            foreach (var node in nodeList)
            {
                var firstIndex = indexByNode[node];
                foreach (var neighbor in node.Neighbors)
                {
                    if (!indexByNode.TryGetValue(neighbor, out var secondIndex) || firstIndex == secondIndex)
                    {
                        continue;
                    }

                    var first = Mathf.Min(firstIndex, secondIndex);
                    var second = Mathf.Max(firstIndex, secondIndex);
                    if (!undirectedEdgeSet.Add((first, second)))
                    {
                        continue;
                    }

                    m_serializedEdgeList.Add(new SerializedEdge
                    {
                        FirstIndex = first,
                        SecondIndex = second
                    });
                }
            }

            foreach (var cluster in m_context.ClusterList)
            {
                var serializedCluster = new SerializedCluster
                {
                    OrderedNodeIndexList = new List<int>(),
                    Mesh = cluster.Mesh
                };

                foreach (var node in cluster.OrderedNodes)
                {
                    if (indexByNode.TryGetValue(node, out var nodeIndex))
                    {
                        serializedCluster.OrderedNodeIndexList.Add(nodeIndex);
                    }
                }

                m_serializedClusterList.Add(serializedCluster);
            }

            m_hasSerializedGraph = m_serializedNodeList.Count > 0;
        }

        /// <summary>
        /// Restores runtime graph state from serialized DTO lists.
        /// </summary>
        private void RestoreGraphSnapshot()
        {
            if (!m_hasSerializedGraph || m_serializedNodeList == null || m_serializedNodeList.Count == 0)
            {
                return;
            }

            m_context = new GraphContext(m_graphConfig);

            var nodeList = new List<GraphNode>(m_serializedNodeList.Count);
            foreach (var serializedNode in m_serializedNodeList)
            {
                var node = new GraphNode(serializedNode.Position);
                nodeList.Add(node);
                m_context.NodeSet.Add(node);
            }

            if (m_serializedEdgeList != null)
            {
                foreach (var edge in m_serializedEdgeList)
                {
                    if (edge.FirstIndex < 0 || edge.FirstIndex >= nodeList.Count ||
                        edge.SecondIndex < 0 || edge.SecondIndex >= nodeList.Count ||
                        edge.FirstIndex == edge.SecondIndex)
                    {
                        continue;
                    }

                    var firstNode = nodeList[edge.FirstIndex];
                    var secondNode = nodeList[edge.SecondIndex];
                    firstNode.AddNeighbor(secondNode);
                    secondNode.AddNeighbor(firstNode);
                }
            }

            if (m_serializedClusterList == null)
            {
                return;
            }

            foreach (var serializedCluster in m_serializedClusterList)
            {
                if (serializedCluster == null)
                {
                    continue;
                }

                var orderedNodes = new List<GraphNode>();
                var indexList = serializedCluster.OrderedNodeIndexList;
                if (indexList != null)
                {
                    foreach (var nodeIndex in indexList)
                    {
                        if (nodeIndex >= 0 && nodeIndex < nodeList.Count)
                        {
                            orderedNodes.Add(nodeList[nodeIndex]);
                        }
                    }
                }

                var cluster = new Cluster();
                cluster.SetOrderedNodes(orderedNodes);
                cluster.SetMesh(serializedCluster.Mesh);
                m_context.ClusterList.Add(cluster);

                if (orderedNodes.Count < 2)
                {
                    continue;
                }

                for (var i = 0; i < orderedNodes.Count; i++)
                {
                    var firstNode = orderedNodes[i];
                    var secondNode = orderedNodes[(i + 1) % orderedNodes.Count];
                    m_context.ClusterShellEdgeSet.Add(new EdgeKey(firstNode, secondNode));
                }
            }

            EnsurePathFinder();
        }

        /// <summary>
        /// Persists the current graph snapshot so graph data survives editor domain reloads.
        /// </summary>
        private void SaveSerializedGraphState()
        {
            m_isSnapshotCaptureRequested = true;
            CaptureGraphSnapshot();
            m_isSnapshotCaptureRequested = false;

#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                return;
            }

            UnityEditor.EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        /// <summary>
        /// Returns all nodes of this graph.
        /// </summary>
        /// <returns>All nodes of this graph.</returns>
        public IReadOnlyList<Vector3> GetAllNodePosition()
        {
            return m_context.NodeSet.Select(node => node.Position).ToArray();
        }

        /// <summary>
        /// Returns all nodes of this graph inside the given circular area.
        /// </summary>
        /// <param name="center">The center of the circular area.</param>
        /// <param name="radius">The radius of the circular area.</param>
        /// <returns>All nodes of this graph inside the given circular area.</returns>
        public IReadOnlyList<Vector3> GetAllNodePosition(Vector3 center, float radius)
        {
            return m_context.NodeSet
                .Where(node =>
                    Vector3.Distance(node.Position.Flatten(), center.Flatten()) <= radius)
                .Select(node => node.Position)
                .ToArray();
        }
    }
}