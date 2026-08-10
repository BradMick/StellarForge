using UnityEngine;

//Planetary-scale depth precision. Depth-buffer resolution degrades with distance² / nearPlane:
//a fixed 0.1 near plane at planetary viewing ranges quantizes depth in steps of hundreds of
//units — which breaks anything comparing depths (the water shader) and invites z-fighting.
//Scales the near plane with altitude above the nearest planet surface: tight near the ground,
//generous from orbit. Attach to any camera that views planets
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class SFDynamicClipPlanes : MonoBehaviour
{
    public float minNear = 0.05f;
    //Generous ceiling matters: at 200k+ viewing range a near plane capped at 200 leaves
    //~16-unit depth steps — enough for coastline z-fighting. Lower this only when
    //close-range foreground geometry (cockpit, ship) actually needs it
    public float maxNear = 2000.0f;

    //Near plane as a fraction of altitude — 0.05 keeps nearby geometry in frame while
    //recovering ~500× depth precision at orbital distance versus a fixed 0.1
    [Range(0.005f, 0.2f)]
    public float nearAltitudeFraction = 0.05f;

    //Far plane also tracks the scene: always generous enough to contain the farthest
    //planet with margin, so bodies never clip out of existence at fixed-far distances
    public float minFar = 100000.0f;

    private Camera targetCamera;
    private SFPlanet[] cachedPlanets = new SFPlanet[0];
    private float nextPlanetScan;

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        float altitude = float.MaxValue;
        float farthest = 0.0f;

        //Rescanning the scene every frame is wasteful; the body set changes rarely.
        //A regenerated system destroys planets though, so any dead entry forces a rescan
        bool stale = Time.unscaledTime >= nextPlanetScan;

        if (!stale)
        {
            for (int i = 0; i < cachedPlanets.Length; i++)
                if (cachedPlanets[i] == null)
                {
                    stale = true;
                    break;
                }
        }

        if (stale)
        {
            cachedPlanets = FindObjectsByType<SFPlanet>(FindObjectsSortMode.None);
            nextPlanetScan = Time.unscaledTime + 1.0f;
        }

        SFPlanet[] planets = cachedPlanets;
        for (int i = 0; i < planets.Length; i++)
        {
            //Visual-only shells (atmosphere) are not surfaces — ignore for altitude/far
            //reach. A regenerating system can also destroy planets mid-frame
            if (planets[i] == null || planets[i].intangible)
                continue;

            float surfaceRadius = planets[i].planetRadius;
            if (planets[i].terrain != null)
                surfaceRadius += planets[i].terrain.MaxHeight(planets[i].planetRadius);

            float centerDistance = Vector3.Distance(transform.position, planets[i].transform.position);

            float a = centerDistance - surfaceRadius;
            if (a < altitude)
                altitude = a;

            float reach = centerDistance + surfaceRadius;
            if (reach > farthest)
                farthest = reach;
        }

        if (altitude == float.MaxValue)
            return;

        targetCamera.nearClipPlane = Mathf.Clamp(Mathf.Abs(altitude) * nearAltitudeFraction, minNear, maxNear);
        targetCamera.farClipPlane = Mathf.Max(minFar, farthest * 1.2f);
    }
}
