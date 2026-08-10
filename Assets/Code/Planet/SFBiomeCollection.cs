using System.Collections.Generic;
using UnityEngine;

//One biome definition. Temperature ranges are in real kelvin so the same asset behaves
//correctly across whatever worlds the solar system generator produces — a 250K planet
//naturally lands in the cold biomes, a 320K one in the hot ones
[System.Serializable]
public class SFBiome
{
    public string name = "Biome";

    [Header("Climate Range")]
    public float temperatureMinK = 260.0f;
    public float temperatureMaxK = 300.0f;
    [Range(0.0f, 1.0f)] public float moistureMin = 0.0f;
    [Range(0.0f, 1.0f)] public float moistureMax = 1.0f;

    [Header("Ground")]
    public Color groundColor = Color.gray;
    //How strongly the biome ground color overrides the height ramp
    [Range(0.0f, 1.0f)] public float groundTint = 0.5f;

    [Header("Vegetation")]
    public Color vegetationColor = new Color(0.20f, 0.40f, 0.15f);
    [Range(0.0f, 1.0f)] public float treeDensity = 0.0f;
    [Range(0.0f, 1.0f)] public float grassDensity = 0.0f;

    public SFBiome() { }

    public SFBiome(string _name, float _tMinK, float _tMaxK, float _mMin, float _mMax,
                   Color _ground, float _tint, Color _vegetation, float _trees, float _grass)
    {
        name = _name;
        temperatureMinK = _tMinK;
        temperatureMaxK = _tMaxK;
        moistureMin = _mMin;
        moistureMax = _mMax;
        groundColor = _ground;
        groundTint = _tint;
        vegetationColor = _vegetation;
        treeDensity = _trees;
        grassDensity = _grass;
    }
}

//Reusable biome set (Assets ▸ Create ▸ StellarForge ▸ Biome Collection). Share one asset
//across many planets; the climate model (driven by each planet's physical profile from the
//solar system generator) decides which biomes actually appear and where
[CreateAssetMenu(fileName = "BiomeCollection", menuName = "StellarForge/Biome Collection")]
public class SFBiomeCollection : ScriptableObject
{
    public List<SFBiome> biomes = new List<SFBiome>();

    //Bumped on every inspector edit so live planets know to rebuild
    [System.NonSerialized] private int version;
    public int Version { get { return version; } }

    private void OnValidate()
    {
        version++;
    }

    //Editor default when the asset is created: an earthlike Whittaker-style set
    private void Reset()
    {
        biomes = new List<SFBiome>
        {
            new SFBiome("Ice Cap",             0.0f, 255.0f, 0.00f, 1.0f,
                new Color(0.92f, 0.95f, 1.00f), 0.90f, Color.white,                     0.00f, 0.00f),
            new SFBiome("Tundra",            250.0f, 275.0f, 0.00f, 0.7f,
                new Color(0.55f, 0.52f, 0.45f), 0.60f, new Color(0.45f, 0.50f, 0.35f), 0.05f, 0.25f),
            new SFBiome("Boreal Forest",     265.0f, 285.0f, 0.35f, 1.0f,
                new Color(0.40f, 0.42f, 0.35f), 0.40f, new Color(0.10f, 0.28f, 0.16f), 0.65f, 0.30f),
            new SFBiome("Grassland",         278.0f, 298.0f, 0.15f, 0.5f,
                new Color(0.55f, 0.52f, 0.35f), 0.40f, new Color(0.42f, 0.52f, 0.22f), 0.08f, 0.75f),
            new SFBiome("Temperate Forest",  278.0f, 296.0f, 0.45f, 1.0f,
                new Color(0.42f, 0.40f, 0.30f), 0.35f, new Color(0.16f, 0.38f, 0.14f), 0.80f, 0.40f),
            new SFBiome("Desert",            290.0f, 340.0f, 0.00f, 0.22f,
                new Color(0.78f, 0.66f, 0.44f), 0.85f, new Color(0.45f, 0.48f, 0.28f), 0.01f, 0.05f),
            new SFBiome("Savanna",           293.0f, 320.0f, 0.20f, 0.5f,
                new Color(0.72f, 0.62f, 0.38f), 0.55f, new Color(0.52f, 0.54f, 0.24f), 0.12f, 0.60f),
            new SFBiome("Tropical Rainforest", 294.0f, 315.0f, 0.50f, 1.0f,
                new Color(0.30f, 0.32f, 0.20f), 0.30f, new Color(0.07f, 0.30f, 0.10f), 0.95f, 0.50f),
        };
    }
}
