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

    //On-screen readout of what this component actually did last frame. The camera planes
    //failing silently produced a multi-day debugging spiral (a stale far plane slicing the
    //star into a crescent, blamed on meshes and billboards) — never again. Leave on until
    //the scale work settles
    public bool debugOverlay = true;

    private Camera targetCamera;
    private SFPlanet[] cachedPlanets = new SFPlanet[0];
    private float nextPlanetScan;
    private string lastVerdict = "never ran";

    //OnPreCull, not LateUpdate: it fires for EVERY render of this camera, including
    //edit-mode Game view repaints. LateUpdate on an ExecuteAlways component only runs on
    //editor ticks, which are not guaranteed to precede a Game view repaint — so in edit
    //mode the camera could render with whatever planes were serialized (near 0.1), and at
    //stellar distances that leaves no depth precision at the geometry: the star renders
    //as a black disc inside its own glare. Empirically confirmed — hand-raising near from
    //0.1 to 0.29 flipped the photosphere from black to correct with no other change
    private void OnPreCull()
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

        //Stars count too — via their own registry, NEVER FindObjectsByType. Spawned stars
        //are DontSave objects, and FindObjectsByType silently skips those: the previous
        //version scanned for stars, found none while one filled the screen, took the
        //early-out below every frame, and left the camera on its serialized planes. The
        //stale far plane then sliced the star — a dark ring at the limb seen head-on, a
        //crescent bite seen off-axis — which spent days being misdiagnosed as mesh
        //winding, depth-state and billboard bugs. The star itself was never broken
        var stars = SFStar.ActiveStars;
        for (int i = 0; i < stars.Count; i++)
        {
            if (stars[i] == null)
                continue;

            float surfaceRadius = stars[i].visualRadius;
            float centerDistance = Vector3.Distance(transform.position, stars[i].transform.position);

            float a = centerDistance - surfaceRadius;
            if (a < altitude)
                altitude = a;

            float reach = centerDistance + surfaceRadius;
            if (reach > farthest)
                farthest = reach;
        }

        //Nothing to frame — leave the camera's authored planes alone
        if (altitude == float.MaxValue)
        {
            lastVerdict = "SKIPPED: no planets or stars found — camera keeps serialized planes "
                + targetCamera.nearClipPlane.ToString("0.##") + " / " + targetCamera.farClipPlane.ToString("0");
            return;
        }

        targetCamera.nearClipPlane = Mathf.Clamp(Mathf.Abs(altitude) * nearAltitudeFraction, minNear, maxNear);
        targetCamera.farClipPlane = Mathf.Max(minFar, farthest * 1.2f);

        lastVerdict = "applied near " + targetCamera.nearClipPlane.ToString("0.##")
            + " / far " + targetCamera.farClipPlane.ToString("0")
            + " | stars " + SFStar.ActiveStars.Count + " planets " + cachedPlanets.Length
            + " | altitude " + altitude.ToString("0");
    }

    //Ground truth on screen. If this text is missing from the Game view entirely, the
    //running assembly predates this code — which is itself the answer
    private void OnGUI()
    {
        if (!debugOverlay)
            return;

        GUI.Label(new Rect(10, 10, 900, 22), "SFDynamicClipPlanes: " + lastVerdict);
    }
}
