using UnityEditor;
using UnityEngine;

//Spawning in the editor is an explicit action. Continuous auto-spawning conflicts with
//the generator's gizmo drawing and each star's own preview, so it is a button instead
[CustomEditor(typeof(SFStarSpawner))]
public class SFStarSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SFStarSpawner spawner = (SFStarSpawner)target;

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "Stars spawn automatically in play mode. Use these buttons to place them in "
            + "the editor — they are not saved to the scene.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Spawn Stars", GUILayout.Height(24)))
            {
                spawner.Spawn();
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Clear Stars"))
            {
                spawner.Clear();
                SceneView.RepaintAll();
            }
        }
    }
}
