using UnityEngine;

//One-drop space environment: procedural starfield skybox, near-black space ambient,
//sun tuning, and skybox clear flags on the main camera. Attach to any scene object
//(the planet root or the camera both work). Runs in edit mode so the scene reads as
//space while designing
[ExecuteAlways]
public class SFSpaceScene : MonoBehaviour
{
    [Header("Stars")]
    [Range(0.5f, 4.0f)] public float starDensity = 1.5f;
    [Range(0.0f, 3.0f)] public float starBrightness = 1.2f;
    [Range(0.0f, 1.0f)] public float galacticBandIntensity = 0.25f;
    public float starSeed = 7.0f;

    [Header("Lighting")]
    //Space is not pitch black on the night side — faint ambient keeps silhouettes readable
    public Color spaceAmbient = new Color(0.015f, 0.02f, 0.035f);

    //Fallback lighting for scenes with no SFStar body. Real star bodies manage their own
    //directional lights, so this stays out of the way whenever one exists
    public bool useFallbackSun = true;
    public Light fallbackSun;
    public float fallbackSunIntensity = 1.3f;
    public Color fallbackSunColor = new Color(1.0f, 0.96f, 0.9f);

    private Material skyboxMaterial;

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }


    private void Apply()
    {
        //Skybox
        if (skyboxMaterial == null)
        {
            Shader shader = Shader.Find("StellarForge/Starfield Skybox");
            if (shader == null)
                return;

            skyboxMaterial = new Material(shader);
            skyboxMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        skyboxMaterial.SetFloat("_StarDensity", starDensity);
        skyboxMaterial.SetFloat("_StarBrightness", starBrightness);
        skyboxMaterial.SetFloat("_BandIntensity", galacticBandIntensity);
        skyboxMaterial.SetFloat("_Seed", starSeed);

        RenderSettings.skybox = skyboxMaterial;

        //Space ambient: flat, dark, slightly blue — no procedural sky gradient in space
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = spaceAmbient;
        RenderSettings.fog = false;

        //Star bodies light the system themselves — only tune a fallback light when the
        //scene has no SFStar at all (planet-only test scenes)
        bool hasStarBody = FindFirstObjectByType<SFStar>() != null;

        if (useFallbackSun && !hasStarBody)
        {
            if (fallbackSun == null)
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length; i++)
                    if (lights[i].type == LightType.Directional)
                    {
                        fallbackSun = lights[i];
                        break;
                    }
            }

            if (fallbackSun != null)
            {
                fallbackSun.intensity = fallbackSunIntensity;
                fallbackSun.color = fallbackSunColor;
            }
        }

        //Camera renders the skybox
        Camera camera = Camera.main;
        if (camera != null)
            camera.clearFlags = CameraClearFlags.Skybox;
    }
}
