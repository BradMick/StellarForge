using UnityEngine;

//Spawns and maintains a smooth water sphere at sea level around a terrain planet.
//Attach next to SFPlanet (and SFPlanetTerrain). The shell is a second SFPlanet with no
//terrain, so it reuses the whole quadtree LOD + culling machinery — smooth curvature up
//close, horizon-culled far side, and no colliders (water depth is analytic:
//|pos - center| - seaRadius). Sea level tracks the terrain's oceanLevel live; keep
//flattenOcean OFF on SFPlanetTerrain so the height field dives below the shell.
//Works in edit mode too: the preview water sphere is transient (never saved)
[ExecuteAlways]
public class SFWaterShell : MonoBehaviour
{
    //Transparent material for the water surface; leave empty for the bundled water shader
    public Material waterMaterial;

    //Derive the water shader's world-unit optics (depth fade, foam band, waves) from the
    //planet's actual scale — the same material asset then works on a 2 m test sphere and
    //a 20 km world. Disable to hand-tune the material values directly
    public bool autoScaleWaterSettings = true;
    //Fractions of the maximum ocean depth (heightScale × radius) — tune the look here,
    //at any planet scale, instead of in raw world units on the material.
    //NOTE: while auto-scale is on, the material asset's DepthFade/Foam/Wave values are ignored
    [Range(0.05f, 1.0f)] public float depthFadeFraction = 0.4f;
    [Range(0.005f, 0.3f)] public float foamFraction = 0.05f;

    //Waves in absolute world units (1 unit = 1 m). Default OFF — vertex waves on a coarse
    //water shell alias badly; proper waves arrive with per-pixel normal animation.
    //Wavelength is auto-clamped above the shell's vertex spacing to prevent aliasing
    public float waveHeightMeters = 0.0f;
    public float waveLengthMeters = 300.0f;
    public float waveSpeedMeters = 8.0f;
    private Material scaledWaterMaterial;

    //Extra radial offset in world units on top of the terrain's sea level
    public float radiusOffset = 0.0f;

    //Water is featureless, so it needs far fewer LOD levels than terrain — just enough
    //that its curvature stays smooth near the camera. Caps the water planet's derived depth
    public int maxLOD = 4;

    private SFPlanet hostPlanet;
    private SFPlanetTerrain terrain;
    private SFPlanet waterPlanet;
    private Material defaultWaterMaterial;

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        if (!ResolveHost())
            return;

        //The water shader's depth-based color and foam need the camera depth texture
        if (Camera.main != null)
            Camera.main.depthTextureMode |= DepthTextureMode.Depth;

        CreateWaterPlanet();
    }

    private void Update()
    {
        if (!Application.isPlaying || waterPlanet == null)
            return;

        SyncWaterPlanet();
    }

    private bool ResolveHost()
    {
        if (hostPlanet == null)
            hostPlanet = GetComponent<SFPlanet>();
        if (terrain == null)
            terrain = GetComponent<SFPlanetTerrain>();

        if (hostPlanet == null)
        {
            Debug.LogWarning("SFWaterShell needs an SFPlanet component on the same GameObject.");
            enabled = false;
            return false;
        }

        return true;
    }

    private void CreateWaterPlanet()
    {
        GameObject waterObject = new GameObject("Water Shell");
        waterObject.transform.SetParent(transform, false);

        if (!Application.isPlaying)
            waterObject.hideFlags = HideFlags.HideAndDontSave;

        //Configure before the new SFPlanet's Start runs (Start is deferred past AddComponent).
        //In edit mode its own preview tick picks the settings up on the next editor update
        waterPlanet = waterObject.AddComponent<SFPlanet>();
        waterPlanet.planetRadius = SeaRadius();
        waterPlanet.LOD = hostPlanet.LOD;   //match base subdivision so shading density agrees
        waterPlanet.quadsPerEdgeSetting = hostPlanet.quadsPerEdgeSetting;
        waterPlanet.targetGroundResolution = hostPlanet.targetGroundResolution;
        waterPlanet.lodSplitFactor = hostPlanet.lodSplitFactor;
        waterPlanet.maxLODCap = maxLOD;
        waterPlanet.editorPreviewLOD = Mathf.Min(2, hostPlanet.editorPreviewLOD);
        waterPlanet.generateColliders = false;
        waterPlanet.surfaceMaterialOverride = GetWaterMaterial();
        SyncCullers();
    }

    //The water planet borrows the host's cullers. Taken from the host's resolved fields —
    //the components may live anywhere (e.g. on the camera), not only on the planet object
    private void SyncCullers()
    {
        if (waterPlanet == null)
            return;

        if (hostPlanet.frustumCuller == null)
            hostPlanet.frustumCuller = GetComponent<FrustumCuller>();
        if (hostPlanet.horizonCuller == null)
            hostPlanet.horizonCuller = GetComponent<HorizonCuller>();

        waterPlanet.frustumCuller = hostPlanet.frustumCuller;
        waterPlanet.horizonCuller = hostPlanet.horizonCuller;
    }

    private void SyncWaterPlanet()
    {
        SyncCullers();

        //Only at impostor range (planet = a few pixels) is the ocean painted into the far
        //maps and the shell hidden — the swap is imperceptible there. At every nearer
        //distance the real shell renders, so approach shows no representation change
        if (Application.isPlaying && waterPlanet.gameObject.activeSelf == hostPlanet.ImpostorRange)
            waterPlanet.gameObject.SetActive(!hostPlanet.ImpostorRange);

        if (!waterPlanet.gameObject.activeSelf)
            return;

        //Track host resolution changes; the water planet's own resolution watch rebuilds it
        waterPlanet.quadsPerEdgeSetting = hostPlanet.quadsPerEdgeSetting;

        //Track live edits: sea level (oceanLevel/heightScale/radius) and material swaps
        float targetRadius = SeaRadius();
        Material targetMaterial = GetWaterMaterial();

        bool radiusChanged = !Mathf.Approximately(waterPlanet.planetRadius, targetRadius);
        bool materialChanged = waterPlanet.surfaceMaterialOverride != targetMaterial;

        if (radiusChanged || materialChanged)
        {
            waterPlanet.planetRadius = targetRadius;
            waterPlanet.surfaceMaterialOverride = targetMaterial;

            //Runtime tiles rebuild through the amortized queue; the editor preview
            //regenerates itself because radius/material are part of its settings hash
            if (Application.isPlaying)
                waterPlanet.RequestFullRebuild();
        }
    }

    //Sea sits at the terrain's oceanLevel (normalized noise units mapped to world height),
    //matching where the color ramp and height field put sea level
    private float SeaRadius()
    {
        float seaLevel = 0.0f;
        if (terrain != null)
            seaLevel = terrain.oceanLevel * terrain.heightScale * hostPlanet.planetRadius;

        return hostPlanet.planetRadius + seaLevel + radiusOffset;
    }

    private Material GetWaterMaterial()
    {
        Material source = waterMaterial;

        if (source == null)
        {
            if (defaultWaterMaterial == null)
            {
                Shader shader = Shader.Find("StellarForge/Planet Water");
                if (shader != null)
                    defaultWaterMaterial = new Material(shader);
                else
                {
                    defaultWaterMaterial = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
                    defaultWaterMaterial.color = new Color(0.08f, 0.28f, 0.45f, 0.65f);
                }
            }
            source = defaultWaterMaterial;
        }

        if (!autoScaleWaterSettings || !source.HasProperty("_DepthFade"))
            return source;

        //Work on a runtime copy so the authored material asset is never modified
        if (scaledWaterMaterial == null || scaledWaterMaterial.shader != source.shader)
        {
            scaledWaterMaterial = new Material(source);
            scaledWaterMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
        scaledWaterMaterial.CopyPropertiesFromMaterial(source);

        //Optics scale with the deepest possible ocean; waves are absolute-size phenomena
        float maxDepth = Mathf.Max(0.0001f, (terrain != null ? terrain.heightScale : 0.02f) * hostPlanet.planetRadius);
        scaledWaterMaterial.SetFloat("_DepthFade", maxDepth * depthFadeFraction);
        scaledWaterMaterial.SetFloat("_FoamDistance", maxDepth * foamFraction);

        //Vertex waves alias unless the wavelength stays well above the shell's vertex
        //spacing — clamp, and translate wavelength/speed into the shader's frequency terms
        float quadSize = Mathf.PI * 0.5f * SeaRadius() / ((1 << Mathf.Max(waterPlanet != null ? waterPlanet.maxLOD : maxLOD, 0)) * hostPlanet.quadsPerEdge);
        float wavelength = Mathf.Max(waveLengthMeters, quadSize * 3.0f);
        float frequency = 2.0f * Mathf.PI / Mathf.Max(wavelength, 0.01f);
        scaledWaterMaterial.SetFloat("_WaveHeight", waveHeightMeters * 0.5f);
        scaledWaterMaterial.SetFloat("_WaveFrequency", frequency);
        scaledWaterMaterial.SetFloat("_WaveSpeed", waveSpeedMeters * frequency);
        //Depth tie-break bias: enough to settle water-vs-seabed contests, small enough
        //never to eat the foam band (which scales with maxDepth)
        scaledWaterMaterial.SetFloat("_DepthBias", Mathf.Clamp(maxDepth * 0.005f, 0.005f, 2.0f));

        return scaledWaterMaterial;
    }

    //Water colors for the far-map bake, so painted distant oceans match the near shell
    public bool TryGetWaterColors(out Color _shallow, out Color _deep)
    {
        if (hostPlanet == null && !ResolveHost())
        {
            _shallow = default(Color);
            _deep = default(Color);
            return false;
        }

        Material source = GetWaterMaterial();
        if (source != null && source.HasProperty("_ShallowColor") && source.HasProperty("_DeepColor"))
        {
            _shallow = source.GetColor("_ShallowColor");
            _deep = source.GetColor("_DeepColor");
            return true;
        }

        _shallow = default(Color);
        _deep = default(Color);
        return false;
    }

    #region Editor Preview

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (Application.isPlaying)
            return;

        UnityEditor.EditorApplication.update -= EditorWaterTick;
        UnityEditor.EditorApplication.update += EditorWaterTick;
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            return;

        UnityEditor.EditorApplication.update -= EditorWaterTick;

        if (waterPlanet != null)
        {
            DestroyImmediate(waterPlanet.gameObject);
            waterPlanet = null;
        }
    }

    private void EditorWaterTick()
    {
        if (this == null)
        {
            UnityEditor.EditorApplication.update -= EditorWaterTick;
            return;
        }

        if (!ResolveHost())
            return;

        //The water shader's depth effects need a depth texture in edit mode too —
        //including on Scene view cameras, which are recreated freely and never inherit it
        if (Camera.main != null)
            Camera.main.depthTextureMode |= DepthTextureMode.Depth;

        foreach (object view in UnityEditor.SceneView.sceneViews)
        {
            UnityEditor.SceneView sceneView = view as UnityEditor.SceneView;
            if (sceneView != null && sceneView.camera != null)
                sceneView.camera.depthTextureMode |= DepthTextureMode.Depth;
        }

        //Self-heals: recreate if the host's stray sweep (or anything else) removed the shell
        if (waterPlanet == null)
            CreateWaterPlanet();

        SyncWaterPlanet();
    }
#endif

    #endregion
}
