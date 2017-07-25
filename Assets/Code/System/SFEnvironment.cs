using UnityEngine;
using System.Collections;

public static class SFEnvironment
{
    public static float CalculateLuminosity(float _stellarMassRatio)
    {
        float n;

        if (_stellarMassRatio < 1.0f)
            n = 1.75f * (_stellarMassRatio - 0.1f) + 3.325f;
        else
            n = 0.5f * (2.0f - _stellarMassRatio) + 4.4f;

        return Mathf.Pow(_stellarMassRatio, n);
    }

    public static int CalculateOrbitalZone(float _stellarLuminosityRatio, float _axis)
    {
        if (_axis < (4.0f * Mathf.Sqrt(_stellarLuminosityRatio)))
            return 1;
        else if (_axis < (15.0f * Mathf.Sqrt(_stellarLuminosityRatio)))
            return 2;
        else
            return 3;
    }

    public static float CalculateOrbitalPeriod(float _a, float _nucleiMass, float _sunMass)
    {
        float periodInYears;

        periodInYears = Mathf.Sqrt(Mathf.Pow(_a, 3.0f) / (_nucleiMass + _sunMass));
        return periodInYears * SFConstants.DAYS_IN_A_YEAR;
    }

    public static float CalculateInclination(float _a)
    {
        int temp;

        temp = (int)(Mathf.Pow(_a, 0.2f) * SFUtilities.About(SFConstants.EARTH_AXIAL_TILT, 0.4f));
        return temp % 360;
    }

    public static float CalculateRMSVelocity(float _molecularWeight, float _exosphericTemp)
    {
        return Mathf.Sqrt((3.0f * SFConstants.MOLAR_GAS_CONSTANT * _exosphericTemp) / _molecularWeight) * SFConstants.CM_PER_METER;
    }

    public static float CalculateKothariCoreRadius(float _dustMass, bool _giant, int _orbitalZone)
    {
        float t1, t2, t3, atomicWeight, atomicNum;

        if (_orbitalZone == 1)
        {
            if (_giant)
            {
                atomicNum       = 4.5f;
                atomicWeight    = 9.5f;
            }
            else
            {
                atomicNum       = 8.0f;
                atomicWeight    = 15.0f;
            }
        }
        else if (_orbitalZone == 2)
        {
            if (_giant)
            {
                atomicNum       = 2.0f;
                atomicWeight    = 2.47f;
            }
            else
            {
                atomicNum       = 5.0f;
                atomicWeight    = 10.0f;
            }
        }
        else
        {
            if (_giant)
            {
                atomicNum       = 4.0f;
                atomicWeight    = 7.0f;
            }
            else
            {
                atomicNum       = 5.0f;
                atomicWeight    = 10.0f;
            }
        }


        t1 = atomicWeight * atomicNum;

        t2 = (2.0f * SFConstants.BETA_20 * Mathf.Pow(SFConstants.SOLAR_MASS_IN_GRAMS, 1.0f / 3.0f)) / (SFConstants.A1_20 * Mathf.Pow(t1, 1.0f / 3.0f));

        t3 = SFConstants.A2_20 * Mathf.Pow(atomicWeight, 4.0f / 3.0f) * Mathf.Pow(SFConstants.SOLAR_MASS_IN_GRAMS, 2.0f / 3.0f);
        t3 = t3 * Mathf.Pow(_dustMass, 2.0f / 3.0f);
        t3 = 1.0f + t3;
        t2 = 1.0f / t3;
        t2 = (t2 * Mathf.Pow(_dustMass, 1.0f / 3.0f)) / SFConstants.CM_PER_KM;

        t2 /= SFConstants.FUDGE_FACTOR; //Make earth = actual earth...

        return t2;
    }

    public static float CalculateEmpiricalDensity(float _m, float _a, float _r_eco, bool gasGiant)
    {
        float temp;

        temp = Mathf.Pow(_m * SFConstants.SUN_MASS_IN_EARTH_MASSES, 1.0f / 8.0f);
        temp = temp * Mathf.Pow(_r_eco / _a, 1.0f / 4.0f);
        if (gasGiant)
            return temp * 1.2f;
        else
            return temp * 5.5f;
    }

    public static float CalculateVolumeRadius(float _mass, float _density)
    {
        float volume;

        _mass = _mass * SFConstants.SOLAR_MASS_IN_GRAMS;
        volume = _mass / _density;

        return Mathf.Pow((3.0f * volume) / (4.0f * Mathf.PI), 1.0f / 3.0f) / SFConstants.CM_PER_KM;
    }

    public static float CalculateSurfaceAcceleration(float _mass, float _equitorialRadius)
    {
        return SFConstants.GRAV_CONSTANT * (_mass * SFConstants.SOLAR_MASS_IN_GRAMS) / Mathf.Pow(_equitorialRadius * SFConstants.CM_PER_KM, 2.0f);
    }

    public static float CalculateSurfaceGravity(float _surfaceAcceleration)
    {
        return _surfaceAcceleration / SFConstants.EARTH_ACCELERATION;
    }

    /*public static float CalculateMinMolecularWeight(SFPlanet _planet)
    {
        float m         = _planet.Mass,
              r         = _planet.EquitorialRadius,
              t_exo     = _planet.ExosphericTemp,
              target    = 5.0e9f;

        float guess1 = CalculateMoleculeLimit(m, r, t_exo);
        float guess2 = guess1;

        float life = CalculateGasLife(guess1, _planet); //Write me 
        //Finish me!!
    }*/

    public static float CalculateMoleculeLimit(float _mass, float _radius, float _exosphericTemp)
    {
        float v_esc = CalculateEscapeVelocity(_mass, _radius);

        return (3.0f * SFConstants.MOLAR_GAS_CONSTANT * _exosphericTemp) / (Mathf.Pow(v_esc / SFConstants.GAS_RETENTION_THRESHOLD, 2.0f) / SFConstants.CM_PER_METER);
    }

    public static float CalculateEscapeVelocity(float _mass, float _radius)
    {
        float radiusInCM,
              massInGrams;

        radiusInCM = _radius * SFConstants.CM_PER_KM;
        massInGrams = _mass * SFConstants.SOLAR_MASS_IN_GRAMS;

        return Mathf.Sqrt(2.0f * SFConstants.GRAV_CONSTANT * massInGrams / radiusInCM);
    }

    /*public static float CalculateGasLife(float _molecularWeight, SFPlanet _planet)
    {

    }*/
}
