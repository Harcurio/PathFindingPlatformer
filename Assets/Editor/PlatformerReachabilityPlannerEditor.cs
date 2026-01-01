using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(PlatformerReachabilityPlanner))]
public class PlatformerReachabilityPlannerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the normal inspector fields first
        DrawDefaultInspector();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Planner Controls", EditorStyles.boldLabel);

        var planner = (PlatformerReachabilityPlanner)target;

        // Optional: warn if not in Play Mode (recommended for physics determinism)
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Running the planner in Edit Mode can be less reliable depending on Physics2D settings. " +
                "Play Mode is recommended.",
                MessageType.Info
            );
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Run A*"))
            {
                planner.RunAStar();
                // If you want Scene gizmos to refresh immediately:
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Run Dijkstra"))
            {
                planner.RunDijkstra();
                SceneView.RepaintAll();
            }
        }
    }
}
