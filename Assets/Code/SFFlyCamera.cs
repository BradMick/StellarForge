using UnityEngine;

//Test fly camera for planetary scales.
//  Click the Game view to capture the mouse, Escape to release.
//  Mouse = look, WASD = move, E/Q = up/down, Shift = boost, scroll wheel = speed trim.
//Speed scales with altitude above the nearest planet's TERRAIN (analytic height query —
//no colliders involved), so ground-skimming is precise and crossing orbits is fast.
//The camera cannot pass through terrain: below radius + height + clearance it is pushed
//back out along the radial. Near the surface the horizon gently levels to planet-up
[RequireComponent(typeof(Camera))]
public class SFFlyCamera : MonoBehaviour
{
    [Header("Look")]
    public float lookSensitivity = 2.5f;
    //How strongly the camera rolls upright toward planet-up when flying low
    public float horizonLeveling = 2.0f;

    [Header("Speed")]
    public float minSpeed = 5.0f;
    public float maxSpeed = 50000.0f;
    //Speed ≈ altitude × this factor (scroll wheel trims it at runtime)
    public float altitudeSpeedFactor = 0.5f;
    public float boostMultiplier = 4.0f;

    [Header("Collision")]
    //Closest the camera may come to the terrain surface, in world units
    public float clearance = 2.0f;
    //Also stay above the sea surface (terrainless bodies like water shells are ignored
    //when this is off, so diving is possible)
    public bool stayAboveWater = true;

    private float speedTrim = 1.0f;

    //Scanning the scene for planets four times a frame is pure waste — the set changes
    //only when a system spawns, so refresh it occasionally instead
    private SFPlanet[] cachedPlanets = new SFPlanet[0];
    private float nextPlanetScan;

    private SFPlanet[] Planets
    {
        get
        {
            //Planets are destroyed and respawned whenever the system regenerates, so a
            //cached entry can go stale between scans. Rescan as soon as any entry has
            //died rather than handing out destroyed references
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

            return cachedPlanets;
        }
    }

    private void Update()
    {
        HandleCursor();

        if (Cursor.lockState == CursorLockMode.Locked)
            HandleLook();

        HandleMovement();
        ClampAbovePlanets();
    }

    private void HandleCursor()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void HandleLook()
    {
        Vector3 up = ReferenceUp();

        float yaw = Input.GetAxis("Mouse X") * lookSensitivity;
        float pitch = -Input.GetAxis("Mouse Y") * lookSensitivity;

        transform.rotation = Quaternion.AngleAxis(yaw, up)
                           * Quaternion.AngleAxis(pitch, transform.right)
                           * transform.rotation;

        //Gently roll upright toward the reference up (strongest near the surface)
        if (horizonLeveling > 0.0f)
        {
            Quaternion level = Quaternion.FromToRotation(transform.up, up);
            float t = 1.0f - Mathf.Exp(-horizonLeveling * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(Quaternion.identity, level, t * UpWeight()) * transform.rotation;
        }
    }

    private void HandleMovement()
    {
        //Scroll wheel trims the speed curve up/down
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
            speedTrim = Mathf.Clamp(speedTrim * (1.0f + scroll), 0.05f, 20.0f);

        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += transform.forward;
        if (Input.GetKey(KeyCode.S)) move -= transform.forward;
        if (Input.GetKey(KeyCode.D)) move += transform.right;
        if (Input.GetKey(KeyCode.A)) move -= transform.right;
        if (Input.GetKey(KeyCode.E)) move += ReferenceUp();
        if (Input.GetKey(KeyCode.Q)) move -= ReferenceUp();

        if (move == Vector3.zero)
            return;

        float altitude = Mathf.Abs(NearestAltitude());
        float speed = Mathf.Clamp(altitude * altitudeSpeedFactor, minSpeed, maxSpeed) * speedTrim;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            speed *= boostMultiplier;

        transform.position += move.normalized * speed * Time.deltaTime;
    }

    //Push the camera back above any planet surface it has dipped below — analytic, exact,
    //works at every LOD because it queries the same height function the terrain is built from
    private void ClampAbovePlanets()
    {
        SFPlanet[] planets = Planets;

        for (int i = 0; i < planets.Length; i++)
        {
            SFPlanet planet = planets[i];

            //A regenerating system can destroy planets mid-frame
            if (planet == null)
                continue;

            //Visual-only shells (atmosphere) never block
            if (planet.intangible)
                continue;

            //Terrainless spheres (water shells) only block when staying above water
            if (planet.terrain == null && !stayAboveWater)
                continue;

            Vector3 local = planet.transform.InverseTransformPoint(transform.position);
            float distance = local.magnitude;
            if (distance < 0.0001f)
                continue;

            Vector3 direction = local / distance;

            float floorRadius = planet.planetRadius + planet.GetHeight(direction);

            if (stayAboveWater && planet.terrain != null)
            {
                float seaRadius = planet.planetRadius
                    * (1.0f + planet.terrain.oceanLevel * planet.terrain.heightScale);
                floorRadius = Mathf.Max(floorRadius, seaRadius);
            }

            floorRadius += clearance;

            if (distance < floorRadius)
                transform.position = planet.transform.TransformPoint(direction * floorRadius);
        }
    }

    //Altitude above the nearest planet's terrain (world units; large when no planets exist)
    private float NearestAltitude()
    {
        float altitude = float.MaxValue;

        SFPlanet[] planets = Planets;
        for (int i = 0; i < planets.Length; i++)
        {
            if (planets[i] == null || planets[i].intangible)
                continue;

            Vector3 local = planets[i].transform.InverseTransformPoint(transform.position);
            float distance = local.magnitude;
            if (distance < 0.0001f)
                continue;

            float a = distance - (planets[i].planetRadius + planets[i].GetHeight(local / distance));
            if (a < altitude)
                altitude = a;
        }

        return altitude == float.MaxValue ? maxSpeed : altitude;
    }

    //World up far from planets, radial planet-up near the surface
    private Vector3 ReferenceUp()
    {
        SFPlanet nearest = null;
        float altitude = float.MaxValue;

        SFPlanet[] planets = Planets;
        for (int i = 0; i < planets.Length; i++)
        {
            if (planets[i] == null || planets[i].intangible)
                continue;

            float a = Vector3.Distance(transform.position, planets[i].transform.position) - planets[i].planetRadius;
            if (a < altitude)
            {
                altitude = a;
                nearest = planets[i];
            }
        }

        if (nearest == null)
            return Vector3.up;

        Vector3 radial = (transform.position - nearest.transform.position).normalized;
        return Vector3.Slerp(Vector3.up, radial, UpWeight()).normalized;
    }

    //How "planetary" the orientation should be: 1 at the surface, 0 beyond one radius up
    private float UpWeight()
    {
        SFPlanet[] planets = Planets;

        float best = 0.0f;
        for (int i = 0; i < planets.Length; i++)
        {
            if (planets[i] == null || planets[i].intangible)
                continue;

            float altitude = Vector3.Distance(transform.position, planets[i].transform.position) - planets[i].planetRadius;
            float w = 1.0f - Mathf.Clamp01(altitude / Mathf.Max(planets[i].planetRadius, 1.0f));
            if (w > best)
                best = w;
        }

        return best;
    }
}
