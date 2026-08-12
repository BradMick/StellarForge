using System.Collections.Generic;
using UnityEngine;

//Creates the system's star bodies in the scene, positioned by the ephemeris.
//
//Edit-mode spawning goes through SFEditorDriver like every other generated thing — this
//component never rebuilds on its own initiative. (An earlier, pre-driver version ran its
//own edit-mode loop and fought the generator's gizmos and each body's preview; the driver
//is what makes edit-mode participation safe now.) Spawned stars are DontSave, so leaving
//play mode destroys the play-spawned set — without the driver hook the scene came back
//starless every time, and the button needed pressing again
[ExecuteAlways]
[RequireComponent(typeof(StellarForge))]
public class SFStarSpawner : MonoBehaviour
#if UNITY_EDITOR
    , SFEditorDriver.ISFEditorClient
#endif
{
    //Scale is universal — see SFScale. Nothing to assign, nothing to tune per system

    [Header("Behaviour")]
    //Keep stars present while editing: respawn through the driver whenever this enables
    //(scene load, domain reload, returning from play mode). Off = button and play only
    public bool autoSpawnInEditor = true;
    //Stars follow their orbits around the barycenter as time advances (binaries only —
    //a single star sits at the origin)
    public bool animateOrbits = true;
    //Simulated days per real second. Playback speed, not scale — safe to vary per scene
    public double daysPerSecond = SFScale.DAYS_PER_SECOND;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<SFSystemMap.Body> bodies = new List<SFSystemMap.Body>();

    private StellarForge forge;
    private double currentDay;

#if UNITY_EDITOR
    //Stars are built after the generator, before planets and shells
    public SFEditorDriver.SF_REBUILD_ORDER RebuildOrder
    {
        get { return SFEditorDriver.SF_REBUILD_ORDER.STAR; }
    }

    //Called only by SFEditorDriver
    public void EditorRebuild()
    {
        if (autoSpawnInEditor)
            Spawn();
    }
#endif

    private void OnEnable()
    {
        if (Application.isPlaying)
            return;

#if UNITY_EDITOR
        //Queue a respawn; the driver decides when. This is what brings the stars back
        //after play mode tears down the play-spawned set
        SFEditorDriver.MarkDirty(this);
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        SFEditorDriver.Forget(this);
#endif
    }

    private void Start()
    {
        //Play mode spawns automatically; edit mode spawns through the driver
        if (Application.isPlaying)
            Spawn();
    }

    private void Update()
    {
        if (!Application.isPlaying || !animateOrbits)
            return;

        currentDay += Time.deltaTime * daysPerSecond;
        UpdatePositions();
    }

    //Build the star bodies. Safe to call repeatedly — it clears first
    public void Spawn()
    {
        Clear();

        if (forge == null)
            forge = GetComponent<StellarForge>();

        //EnsureGenerated, not Map: this runs from Start in play mode, and Start order
        //between two components on the same GameObject is undefined — reading Map directly
        //hit a null map whenever this won the race against StellarForge.Start
        SFSystemMap map = forge != null ? forge.EnsureGenerated() : null;

        if (map == null || map.primaryStar == null)
        {
            Debug.LogWarning("SFStarSpawner: the generator produced no system. Check the "
                + "StellarForge settings on this object.");
            return;
        }

        SpawnStar(map.primaryStar);

        if (map.secondaryStar != null)
            SpawnStar(map.secondaryStar);

        UpdatePositions();
    }

    private void SpawnStar(SFSystemMap.Body _body)
    {
        GameObject starObject = new GameObject(_body.name);
        starObject.transform.SetParent(transform, false);

        //Kept out of the scene file: these are generated, and a saved copy would return
        //as a duplicate. Not HideAndDontSave — hidden objects skip their component
        //lifecycle in edit mode, which would stop the star building its geometry
        starObject.hideFlags = HideFlags.DontSave;

        float radius = SFScale.StarRadiusToWorld(_body.star.Radius);

        SFStar star = starObject.AddComponent<SFStar>();
        star.worldUnitsPerAU = (float)SFScale.WORLD_UNITS_PER_AU;
        star.Configure(_body.star, radius);

        spawned.Add(starObject);
        bodies.Add(_body);
    }

    private void UpdatePositions()
    {
        for (int i = 0; i < spawned.Count && i < bodies.Count; i++)
        {
            if (spawned[i] == null)
                continue;

            spawned[i].transform.localPosition = bodies[i].GetPosition(currentDay).ToVector3(SFScale.WORLD_UNITS_PER_AU);
        }
    }

    public void Clear()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] == null)
                continue;

            if (Application.isPlaying)
                Destroy(spawned[i]);
            else
                DestroyImmediate(spawned[i]);
        }

        spawned.Clear();
        bodies.Clear();

        //The list above is not serialized, so a domain reload or play transition loses
        //track of stars that survived it (they are DontSave — scene loads do not destroy
        //them). Sweep the actual children so a lost list can never mean duplicate suns
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;

            if ((child.hideFlags & HideFlags.DontSave) != 0 && child.GetComponent<SFStar>() != null)
            {
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }
    }
}
