#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Aori.Graph.Editor
{
    /// <summary>
    /// Custom inspector for the Graph class that provides a button to trigger BuildGraph.
    /// </summary>
    [CustomEditor(typeof(GraphSystem))]
    internal class GraphEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Graph Building", EditorStyles.boldLabel);

            if (GUILayout.Button("Build Graph", GUILayout.Height(40)))
            {
                var graphSystem = target as GraphSystem;
                if (graphSystem)
                {
                    graphSystem.BuildGraph();
                }
            }

            EditorGUILayout.HelpBox(
                "Click 'Build Graph' to construct the navigation graph from all GraphEntry components in the scene. " +
                "The graph will be simplified based on the configured angle threshold and vertices from nearby furniture will be blended.",
                MessageType.Info
            );

            var terminatePhaseProperty = serializedObject.FindProperty("m_terminateAfterPhase");
            var graph = target as GraphSystem;
            var phaseCount = graph ? graph.PhaseCount : 0;
            var terminatePhase = terminatePhaseProperty != null
                ? Mathf.Clamp(terminatePhaseProperty.intValue, 0, phaseCount)
                : 0;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Steps to execute", EditorStyles.label);
            for (var phase = 1; phase <= terminatePhase; phase++)
            {
                var stepName = graph ? graph.GetPhaseStepString(phase) : string.Empty;
                EditorGUILayout.LabelField($"- Phase {phase}: {stepName}", EditorStyles.label);
            }
        }
    }
}

#endif