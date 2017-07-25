using UnityEngine;
using System.Collections;

public static class SFUtilities
{
    public static int RoundToInteger(float x)
    {
        return Mathf.RoundToInt(x + 0.5f);
    }

    public static float StellarDustLimit(float _stellarMassRatio)
    {
        return 200.0f * Mathf.Pow(_stellarMassRatio, 1.0f / 3.0f);
    }
    //4.
    public static float CalculateInnerPlanetBoundary(float _stellarMassRatio)
    {
        return 0.3f * Mathf.Pow(_stellarMassRatio, 1.0f / 3.0f);
    }

    //5.a
    public static float CalculateOuterPlanetBoundary(float _stellarMassRatio)
    {
        return 50.0f * Mathf.Pow(_stellarMassRatio, 1.0f / 3.0f);
    }

    public static float CalculateEccentricity()
    {
        float e;

        e = 1.0f - Mathf.Pow(Random.Range(0.0f, 1.0f), SFConstants.ECCENTRICITY_COEFFICIENT);

        if (e > 0.99f)
            e = 0.99f;

        return e;
    }

    //5.b
    public static float CalculateOuterPlanetLimitBinarySystem(float _mass1, float _mass2, float _eccen, float _axis)
    {
        float mu = _mass2 / (_mass1 + _mass2);
        float e  = _eccen;
        float a  = _axis;

        return (0.464f + (-0.380f * mu) + (-0.631f * e) + (0.586f * mu * e) + (0.150f * Mathf.Pow(e, 2.0f)) + (-0.198f * mu * Mathf.Pow(e, 2.0f))) * a;
    }

    //7.a
    public static float CalculateInnerEffectLimit(float _a, float _e, float _mass)
    {
        return _a * (1.0f - _e) * (1.0f - _mass) / (1.0f + SFConstants.CLOUD_ECCENTRICITY);
    }

    //7.b
    public static float CalculateOuterEffectLimit(float _a, float _e, float _mass)
    {
        return _a * (1.0f + _e) * (1.0f + _mass) / (1.0f - SFConstants.CLOUD_ECCENTRICITY);
    }

    public static float CalculateInnerEffectLimit_MASSLESS(float _r_p, float _x_p)
    {
        return _r_p - _x_p - SFConstants.CLOUD_ECCENTRICITY * (_r_p - _x_p) / (1 + SFConstants.CLOUD_ECCENTRICITY);
    }

    public static float CalculateOuterEffectLimit_MASSLESS(float _r_p, float _x_p)
    {
        return _r_p + _x_p + SFConstants.CLOUD_ECCENTRICITY * (_r_p + _x_p) / (1 - SFConstants.CLOUD_ECCENTRICITY);
    }

    //8.
    //public static float CalculateCriticalMass(float _a, float _e, float _stellarLuminosityRatio)
    public static float CalculateCriticalMass(float _a, float _e, float _stellarLuminosityRatio)
    {
        float temp,
              r_p;

        r_p = _a - _a * _e;
        temp = r_p * Mathf.Sqrt(_stellarLuminosityRatio);

        return SFConstants.B * Mathf.Pow(temp, -0.75f);
    }

    public static float CalculateDustDensity(float _a, float _stellarLuminosityRatio)
    {
        return SFConstants.DUST_DENSITY_COEFFICIENT * Mathf.Sqrt(_stellarLuminosityRatio) * Mathf.Exp(-SFConstants.ALPHA * Mathf.Pow(_a, 1.0f / SFConstants.N));
    }

    public static float CalculateDustAndGasDensity(float _a, float _stellarLuminosityRatio, float _critMass, float _mass)
    {
        return SFConstants.K * CalculateDustDensity(_a, _stellarLuminosityRatio) / (1 + Mathf.Sqrt(_critMass / _mass) * (SFConstants.K - 1.0f));
    }

    public static float CalculateBandWidth(float _a, float _e, float _xA, float _xP, float _rA, float _rP)
    {
        return 2 * _a * _e +
               _xA + _xP +
               SFConstants.CLOUD_ECCENTRICITY * (_rA + _xA) / (1 - SFConstants.CLOUD_ECCENTRICITY) +
               SFConstants.CLOUD_ECCENTRICITY * (_rP - _xP) / (1 + SFConstants.CLOUD_ECCENTRICITY);
    }

    public static float About(float _value, float _variation)
    {
        return _value + (_value * Random.Range(-_variation, _variation));
    }
}
