using System.Collections.Generic;
using UnityEngine;

//Turns the generated system into scene bodies: stars that light it and planets you can
//fly to. Positions come from the same ephemeris the map gizmos draw, evaluated against
//the universe clock, so what the editor overlay showed is what the world contains.
//This is where the generator and the planet engine meet — SFPlanetData supplies the
//physics, ApplyPhysicalProfile turns it into climate and biomes, and the archetype
//decides how the world is dressed
//Not ExecuteAlways: bodies are spawned in play mode, or on demand from the inspector.
//The editor's picture of the system is StellarForge's map overlay
[RequireComponent(typeof(StellarForge))]
public class SFSystemSpawner : MonoBehaviour
{
    [Header("Scale")]
    //Owns every real-to-world conversion. Without one, sensible gameplay defaults are used
    public SFSystemScaleProfile scaleProfile;

    [Header("Planets")]
    //Archetypes dress each world according to the type the generator classified it as —
    //ramp, biomes, materials, terrain character, which shells it gets. The spawner builds
    //the components itself; no template is needed
    public SFArchetypeLibrary archetypes;
    //Optional override for hand-authored special worlds. Leave empty for generated ones
    public GameObject planetPrefab;
    public bool spawnPlanets = true;
    //Gas giants have no surface to walk on; skip them until the giant renderer lands
    public bool spawnGasGiants = false;

    [Header("Stars")]
    public bool spawnStars = true;

    [Header("Behaviour")]
    public bool autoRefresh = true;
    //Report what the spawner is doing — how many bodies the map has and what it created
    public bool verboseLogging = false;
    //Bodies follow their orbits as the clock advances
    public bool animateOrbits = true;

    private readonly List<GameObject> spawnedStars = new List<GameObject>();
    private readonly List<GameObject> spawnedPlanets = new List<GameObject>();
    private readonly List<SFSystemMap.Body> starBodies = new List<SFSystemMap.Body>();
    private readonly List<SFSystemMap.Body> planetBodies = new List<SFSystemMap.Body>();

    private StellarForge forge;
    private int lastSystemHash = int.MinValue;

    private double WorldUnitsPerAU
    {
        get { return scaleProfile != null ? scaleProfile.worldUnitsPerAU : 1.496e8; }
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        //Tear the preview down before the play-mode swap, otherwise the edit-mode bodies
        //survive the transition and the scene ends up with two of everything
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif

        //Do NOT spawn from OnEnable. Creating child objects perturbs the hierarchy, which
        //in edit mode can cycle this component's enable state — which would spawn again,
        //destroying and rebuilding every planet in a loop. Update() handles spawning when
        //the system inputs actually change
        lastSystemHash = int.MinValue;
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
#endif

        //Likewise do not tear down here — an enable/disable cycle would then destroy live
        //bodies. Explicit teardown happens on play-mode transitions and on regeneration
    }

#if UNITY_EDITOR
    private void OnPlayModeChanged(UnityEditor.PlayModeStateChange _change)
    {
        //Clear on the way into and out of play mode; each side spawns its own bodies
        if (_change == UnityEditor.PlayModeStateChange.ExitingEditMode ||
            _change == UnityEditor.PlayModeStateChange.ExitingPlayMode)
        {
            Clear();
        }
    }
#endif

    private void Update()
    {
        //Spawning real bodies is a PLAY MODE job. In edit mode the scene is described by
        //StellarForge's map overlay instead: spawning there meant three ExecuteAlways
        //systems (generator, spawner, and each planet's own preview) writing to the same
        //hierarchy every frame, which destroyed and rebuilt planets endlessly.
        //Use the Spawn Now button on the inspector for a one-off editor preview
        if (!Application.isPlaying)
            return;

        if (autoRefresh)
        {
            int hash = ComputeSystemHash();
            if (hash != lastSystemHash)
            {
                lastSystemHash = hash;
                Refresh();
            }
        }

        if (animateOrbits)
            UpdatePositions();
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        //The generator may not have run yet in a fresh play session
        if (forge == null)
            forge = GetComponent<StellarForge>();

        if (forge != null)
            forge.EnsureGenerated();

        lastSystemHash = ComputeSystemHash();
        Refresh();
    }

    //Keyed on the INPUTS that define a system, never on the generated output. Hashing
    //generated values (masses, luminosities) meant every regeneration produced a slightly
    //different hash, so the spawner refreshed, which triggered another regeneration —
    //planets were destroyed and rebuilt every frame
    private int ComputeSystemHash()
    {
        if (forge == null)
            forge = GetComponent<StellarForge>();

        if (forge == null)
            return 0;

        int hash = 17;
        hash = hash * 31 + forge.SystemSeed;
        hash = hash * 31 + forge.primaryMass.GetHashCode();
        hash = hash * 31 + forge.companionMass.GetHashCode();
        hash = hash * 31 + forge.binarySeparation.GetHashCode();
        hash = hash * 31 + forge.binaryEccentricity.GetHashCode();
        hash = hash * 31 + (int)forge.binaryType;
        hash = hash * 31 + (scaleProfile != null ? scaleProfile.GetInstanceID() : 0);
        hash = hash * 31 + (planetPrefab != null ? planetPrefab.GetInstanceID() : 0);
        hash = hash * 31 + (archetypes != null ? archetypes.GetInstanceID() : 0);
        hash = hash * 31 + (spawnPlanets ? 1 : 0) + (spawnGasGiants ? 2 : 0) + (spawnStars ? 4 : 0);
        return hash;
    }

    public void Refresh()
    {
        //Clear() already removes everything this spawner created. SweepOrphans is only
        //for strays left by a domain reload, and running it here risks destroying bodies
        //that are legitimately live
        Clear();

        if (forge == null)
            forge = GetComponent<StellarForge>();

        SFSystemMap map = forge != null ? forge.Map : null;

        if (map == null || map.primaryStar == null)
        {
            //The map is empty — nothing to spawn. Say so plainly rather than silently
            //producing an empty system
            if (verboseLogging)
                Debug.LogWarning("SFSystemSpawner: no system map yet. Select the StellarForge "
                    + "object so it generates, or check that a system is actually being produced.");
            return;
        }

        if (verboseLogging)
        {
            int planetCount = 0;
            for (int i = 0; i < map.bodies.Count; i++)
                if (!map.bodies[i].isStar && map.bodies[i].planetData != null)
                    planetCount++;

            Debug.Log("SFSystemSpawner: map has " + map.bodies.Count + " bodies ("
                + planetCount + " planets). spawnPlanets=" + spawnPlanets
                + " spawnGasGiants=" + spawnGasGiants);
        }

        if (spawnStars)
        {
            SpawnStar(map.primaryStar);

            if (map.secondaryStar != null)
                SpawnStar(map.secondaryStar);
        }

        if (spawnPlanets)
        {
            for (int i = 0; i < map.bodies.Count; i++)
            {
                SFSystemMap.Body body = map.bodies[i];

                if (body.isStar || body.planetData == null)
                    continue;

                if (body.planetData.GasGiant && !spawnGasGiants)
                    continue;

                SpawnPlanet(body);
            }
        }

        if (verboseLogging)
            Debug.Log("SFSystemSpawner: spawned " + spawnedStars.Count + " stars and "
                + spawnedPlanets.Count + " planets.");

        UpdatePositions();
    }

    private void SpawnStar(SFSystemMap.Body _body)
    {
        GameObject starObject = new GameObject(_body.name);
        starObject.transform.SetParent(transform, false);
        //Never written to the scene file — these are generated, and a saved copy would
        //come back as a duplicate on the next load. Not HideAndDontSave: hidden objects
        //skip their component lifecycle in edit mode
        starObject.hideFlags = HideFlags.DontSave;

        float radius = scaleProfile != null
            ? scaleProfile.StarRadiusToWorld(_body.star.Radius)
            : _body.star.Radius * 6963.4f;

        SFStar star = starObject.AddComponent<SFStar>();
        star.worldUnitsPerAU = (float)WorldUnitsPerAU;
        star.Configure(_body.star, radius);

        spawnedStars.Add(starObject);
        starBodies.Add(_body);
    }

    //A generated world becomes a real planet. The generator supplies the physics, the
    //archetype supplies the art, and the spawner assembles the components — no template
    //needed, because everything a planet is made of is either derived or authored in an
    //archetype asset
    private void SpawnPlanet(SFSystemMap.Body _body)
    {
        SFPlanetData data = _body.planetData;

        GameObject planetObject = planetPrefab != null
            ? Instantiate(planetPrefab, transform)
            : new GameObject();

        if (planetPrefab == null)
            planetObject.transform.SetParent(transform, false);

        planetObject.name = _body.name + " (" + data.PlanetType + ")";
        //Generated content is never saved into the scene — see SpawnStar
        //DontSave keeps generated bodies out of the scene file, but NOT HideAndDontSave —
        //hidden objects do not get their component lifecycle run in edit mode, so the
        //planet would never generate its terrain
        planetObject.hideFlags = HideFlags.DontSave;

        float radius = scaleProfile != null
            ? scaleProfile.PlanetRadiusToWorld(data.EquitorialRadius)
            : data.EquitorialRadius * 0.001f;

        SFPlanetArchetype archetype = archetypes != null ? archetypes.Find(data.PlanetType) : null;

        //Terrain MUST exist before SFPlanet runs its setup — the planet resolves its
        //terrain reference during Start and generates from it. Adding SFPlanet first
        //meant it started with no terrain and produced nothing
        SFPlanetTerrain terrain = GetOrAdd<SFPlanetTerrain>(planetObject);
        terrain.ApplyPhysicalProfile(data);

        //Each world gets its own terrain seed, derived from the system so the same system
        //always produces the same worlds
        terrain.seed = forge.SystemSeed * 31 + data.PlanetIndex;

        terrain.heightScale = scaleProfile != null ? scaleProfile.terrainHeightScale : 0.08f;

        if (archetype != null)
        {
            terrain.colorRamp = archetype.colorRamp;
            terrain.biomes = archetype.biomes;
            terrain.surfaceMaterial = archetype.surfaceMaterial;
            terrain.cliffColor = archetype.cliffColor;

            if (archetype.heightScaleOverride > 0.0f)
                terrain.heightScale = archetype.heightScaleOverride;

            terrain.continentFrequency = archetype.continentFrequency;
            terrain.mountainAmount = archetype.mountainAmount;
            terrain.mountainMaskCoverage = archetype.mountainMaskCoverage;
            terrain.detailAmount = archetype.detailAmount;
            terrain.plainsBias = archetype.plainsBias;
            terrain.domainWarpStrength = archetype.domainWarpStrength;
        }

        //--- Culling: also before the planet, so it finds them during setup ---
        GetOrAdd<FrustumCuller>(planetObject);

        HorizonCuller horizon = GetOrAdd<HorizonCuller>(planetObject);
        horizon.sphereCenter = planetObject.transform;
        horizon.sphereRadius = radius;

        //--- The planet itself, last: everything it depends on now exists ---
        SFPlanet planet = GetOrAdd<SFPlanet>(planetObject);
        planet.planetRadius = radius;
        planet.generateColliders = archetype == null || archetype.generateColliders;
        planet.terrain = terrain;

        //--- Shells: the physics decides whether they exist, the archetype how they look ---
        bool wantsWater = terrain.HasOcean && (archetype == null || archetype.allowWater);

        if (wantsWater)
        {
            SFWaterShell water = GetOrAdd<SFWaterShell>(planetObject);
            water.enabled = true;

            if (archetype != null && archetype.waterMaterial != null)
                water.waterMaterial = archetype.waterMaterial;
        }
        else
        {
            SFWaterShell water = planetObject.GetComponent<SFWaterShell>();
            if (water != null)
                water.enabled = false;
        }

        bool wantsAtmosphere = terrain.HasAtmosphere && (archetype == null || archetype.allowAtmosphere);

        if (wantsAtmosphere)
        {
            SFAtmosphere atmosphere = GetOrAdd<SFAtmosphere>(planetObject);
            atmosphere.enabled = true;

            //Thicker air scatters more; the palette comes from the archetype
            atmosphere.density = Mathf.Clamp(data.SurfacePressure / 1000.0f, 0.2f, 2.5f);

            if (archetype != null)
            {
                atmosphere.dayColor = archetype.atmosphereDayColor;
                atmosphere.sunsetColor = archetype.atmosphereSunsetColor;
                atmosphere.heightFraction = archetype.atmosphereHeightFraction;
            }
        }
        else
        {
            SFAtmosphere atmosphere = planetObject.GetComponent<SFAtmosphere>();
            if (atmosphere != null)
                atmosphere.enabled = false;
        }

        //Terrain tiles are transient and hidden, so the planet itself would be impossible
        //to click or frame in the Scene view. A gizmo marker on the root gives it a
        //selectable, framable presence without adding anything to the render
        SFBodyMarker marker = GetOrAdd<SFBodyMarker>(planetObject);
        marker.radius = radius;
        marker.markerColor = PlanetMarkerColor(data.PlanetType);
        marker.label = _body.name + "  " + data.PlanetType;

        spawnedPlanets.Add(planetObject);
        planetBodies.Add(_body);
    }

    private static Color PlanetMarkerColor(SF_PLANET_TYPE _type)
    {
        switch (_type)
        {
            case SF_PLANET_TYPE.TERRESTRIAL: return new Color(0.35f, 0.75f, 1.0f);
            case SF_PLANET_TYPE.WATER:       return new Color(0.2f, 0.5f, 1.0f);
            case SF_PLANET_TYPE.MARTIAN:     return new Color(0.9f, 0.45f, 0.3f);
            case SF_PLANET_TYPE.VENUSIAN:    return new Color(1.0f, 0.8f, 0.4f);
            case SF_PLANET_TYPE.ICE:         return new Color(0.75f, 0.9f, 1.0f);
            case SF_PLANET_TYPE.JOVIAN:      return new Color(1.0f, 0.65f, 0.35f);
            case SF_PLANET_TYPE.SUB_JOVIAN:  return new Color(0.9f, 0.7f, 0.5f);
            case SF_PLANET_TYPE.GAS_DWARF:   return new Color(0.7f, 0.8f, 0.7f);
            case SF_PLANET_TYPE.ONE_FACE:    return new Color(1.0f, 0.5f, 0.6f);
            case SF_PLANET_TYPE.ASTEROIDS:   return new Color(0.6f, 0.55f, 0.5f);
            default:                         return new Color(0.7f, 0.7f, 0.7f);
        }
    }

    //Components come from the prefab when one is supplied, and are created otherwise
    private static T GetOrAdd<T>(GameObject _object) where T : Component
    {
        T component = _object.GetComponent<T>();
        return component != null ? component : _object.AddComponent<T>();
    }

    //Bodies follow the ephemeris against the universe clock — the same positions the
    //editor map draws, so nothing jumps when the game starts
    private void UpdatePositions()
    {
        double time = 0.0;

        if (Application.isPlaying)
        {
            SFUniverseClock clock = SFUniverseClock.Instance;

            if (clock != null)
            {
                //Keep the clock's rate in step with the profile
                if (scaleProfile != null)
                    clock.daysPerSecond = scaleProfile.daysPerSecond;

                time = clock.CurrentDay;
            }
        }

        for (int i = 0; i < spawnedStars.Count && i < starBodies.Count; i++)
            PlaceBody(spawnedStars[i], starBodies[i], time);

        for (int i = 0; i < spawnedPlanets.Count && i < planetBodies.Count; i++)
        {
            PlaceBody(spawnedPlanets[i], planetBodies[i], time);
            SpinPlanet(spawnedPlanets[i], planetBodies[i], time);
        }
    }

    private void PlaceBody(GameObject _object, SFSystemMap.Body _body, double _time)
    {
        if (_object == null)
            return;

        Vector3d positionAU = _body.GetPosition(_time);
        Vector3 world = positionAU.ToVector3(WorldUnitsPerAU);

        _object.transform.localPosition = world;

        if (verboseLogging)
            Debug.Log(_object.name
                + " | orbit=" + (_body.orbit != null ? _body.orbit.semiMajorAxis.ToString("0.000") + " AU" : "NONE")
                + " | parent=" + (_body.parent != null ? _body.parent.name : "barycenter")
                + " | posAU=(" + positionAU.x.ToString("0.000") + ", " + positionAU.z.ToString("0.000") + ")"
                + " | unitsPerAU=" + WorldUnitsPerAU.ToString("0")
                + " | world=" + world.ToString("F0"));
    }

    //Planets turn on their own axis, tilted, at the day length the generator computed
    private void SpinPlanet(GameObject _object, SFSystemMap.Body _body, double _time)
    {
        if (_object == null || _body.planetData == null)
            return;

        float dayHours = _body.planetData.LengthOfDay;
        if (dayHours <= 0.0f || float.IsNaN(dayHours) || float.IsInfinity(dayHours))
            return;

        //Rotations completed since epoch — days elapsed divided by the day length
        double rotations = (_time * 24.0) / dayHours;
        float spin = (float)((rotations % 1.0) * 360.0);

        _object.transform.localRotation = Quaternion.AngleAxis(_body.planetData.AxialTilt, Vector3.forward)
                                        * Quaternion.AngleAxis(spin, Vector3.up);
    }

    //Public teardown for the inspector's Clear button
    public void ClearSpawned()
    {
        Clear();
    }

    private void Clear()
    {
        DestroyAll(spawnedStars);
        DestroyAll(spawnedPlanets);

        starBodies.Clear();
        planetBodies.Clear();
        lastSystemHash = int.MinValue;
    }

    //Domain reloads and play-mode swaps can strand generated bodies whose owning lists
    //were cleared. Anything transient still parented here is an orphan by definition
    private void SweepOrphans()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;

            if ((child.hideFlags & HideFlags.DontSave) == 0)
                continue;

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private void DestroyAll(List<GameObject> _objects)
    {
        for (int i = 0; i < _objects.Count; i++)
        {
            if (_objects[i] == null)
                continue;

            if (Application.isPlaying)
                Destroy(_objects[i]);
            else
                DestroyImmediate(_objects[i]);
        }

        _objects.Clear();
    }
}
