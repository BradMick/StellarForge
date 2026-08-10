using System.Collections.Generic;
using UnityEngine;

//The generated system as positioned bodies: the barycenter sits at this transform's
//origin, stars orbit it (a single star simply sits on it), and planets orbit the
//primary star — which is what the accretion physics actually models for binaries
//(the companion truncates the primary's disc rather than hosting its own planets).
//Circumbinary (P-type) systems come later; the tree structure already supports them.
//Everything here is data + gizmos — spawning real planet prefabs is the next milestone
public class SFSystemMap
{
    public class Body
    {
        public string name;
        public SFOrbit orbit;               //null = sits at the barycenter
        public Body parent;                 //null = orbits the barycenter
        public SFPlanetData planetData;     //null for stars

        //Stars carry their full physical description (mass, luminosity, radius,
        //temperature, colour, class, age) — the same SFSun the generator produced
        public bool isStar;
        public SFSun star;

        //Position in AU relative to the barycenter, at a given time
        public Vector3d GetPosition(double _timeDays)
        {
            Vector3d local = orbit != null ? orbit.GetPosition(_timeDays) : new Vector3d(0.0, 0.0, 0.0);

            if (parent != null)
                return parent.GetPosition(_timeDays) + local;

            return local;
        }
    }

    public readonly List<Body> bodies = new List<Body>();

    public Body primaryStar;
    public Body secondaryStar;

    //Zone radii in AU, for the map overlay
    public float innerPlanetLimit;
    public float outerPlanetLimit;
    public float habitableZoneInner;
    public float habitableZoneOuter;
    public float frostLine;
    public float outerDustLimit;
    public float binarySeparation;

    //True when a companion has truncated the disc inside the inner planet limit — the
    //system physically cannot form planets and the map should say so plainly
    public bool discSterilized;

    //Build the tree from a completed generator run
    //Circumbinary: planets orbit the barycenter rather than the primary star
    public bool circumbinary;

    public void Build(SFSun _sun, SFSun _companion, float _binaryEccentricity, float _binarySeparation,
                      List<SFPlanetData> _planets, string _systemName, bool _circumbinary)
    {
        bodies.Clear();

        float companionMass = _companion != null ? _companion.Mass : 0.0f;
        circumbinary = _circumbinary && _companion != null;

        //Circumbinary planets orbit the combined mass, and the pair clears an inner zone
        float systemMass = circumbinary ? _sun.Mass + companionMass : _sun.Mass;

        innerPlanetLimit = _sun.InnerPlanetBoundary > 0.0f
            ? _sun.InnerPlanetBoundary
            : SFUtilities.CalculateInnerPlanetBoundary(systemMass);

        outerPlanetLimit = _sun.OuterPlanetBoundary > 0.0f
            ? _sun.OuterPlanetBoundary
            : SFUtilities.CalculateOuterPlanetBoundary(systemMass);
        //A close companion adds real warmth. Planets orbit the primary (S-type), so the
        //companion's contribution is diluted by how far away it is compared to the
        //planet's own orbit — a rough but honest correction that keeps the zones useful
        float effectiveLuminosity = _sun.Luminosity;

        if (circumbinary)
        {
            //Both stars are effectively at the centre — the planets get the full sum
            effectiveLuminosity += _companion.Luminosity;
        }
        else if (_companion != null && _binarySeparation > 0.0f)
        {
            //S-type: the distant companion adds a diluted contribution
            float separationFactor = Mathf.Pow(Mathf.Max(_binarySeparation, 0.1f), 2.0f);
            effectiveLuminosity += _companion.Luminosity / separationFactor;
        }

        habitableZoneInner = SFEnvironment.CalculateHabitableZoneInner(effectiveLuminosity);
        habitableZoneOuter = SFEnvironment.CalculateHabitableZoneOuter(effectiveLuminosity);
        frostLine = SFEnvironment.CalculateFrostLine(effectiveLuminosity);
        outerDustLimit = _sun.OuterDustLimit;
        binarySeparation = _companion != null ? _binarySeparation : 0.0f;
        //No usable band: an S-type companion truncated the disc below the inner limit, or
        //a circumbinary pair cleared everything out past the outer limit
        discSterilized = outerPlanetLimit <= innerPlanetLimit * 1.05f;

        //--- Stars ---
        primaryStar = MakeStar(_systemName + " A", _sun);

        if (_companion != null)
        {
            //Both stars orbit the barycenter on ellipses sized by the inverse mass ratio
            float totalMass = _sun.Mass + companionMass;
            double periodDays = System.Math.Sqrt(System.Math.Pow(_binarySeparation, 3.0) / totalMass)
                              * SFConstants.DAYS_IN_A_YEAR;

            double primaryAxis = _binarySeparation * (companionMass / totalMass);
            double secondaryAxis = _binarySeparation * (_sun.Mass / totalMass);

            secondaryStar = MakeStar(_systemName + " B", _companion);

            //Opposite phase — the stars are always on opposite sides of the barycenter
            primaryStar.orbit = new SFOrbit(primaryAxis, _binaryEccentricity, periodDays, 0.0, 0.0);
            secondaryStar.orbit = new SFOrbit(secondaryAxis, _binaryEccentricity, periodDays, System.Math.PI, 0.0);
        }

        //--- Planets: S-type, orbiting the primary ---
        for (int i = 0; i < _planets.Count; i++)
        {
            SFPlanetData data = _planets[i];

            Body body = new Body();
            body.name = _systemName + " " + (i + 1);
            body.planetData = data;
            //Circumbinary worlds circle the barycenter (both suns in their sky);
            //S-type worlds circle the primary and watch the companion wander
            body.parent = circumbinary ? null : primaryStar;

            //Deterministic starting position and orientation for each planet
            double meanAnomaly = SFUtilities.Range(0.0f, Mathf.PI * 2.0f);
            double argument = SFUtilities.Range(0.0f, Mathf.PI * 2.0f);

            body.orbit = new SFOrbit(data.Axis, data.Eccen, data.OrbitalPeriod, meanAnomaly, argument);

            bodies.Add(body);
        }
    }

    private Body MakeStar(string _name, SFSun _star)
    {
        Body star = new Body();
        star.name = _name;
        star.isStar = true;
        star.star = _star;

        bodies.Add(star);
        return star;
    }
}
