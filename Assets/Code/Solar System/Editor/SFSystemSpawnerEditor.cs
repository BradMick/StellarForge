using UnityEditor;
using UnityEngine;

//Editor spawning is explicit. Continuous auto-spawning in edit mode put three
//ExecuteAlways systems in conflict — the generator regenerating, the spawner rebuilding,
//and each planet running its own terrain preview — so bodies were destroyed and recreated
//every frame. Buttons make it a deliberate one-off action instead
[CustomEditor(typeof(SFSystemSpawner))]
public class SFSystemSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SFSystemSpawner spawner = (SFSystemSpawner)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Editor Preview", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Bodies spawn automatically in play mode. Use these to preview the real bodies "
            + "in the editor — they are not saved to the scene.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Spawn Now", GUILayout.Height(24)))
            {
                StellarForge forge = spawner.GetComponent<StellarForge>();
                if (forge != null)
                    forge.EnsureGenerated();

                spawner.Refresh();
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Clear Spawned Bodies"))
            {
                spawner.ClearSpawned();
                SceneView.RepaintAll();
            }
        }
    }
}
