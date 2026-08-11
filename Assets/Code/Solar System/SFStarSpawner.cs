using System.Collections.Generic;
using UnityEngine;

//Creates the system's star bodies in the scene, positioned by the ephemeris.
//
//Deliberately NOT ExecuteAlways, and it never spawns from OnEnable. An earlier version
//did both, which put it in conflict with the generator's gizmo drawing and each body's
//own editor preview — bodies were destroyed and rebuilt every frame. Spawning happens in
//play mode, or when you press the button on the inspector. Nothing here runs on its own
[RequireComponent(typeof(StellarForge))]
public class SFStarSpawner : MonoBehaviour
{
    [Header("Scale")]
    //Owns the AU-to-world and radius conversions. Sensible defaults are used without one
    public SFSystemScaleProfile scaleProfile;

    [Header("Behaviour")]
    //Stars follow their orbits around the barycenter as time advances (binaries only —
    //a single star sits at the origin)
    public bool animateOrbits = true;
    //Simulated days per real second when no scale profile is assigned
    public double daysPerSecond = 0.5;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<SFSystemMap.Body> bodies = new List<SFSystemMap.Body>();

    private StellarForge forge;
    private double currentDay;

    private double WorldUnitsPerAU
    {
        get { return scaleProfile != null ? scaleProfile.worldUnitsPerAU : 100000.0; }
    }

    private double DaysPerSecond
    {
        get { return scaleProfile != null ? scaleProfile.daysPerSecond : daysPerSecond; }
    }

    private void Start()
    {
        //Play mode spawns automatically; edit mode uses the inspector button
        Spawn();
    }

    private void Update()
    {
        if (!Application.isPlaying || !animateOrbits)
            return;

        currentDay += Time.deltaTime * DaysPerSecond;
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

        float radius = scaleProfile != null
            ? scaleProfile.StarRadiusToWorld(_body.star.Radius)
            : _body.star.Radius * 35000.0f;

        SFStar star = starObject.AddComponent<SFStar>();
        star.worldUnitsPerAU = (float)WorldUnitsPerAU;
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

            spawned[i].transform.localPosition = bodies[i].GetPosition(currentDay).ToVector3(WorldUnitsPerAU);
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
    }
}
