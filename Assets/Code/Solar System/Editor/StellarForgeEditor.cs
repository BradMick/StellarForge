using UnityEditor;
using UnityEngine;

//Saving a system is an explicit, confirmed action.
//
//Writing generated bodies into an asset is destructive in a way a slider is not: it
//replaces the physics every placed object was positioned against, so everything authored
//on top of the old system goes with it. Reconciling is not possible — a different seed
//means a different planet count at different orbits — so the honest options are "keep the
//old system" or "wipe and take the new one", and the designer picks
[CustomEditor(typeof(StellarForge))]
public class StellarForgeEditor : Editor
{
    private SFSystemAsset targetAsset;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        StellarForge forge = (StellarForge)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("System Asset", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "A generated system is scratch until it is saved. Saving writes the physics — "
            + "stars, planets, orbits — into an asset that the spawner reads, so a build "
            + "never re-runs the simulation and play mode stops regenerating the system "
            + "you were looking at.",
            MessageType.Info);

        targetAsset = (SFSystemAsset)EditorGUILayout.ObjectField(
            "Save Into", targetAsset, typeof(SFSystemAsset), false);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Save System To Asset", GUILayout.Height(24)))
                SaveWithConfirmation(forge);

            if (GUILayout.Button("Create New System Asset"))
                CreateAndSave(forge);
        }

        if (targetAsset != null)
        {
            int content = targetAsset.ContentCount;
            EditorGUILayout.LabelField(
                targetAsset.PlanetCount + " planets stored"
                + (content > 0 ? ", " + content + " placed objects" : ""),
                EditorStyles.miniLabel);
        }
    }

    private void SaveWithConfirmation(StellarForge _forge)
    {
        if (targetAsset == null)
        {
            EditorUtility.DisplayDialog("No asset selected",
                "Pick a system asset to save into, or use Create New System Asset.", "OK");
            return;
        }

        //An empty asset has nothing to lose — do not make the designer dismiss a warning
        //about destroying nothing. The prompt has to mean something when it does appear
        if (targetAsset.PlanetCount > 0)
        {
            int content = targetAsset.ContentCount;

            string message = "\"" + targetAsset.name + "\" already holds a generated system ("
                + targetAsset.PlanetCount + " planets).\n\n"
                + "Overwriting replaces its physics with the system currently generated.";

            if (content > 0)
            {
                message += "\n\nThis will also remove " + content + " placed object"
                    + (content == 1 ? "" : "s") + " from the system. Placed content is "
                    + "positioned against the old planets and cannot be carried over.";
            }

            message += "\n\nThis cannot be undone.";

            if (!EditorUtility.DisplayDialog("Overwrite " + targetAsset.name + "?",
                    message, "Overwrite", "Cancel"))
                return;
        }

        Save(_forge, targetAsset);
    }

    private void CreateAndSave(StellarForge _forge)
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "New System Asset",
            string.IsNullOrEmpty(_forge.Designation) ? "New System" : _forge.Designation,
            "asset",
            "Where should this system be saved?");

        if (string.IsNullOrEmpty(path))
            return;

        SFSystemAsset asset = ScriptableObject.CreateInstance<SFSystemAsset>();
        AssetDatabase.CreateAsset(asset, path);

        targetAsset = asset;
        Save(_forge, asset);
    }

    private static void Save(StellarForge _forge, SFSystemAsset _asset)
    {
        _forge.SaveToAsset(_asset);

        EditorUtility.SetDirty(_asset);
        AssetDatabase.SaveAssets();

        Debug.Log("Saved " + _asset.PlanetCount + " planets into " + _asset.name, _asset);
        Selection.activeObject = _asset;
    }
}
