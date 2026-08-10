using UnityEditor;
using UnityEngine;

//Generation is explicit rather than continuous. The map overlay draws whatever was last
//generated; changing an inspector value regenerates once, and this button forces a fresh
//run (useful after editing an asset the generator reads)
[CustomEditor(typeof(StellarForge))]
public class StellarForgeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        StellarForge forge = (StellarForge)target;

        EditorGUILayout.Space();

        if (GUILayout.Button("Regenerate System", GUILayout.Height(24)))
        {
            forge.ForceRegenerate();
            SceneView.RepaintAll();
        }

        //Report what the current system actually contains
        SFSystemMap map = forge.Map;

        if (map != null && map.primaryStar != null)
        {
            int planets = 0;
            for (int i = 0; i < map.bodies.Count; i++)
                if (!map.bodies[i].isStar && map.bodies[i].planetData != null)
                    planets++;

            EditorGUILayout.HelpBox(
                map.primaryStar.star.SpectralClass + "-class primary, "
                + planets + " planets"
                + (map.secondaryStar != null ? ", binary companion" : "")
                + (map.circumbinary ? " (circumbinary)" : ""),
                MessageType.None);
        }
        else
            EditorGUILayout.HelpBox("No system generated yet.", MessageType.Warning);
    }
}
