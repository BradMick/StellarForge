using UnityEngine;

//Atmosphere shell around a terrain planet. Attach next to SFPlanet. Spawns a smooth
//inside-out shell (a terrainless SFPlanet, like the water shell) wearing the atmosphere
//shader: limb glow from space, sky dome from the surface, sun-aware day/night with a
//sunset terminator band. Works in edit mode; the shell is transient (never saved).
//
//The edit-mode preview is driven by SFEditorDriver, not by this component — it rebuilds
//when marked dirty and never watches for its own changes. See SFEditorDriver for why
[ExecuteAlways]
public class SFAtmosphere : MonoBehaviour
#if UNITY_EDITOR
    , SFEditorDriver.ISFEditorClient
#endif
{
    //Shell height as a fraction of planet radius (Earth's sensible-atmosphere ≈ 0.02;
    //gameplay planets read better a little thicker)
    [Range(0.01f, 0.25f)]
    public float heightFraction = 0.05f;

    public Color dayColor = new Color(0.35f, 0.55f, 1.0f);
    public Color sunsetColor = new Color(1.0f, 0.45f, 0.2f);
    [Range(0.0f, 3.0f)] public float density = 1.0f;
    [Range(0.5f, 8.0f)] public float rimFalloff = 2.5f;

    //Sun the day/night cycle follows; auto-finds the first directional light when empty
    public Light sun;

    private SFPlanet hostPlanet;
    private SFPlanet shellPlanet;
    private Material atmosphereMaterial;
    private float nextSunScan;

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        if (ResolveHost())
            CreateShell();
    }

    private void Update()
    {
        if (!Application.isPlaying || shellPlanet == null)
            return;

        SyncShell();
    }

    private bool ResolveHost()
    {
        if (hostPlanet == null)
            hostPlanet = GetComponent<SFPlanet>();

        if (hostPlanet == null)
        {
            Debug.LogWarning("SFAtmosphere needs an SFPlanet component on the same GameObject.");
            enabled = false;
            return false;
        }

        if (sun == null)
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].type == LightType.Directional)
                {
                    sun = lights[i];
                    break;
                }
        }

        return true;
    }

    private void CreateShell()
    {
        GameObject shellObject = new GameObject("Atmosphere");
        shellObject.transform.SetParent(transform, false);

        if (!Application.isPlaying)
            shellObject.hideFlags = HideFlags.HideAndDontSave;

        //Smooth sphere, no terrain, no colliders, no cullers (a transparent dome must
        //never lose its far-side limb tiles), modest depth — curvature only.
        //In edit mode this shell rebuilds it directly (see EditorRebuild) — a shell planet
        //never queues itself with the driver, so nothing else can rebuild it behind our back
        shellPlanet = shellObject.AddComponent<SFPlanet>();
        shellPlanet.isShellPlanet = true;
        shellPlanet.planetRadius = ShellRadius();
        shellPlanet.LOD = 2;
        shellPlanet.quadsPerEdgeSetting = SF_QUADS_PER_EDGE.Quads8;
        shellPlanet.maxLODCap = 3;
        shellPlanet.editorPreviewLOD = 2;
        shellPlanet.generateColliders = false;
        shellPlanet.intangible = true;
        shellPlanet.cullSubdivision = false;
        shellPlanet.surfaceMaterialOverride = GetAtmosphereMaterial();
    }

    private void SyncShell()
    {
        float targetRadius = ShellRadius();
        if (!Mathf.Approximately(shellPlanet.planetRadius, targetRadius))
        {
            shellPlanet.planetRadius = targetRadius;
            shellPlanet.RequestFullRebuild();
        }

        Material material = GetAtmosphereMaterial();
        material.SetColor("_DayColor", dayColor);
        material.SetColor("_SunsetColor", sunsetColor);
        material.SetFloat("_Density", density);
        material.SetFloat("_Falloff", rimFalloff);
        //Proximity fade spans about one shell height — the entry transition's gradient
        material.SetFloat("_FadeRange", hostPlanet.planetRadius * heightFraction * 1.5f);
        material.SetVector("_PlanetCenter", hostPlanet.transform.position);
        material.SetFloat("_ShellRadius", ShellRadius());

        //Self-heals if the sun is created or reconfigured mid-session. Scanning every
        //frame is wasteful, so only look again periodically while none is found
        if (sun == null || sun.type != LightType.Directional)
        {
            if (Time.unscaledTime >= nextSunScan)
            {
                nextSunScan = Time.unscaledTime + 1.0f;
                sun = null;

                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length; i++)
                    if (lights[i].type == LightType.Directional)
                    {
                        sun = lights[i];
                        break;
                    }
            }
        }

        if (sun != null)
            material.SetVector("_SunDirection", sun.transform.forward);
    }

    private float ShellRadius()
    {
        return hostPlanet.planetRadius * (1.0f + heightFraction);
    }

    private Material GetAtmosphereMaterial()
    {
        if (atmosphereMaterial == null)
        {
            Shader shader = Shader.Find("StellarForge/Planet Atmosphere");
            if (shader != null)
            {
                atmosphereMaterial = new Material(shader);
                atmosphereMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        return atmosphereMaterial;
    }

    #region Editor Preview

#if UNITY_EDITOR
    //Shells attach to a planet that must already have been built this tick
    public SFEditorDriver.SF_REBUILD_ORDER RebuildOrder
    {
        get { return SFEditorDriver.SF_REBUILD_ORDER.SHELL; }
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            return;

        //Ask for one rebuild; the driver owns the loop
        SFEditorDriver.MarkDirty(this);
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            return;

        SFEditorDriver.Forget(this);

        if (shellPlanet != null)
        {
            DestroyImmediate(shellPlanet.gameObject);
            shellPlanet = null;
        }
    }

    //Inspector edits queue a rebuild and nothing else. Doing the work here would run it
    //once per changed field, mid-drag, outside the driver's ordering
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        SFEditorDriver.MarkDirty(this);
    }

    //Called only by SFEditorDriver
    public void EditorRebuild()
    {
        if (!ResolveHost())
            return;

        //Self-heals: the host planet rebuilding destroys its children, this shell included
        if (shellPlanet == null)
            CreateShell();

        SyncShell();

        //The shell planet is ours to drive: it is flagged isShellPlanet, so it never queues
        //itself and would otherwise sit unbuilt. Sync first, rebuild second — the rebuild
        //has to see this frame's shell radius and material
        if (shellPlanet != null)
            shellPlanet.EditorRebuild();
    }
#endif

    #endregion
}
