using UnityEngine;

//Everything a generated world needs that physics cannot decide: which colour ramp, which
//biomes, what the terrain layers emphasise, whether it has water or sky, and what the
//materials look like. The generator classifies a planet (SF_PLANET_TYPE) and the matching
//archetype dresses it — so a Martian world comes out rust and cratered while a terrestrial
//one comes out blue and forested, from the same engine.
//Create via Assets ▸ Create ▸ StellarForge ▸ Planet Archetype
[CreateAssetMenu(fileName = "PlanetArchetype", menuName = "StellarForge/Planet Archetype")]
public class SFPlanetArchetype : ScriptableObject
{
    [Header("Applies To")]
    //Planet types this archetype dresses. The spawner picks the first archetype that
    //claims a world's classified type
    public SF_PLANET_TYPE[] planetTypes = new SF_PLANET_TYPE[] { SF_PLANET_TYPE.TERRESTRIAL };

    [Header("Surface Appearance")]
    public SFTerrainColorRamp colorRamp;
    public SFBiomeCollection biomes;
    public Material surfaceMaterial;
    public Color cliffColor = new Color(0.42f, 0.38f, 0.34f);

    [Header("Terrain Character")]
    //Relief as a fraction of planet radius. Overrides the scale profile when > 0, so a
    //rugged ice moon can be craggier than a smooth ocean world
    [Range(0.0f, 0.3f)] public float heightScaleOverride = 0.0f;
    [Range(0.25f, 5.0f)] public float continentFrequency = 1.5f;
    [Range(0.0f, 1.0f)] public float mountainAmount = 0.5f;
    [Range(0.0f, 1.0f)] public float mountainMaskCoverage = 0.3f;
    [Range(0.0f, 1.0f)] public float detailAmount = 0.12f;
    //> 1 flattens lowlands into plains; higher suits eroded or sedimentary worlds
    [Range(0.25f, 4.0f)] public float plainsBias = 2.0f;
    [Range(0.0f, 1.0f)] public float domainWarpStrength = 0.4f;

    [Header("Shells")]
    //Water is also gated by the planet's actual hydrosphere — this only says whether the
    //archetype permits one at all
    public bool allowWater = true;
    public Material waterMaterial;

    public bool allowAtmosphere = true;
    public Color atmosphereDayColor = new Color(0.35f, 0.55f, 1.0f);
    public Color atmosphereSunsetColor = new Color(1.0f, 0.45f, 0.2f);
    [Range(0.01f, 0.25f)] public float atmosphereHeightFraction = 0.05f;

    [Header("Physics")]
    //Airless rock does not need colliders on distant tiles
    public bool generateColliders = true;

    public bool Matches(SF_PLANET_TYPE _type)
    {
        if (planetTypes == null)
            return false;

        for (int i = 0; i < planetTypes.Length; i++)
            if (planetTypes[i] == _type)
                return true;

        return false;
    }
}
