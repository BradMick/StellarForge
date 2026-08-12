using System.Collections.Generic;
using UnityEngine;

//A generated solar system, saved.
//
//StellarForge runs a real accretion simulation followed by the Fogg environment chain and,
//optionally, a habitability seed search — expensive, and the whole point of it is that the
//answer is FIXED once a designer is happy with it. This asset is that answer: the physical
//truth of one system, written once at author time and read thereafter.
//
//Why an asset rather than living on the component:
//  - A shipped build never runs accretion. It loads data
//  - Play mode stops regenerating the system you were just looking at in edit mode
//  - Systems become content: versionable, diffable, and hand-placeable by a later
//    galaxy layer that instantiates them on demand
//
//What lives here, and how freely each part changes:
//  PHYSICS  — what the generator produced. Authoritative. Changing it means regenerating,
//             which is deliberate and destructive (see SFSystemAsset.ContentCount)
//  AUTHORED — the little a designer decides per planet: the terrain seed, and the content
//             manifest. NOT a dressing layer — a planet's look is derived from its
//             SF_PLANET_TYPE and physics, so there is nothing here that could contradict
//             the generator (see SFPlanetAppearance)
[CreateAssetMenu(fileName = "New System", menuName = "StellarForge/System", order = 0)]
public class SFSystemAsset : ScriptableObject
{
    [Header("Identity")]
    public string designation = "Sol";
    public string systemName = "The Solar System";

    [Header("Provenance")]
    //What produced this system. Kept so a designer can see where it came from, and so
    //regenerating an existing asset can start from the same inputs. NOT used at load —
    //loading reads the stored bodies, it never re-runs the simulation
    public int systemSeed;
    public float primaryMass = 1.0f;
    public bool overrodeLuminosity;
    public float primaryLuminosity = 1.0f;
    public float companionMass;
    public float binarySeparation;
    public float binaryEccentricity;
    public SF_BINARY_TYPE binaryType = SF_BINARY_TYPE.S_TYPE_CIRCUMSTELLAR;

    [Header("Generated Physics (authoritative — regenerate to change)")]
    //Both stars are full SFSun descriptions.
    //
    //hasCompanion is not redundant: Unity cannot serialize a null managed reference, so a
    //null companion comes back from disk as a zeroed SFSun rather than as null. Without the
    //flag every single-star system would load as a binary whose companion has zero mass
    [SerializeField] private SFSun primaryStar = new SFSun();
    [SerializeField] private bool hasCompanion;
    [SerializeField] private SFSun companionStar = new SFSun();
    //Physics per planet, in generated order. A planet's index in this list is its identity:
    //appearance and content both key off it
    [SerializeField] private List<SFPlanetData> planets = new List<SFPlanetData>();

    [Header("Designer Layer")]
    //One entry per planet, index-aligned with `planets`. Split from physics because these
    //are the only things a designer may freely change
    [SerializeField] private List<SFPlanetAppearance> appearance = new List<SFPlanetAppearance>();

    public SFSun PrimaryStar { get { return primaryStar; } }

    //Null for a single star, matching what the generator hands out — callers already
    //branch on a null companion, so the stored flag stays an implementation detail
    public SFSun CompanionStar { get { return hasCompanion ? companionStar : null; } }
    public int PlanetCount { get { return planets.Count; } }

    public SFPlanetData GetPlanet(int _index)
    {
        return _index >= 0 && _index < planets.Count ? planets[_index] : null;
    }

    //Never null for a valid index: a planet with no authored appearance still needs
    //defaults for the spawner to build from
    public SFPlanetAppearance GetAppearance(int _index)
    {
        if (_index < 0 || _index >= planets.Count)
            return null;

        //Physics and appearance can fall out of step if the list was regenerated with a
        //different planet count — grow to match rather than indexing past the end
        while (appearance.Count < planets.Count)
            appearance.Add(new SFPlanetAppearance());

        return appearance[_index];
    }

    //Total placed objects across every planet. The confirmation prompt before a
    //destructive regeneration reports this
    public int ContentCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < appearance.Count; i++)
                total += appearance[i] != null ? appearance[i].content.Count : 0;

            return total;
        }
    }

    //Replace the stored system wholesale. DESTRUCTIVE: the caller is responsible for having
    //confirmed with the user, because every appearance tweak and placed object goes with it.
    //Reconciling old content against a newly generated system is not attempted — a new seed
    //means a different planet count with different masses at different orbits, so there is
    //no correspondence to preserve and a silent mismatch is worse than an honest wipe
    public void StoreGeneratedSystem(SFSun _primary, SFSun _companion, List<SFPlanetData> _planets)
    {
        primaryStar = _primary ?? new SFSun();

        //Store a real object either way; the flag is what says whether it means anything
        hasCompanion = _companion != null;
        companionStar = _companion ?? new SFSun();

        planets.Clear();
        if (_planets != null)
            planets.AddRange(_planets);

        //Appearance is index-keyed, so a new planet list invalidates all of it
        appearance.Clear();
        for (int i = 0; i < planets.Count; i++)
            appearance.Add(new SFPlanetAppearance());
    }
}

//The only per-planet things a designer authors.
//
//Deliberately tiny. A planet's look is DERIVED from its SF_PLANET_TYPE and the physics the
//generator computed for it — a Martian world reads as Martian because it genuinely has no
//hydrosphere and six millibars of atmosphere, not because someone dressed it that way. That
//means a hot Mars and a cold Mars differ on their own, and it makes an
//archetype-says-ocean/physics-says-dry contradiction unrepresentable.
//
//So there are no ramp, material, or terrain-character fields here. Wanting a world to look
//different means changing what it IS, in the generator, and regenerating
[System.Serializable]
public class SFPlanetAppearance
{
    //Reshuffles WHERE continents land without changing what the world is — radius, gravity,
    //temperature and hydrosphere all hold, so the physics stays exactly as generated. The
    //generator has no opinion about which arrangement of coastlines it produced, which is
    //what makes this the one appearance choice that is legitimately a designer's
    public int terrainSeed = 0;

    //Placed content on this world, as a manifest rather than as objects. The scene holds
    //real GameObjects a designer can select and edit; this records what they are and where,
    //so the system can be rebuilt from the asset alone
    public List<SFPlacedObject> content = new List<SFPlacedObject>();
}

//One placed thing on a planet's surface.
//
//Anchored as (direction, rotation) per architecture Law 2 — never world coordinates. A
//planet rotates, orbits, and gets rebased around a moving anchor; a world position would be
//wrong by the next frame, while a direction on the unit sphere stays true forever
[System.Serializable]
public class SFPlacedObject
{
    //What to instantiate
    public GameObject prefab;

    //Where, as a unit-sphere direction from the planet's centre. Combined with the terrain
    //height at that direction, this is a complete surface position at any scale
    public Vector3 direction = Vector3.up;

    //Spin about the local surface normal, in degrees. The rest of the orientation follows
    //from the normal itself, so one angle is all an authored placement needs
    public float headingDegrees = 0.0f;

    //Lift along the normal, in world units. Landing pads sit flush; a hab module on legs
    //does not
    public float altitudeOffset = 0.0f;

    public float scale = 1.0f;

    //Designer-facing label, so a content list is readable without resolving every prefab
    public string label = "";
}
