using UnityEngine;

//A closed-form Keplerian orbit. Position is a pure function of time — no integration,
//so there is no drift, time can be scaled or rewound freely, and any body's position at
//any date can be asked for directly (missions, arrival estimates, save/load).
//Doubles throughout per Law 4; the render layer converts to floats relative to an anchor.
//Orbits are laid out in the XZ plane (Unity's ground plane) with +Y as the system north
[System.Serializable]
public class SFOrbit
{
    //Semi-major axis in AU
    public double semiMajorAxis = 1.0;
    //0 = circle, approaching 1 = highly elliptical
    public double eccentricity = 0.0;
    //Orbital period in days
    public double periodDays = 365.256;
    //Where the body sits on its orbit at t=0, in radians
    public double meanAnomalyAtEpoch = 0.0;
    //Rotation of the ellipse within the orbital plane, in radians
    public double argumentOfPeriapsis = 0.0;

    public SFOrbit() { }

    public SFOrbit(double _semiMajorAxis, double _eccentricity, double _periodDays,
                   double _meanAnomalyAtEpoch, double _argumentOfPeriapsis)
    {
        semiMajorAxis = _semiMajorAxis;
        eccentricity = System.Math.Min(System.Math.Max(_eccentricity, 0.0), 0.99);
        periodDays = System.Math.Max(_periodDays, 0.0001);
        meanAnomalyAtEpoch = _meanAnomalyAtEpoch;
        argumentOfPeriapsis = _argumentOfPeriapsis;
    }

    //Position relative to the focus (the parent body), in AU
    public Vector3d GetPosition(double _timeDays)
    {
        double meanAnomaly = meanAnomalyAtEpoch + 2.0 * System.Math.PI * (_timeDays / periodDays);
        double eccentricAnomaly = SolveKepler(meanAnomaly, eccentricity);

        //Position in the orbital plane, periapsis along +X
        double x = semiMajorAxis * (System.Math.Cos(eccentricAnomaly) - eccentricity);
        double z = semiMajorAxis * System.Math.Sqrt(1.0 - eccentricity * eccentricity)
                 * System.Math.Sin(eccentricAnomaly);

        //Rotate the ellipse by the argument of periapsis
        double cos = System.Math.Cos(argumentOfPeriapsis);
        double sin = System.Math.Sin(argumentOfPeriapsis);

        return new Vector3d(x * cos - z * sin, 0.0, x * sin + z * cos);
    }

    //Newton-Raphson on Kepler's equation M = E - e·sin(E)
    private static double SolveKepler(double _meanAnomaly, double _eccentricity)
    {
        //Wrap to [-pi, pi] so the initial guess is always close
        double m = _meanAnomaly % (2.0 * System.Math.PI);
        if (m > System.Math.PI) m -= 2.0 * System.Math.PI;
        if (m < -System.Math.PI) m += 2.0 * System.Math.PI;

        double e = m;   //good starting guess for low eccentricity

        for (int i = 0; i < 12; i++)
        {
            double delta = (e - _eccentricity * System.Math.Sin(e) - m)
                         / (1.0 - _eccentricity * System.Math.Cos(e));
            e -= delta;

            if (System.Math.Abs(delta) < 1.0E-12)
                break;
        }

        return e;
    }
}

//Minimal double-precision vector — the simulation layer's currency (Law 4).
//Unity's Vector3 is float and belongs to the render layer only
[System.Serializable]
public struct Vector3d
{
    public double x, y, z;

    public Vector3d(double _x, double _y, double _z)
    {
        x = _x;
        y = _y;
        z = _z;
    }

    public static Vector3d operator +(Vector3d _a, Vector3d _b)
    {
        return new Vector3d(_a.x + _b.x, _a.y + _b.y, _a.z + _b.z);
    }

    public static Vector3d operator -(Vector3d _a, Vector3d _b)
    {
        return new Vector3d(_a.x - _b.x, _a.y - _b.y, _a.z - _b.z);
    }

    public static Vector3d operator *(Vector3d _a, double _s)
    {
        return new Vector3d(_a.x * _s, _a.y * _s, _a.z * _s);
    }

    public double Magnitude
    {
        get { return System.Math.Sqrt(x * x + y * y + z * z); }
    }

    //Convert to Unity space at a given scale (AU → world units)
    public Vector3 ToVector3(double _scale)
    {
        return new Vector3((float)(x * _scale), (float)(y * _scale), (float)(z * _scale));
    }
}
