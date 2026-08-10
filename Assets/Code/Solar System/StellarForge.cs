using UnityEngine;
using System.Collections;
using System.Collections.Generic;

//How planets are arranged around a binary pair
public enum SF_BINARY_TYPE
{
    //Planets orbit the primary star; the companion truncates the disc from outside
    S_TYPE_CIRCUMSTELLAR,
    //Planets orbit both stars from beyond the pair (Tatooine); the binary clears an
    //inner zone roughly 2-4x its separation
    P_TYPE_CIRCUMBINARY
}

[ExecuteAlways]
public class StellarForge : MonoBehaviour
{
    public int    SystemSeed    = 90132;

    [Header("Primary Star")]
    //Mass is the one free parameter of a main-sequence star: luminosity, radius,
    //temperature, colour and lifetime all follow from it. The range is what the Dole
    //accretion and Fogg environment formulas are calibrated for — roughly M-dwarf
    //through late A-class
    [Range(0.2f, 1.5f)]
    public float  primaryMass       = 1.0f;   //Solar masses (Sol = 1.0)

    //Off (default) = luminosity derived from mass, which is what real stars do.
    //On = author it by hand for a fictional or evolved star (giants, subdwarfs)
    public bool   overrideLuminosity = false;
    public float  primaryLuminosity  = 1.0f;  //Solar luminosities; used only when overriding

    [Header("Binary Companion (companionMass = 0 for a single star)")]
    //S-type: planets orbit the primary; the companion truncates its disc from outside.
    //Needs a WIDE separation (20+ AU) to leave room. One sun close, one distant and bright.
    //P-type: planets orbit BOTH stars from outside the pair; the binary's gravity clears
    //an inner zone (~2-4x separation). Needs a TIGHT separation. Two suns in the sky.
    public SF_BINARY_TYPE binaryType = SF_BINARY_TYPE.S_TYPE_CIRCUMSTELLAR;
    //These three only matter together: a companion of companionMass orbiting the primary
    //at binarySeparation AU. The companion truncates the primary's planet-forming disc,
    //so wide separations (20+ AU) are needed to leave room for planets
    //Capped at the primary's mass — the heavier star of a pair is the primary by
    //definition, and a companion cannot outweigh it without swapping their roles
    [Range(0.0f, 1.5f)]
    public float  companionMass     = 0.0f;   //Solar masses; 0 = no companion
    public float  binarySeparation  = 0.0f;   //Separation between the stars, in AU
    [Range(0.0f, 0.95f)]
    public float  binaryEccentricity = 0.0f;  //Of the two stars' mutual orbit

    private void OnValidate()
    {
        primaryMass = Mathf.Clamp(primaryMass, 0.2f, 1.5f);
        primaryLuminosity = Mathf.Max(primaryLuminosity, 0.0f);

        //A companion heavier than the primary would just be the primary — keep the
        //slider's upper end meaningful by clamping to the primary's mass
        if (companionMass > primaryMass)
            companionMass = primaryMass;

        //Below the hydrogen-burning limit it is a brown dwarf, not a star
        if (companionMass > 0.0f && companionMass < 0.08f)
            companionMass = 0.0f;

        binarySeparation = Mathf.Max(binarySeparation, 0.0f);
        mapScale = Mathf.Max(mapScale, 0.01f);

#if UNITY_EDITOR
        //Inspector edits must show up immediately: OnValidate fires on every field change,
        //but the Scene view will not repaint on its own while nothing is selected/moving
        if (!Application.isPlaying)
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();

        UnityEditor.SceneView.RepaintAll();
#endif
    }

    //Every value the generator consumes — a change to any of them invalidates the map
    private int ComputeGenerationHash()
    {
        int hash = 17;
        hash = hash * 31 + SystemSeed;
        hash = hash * 31 + primaryMass.GetHashCode();
        hash = hash * 31 + (overrideLuminosity ? 1 : 0);
        hash = hash * 31 + (overrideLuminosity ? primaryLuminosity.GetHashCode() : 0);
        hash = hash * 31 + companionMass.GetHashCode();
        hash = hash * 31 + binarySeparation.GetHashCode();
        hash = hash * 31 + binaryEccentricity.GetHashCode();
        hash = hash * 31 + (int)binaryType;
        hash = hash * 31 + (Designation != null ? Designation.GetHashCode() : 0);
        hash = hash * 31 + (requireHabitable ? 1 : 0);
        hash = hash * 31 + habitableSearchLimit;
        hash = hash * 31 + (requireInHabitableZone ? 1 : 0);
        hash = hash * 31 + minimumHydrosphere.GetHashCode();
        return hash;
    }

    //A world worth landing on: temperate, wet, breathable-scale pressure, not tidally
    //locked — and optionally inside the star's liquid-water band
    public bool IsHabitable(SFPlanetData _planet)
    {
        if (_planet.PlanetType != SF_PLANET_TYPE.TERRESTRIAL)
            return false;

        if (_planet.HydrosphereCoverage < minimumHydrosphere)
            return false;

        if (_planet.ResonantPeriod)
            return false;

        if (requireInHabitableZone)
        {
            //Same companion-corrected zone the map draws, so search and overlay agree
            if (_planet.Axis < SystemMap.habitableZoneInner || _planet.Axis > SystemMap.habitableZoneOuter)
                return false;
        }

        return true;
    }

    public bool SystemHasHabitablePlanet()
    {
        for (int i = 0; i < PLANET_LIST.Count; i++)
            if (IsHabitable(PLANET_LIST[i]))
                return true;

        return false;
    }
    public string Designation   = "Sol";                  //Catalogue designation of the Star...
    public string Name          = "The Solar System";     //Local name of the Star...


    [Header("Habitability")]
    //Search seeds until the system contains a world worth landing on. The found seed is
    //written back to SystemSeed, so the result stays deterministic and reproducible
    public bool  requireHabitable      = false;
    //How many seeds to try before giving up and keeping the last one
    public int   habitableSearchLimit  = 500;
    //Also require the world to sit inside the star's liquid-water band
    public bool  requireInHabitableZone = true;
    //Minimum surface water for the planet to count as habitable
    [Range(0.0f, 1.0f)]
    public float minimumHydrosphere    = 0.05f;

    [Header("System Map Gizmos")]
    //Draw the system in the Scene view (orbits, zone rings) — regenerates in edit mode
    public bool  drawSystemMap    = true;

    //World units per AU for the gizmo overlay
    public float mapScale = 10.0f;
    public bool  showOrbits       = true;
    public bool  showZones        = true;
    public bool  showLabels       = true;

    private SFSun     Sun              = new SFSun();
    private SFSun     Companion;                        //null for a single star

    public SFSun PrimaryStar   { get { return Sun; } }
    public SFSun CompanionStar { get { return Companion; } }

    public bool IsCircumbinary
    {
        get { return Companion != null && binaryType == SF_BINARY_TYPE.P_TYPE_CIRCUMBINARY; }
    }

    //What the PLANETS orbit and are lit by. Circumbinary worlds see the pair as one
    //source; S-type worlds orbit the primary alone (the companion only warms them a
    //little, which the habitable-zone correction accounts for separately)
    public float SystemMass
    {
        get { return IsCircumbinary ? Sun.Mass + Companion.Mass : Sun.Mass; }
    }

    public float SystemLuminosity
    {
        get { return IsCircumbinary ? Sun.Luminosity + Companion.Luminosity : Sun.Luminosity; }
    }

    //Ecosphere of whatever the planets actually orbit
    public float SystemEcosphereRadius
    {
        get { return Mathf.Sqrt(SystemLuminosity); }
    }
    private SFAccrete Accrete          = new SFAccrete();
    private Transform SunTransform;

    private List<SFNuclei> NUCLEI_LIST = new List<SFNuclei>();
    private List<SFPlanetData> PLANET_LIST = new List<SFPlanetData>();

    private SFSystemMap SystemMap = new SFSystemMap();
    //Hash of every input that affects generation — the edit-mode map rebuilds when it changes
    private int lastGenerationHash = int.MinValue;

    public SFSystemMap Map { get { return SystemMap; } }

    void Start()
    {
        if (!Application.isPlaying)
            return;

        TheForge();

        Debug.Log("--------------------------------------------------");
        //for (int i = 0; i < DUST_LIST.Count; i++)
        //    Debug.Log("Dust band " + i + " has dust? " + DUST_LIST[i].DustPresent + " / has gas? " + DUST_LIST[i].GasPresent + " / Has Planet? " + DUST_LIST[i].HasPlanet + " / inner edge: " + DUST_LIST[i].InnerEdge.ToString("0.00") + " / outer edge: " + DUST_LIST[i].OuterEdge.ToString("0.00"));

        Debug.Log("Seed " + SystemSeed + " run Complete, " + Accrete.NUCLEI_USED + " nuclei injected into the cloud resulting in: " + PLANET_LIST.Count + " planets...");
        Debug.Log("Primary: " + Sun.SpectralClass + " | " + Sun.Mass.ToString("0.00") + " Msol | "
            + Sun.Luminosity.ToString("0.000") + " Lsol | r " + Sun.Radius.ToString("0.00") + " Rsol | "
            + Sun.Temperature.ToString("0") + " K | age " + (Sun.Age / 1.0E9f).ToString("0.0")
            + " Gyr of " + (Sun.Life / 1.0E9f).ToString("0.0") + " Gyr | ecosphere " + SystemEcosphereRadius.ToString("0.00") + " AU");

        if (Companion != null)
            Debug.Log("Companion: " + Companion.SpectralClass + " | " + Companion.Mass.ToString("0.00") + " Msol | "
                + Companion.Luminosity.ToString("0.000") + " Lsol | r " + Companion.Radius.ToString("0.00") + " Rsol | "
                + Companion.Temperature.ToString("0") + " K | separation " + binarySeparation.ToString("0.00") + " AU | "
                + (IsCircumbinary
                    ? "P-type circumbinary — planets orbit both stars beyond " + Sun.InnerPlanetBoundary.ToString("0.00") + " AU"
                    : "S-type — planets orbit the primary inside " + Sun.OuterPlanetBoundary.ToString("0.00") + " AU"));

        for (int i = 0; i < PLANET_LIST.Count; i++)
        {
            SFPlanetData p = PLANET_LIST[i];

            Debug.Log("Planet " + (i + 1) + ": " + p.PlanetType
                + " | " + p.Axis.ToString("0.00") + " AU"
                + " | r " + p.EquitorialRadius.ToString("0") + " km"
                + " | " + (p.Mass * SFConstants.SUN_MASS_IN_EARTH_MASSES).ToString("0.00") + " Me"
                + " | g " + p.SurfaceGravity.ToString("0.00")
                + " | " + p.SurfaceTemp.ToString("0") + " K (" + p.MinTemp.ToString("0") + "-" + p.MaxTemp.ToString("0") + ")"
                + " | " + p.SurfacePressure.ToString("0") + " mb"
                + " | hydro " + (p.HydrosphereCoverage * 100.0f).ToString("0") + "%"
                + " | ice " + (p.IceCoverage * 100.0f).ToString("0") + "%"
                + " | cloud " + (p.CloudCoverage * 100.0f).ToString("0") + "%"
                + " | day " + p.LengthOfDay.ToString("0.0") + " h"
                + (p.ResonantPeriod ? " [resonant]" : "")
                + (p.GreenhouseEffect ? " [greenhouse]" : ""));
        }
    }

    #region System Map Gizmos

    //Scene-view overlay of the generated system: zone rings (planet limits, habitable
    //zone, frost line, dust limit) and every body's orbit, drawn from the same ephemeris
    //the simulation will use. Regenerates in edit mode whenever the seed changes
    private void OnDrawGizmos()
    {
        if (!drawSystemMap)
            return;

        //Rebuild whenever ANY generation input changed — not just the seed
        int hash = ComputeGenerationHash();
        if (!Application.isPlaying && lastGenerationHash != hash)
        {
            lastGenerationHash = hash;
            TheForge();
        }

        if (SystemMap.primaryStar == null)
            return;

        Vector3 origin = transform.position;

        if (showZones)
        {
            //Zones centre on whatever the planets orbit: the barycenter for circumbinary
            //systems, the primary star (offset from origin in a binary) otherwise
            double time = Application.isPlaying ? Time.timeSinceLevelLoad : 0.0;
            Vector3 zoneCenter = SystemMap.circumbinary
                ? origin
                : origin + SystemMap.primaryStar.GetPosition(time).ToVector3(mapScale);

            DrawZones(zoneCenter);

            //The binary separation is a property of the pair, so it rings the barycenter
            if (SystemMap.binarySeparation > 0.0f)
                DrawBinaryRing(origin);
        }

        if (showOrbits)
            DrawOrbits(origin);
    }

    private void DrawBinaryRing(Vector3 _origin)
    {
        DrawRing(_origin, SystemMap.binarySeparation, new Color(1.0f, 0.85f, 0.3f, 0.7f));

#if UNITY_EDITOR
        if (showLabels)
            DrawLabel(_origin, SystemMap.binarySeparation, "binary separation " + SystemMap.binarySeparation.ToString("0.00") + " AU");

        if (SystemMap.discSterilized)
        {
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.Label(_origin + Vector3.up * mapScale * 0.5f,
                "NO PLANETS: companion truncates the disc to " + SystemMap.outerPlanetLimit.ToString("0.00")
                + " AU, inside the " + SystemMap.innerPlanetLimit.ToString("0.00")
                + " AU inner limit.\nWiden binarySeparation (20+ AU is typical).");
        }
#endif
    }

    private void DrawZones(Vector3 _origin)
    {
        //Dust cloud extent — the raw material the system formed from
        DrawRing(_origin, SystemMap.outerDustLimit, new Color(0.35f, 0.30f, 0.25f, 0.5f));

        //Where planets can hold stable orbits
        DrawRing(_origin, SystemMap.innerPlanetLimit, new Color(0.8f, 0.35f, 0.25f, 0.9f));
        DrawRing(_origin, SystemMap.outerPlanetLimit, new Color(0.8f, 0.35f, 0.25f, 0.9f));

        //Liquid water band
        DrawRing(_origin, SystemMap.habitableZoneInner, new Color(0.3f, 0.9f, 0.4f, 0.9f));
        DrawRing(_origin, SystemMap.habitableZoneOuter, new Color(0.3f, 0.9f, 0.4f, 0.9f));

        //Beyond here ices survive and giants grow
        DrawRing(_origin, SystemMap.frostLine, new Color(0.5f, 0.8f, 1.0f, 0.9f));

#if UNITY_EDITOR
        if (showLabels)
        {
            DrawLabel(_origin, SystemMap.innerPlanetLimit, "inner limit " + SystemMap.innerPlanetLimit.ToString("0.00") + " AU");
            DrawLabel(_origin, SystemMap.habitableZoneInner, "HZ " + SystemMap.habitableZoneInner.ToString("0.00")
                + "-" + SystemMap.habitableZoneOuter.ToString("0.00") + " AU");
            DrawLabel(_origin, SystemMap.frostLine, "frost line " + SystemMap.frostLine.ToString("0.00") + " AU");
            DrawLabel(_origin, SystemMap.outerPlanetLimit, "outer limit " + SystemMap.outerPlanetLimit.ToString("0.00") + " AU");
        }
#endif
    }

    private void DrawOrbits(Vector3 _origin)
    {
        double time = Application.isPlaying ? Time.timeSinceLevelLoad : 0.0;

        for (int i = 0; i < SystemMap.bodies.Count; i++)
        {
            SFSystemMap.Body body = SystemMap.bodies[i];

            Color color = body.isStar
                ? body.star.StarColor
                : PlanetTypeColor(body.planetData.PlanetType);

            //The orbit path, drawn around wherever its parent currently is
            if (body.orbit != null)
            {
                Vector3 parentOffset = body.parent != null
                    ? body.parent.GetPosition(time).ToVector3(mapScale)
                    : Vector3.zero;

                DrawOrbitPath(_origin + parentOffset, body.orbit, time, color);
            }

            //The body itself
            Vector3 position = _origin + body.GetPosition(time).ToVector3(mapScale);
            Gizmos.color = color;

            float size = body.isStar
                ? Mathf.Max(mapScale * 0.06f * body.star.Radius, mapScale * 0.03f)
                : Mathf.Max(mapScale * 0.02f, 0.05f);

            Gizmos.DrawSphere(position, size);

            //Ring the world you could actually live on
            bool habitable = !body.isStar && IsHabitable(body.planetData);
            if (habitable)
            {
                Gizmos.color = new Color(0.4f, 1.0f, 0.5f);
                Gizmos.DrawWireSphere(position, size * 2.5f);
            }

#if UNITY_EDITOR
            if (showLabels)
            {
                string text = body.isStar
                    ? body.name + "  " + body.star.SpectralClass + "  " + body.star.Temperature.ToString("0") + "K  "
                      + body.star.Mass.ToString("0.00") + " Msol  " + body.star.Luminosity.ToString("0.00") + " Lsol"
                    : body.name + "  " + body.planetData.PlanetType + (habitable ? "  ★ HABITABLE" : "");

                UnityEditor.Handles.color = habitable ? new Color(0.4f, 1.0f, 0.5f) : color;
                UnityEditor.Handles.Label(position + Vector3.up * size * 2.0f, text);
            }
#endif
        }
    }

    //Orbit paths are fixed ellipses — only the focus moves. Solving Kepler for every
    //segment every frame costs thousands of Newton-Raphson iterations per redraw, so the
    //shape is computed once per orbit and cached
    private readonly Dictionary<SFOrbit, Vector3[]> orbitPathCache = new Dictionary<SFOrbit, Vector3[]>();
    private float cachedPathScale = -1.0f;

    private void DrawOrbitPath(Vector3 _focus, SFOrbit _orbit, double _time, Color _color)
    {
        const int segments = 96;

        //A scale change invalidates every cached shape
        if (!Mathf.Approximately(cachedPathScale, mapScale))
        {
            orbitPathCache.Clear();
            cachedPathScale = mapScale;
        }

        Vector3[] path;
        if (!orbitPathCache.TryGetValue(_orbit, out path))
        {
            path = new Vector3[segments + 1];

            for (int i = 0; i <= segments; i++)
            {
                double t = _orbit.periodDays * ((double)i / segments);
                path[i] = _orbit.GetPosition(t).ToVector3(mapScale);
            }

            orbitPathCache[_orbit] = path;
        }

        Gizmos.color = new Color(_color.r, _color.g, _color.b, 0.55f);

        for (int i = 1; i <= segments; i++)
            Gizmos.DrawLine(_focus + path[i - 1], _focus + path[i]);
    }

    //Unit circle computed once — rings are just this scaled and offset, which avoids
    //hundreds of trig calls per frame across all the zone rings
    private const int RingSegments = 96;
    private static Vector3[] unitRing;

    private static Vector3[] GetUnitRing()
    {
        if (unitRing != null)
            return unitRing;

        unitRing = new Vector3[RingSegments + 1];

        for (int i = 0; i <= RingSegments; i++)
        {
            float angle = (Mathf.PI * 2.0f) * ((float)i / RingSegments);
            unitRing[i] = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
        }

        return unitRing;
    }

    private void DrawRing(Vector3 _origin, float _radiusAU, Color _color)
    {
        if (_radiusAU <= 0.0f)
            return;

        float radius = _radiusAU * mapScale;
        Vector3[] ring = GetUnitRing();

        Gizmos.color = _color;

        for (int i = 1; i <= RingSegments; i++)
            Gizmos.DrawLine(_origin + ring[i - 1] * radius, _origin + ring[i] * radius);
    }

#if UNITY_EDITOR
    private void DrawLabel(Vector3 _origin, float _radiusAU, string _text)
    {
        if (_radiusAU <= 0.0f)
            return;

        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(_origin + new Vector3(0.0f, 0.0f, _radiusAU * mapScale), _text);
    }
#endif

    private static Color PlanetTypeColor(SF_PLANET_TYPE _type)
    {
        switch (_type)
        {
            case SF_PLANET_TYPE.TERRESTRIAL: return new Color(0.35f, 0.75f, 1.0f);
            case SF_PLANET_TYPE.WATER:       return new Color(0.2f, 0.5f, 1.0f);
            case SF_PLANET_TYPE.MARTIAN:     return new Color(0.9f, 0.45f, 0.3f);
            case SF_PLANET_TYPE.VENUSIAN:    return new Color(1.0f, 0.8f, 0.4f);
            case SF_PLANET_TYPE.ICE:         return new Color(0.75f, 0.9f, 1.0f);
            case SF_PLANET_TYPE.JOVIAN:      return new Color(1.0f, 0.65f, 0.35f);
            case SF_PLANET_TYPE.SUB_JOVIAN:  return new Color(0.9f, 0.7f, 0.5f);
            case SF_PLANET_TYPE.GAS_DWARF:   return new Color(0.7f, 0.8f, 0.7f);
            case SF_PLANET_TYPE.ONE_FACE:    return new Color(1.0f, 0.5f, 0.6f);
            case SF_PLANET_TYPE.ASTEROIDS:   return new Color(0.6f, 0.55f, 0.5f);
            default:                         return new Color(0.7f, 0.7f, 0.7f);
        }
    }

    #endregion

    void InitializeTheSun()
    {
        //Main-sequence range this simulation is calibrated for; outside it the accretion
        //and environment formulas stop being meaningful
        primaryMass = Mathf.Clamp(primaryMass, 0.2f, 1.5f);

        //Mass drives everything: luminosity, radius, temperature, colour, class, lifetime
        Sun.DeriveFromMass(primaryMass, overrideLuminosity ? primaryLuminosity : 0.0f);

        //Keep the inspector honest about what the star is actually radiating
        primaryLuminosity = Sun.Luminosity;

        //The companion is described exactly the same way, and shares the pair's age
        if (companionMass > 0.001f)
        {
            Companion = new SFSun();
            Companion.DeriveFromMass(companionMass, 0.0f);
        }
        else
            Companion = null;

        float sunMaxAge = SFConstants.SUN_MAX_AGE;

        if (Sun.Life < SFConstants.SUN_MAX_AGE)
            sunMaxAge = Sun.Life;

        Sun.Age = SFUtilities.Range(SFConstants.SUN_MIN_AGE, sunMaxAge);

        //Binary stars form together, so they share an age
        if (Companion != null)
            Companion.Age = Sun.Age;

        Sun.OuterPlanetBoundary = 0.0f;
        Sun.InnerPlanetBoundary = 0.0f;

        if (companionMass > 0.001f && binarySeparation > 0.0f)
        {
            if (binaryType == SF_BINARY_TYPE.P_TYPE_CIRCUMBINARY)
            {
                //Planets orbit the pair from outside; the binary sweeps the inner region
                Sun.InnerPlanetBoundary = SFUtilities.CalculateInnerPlanetLimitCircumbinary(
                    Sun.Mass, companionMass, binaryEccentricity, binarySeparation);

                float outerLimit = SFUtilities.CalculateOuterPlanetBoundary(Sun.Mass + companionMass);

                if (Sun.InnerPlanetBoundary >= outerLimit)
                {
                    Debug.LogWarning("StellarForge: the pair at " + binarySeparation.ToString("0.00")
                        + " AU clears everything out to " + Sun.InnerPlanetBoundary.ToString("0.00")
                        + " AU, beyond the " + outerLimit.ToString("0.00")
                        + " AU outer limit — no planets can form. Reduce binarySeparation "
                        + "(circumbinary systems want tight pairs, typically under 1 AU).");
                }
            }
            else
            {
                //The companion truncates the primary's disc from outside (Holman-Wiegert)
                float boundary = SFUtilities.CalculateOuterPlanetLimitBinarySystem(Sun.Mass, companionMass, binaryEccentricity, binarySeparation);
                float innerLimit = SFUtilities.CalculateInnerPlanetBoundary(Sun.Mass);

                if (boundary <= innerLimit * 1.05f)
                {
                    Debug.LogWarning("StellarForge: companion at " + binarySeparation.ToString("0.00")
                        + " AU truncates the disc to " + boundary.ToString("0.00")
                        + " AU, inside the inner planet limit of " + innerLimit.ToString("0.00")
                        + " AU — no planets can form. Widen binarySeparation (20+ AU is typical), "
                        + "or switch to P-type for a tight pair.");
                }

                Sun.OuterPlanetBoundary = boundary;
            }
        }
    }

    void GeneratePlanet(int _i, SFNuclei _nuclei)
    {
        //Coalescence can leave a protoplanet with non-physical mass (the merge maths can
        //go negative when two nuclei nearly cancel). Such a body has no meaningful
        //radius, gravity or climate — NaNs would propagate through every downstream
        //calculation and into the ephemeris, so drop it here
        if (!(_nuclei.Mass > 0.0f) || float.IsNaN(_nuclei.Mass) || float.IsInfinity(_nuclei.Mass))
        {
            Debug.LogWarning("StellarForge: discarded a protoplanet at " + _nuclei.Axis.ToString("0.00")
                + " AU with non-physical mass (" + _nuclei.Mass + ").");
            return;
        }

        SFPlanetData temp = new SFPlanetData();

        //Everything the accretion phase knows about this body carries over
        temp.Axis                   = _nuclei.Axis;
        temp.Eccen                  = _nuclei.Eccen;
        temp.Mass                   = _nuclei.Mass;
        temp.GasMass                = _nuclei.GasMass;
        temp.DustMass               = _nuclei.DustMass;
        temp.GasGiant               = _nuclei.GasGiant;

        temp.Atmosphere             = null;
        temp.GasCount               = 0;
        temp.SurfaceTemp            = 0.0f;
        temp.HighTemp               = 0.0f;
        temp.LowTemp                = 0.0f;
        temp.MaxTemp                = 0.0f;
        temp.MinTemp                = 0.0f;
        temp.GreenhouseTempRise     = 0.0f;
        temp.PlanetIndex            = _i;
        temp.ResonantPeriod         = false;
        temp.OrbitalZone            = SFEnvironment.CalculateOrbitalZone(SystemLuminosity, _nuclei.Axis);
        temp.OrbitalPeriod          = SFEnvironment.CalculateOrbitalPeriod(_nuclei.Axis, _nuclei.Mass, SystemMass);
        temp.AxialTilt              = SFEnvironment.CalculateInclination(_nuclei.Axis);
        temp.ExosphericTemp         = SFConstants.EARTH_EXOSPHERE_TEMP / Mathf.Pow(_nuclei.Axis / SystemEcosphereRadius, 2.0f);
        temp.RMSVelocity            = SFEnvironment.CalculateRMSVelocity(SFConstants.MOL_NITROGEN, temp.ExosphericTemp);
        temp.CoreRadius             = SFEnvironment.CalculateKothariCoreRadius(_nuclei.DustMass, _nuclei.GasGiant, temp.OrbitalZone);

        //Density/radius depend on whether this actually IS a gas giant — passing a
        //hardcoded flag here gave every terrestrial a gas giant's density
        temp.Density                = SFEnvironment.CalculateEmpiricalDensity(_nuclei.Mass, _nuclei.Axis, SystemEcosphereRadius, _nuclei.GasGiant);
        temp.EquitorialRadius       = SFEnvironment.CalculateVolumeRadius(_nuclei.Mass, temp.Density);

        temp.SurfaceAcceleration    = SFEnvironment.CalculateSurfaceAcceleration(_nuclei.Mass, temp.EquitorialRadius);
        temp.SurfaceGravity         = SFEnvironment.CalculateSurfaceGravity(temp.SurfaceAcceleration);
        temp.EscapeVelocity         = SFEnvironment.CalculateEscapeVelocity(_nuclei.Mass, temp.EquitorialRadius);

        //--- Environment chain: rotation, atmosphere, then the coupled climate solution ---

        bool resonant;
        temp.LengthOfDay            = SFEnvironment.CalculateDayLength(temp, SystemMass, Sun.Age, out resonant);
        temp.ResonantPeriod         = resonant;

        temp.MolecularWeightRetained = SFEnvironment.CalculateMinMolecularWeight(temp, Sun.Age);

        if (temp.GasGiant)
        {
            //Giants are all atmosphere — no surface conditions to solve
            temp.SurfacePressure    = 0.0f;
            temp.BoilingPoint       = 0.0f;
            temp.HydrosphereCoverage = 0.0f;
            temp.CloudCoverage      = 1.0f;
            temp.IceCoverage        = 0.0f;
            temp.Albedo             = SFUtilities.About(SFConstants.GAS_GIANT_ALBEDO, 0.1f);
            temp.SurfaceTemp        = SFEnvironment.CalculateEffectiveTemp(SystemEcosphereRadius, temp.Axis, temp.Albedo);
            temp.GreenhouseTempRise = 0.0f;
            temp.HighTemp = temp.LowTemp = temp.MaxTemp = temp.MinTemp = temp.SurfaceTemp;
        }
        else
        {
            //A greenhouse runs away when the world is warm enough and dark enough to trap it
            float effectiveTemp     = SFEnvironment.CalculateEffectiveTemp(SystemEcosphereRadius, temp.Axis, SFConstants.EARTH_ALBEDO);
            temp.GreenhouseEffect   = temp.OrbitalZone == 1
                                   && effectiveTemp > SFConstants.FREEZING_POINT_OF_WATER
                                   && SystemEcosphereRadius / temp.Axis > 1.0f;

            bool accretedGas        = (temp.GasMass / Mathf.Max(temp.Mass, 1.0E-30f)) > 0.05f;

            temp.VolatileGasInventory = SFEnvironment.CalculateVolatileGasInventory(temp.Mass, temp.EscapeVelocity,
                                            temp.RMSVelocity, SystemMass, temp.OrbitalZone, temp.GreenhouseEffect, accretedGas);

            temp.SurfacePressure    = SFEnvironment.CalculateSurfacePressure(temp.VolatileGasInventory,
                                            temp.EquitorialRadius, temp.SurfaceGravity);
            temp.BoilingPoint       = SFEnvironment.CalculateBoilingPoint(temp.SurfacePressure);

            //Everything below couples together — solve it as a system
            SFEnvironment.IterateSurfaceTemperature(temp, SystemEcosphereRadius);
        }

        temp.PlanetType             = SFEnvironment.ClassifyPlanet(temp);

        PLANET_LIST.Add(temp);
    }

    void TheForge()
    {
        if (requireHabitable)
            ForgeUntilHabitable();
        else
            ForgeOnce(SystemSeed);

        //Positioned bodies: barycenter at origin, stars around it, planets around the primary
        SystemMap.Build(Sun, Companion, binaryEccentricity, binarySeparation, PLANET_LIST, Designation, IsCircumbinary);
    }

    //Run the whole simulation for one seed. Everything downstream is a pure function of
    //it, so a seed that produced a good system will produce it again forever
    void ForgeOnce(int _seed)
    {
        SFUtilities.InitRandom(_seed);

        SunTransform = this.transform;

        PLANET_LIST.Clear();
        NUCLEI_LIST.Clear();
        Accrete = new SFAccrete();

        InitializeTheSun();

        //In a circumbinary system the pair acts as a single source: planets far outside it
        //feel the combined mass and are lit by the combined luminosity
        float accretionMass = SystemMass;
        float accretionLuminosity = SystemLuminosity;

        Accrete.SetInitialConditions(0.0f, Sun.OuterDustLimit);
        NUCLEI_LIST = Accrete.DistributePlanetaryMasses(accretionMass, accretionLuminosity,
                                                        Sun.OuterPlanetBoundary, Sun.InnerPlanetBoundary);

        for (int i = 0; i < NUCLEI_LIST.Count; i++)
        {
            GeneratePlanet(i, NUCLEI_LIST[i]);
        }
    }

    //Accretion is honest, so most seeds produce no home world. Walk seeds from the
    //current one until a habitable planet turns up, then adopt that seed — the system
    //stays fully deterministic and reproducible, just chosen rather than lucky
    void ForgeUntilHabitable()
    {
        int limit = Mathf.Max(habitableSearchLimit, 1);
        int startingSeed = SystemSeed;

        for (int attempt = 0; attempt < limit; attempt++)
        {
            int seed = startingSeed + attempt;

            ForgeOnce(seed);

            //The map holds the companion-corrected habitable zone the check reads
            SystemMap.Build(Sun, Companion, binaryEccentricity, binarySeparation, PLANET_LIST, Designation, IsCircumbinary);

            if (SystemHasHabitablePlanet())
            {
                if (seed != SystemSeed)
                {
                    SystemSeed = seed;
                    Debug.Log("StellarForge: habitable system found at seed " + seed
                        + " (searched " + (attempt + 1) + " seeds).");
                }
                return;
            }
        }

        //Nothing found — the star itself may be the obstacle (too dim, too hot, or a
        //companion truncating the disc), which no amount of reseeding can fix
        Debug.LogWarning("StellarForge: no habitable system in " + limit + " seeds from "
            + startingSeed + ". The star or companion may make habitability impossible — "
            + "try a primary mass near 1.0, or widen binarySeparation.");

        ForgeOnce(startingSeed);
    }
}