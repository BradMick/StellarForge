using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

//Bakes tile geometry and colors on the GPU: whole batches of tiles per compute dispatch,
//with async readback at runtime (no pipeline stalls — data arrives a frame or two later)
//and a synchronous path for instant editor previews. One batch in flight at a time so the
//output buffer is never overwritten mid-readback. Falls back to CPU generation per tile
//if a readback errors, and SFPlanet falls back entirely when compute is unsupported
public class SFPlanetGPUBaker
{
    [StructLayout(LayoutKind.Sequential)]
    private struct BakedVertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Color color;
        public Vector2 uv;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TileRequestGPU
    {
        public int face;
        public int lod;
        public int x;
        public int z;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BiomeGPU
    {
        public float temperatureMinK;
        public float temperatureMaxK;
        public float moistureMin;
        public float moistureMax;
        public Color groundColor;
        public Color vegetationColor;
        public float groundTint;
        public float treeDensity;
        public float grassDensity;
        public float pad;
    }

    private const int VertexStride = 48;    //3 + 3 + 4 + 2 floats
    private const int MaxAsyncBatch = 128;

    private static ComputeShader sharedShader;
    private static bool shaderLoadAttempted;

    public static bool Supported
    {
        get
        {
            if (!SystemInfo.supportsComputeShaders)
                return false;

            if (!shaderLoadAttempted)
            {
                shaderLoadAttempted = true;
                sharedShader = Resources.Load<ComputeShader>("SFPlanetBaker");
            }

            return sharedShader != null;
        }
    }

    private readonly SFPlanet planet;
    private readonly int kernel;
    private readonly int faceColorKernel;

    //Per-cube-face baked color maps — per-pixel surface color independent of mesh LOD
    private RenderTexture[] faceMaps;
    private int faceMapResolution = -1;
    private int pendingFaceMask;

    //Far maps: lower-res variants with the ocean painted in, used by the far-frozen tier
    //(where the transparent water shell is hidden)
    private const int FarMapResolution = 512;
    private RenderTexture[] farMaps;
    private int pendingFarMask;
    private SFWaterShell waterShell;
    private bool waterShellSearched;

    private ComputeBuffer permBuffer, biomeBuffer, tileBuffer, vertexBuffer;
    private Texture2D rampTexture;
    private int tileCapacity = -1;
    private readonly int[] permData = new int[8 * 512];

    private int uploadedVersion = int.MinValue;
    private int uploadedSeed = int.MinValue;

    private readonly List<SFSurface> pending = new List<SFSurface>();
    private readonly List<SFSurface> inFlight = new List<SFSurface>();
    private bool readbackPending;
    private int inFlightVerticesPerTile;
    private int vertexCapacity = -1;

    public SFPlanetGPUBaker(SFPlanet _planet)
    {
        planet = _planet;
        kernel = sharedShader.FindKernel("BakeTiles");
        faceColorKernel = sharedShader.FindKernel("BakeFaceColor");
    }

    public void RequestBake(SFSurface _surface)
    {
        if (!pending.Contains(_surface))
            pending.Add(_surface);
    }

    //Dispatch the next batch. Synchronous mode (editor previews) bakes EVERYTHING pending
    //and blocks on GetData; async mode caps the batch and reads back without stalling
    public void Process(bool _synchronous)
    {
        if (readbackPending && !_synchronous)
            return;

        pending.RemoveAll(s => s == null || s.surfaceObject == null);
        if (pending.Count == 0)
            return;

        UploadParameters();

        int count = _synchronous ? pending.Count : Mathf.Min(pending.Count, MaxAsyncBatch);
        int resolution = planet.meshResolution;
        int verticesPerTile = resolution * resolution;

        EnsureTileBuffers(count, verticesPerTile);

        TileRequestGPU[] requests = new TileRequestGPU[count];
        inFlight.Clear();
        for (int i = 0; i < count; i++)
        {
            SFSurface surface = pending[i];
            inFlight.Add(surface);
            requests[i] = new TileRequestGPU
            {
                face = (int)surface.cubeIndex,
                lod = surface.LOD,
                x = surface.xIndex,
                z = surface.zIndex
            };
        }
        pending.RemoveRange(0, count);

        tileBuffer.SetData(requests, 0, 0, count);
        sharedShader.SetInt("_TileCount", count);
        sharedShader.SetBuffer(kernel, "_Tiles", tileBuffer);
        sharedShader.SetBuffer(kernel, "_Vertices", vertexBuffer);

        int groups = (resolution + 7) / 8;
        sharedShader.Dispatch(kernel, groups, groups, count);

        if (_synchronous)
        {
            BakedVertex[] data = new BakedVertex[count * verticesPerTile];
            vertexBuffer.GetData(data, 0, 0, count * verticesPerTile);

            for (int i = 0; i < inFlight.Count; i++)
                ApplyTile(inFlight[i], data, i * verticesPerTile, verticesPerTile);

            inFlight.Clear();
        }
        else
        {
            readbackPending = true;
            inFlightVerticesPerTile = verticesPerTile;
            AsyncGPUReadback.Request(vertexBuffer, count * verticesPerTile * VertexStride, 0, OnReadback);
        }
    }

    private void OnReadback(AsyncGPUReadbackRequest _request)
    {
        readbackPending = false;

        if (planet == null)
            return;

        int verticesPerTile = inFlightVerticesPerTile;

        if (_request.hasError)
        {
            //GPU readback failed — generate this batch on the CPU instead
            for (int i = 0; i < inFlight.Count; i++)
                if (inFlight[i] != null && inFlight[i].surfaceObject != null)
                    inFlight[i].GenerateCPUFallback();

            inFlight.Clear();
            return;
        }

        Unity.Collections.NativeArray<BakedVertex> data = _request.GetData<BakedVertex>();

        for (int i = 0; i < inFlight.Count; i++)
            ApplyTileNative(inFlight[i], data, i * verticesPerTile, verticesPerTile);

        inFlight.Clear();
    }

    private void ApplyTile(SFSurface _surface, BakedVertex[] _data, int _offset, int _count)
    {
        if (_surface == null || _surface.surfaceObject == null)
            return;

        Vector3[] positions = new Vector3[_count];
        Vector3[] normals = new Vector3[_count];
        Color[] colors = new Color[_count];
        Vector2[] uvs = new Vector2[_count];

        for (int v = 0; v < _count; v++)
        {
            BakedVertex bv = _data[_offset + v];
            positions[v] = bv.position;
            normals[v] = bv.normal;
            colors[v] = bv.color;
            uvs[v] = bv.uv;
        }

        _surface.ApplyBakedData(positions, normals, colors, uvs);
    }

    private void ApplyTileNative(SFSurface _surface, Unity.Collections.NativeArray<BakedVertex> _data, int _offset, int _count)
    {
        if (_surface == null || _surface.surfaceObject == null)
            return;

        Vector3[] positions = new Vector3[_count];
        Vector3[] normals = new Vector3[_count];
        Color[] colors = new Color[_count];
        Vector2[] uvs = new Vector2[_count];

        for (int v = 0; v < _count; v++)
        {
            BakedVertex bv = _data[_offset + v];
            positions[v] = bv.position;
            normals[v] = bv.normal;
            colors[v] = bv.color;
            uvs[v] = bv.uv;
        }

        _surface.ApplyBakedData(positions, normals, colors, uvs);
    }

    #region Face Color Maps

    //Queue all six faces (both map sets) for a color rebake
    public void RequestFaceBake()
    {
        pendingFaceMask = 0x3F;
        pendingFarMask = 0x3F;
    }

    public RenderTexture GetFaceMap(int _face)
    {
        EnsureFaceMaps();
        return faceMaps[_face];
    }

    public RenderTexture GetFarMap(int _face)
    {
        EnsureFaceMaps();
        return farMaps[_face];
    }

    //Bake queued faces: all of them synchronously (editor / initial load), or one texture
    //per call at runtime so live rebakes amortize across frames
    public void ProcessFaceBakes(bool _synchronous)
    {
        if ((pendingFaceMask == 0 && pendingFarMask == 0) || planet.terrain == null)
            return;

        EnsureFaceMaps();
        UploadParameters();

        do
        {
            if (pendingFaceMask != 0)
            {
                int face = LowestFace(pendingFaceMask);
                pendingFaceMask &= ~(1 << face);
                BakeFace(faceMaps[face], face, faceMapResolution, false);
            }
            else
            {
                int face = LowestFace(pendingFarMask);
                pendingFarMask &= ~(1 << face);
                BakeFace(farMaps[face], face, FarMapResolution, true);
            }
        }
        while (_synchronous && (pendingFaceMask != 0 || pendingFarMask != 0));
    }

    private static int LowestFace(int _mask)
    {
        int face = 0;
        while ((_mask & (1 << face)) == 0)
            face++;
        return face;
    }

    private void BakeFace(RenderTexture _target, int _face, int _resolution, bool _paintWater)
    {
        sharedShader.SetInt("_FaceIndex", _face);
        sharedShader.SetInt("_ColorMapResolution", _resolution);
        sharedShader.SetInt("_PaintWater", _paintWater ? 1 : 0);

        if (_paintWater)
        {
            Color shallow, deep;
            GetFarWaterColors(out shallow, out deep);
            sharedShader.SetVector("_WaterShallow", shallow);
            sharedShader.SetVector("_WaterDeep", deep);
        }

        sharedShader.SetTexture(faceColorKernel, "_FaceColor", _target);
        int groups = (_resolution + 7) / 8;
        sharedShader.Dispatch(faceColorKernel, groups, groups, 1);
        _target.GenerateMips();
    }

    //Far maps paint the ocean with the water shell's own colors so near and far agree
    private void GetFarWaterColors(out Color _shallow, out Color _deep)
    {
        if (!waterShellSearched)
        {
            waterShellSearched = true;
            waterShell = planet.GetComponent<SFWaterShell>();
        }

        if (waterShell != null && waterShell.TryGetWaterColors(out _shallow, out _deep))
            return;

        _shallow = new Color(0.35f, 0.75f, 0.75f);
        _deep = new Color(0.02f, 0.12f, 0.30f);
    }

    private void EnsureFaceMaps()
    {
        int resolution = planet.terrain != null ? planet.terrain.colorMapResolution : 1024;

        if (faceMaps != null && faceMapResolution == resolution)
            return;

        ReleaseFaceMaps();

        faceMapResolution = resolution;
        faceMaps = new RenderTexture[6];
        for (int i = 0; i < 6; i++)
        {
            faceMaps[i] = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32)
            {
                name = planet.name + " FaceColor " + i,
                enableRandomWrite = true,
                useMipMap = true,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            faceMaps[i].Create();
        }

        farMaps = new RenderTexture[6];
        for (int i = 0; i < 6; i++)
        {
            farMaps[i] = new RenderTexture(FarMapResolution, FarMapResolution, 0, RenderTextureFormat.ARGB32)
            {
                name = planet.name + " FarColor " + i,
                enableRandomWrite = true,
                useMipMap = true,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear
            };
            farMaps[i].Create();
        }

        pendingFaceMask = 0x3F;
        pendingFarMask = 0x3F;
        planet.ApplyColorMapsToTiles();
    }

    private void ReleaseFaceMaps()
    {
        if (faceMaps != null)
        {
            for (int i = 0; i < faceMaps.Length; i++)
                if (faceMaps[i] != null)
                    faceMaps[i].Release();
            faceMaps = null;
        }

        if (farMaps != null)
        {
            for (int i = 0; i < farMaps.Length; i++)
                if (farMaps[i] != null)
                    farMaps[i].Release();
            farMaps = null;
        }

        faceMapResolution = -1;
    }

    #endregion

    #region Parameter Upload

    private void UploadParameters()
    {
        SFPlanetTerrain terrain = planet.terrain;
        int version = terrain != null ? terrain.CombinedVersion : 0;
        int seed = terrain != null ? terrain.seed : 0;

        if (permBuffer == null)
            permBuffer = new ComputeBuffer(8 * 512, sizeof(int));

        //Heavy uploads (noise tables, biome list, ramp texture) only when something changed
        if (version != uploadedVersion || seed != uploadedSeed || rampTexture == null)
        {
            uploadedVersion = version;
            uploadedSeed = seed;

            if (terrain != null)
                terrain.FillPermutationTables(permData);
            permBuffer.SetData(permData);

            UploadBiomes(terrain);
            UploadRamp(terrain);
        }

        ComputeShader cs = sharedShader;

        cs.SetInt("_MeshResolution", planet.meshResolution);
        cs.SetFloat("_PlanetRadius", planet.planetRadius);
        cs.SetInt("_HasTerrain", terrain != null ? 1 : 0);

        if (terrain != null)
        {
            cs.SetFloat("_HeightScale", terrain.heightScale);
            cs.SetFloat("_OceanLevel", terrain.oceanLevel);
            cs.SetInt("_FlattenOcean", terrain.flattenOcean ? 1 : 0);
            cs.SetFloat("_PlainsBias", terrain.plainsBias);
            cs.SetFloat("_Persistence", terrain.persistence);
            cs.SetFloat("_Lacunarity", terrain.lacunarity);
            cs.SetFloat("_ContinentFrequency", terrain.continentFrequency);
            cs.SetInt("_ContinentOctaves", terrain.continentOctaves);
            cs.SetFloat("_WarpStrength", terrain.domainWarpStrength);
            cs.SetFloat("_WarpFrequency", terrain.domainWarpFrequency);
            cs.SetFloat("_MountainFrequency", terrain.mountainFrequency);
            cs.SetInt("_MountainOctaves", terrain.mountainOctaves);
            cs.SetFloat("_MountainAmount", terrain.mountainAmount);
            cs.SetFloat("_MountainMaskFrequency", terrain.mountainMaskFrequency);
            cs.SetFloat("_MountainMaskCoverage", terrain.mountainMaskCoverage);
            cs.SetFloat("_DetailFrequency", terrain.detailFrequency);
            cs.SetInt("_DetailOctaves", terrain.detailOctaves);
            cs.SetFloat("_DetailAmount", terrain.detailAmount);

            SFClimateProfile climate = terrain.climate;
            cs.SetFloat("_MeanTempK", climate.meanSurfaceTempK);
            cs.SetFloat("_EquatorPoleDeltaK", climate.equatorPoleDeltaK);
            cs.SetFloat("_AltitudeLapseK", climate.altitudeLapseK);
            cs.SetFloat("_PressureMb", climate.surfacePressureMb);
            cs.SetFloat("_Hydrosphere", climate.hydrosphere);
            cs.SetFloat("_CloudCoverage", climate.cloudCoverage);
            cs.SetFloat("_IceCoverage", climate.iceCoverage);
            cs.SetFloat("_AxialTiltDeg", climate.axialTiltDeg);
            cs.SetFloat("_ClimateNoiseFrequency", climate.climateNoiseFrequency);
            cs.SetFloat("_ClimateNoiseStrengthK", climate.climateNoiseStrengthK);
            cs.SetFloat("_MoistureFrequency", climate.moistureFrequency);

            cs.SetVector("_CliffColor", terrain.cliffColor);
            cs.SetFloat("_CliffThreshold", terrain.cliffThreshold);
            cs.SetFloat("_CliffSoftness", terrain.cliffSoftness);
            cs.SetFloat("_VegNoiseFrequency", terrain.vegetationNoiseFrequency);
        }

        cs.SetInt("_BiomeCount", terrain != null && terrain.biomes != null ? terrain.biomes.biomes.Count : 0);
        cs.SetBuffer(kernel, "_Perm", permBuffer);
        cs.SetBuffer(kernel, "_Biomes", biomeBuffer);
        cs.SetTexture(kernel, "_RampTexture", rampTexture);
        //The face-color kernel shares the same tables and ramp
        cs.SetBuffer(faceColorKernel, "_Perm", permBuffer);
        cs.SetBuffer(faceColorKernel, "_Biomes", biomeBuffer);
        cs.SetTexture(faceColorKernel, "_RampTexture", rampTexture);
    }

    private void UploadBiomes(SFPlanetTerrain _terrain)
    {
        int count = _terrain != null && _terrain.biomes != null ? _terrain.biomes.biomes.Count : 0;
        int capacity = Mathf.Max(1, count);

        if (biomeBuffer == null || biomeBuffer.count < capacity)
        {
            if (biomeBuffer != null)
                biomeBuffer.Release();
            biomeBuffer = new ComputeBuffer(capacity, Marshal.SizeOf(typeof(BiomeGPU)));
        }

        BiomeGPU[] data = new BiomeGPU[capacity];
        for (int i = 0; i < count; i++)
        {
            SFBiome b = _terrain.biomes.biomes[i];
            data[i] = new BiomeGPU
            {
                temperatureMinK = b.temperatureMinK,
                temperatureMaxK = b.temperatureMaxK,
                moistureMin = b.moistureMin,
                moistureMax = b.moistureMax,
                groundColor = b.groundColor,
                vegetationColor = b.vegetationColor,
                groundTint = b.groundTint,
                treeDensity = b.treeDensity,
                grassDensity = b.grassDensity
            };
        }
        biomeBuffer.SetData(data);
    }

    private void UploadRamp(SFPlanetTerrain _terrain)
    {
        if (rampTexture == null)
        {
            rampTexture = new Texture2D(256, 1, TextureFormat.RGBA32, false, true);
            rampTexture.wrapMode = TextureWrapMode.Clamp;
            rampTexture.hideFlags = HideFlags.HideAndDontSave;
        }

        Color[] pixels = new Color[256];
        for (int x = 0; x < 256; x++)
        {
            float t = x / 255.0f;
            pixels[x] = _terrain != null && _terrain.colorRamp != null
                ? _terrain.colorRamp.Evaluate(t)
                : Color.white;
        }
        rampTexture.SetPixels(pixels);
        rampTexture.Apply(false, false);
    }

    private void EnsureTileBuffers(int _tileCount, int _verticesPerTile)
    {
        int neededVertices = _tileCount * _verticesPerTile;
        if (tileCapacity >= _tileCount && vertexCapacity >= neededVertices)
            return;

        if (tileBuffer != null) tileBuffer.Release();
        if (vertexBuffer != null) vertexBuffer.Release();

        tileCapacity = Mathf.Max(tileCapacity, _tileCount);
        vertexCapacity = Mathf.Max(vertexCapacity, neededVertices);
        tileBuffer = new ComputeBuffer(tileCapacity, Marshal.SizeOf(typeof(TileRequestGPU)));
        vertexBuffer = new ComputeBuffer(vertexCapacity, VertexStride);
    }

    #endregion

    public void Dispose()
    {
        ReleaseFaceMaps();

        if (permBuffer != null) { permBuffer.Release(); permBuffer = null; }
        if (biomeBuffer != null) { biomeBuffer.Release(); biomeBuffer = null; }
        if (tileBuffer != null) { tileBuffer.Release(); tileBuffer = null; }
        if (vertexBuffer != null) { vertexBuffer.Release(); vertexBuffer = null; }

        if (rampTexture != null)
        {
            if (Application.isPlaying)
                Object.Destroy(rampTexture);
            else
                Object.DestroyImmediate(rampTexture);
            rampTexture = null;
        }

        pending.Clear();
        inFlight.Clear();
        readbackPending = false;
        tileCapacity = -1;
        uploadedVersion = int.MinValue;
    }
}
