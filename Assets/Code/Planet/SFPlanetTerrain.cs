using UnityEngine;

//The planet's physical climate parameters — the bridge between the solar system generator
//and the terrain/biome engine. ApplyPhysicalProfile(SFPlanetData) fills these from
//generated physics; designers then override freely. All pure data, HLSL-portable
[System.Serializable]
public class SFClimateProfile
{
    //Mean surface temperature in kelvin (Earth ≈ 288)
    public float meanSurfaceTempK   = 288.0f;
    //Equator-to-pole temperature span in kelvin
    public float equatorPoleDeltaK  = 50.0f;
    //Temperature drop from sea level to the highest peaks, in kelvin
    public float altitudeLapseK     = 60.0f;
    //Surface pressure in millibars (Earth ≈ 1013). Thin atmospheres dry out and kill vegetation
    public float surfacePressureMb  = 1000.0f;
    //Fraction of the surface covered by water (drives moisture baseline)
    [Range(0.0f, 1.0f)] public float hydrosphere   = 0.6f;
    [Range(0.0f, 1.0f)] public float cloudCoverage = 0.4f;
    [Range(0.0f, 1.0f)] public float iceCoverage   = 0.1f;
    public float axialTiltDeg = 23.0f;

    //Local climate texture
    [Range(0.5f, 10.0f)] public float climateNoiseFrequency  = 3.0f;
    [Range(0.0f, 25.0f)] public float climateNoiseStrengthK  = 8.0f;
    [Range(0.25f, 6.0f)] public float moistureFrequency      = 1.8f;
}

//Valid baked color map resolutions (per cube face) — power-of-two only
public enum SF_COLOR_MAP_RESOLUTION
{
    Res256 = 256,
    Res512 = 512,
    Res1024 = 1024,
    Res2048 = 2048,
    Res4096 = 4096
}

//Terrain engine for a planet: layered, deterministic height field (domain-warped continents,
//ridged mountain belts, fine detail, plains redistribution) plus a physically-informed
//climate model that drives biome selection and vegetation. Everything is a pure function
//of the unit sphere direction — the contract that keeps stitching, normals, raycasts and
//the future GPU port consistent.
//Attach next to SFPlanet; auto-found. The solar system generator calls
//ApplyPhysicalProfile(SFPlanetData) to set the physical bones; designers customize the rest
public class SFPlanetTerrain : MonoBehaviour
{
    [Header("Appearance")]
    //Material for every surface tile; leave empty to use the bundled vertex-color shader
    public Material surfaceMaterial;
    //Height-to-color ramp asset (Assets ▸ Create ▸ StellarForge ▸ Terrain Color Ramp)
    public SFTerrainColorRamp colorRamp;
    //Biome definitions (Assets ▸ Create ▸ StellarForge ▸ Biome Collection)
    public SFBiomeCollection biomes;
    //Resolution of the baked per-face color maps (per-pixel surface color; GPU baker only)
    public SF_COLOR_MAP_RESOLUTION colorMapResolutionSetting = SF_COLOR_MAP_RESOLUTION.Res1024;
    public int colorMapResolution { get { return (int)colorMapResolutionSetting; } }

    [Header("Height Field")]
    public int   seed        = 0;
    //Peak terrain height as a fraction of planet radius (0.02 = 2% of radius)
    public float heightScale = 0.02f;

    [Header("Continents")]
    [Range(0.25f, 5.0f)] public float continentFrequency = 1.2f;
    [Range(1, 8)] public int continentOctaves = 4;
    //Bends the noise space so landmasses flow (peninsulas, bays) instead of forming blobs
    [Range(0.0f, 1.0f)] public float domainWarpStrength  = 0.35f;
    [Range(0.25f, 5.0f)] public float domainWarpFrequency = 1.6f;

    [Header("Mountains")]
    [Range(0.5f, 12.0f)] public float mountainFrequency = 3.5f;
    [Range(1, 8)] public int mountainOctaves = 4;
    //Mountain amplitude relative to the continent base
    [Range(0.0f, 1.0f)] public float mountainAmount = 0.7f;
    //Low-frequency mask restricts ridged mountains into belts/chains
    [Range(0.25f, 4.0f)] public float mountainMaskFrequency = 1.4f;
    //Fraction of the planet where mountain ranges may appear
    [Range(0.0f, 1.0f)] public float mountainMaskCoverage = 0.4f;

    [Header("Detail")]
    [Range(1.0f, 40.0f)] public float detailFrequency = 10.0f;
    [Range(1, 8)] public int detailOctaves = 3;
    //Seasoning, not structure — past ~0.25 the planet reads as gravel
    [Range(0.0f, 1.0f)] public float detailAmount = 0.15f;

    [Header("Shaping")]
    //> 1 flattens lowlands into plains and shelves while keeping peaks steep
    [Range(0.25f, 4.0f)] public float plainsBias = 1.7f;
    //Amplitude falloff per octave — natural terrain lives near 0.45-0.55
    [Range(0.1f, 0.9f)] public float persistence = 0.5f;
    //Frequency step between octaves — natural terrain lives near 2. Large values skip the
    //mid frequencies entirely and produce a uniform "bumpy noise ball"
    [Range(1.5f, 3.5f)] public float lacunarity = 2.0f;
    //Clamp everything below oceanLevel flat (only useful without a water shell)
    public bool flattenOcean = false;
    //Sea level in normalized height units [-1, 1] — shared by the water shell and color ramp
    [Range(-1.0f, 1.0f)] public float oceanLevel = 0.0f;

    [Header("Climate")]
    public SFClimateProfile climate = new SFClimateProfile();

    [Header("Surface Layers")]
    //Exposed rock where the surface is steep, regardless of altitude or biome
    public Color cliffColor = new Color(0.42f, 0.38f, 0.34f);
    [Range(0.0f, 1.0f)] public float cliffThreshold = 0.12f;
    [Range(0.01f, 0.5f)] public float cliffSoftness = 0.1f;
    //Patchiness of vegetation within a biome
    public float vegetationNoiseFrequency = 8.0f;

    //Noise instances per purpose, all derived from the seed
    private PerlinNoise3D baseNoise, warpNoise, mountainNoise, maskNoise, detailNoise;
    private PerlinNoise3D climateNoise, moistureNoise, vegetationNoise;
    private int builtSeed = int.MinValue;

    private static readonly Vector3 warpOffsetA = new Vector3(17.3f, 9.1f, 4.7f);
    private static readonly Vector3 warpOffsetB = new Vector3(5.2f, 12.8f, 31.4f);

    //Bumped on every inspector edit; combined with asset versions so live planets can
    //detect any change and rebuild their tiles in real time
    [System.NonSerialized] private int version;

    private void OnValidate()
    {
        //Range attributes only clamp future inspector edits — repair any stored values
        //that predate the ranges (out-of-range lacunarity is the classic bumpy-ball cause)
        lacunarity = Mathf.Clamp(lacunarity, 1.5f, 3.5f);
        persistence = Mathf.Clamp(persistence, 0.1f, 0.9f);
        detailAmount = Mathf.Clamp01(detailAmount);
        climate.climateNoiseFrequency = Mathf.Clamp(climate.climateNoiseFrequency, 0.5f, 10.0f);
        climate.climateNoiseStrengthK = Mathf.Clamp(climate.climateNoiseStrengthK, 0.0f, 25.0f);
        climate.moistureFrequency = Mathf.Clamp(climate.moistureFrequency, 0.25f, 6.0f);

        version++;
    }

    public int CombinedVersion
    {
        get
        {
            int v = version * 397;
            if (colorRamp != null) v += colorRamp.Version;
            if (biomes != null) v += biomes.Version * 7919;
            return v;
        }
    }

    private void EnsureNoise()
    {
        if (baseNoise != null && builtSeed == seed)
            return;

        builtSeed = seed;
        baseNoise       = new PerlinNoise3D(seed);
        warpNoise       = new PerlinNoise3D(seed + 101);
        mountainNoise   = new PerlinNoise3D(seed + 202);
        maskNoise       = new PerlinNoise3D(seed + 303);
        detailNoise     = new PerlinNoise3D(seed + 404);
        climateNoise    = new PerlinNoise3D(seed + 505);
        moistureNoise   = new PerlinNoise3D(seed + 606);
        vegetationNoise = new PerlinNoise3D(seed + 707);
    }

    //Permutation tables for the GPU baker, packed in the kernel's table order:
    //base, warp, mountain, mask, detail, climate, moisture, vegetation (8 × 512 ints)
    public void FillPermutationTables(int[] _destination)
    {
        EnsureNoise();
        baseNoise.CopyTo(_destination, 0);
        warpNoise.CopyTo(_destination, 512);
        mountainNoise.CopyTo(_destination, 1024);
        maskNoise.CopyTo(_destination, 1536);
        detailNoise.CopyTo(_destination, 2048);
        climateNoise.CopyTo(_destination, 2560);
        moistureNoise.CopyTo(_destination, 3072);
        vegetationNoise.CopyTo(_destination, 3584);
    }

    #region Height Field

    //Height in world units above (below) the sphere surface for a unit-sphere direction
    public float GetHeight(Vector3 _direction, float _radius)
    {
        EnsureNoise();

        //Domain warp: bend the sample space with low-frequency vector noise so landmasses
        //flow organically instead of forming round blobs
        Vector3 warped = _direction + domainWarpStrength * new Vector3(
            warpNoise.Sample(_direction * domainWarpFrequency),
            warpNoise.Sample(_direction * domainWarpFrequency + warpOffsetA),
            warpNoise.Sample(_direction * domainWarpFrequency + warpOffsetB));

        float continents = FBM(baseNoise, warped, continentFrequency, continentOctaves);

        //Ridged mountains gated into belts by a low-frequency mask — ranges form in chains
        //like tectonics, not sprinkled uniformly
        float maskSample = 0.5f + 0.5f * FBM(maskNoise, _direction, mountainMaskFrequency, 2);
        float mask = SmoothStep01((maskSample - (1.0f - mountainMaskCoverage - 0.1f)) / 0.2f);
        float ridged = RidgedFBM(mountainNoise, warped, mountainFrequency, mountainOctaves);

        float detail = FBM(detailNoise, _direction, detailFrequency, detailOctaves);

        float h = continents + mountainAmount * mask * ridged + detailAmount * detail;
        h /= 1.0f + mountainAmount + detailAmount;
        h = Mathf.Clamp(h, -1.0f, 1.0f);

        //Redistribution: real hypsometry is bottom-heavy — flatten lowlands into plains
        //and coastal shelves, keep peaks dramatic
        h = Mathf.Sign(h) * Mathf.Pow(Mathf.Abs(h), plainsBias);

        if (flattenOcean && h < oceanLevel)
            h = oceanLevel;

        return h * heightScale * _radius;
    }

    //Highest point terrain can reach above the sphere surface — used by the analytic raycast
    public float MaxHeight(float _radius)
    {
        return heightScale * _radius;
    }

    private float FBM(PerlinNoise3D _noise, Vector3 _p, float _frequency, int _octaves)
    {
        float amplitude = 1.0f, sum = 0.0f, range = 0.0f, frequency = _frequency;

        for (int i = 0; i < _octaves; i++)
        {
            sum += _noise.Sample(_p * frequency) * amplitude;
            range += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return sum / range;
    }

    //Sharp ridgelines and V-valleys: fold the noise with 1-|n| and square for crestiness
    private float RidgedFBM(PerlinNoise3D _noise, Vector3 _p, float _frequency, int _octaves)
    {
        float amplitude = 1.0f, sum = 0.0f, range = 0.0f, frequency = _frequency;

        for (int i = 0; i < _octaves; i++)
        {
            float signal = 1.0f - Mathf.Abs(_noise.Sample(_p * frequency));
            signal *= signal;
            sum += signal * amplitude;
            range += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return sum / range;
    }

    private static float SmoothStep01(float _t)
    {
        _t = Mathf.Clamp01(_t);
        return _t * _t * (3.0f - 2.0f * _t);
    }

    #endregion

    #region Climate & Biomes

    //Local climate at a surface point. Baselines come from the physical profile (solar
    //system generator), local variation from latitude, altitude and noise
    public void GetClimate(Vector3 _direction, out float _temperatureK, out float _moisture)
    {
        float range = heightScale > 0.0f ? heightScale : 1.0f;
        float hNorm = Mathf.Clamp(GetHeight(_direction, 1.0f) / range, -1.0f, 1.0f);
        ComputeClimate(_direction, hNorm, out _temperatureK, out _moisture);
    }

    private void ComputeClimate(Vector3 _direction, float _heightNorm, out float _temperatureK, out float _moisture)
    {
        EnsureNoise();

        float latitude = Mathf.Abs(_direction.y);
        float sea = Mathf.Clamp(oceanLevel, -0.99f, 0.99f);
        float altitude01 = Mathf.Clamp01((_heightNorm - sea) / (1.0f - sea));

        //High axial tilt averages seasons out and softens the equator-pole gradient
        float gradient = climate.equatorPoleDeltaK * (1.0f - 0.3f * Mathf.Clamp01(climate.axialTiltDeg / 90.0f));

        _temperatureK = climate.meanSurfaceTempK
                      + gradient * (0.5f - latitude)
                      - climate.altitudeLapseK * altitude01
                      - climate.iceCoverage * 20.0f * latitude
                      + FBM(climateNoise, _direction, climate.climateNoiseFrequency, 2) * climate.climateNoiseStrengthK;

        //Thin atmospheres hold little moisture no matter how much surface water exists
        float pressureFactor = Mathf.Clamp01(Mathf.InverseLerp(10.0f, 500.0f, climate.surfacePressureMb));
        float baseline = climate.hydrosphere * 0.55f + climate.cloudCoverage * 0.3f;

        _moisture = Mathf.Clamp01(baseline + FBM(moistureNoise, _direction, climate.moistureFrequency, 3) * 0.45f)
                  * Mathf.Sqrt(pressureFactor);
    }

    //Vegetation only survives in a liquid-water temperature band with enough atmosphere —
    //Mars-like and frozen worlds come out barren purely from their physics
    private float VegetationViability(float _temperatureK)
    {
        float cold = Mathf.Clamp01(Mathf.InverseLerp(256.0f, 271.0f, _temperatureK));
        float hot = 1.0f - Mathf.Clamp01(Mathf.InverseLerp(312.0f, 332.0f, _temperatureK));
        float pressure = Mathf.Clamp01(Mathf.InverseLerp(60.0f, 300.0f, climate.surfacePressureMb));
        return cold * hot * pressure;
    }

    private static float RangeWeight(float _value, float _min, float _max, float _margin)
    {
        if (_value < _min) return Mathf.Clamp01(1.0f - (_min - _value) / _margin);
        if (_value > _max) return Mathf.Clamp01(1.0f - (_value - _max) / _margin);
        return 1.0f;
    }

    //Blended biome data at a climate point. Returns total weight (0 = no biome matches)
    private float BlendBiomes(float _temperatureK, float _moisture,
                              ref Color _ground, ref float _tint,
                              ref Color _vegetation, ref float _trees, ref float _grass)
    {
        float total = 0.0f;

        for (int i = 0; i < biomes.biomes.Count; i++)
        {
            SFBiome b = biomes.biomes[i];

            float w = RangeWeight(_temperatureK, b.temperatureMinK, b.temperatureMaxK, 8.0f)
                    * RangeWeight(_moisture, b.moistureMin, b.moistureMax, 0.12f);

            if (w <= 0.0f)
                continue;

            total += w;
            _ground += b.groundColor * w;
            _tint += b.groundTint * w;
            _vegetation += b.vegetationColor * w;
            _trees += b.treeDensity * w;
            _grass += b.grassDensity * w;
        }

        if (total > 0.0001f)
        {
            float inv = 1.0f / total;
            _ground *= inv;
            _tint *= inv;
            _vegetation *= inv;
            _trees *= inv;
            _grass *= inv;
        }

        return total;
    }

    //Tree/grass density at a surface point — the hook the future scattering system samples.
    //Returns zeros underwater, on cliffs-not-considered (pass slope handling to caller),
    //outside viability, or with no biome asset
    public void GetVegetation(Vector3 _direction, out float _treeDensity, out float _grassDensity)
    {
        _treeDensity = 0.0f;
        _grassDensity = 0.0f;

        if (biomes == null || biomes.biomes.Count == 0)
            return;

        EnsureNoise();

        float range = heightScale > 0.0f ? heightScale : 1.0f;
        float hNorm = Mathf.Clamp(GetHeight(_direction, 1.0f) / range, -1.0f, 1.0f);
        float sea = Mathf.Clamp(oceanLevel, -0.99f, 0.99f);

        if (hNorm <= sea)
            return;

        float temperatureK, moisture;
        ComputeClimate(_direction, hNorm, out temperatureK, out moisture);

        Color ground = Color.black, vegetation = Color.black;
        float tint = 0.0f, trees = 0.0f, grass = 0.0f;
        if (BlendBiomes(temperatureK, moisture, ref ground, ref tint, ref vegetation, ref trees, ref grass) <= 0.0001f)
            return;

        float viability = VegetationViability(temperatureK);
        float patch = Mathf.Clamp01(0.5f + 0.8f * FBM(vegetationNoise, _direction, vegetationNoiseFrequency, 2));

        _treeDensity = Mathf.Clamp01(trees) * viability * patch;
        _grassDensity = Mathf.Clamp01(grass) * viability * patch;
    }

    #endregion

    #region Surface Color

    //Final vertex color: height ramp → cliff rock by steepness → biome ground tint →
    //vegetation layer. Alpha carries vegetation coverage (the separate layer channel for
    //shaders and the future scattering system). Below the waterline: seabed ramp only
    public Color GetSurfaceColor(Vector3 _direction, Vector3 _normal, float _height, float _radius)
    {
        EnsureNoise();

        float range = heightScale * _radius;
        float hNorm = range > 0.0f ? Mathf.Clamp(_height / range, -1.0f, 1.0f) : 0.0f;
        float sea = Mathf.Clamp(oceanLevel, -0.99f, 0.99f);

        //Sea-level-relative ramp position: 0.5 is always the waterline
        float t = hNorm >= sea
            ? 0.5f + 0.5f * (hNorm - sea) / (1.0f - sea)
            : 0.5f * (hNorm + 1.0f) / (sea + 1.0f);

        Color color = colorRamp != null ? colorRamp.Evaluate(t) : Color.white;
        color.a = 0.0f;

        if (hNorm <= sea)
            return color;

        //Exposed rock on steep surfaces regardless of altitude — sand-colored cliffs at
        //beach height look painted-on
        float steepness = 1.0f - Mathf.Clamp01(Vector3.Dot(_normal, _direction));
        float cliff = Mathf.Clamp01((steepness - cliffThreshold) / cliffSoftness);
        color = Color.Lerp(color, cliffColor, cliff);
        color.a = 0.0f;

        if (biomes == null || biomes.biomes.Count == 0)
            return color;

        float temperatureK, moisture;
        ComputeClimate(_direction, hNorm, out temperatureK, out moisture);

        Color ground = Color.black, vegetation = Color.black;
        float tint = 0.0f, trees = 0.0f, grass = 0.0f;
        if (BlendBiomes(temperatureK, moisture, ref ground, ref tint, ref vegetation, ref trees, ref grass) <= 0.0001f)
            return color;

        ground.a = 1.0f;
        color = Color.Lerp(color, ground, tint * (1.0f - cliff));

        //Vegetation layer: biome density × in-biome patchiness × viability, suppressed on
        //cliffs and faded just above the shoreline (beaches stay bare)
        float viability = VegetationViability(temperatureK);
        float patch = Mathf.Clamp01(0.5f + 0.8f * FBM(vegetationNoise, _direction, vegetationNoiseFrequency, 2));
        float shoreFade = Mathf.Clamp01(Mathf.InverseLerp(0.505f, 0.535f, t));
        float coverage = Mathf.Clamp01(grass * 0.8f + trees * 0.9f) * viability * patch * (1.0f - cliff) * shoreFade;

        vegetation.a = 1.0f;
        color = Color.Lerp(color, vegetation, coverage);
        color.a = coverage;

        return color;
    }

    #endregion

    #region Solar System Generator Bridge

    //Fill the climate profile from a generated planet's physics — the solar system
    //generator creates the bones, the designer customizes from there. oceanLevel is set
    //to an approximate quantile of the hydrosphere coverage; adjust to taste afterward
    public void ApplyPhysicalProfile(SFPlanetData _data)
    {
        climate.meanSurfaceTempK = _data.SurfaceTemp;
        climate.equatorPoleDeltaK = Mathf.Max(10.0f, _data.HighTemp - _data.LowTemp);
        climate.surfacePressureMb = _data.SurfacePressure;
        climate.hydrosphere = Mathf.Clamp01(_data.HydrosphereCoverage);
        climate.cloudCoverage = Mathf.Clamp01(_data.CloudCoverage);
        climate.iceCoverage = Mathf.Clamp01(_data.IceCoverage);
        climate.axialTiltDeg = _data.AxialTilt;

        //Sea level follows the water inventory. A dry world's ocean level sits below the
        //deepest terrain so no water shell is ever drawn, and the colour ramp treats the
        //whole surface as land rather than seabed
        oceanLevel = _data.HydrosphereCoverage > 0.001f
            ? Mathf.Lerp(-0.9f, 0.9f, Mathf.Clamp01(_data.HydrosphereCoverage))
            : -1.0f;

        version++;
    }

    //True when this world has enough water for a visible ocean — the spawner uses it to
    //decide whether a water shell belongs on the planet at all
    public bool HasOcean
    {
        get { return climate.hydrosphere > 0.001f && oceanLevel > -0.99f; }
    }

    //True when there is enough atmosphere to be worth rendering a sky
    public bool HasAtmosphere
    {
        get { return climate.surfacePressureMb > 1.0f; }
    }

    #endregion
}
