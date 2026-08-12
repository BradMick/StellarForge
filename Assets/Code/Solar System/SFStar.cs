using UnityEngine;

//A star as a scene body: emissive photosphere, prominence layer, stacked glare
//billboards, and the directional light
//it casts on everything else. Configure() takes the SFSun the generator produced, so
//size, colour and brightness all follow from the derived physics — an M-dwarf renders
//small and deep orange, an A-star large and blue-white, with no authoring.
//The light is a proxy: at stellar distances the rays are effectively parallel, so a
//directional light aimed from the star at the scene is both correct and cheap.
//
//The edit-mode rebuild is driven by SFEditorDriver, not by this component. See
//SFEditorDriver for why
[ExecuteAlways]
public class SFStar : MonoBehaviour
#if UNITY_EDITOR
    , SFEditorDriver.ISFEditorClient
#endif
{
    [Header("Physical Description")]
    //Populated by the generator; editable here for standalone testing
    public float mass = 1.0f;               //solar masses
    public float luminosity = 1.0f;         //solar luminosities
    public float temperature = 5778.0f;     //kelvin
    public Color starColor = Color.white;
    public string spectralClass = "G";

    [Header("Rendering")]
    //World-space radius of the photosphere. The spawn pass sets this from the star's
    //real radius through the system scale profile
    public float visualRadius = 100.0f;
    //Chromosphere/prominence shell — the animated plasma tongues arcing off the limb
    [Range(1.0f, 1.6f)] public float prominenceScale = 1.06f;
    [Range(0.0f, 4.0f)] public float prominenceIntensity = 0.9f;
    //Higher = fewer, sharper tongues; lower = a fuller, softer chromosphere
    [Range(0.0f, 0.9f)] public float prominenceCoverage = 0.55f;
    [Range(0.0f, 2.0f)] public float prominenceSpeed = 0.25f;

    //Without HDR, emission above ~1.2 clips to white and the star loses its colour
    [Range(0.5f, 4.0f)] public float surfaceIntensity = 1.15f;

    [Header("Glare")]
    //Stacked camera-facing billboards build the halo: a tight core, a mid glow, and a
    //wide faint bloom. Billboards (not shells) are what make glare look symmetric and
    //soft from every angle
    [Range(0.0f, 4.0f)] public float glareIntensity = 1.0f;
    //Overall size of the widest halo, as a multiple of the photosphere radius
    [Range(1.5f, 12.0f)] public float glareScale = 4.5f;
    //Four-point anamorphic flare on the brightest core. Subtle by default — a lens
    //artifact, not a feature of the star
    [Range(0.0f, 1.0f)] public float glareSpikes = 0.0f;
    //Radial coronal filaments. Off by default — at any real viewing distance they read
    //as a cartoon starburst rather than a corona. Keep low (0.1-0.2) if used at all
    [Range(0.0f, 2.0f)] public float coronaStreamers = 0.0f;

    [Header("Lighting")]
    //Casts a directional light on the system. Turn off for a companion you want visible
    //but not lighting the scene
    public bool castsLight = true;
    //Illumination at 1 AU for a 1 Lsol star — everything else scales from this
    public float lightIntensityAtOneAU = 1.3f;
    //What the light should be aimed at; falls back to the main camera
    public Transform lightTarget;

    //Every live star, registered on enable. THE way to enumerate stars — spawned stars are
    //DontSave objects, and FindObjectsByType silently skips those, which left the camera's
    //clip-plane fitter certain no stars existed while one filled the screen
    public static readonly System.Collections.Generic.List<SFStar> ActiveStars =
        new System.Collections.Generic.List<SFStar>();

    private GameObject surfaceObject;
    private GameObject prominenceObject;
    private Material surfaceMaterial;
    private Material prominenceMaterial;
    private Light sunLight;

    //Glare layers: core, halo, bloom — each a billboard quad with its own falloff
    private const int GlareLayerCount = 3;
    private readonly GameObject[] glareObjects = new GameObject[GlareLayerCount];
    private readonly Material[] glareMaterials = new Material[GlareLayerCount];
    private static Mesh quadMesh;
    private static Mesh sphereMesh;

    //Per-layer tuning: size multiplier, falloff power, intensity multiplier.
    //Layer 0 is a tight incandescent ring hugging the disc, layer 1 the thick coloured
    //corona, layer 2 a wide faint bloom
    private static readonly float[] glareLayerScale     = { 0.30f, 0.75f, 1.8f };
    private static readonly float[] glareLayerFalloff   = { 8.0f, 2.6f, 1.4f };
    private static readonly float[] glareLayerIntensity = { 1.3f, 0.85f, 0.25f };

    //Take the generator's physical description — the single source of truth for a star
    public void Configure(SFSun _star, float _visualRadius)
    {
        mass = _star.Mass;
        luminosity = _star.Luminosity;
        temperature = _star.Temperature;
        starColor = _star.StarColor;
        spectralClass = _star.SpectralClass;
        visualRadius = _visualRadius;

        Rebuild();
    }

#if UNITY_EDITOR
    //Stars are built from the generated system, before planets and shells
    public SFEditorDriver.SF_REBUILD_ORDER RebuildOrder
    {
        get { return SFEditorDriver.SF_REBUILD_ORDER.STAR; }
    }

    //Called only by SFEditorDriver
    public void EditorRebuild()
    {
        if (isActiveAndEnabled)
            Rebuild();
    }
#endif

    private void OnEnable()
    {
        if (!ActiveStars.Contains(this))
            ActiveStars.Add(this);

        //Play mode has no driver — build immediately. In edit mode the driver decides when
        if (Application.isPlaying)
        {
            Rebuild();
            return;
        }

#if UNITY_EDITOR
        SFEditorDriver.MarkDirty(this);
#endif
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            if (isActiveAndEnabled)
                Rebuild();
            return;
        }

#if UNITY_EDITOR
        //Queue a rebuild and nothing else — the driver owns the loop
        SFEditorDriver.MarkDirty(this);
#endif
    }

    private void Update()
    {
        UpdateLight();
    }

    private void Rebuild()
    {
        EnsureObjects();
        ApplyMaterials();
        UpdateLight();
    }

    private void EnsureObjects()
    {
        //Both shells use the dense sphere rather than GameObject.CreatePrimitive: the
        //primitive's ~20 segments are plainly polygonal at stellar size, and a star is not a
        //collidable surface at any scale we care about, so there is no collider to strip
        if (surfaceObject == null)
        {
            surfaceObject = new GameObject("Photosphere");
            surfaceObject.transform.SetParent(transform, false);
            surfaceObject.hideFlags = HideFlags.HideAndDontSave;

            surfaceObject.AddComponent<MeshFilter>().sharedMesh = GetSphereMesh();
            MeshRenderer renderer = surfaceObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        if (prominenceObject == null)
        {
            prominenceObject = new GameObject("Prominences");
            prominenceObject.transform.SetParent(transform, false);
            prominenceObject.hideFlags = HideFlags.HideAndDontSave;

            prominenceObject.AddComponent<MeshFilter>().sharedMesh = GetSphereMesh();
            MeshRenderer renderer = prominenceObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        //Glare billboards — quads the shader orients toward the camera
        for (int i = 0; i < GlareLayerCount; i++)
        {
            if (glareObjects[i] == null)
            {
                glareObjects[i] = new GameObject("Glare " + i);
                glareObjects[i].transform.SetParent(transform, false);
                glareObjects[i].hideFlags = HideFlags.HideAndDontSave;

                glareObjects[i].AddComponent<MeshFilter>().sharedMesh = GetQuadMesh();
                MeshRenderer renderer = glareObjects[i].AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            float size = visualRadius * 2.0f * glareScale * glareLayerScale[i];
            glareObjects[i].transform.localScale = new Vector3(size, size, size);
        }

        //Unity's primitive sphere has radius 0.5, so diameter = scale
        surfaceObject.transform.localScale = Vector3.one * visualRadius * 2.0f;
        prominenceObject.transform.localScale = Vector3.one * visualRadius * 2.0f * prominenceScale;

        //Slow differential rotation makes the whole star feel alive
        if (Application.isPlaying)
        {
            surfaceObject.transform.Rotate(Vector3.up, Time.deltaTime * 1.5f, Space.Self);
            prominenceObject.transform.Rotate(Vector3.up, Time.deltaTime * 1.1f, Space.Self);
        }
    }

    private void ApplyMaterials()
    {
        if (surfaceMaterial == null)
        {
            Shader shader = Shader.Find("StellarForge/Star Surface");
            if (shader != null)
            {
                surfaceMaterial = new Material(shader);
                surfaceMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        if (prominenceMaterial == null)
        {
            Shader shader = Shader.Find("StellarForge/Star Prominence");
            if (shader != null)
            {
                prominenceMaterial = new Material(shader);
                prominenceMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        Shader glareShader = Shader.Find("StellarForge/Star Glare");
        for (int i = 0; i < GlareLayerCount; i++)
        {
            if (glareMaterials[i] == null && glareShader != null)
            {
                glareMaterials[i] = new Material(glareShader);
                glareMaterials[i].hideFlags = HideFlags.HideAndDontSave;
            }

            if (glareMaterials[i] == null || glareObjects[i] == null)
                continue;

            //The inner ring burns near-white; the corona layers carry the star's colour
            //pushed warm, which is what gives the halo its glowing ember quality
            Color warm = new Color(
                Mathf.Min(starColor.r * 1.1f, 1.0f),
                starColor.g * 0.62f,
                starColor.b * 0.22f);

            Color layerColor = i == 0
                ? Color.Lerp(starColor, Color.white, 0.65f)
                : Color.Lerp(starColor, warm, 0.8f);

            glareMaterials[i].SetColor("_GlareColor", layerColor);
            glareMaterials[i].SetFloat("_Falloff", glareLayerFalloff[i]);
            glareMaterials[i].SetFloat("_Intensity", glareIntensity * glareLayerIntensity[i]);
            //The inner ring sits right at the disc edge; outer layers start from zero
            glareMaterials[i].SetFloat("_CoreSize", i == 0 ? 0.55f : 0.0f);
            glareMaterials[i].SetFloat("_SpikeStrength", i == 0 ? glareSpikes : 0.0f);
            glareMaterials[i].SetFloat("_Pulse", i == 0 ? 0.02f : 0.05f);
            glareMaterials[i].SetFloat("_PulseSpeed", 0.3f + i * 0.17f);

            //Streamers belong to the mid halo — on the tight core they would just be
            //noise, and on the wide bloom they would be too faint to read
            glareMaterials[i].SetFloat("_StreamerStrength", i == 1 ? coronaStreamers : 0.0f);
            glareMaterials[i].SetFloat("_StreamerCount", 26.0f);
            glareMaterials[i].SetFloat("_StreamerSpeed", 0.08f);

            glareObjects[i].GetComponent<MeshRenderer>().sharedMaterial = glareMaterials[i];
        }

        if (surfaceMaterial != null)
        {
            surfaceMaterial.SetColor("_StarColor", starColor);
            surfaceMaterial.SetFloat("_Intensity", surfaceIntensity);
            surfaceObject.GetComponent<MeshRenderer>().sharedMaterial = surfaceMaterial;
        }

        if (prominenceMaterial != null)
        {
            //Prominences glow hotter and redder than the photosphere — hydrogen-alpha
            Color plasma = Color.Lerp(starColor, new Color(1.0f, 0.45f, 0.2f), 0.6f);

            prominenceMaterial.SetColor("_StarColor", plasma);
            prominenceMaterial.SetFloat("_Intensity", prominenceIntensity);
            prominenceMaterial.SetFloat("_Threshold", prominenceCoverage);
            prominenceMaterial.SetFloat("_Speed", prominenceSpeed);
            prominenceObject.GetComponent<MeshRenderer>().sharedMaterial = prominenceMaterial;
        }

    }

    //The star's light on the rest of the system. Aiming it from the star toward whatever
    //matters keeps day/night correct as the star (or the target) moves
    private void UpdateLight()
    {
        if (!castsLight)
        {
            if (sunLight != null)
                sunLight.enabled = false;
            return;
        }

        if (sunLight == null)
        {
            GameObject lightObject = new GameObject("Starlight");
            lightObject.transform.SetParent(transform, false);
            lightObject.hideFlags = HideFlags.HideAndDontSave;

            sunLight = lightObject.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.shadows = LightShadows.Soft;
        }

        sunLight.enabled = true;
        sunLight.color = starColor;

        Transform target = lightTarget;
        if (target == null && Camera.main != null)
            target = Camera.main.transform;

        if (target != null)
        {
            Vector3 toTarget = target.position - transform.position;

            if (toTarget.sqrMagnitude > 0.0001f)
                sunLight.transform.rotation = Quaternion.LookRotation(toTarget.normalized);

            //Inverse-square falloff against the distance the target actually sits at,
            //expressed in the same units the visual radius uses
            float distanceAU = DistanceInAU(toTarget.magnitude);
            sunLight.intensity = Mathf.Clamp(lightIntensityAtOneAU * luminosity / Mathf.Max(distanceAU * distanceAU, 0.01f),
                                             0.0f, lightIntensityAtOneAU * 4.0f);
        }
        else
            sunLight.intensity = lightIntensityAtOneAU * luminosity;
    }

    //World units per AU — set by the spawn pass through the scale profile. Until then,
    //assume the star's own visual radius stands in for a sensible unit
    public float worldUnitsPerAU = 1000.0f;

    private float DistanceInAU(float _worldDistance)
    {
        return _worldDistance / Mathf.Max(worldUnitsPerAU, 0.0001f);
    }

    //Unit quad centred on the origin — the billboard shader spins it toward the camera
    //A dense UV sphere for the photosphere and prominence shells.
    //
    //Unity's primitive sphere is about 20 segments — fine for a prop, plainly polygonal when
    //it is 90,000 units across and fills the screen. Its coarse UVs also band the granulation
    //noise into visible hexagonal cells, which reads as a wireframe rather than plasma.
    //Built once and shared by every star
    private static Mesh GetSphereMesh()
    {
        if (sphereMesh != null)
            return sphereMesh;

        const int segments = 128;   //longitude divisions
        const int rings = 64;       //latitude divisions

        int vertexCount = (segments + 1) * (rings + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];

        int v = 0;
        for (int y = 0; y <= rings; y++)
        {
            float lat = Mathf.PI * y / rings;
            float sinLat = Mathf.Sin(lat);
            float cosLat = Mathf.Cos(lat);

            for (int x = 0; x <= segments; x++)
            {
                float lon = 2.0f * Mathf.PI * x / segments;

                //Radius 0.5 so the mesh matches Unity's primitive convention: the object's
                //local scale is then the star's diameter, as the callers already assume
                Vector3 unit = new Vector3(sinLat * Mathf.Cos(lon), cosLat, sinLat * Mathf.Sin(lon));

                vertices[v] = unit * 0.5f;
                normals[v] = unit;
                uv[v] = new Vector2((float)x / segments, 1.0f - (float)y / rings);
                v++;
            }
        }

        int[] triangles = new int[segments * rings * 6];
        int t = 0;
        for (int y = 0; y < rings; y++)
        {
            for (int x = 0; x < segments; x++)
            {
                int a = y * (segments + 1) + x;
                int b = a + segments + 1;

                triangles[t++] = a;
                triangles[t++] = b;
                triangles[t++] = a + 1;

                triangles[t++] = a + 1;
                triangles[t++] = b;
                triangles[t++] = b + 1;
            }
        }

        sphereMesh = new Mesh();
        sphereMesh.name = "SFStarSphere";
        sphereMesh.hideFlags = HideFlags.HideAndDontSave;
        //Past 65k vertices the default 16-bit index buffer wraps around
        sphereMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        sphereMesh.vertices = vertices;
        sphereMesh.normals = normals;
        sphereMesh.uv = uv;
        sphereMesh.triangles = triangles;

        return sphereMesh;
    }

    private static Mesh GetQuadMesh()
    {
        if (quadMesh != null)
            return quadMesh;

        quadMesh = new Mesh();
        quadMesh.name = "SFStarGlareQuad";
        quadMesh.hideFlags = HideFlags.HideAndDontSave;

        quadMesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0.0f),
            new Vector3( 0.5f, -0.5f, 0.0f),
            new Vector3( 0.5f,  0.5f, 0.0f),
            new Vector3(-0.5f,  0.5f, 0.0f)
        };

        quadMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        quadMesh.normals = new Vector3[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };

        //Generous bounds so the billboard is never frustum-culled when it swings to face
        //the camera from an oblique angle
        quadMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2.0f);

        return quadMesh;
    }

    private void OnDisable()
    {
        ActiveStars.Remove(this);

#if UNITY_EDITOR
        SFEditorDriver.Forget(this);
#endif

        //Transient children are rebuilt on enable; never leave them in the scene
        if (surfaceObject != null) DestroyImmediate(surfaceObject);
        if (prominenceObject != null) DestroyImmediate(prominenceObject);
        if (sunLight != null) DestroyImmediate(sunLight.gameObject);

        for (int i = 0; i < GlareLayerCount; i++)
        {
            if (glareObjects[i] != null)
                DestroyImmediate(glareObjects[i]);
            glareObjects[i] = null;
        }

        surfaceObject = null;
        prominenceObject = null;
        sunLight = null;
    }
}
