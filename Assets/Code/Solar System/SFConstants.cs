using UnityEngine;
using System.Collections;

public class SFConstants : MonoBehaviour
{
    public const float N                        = 3.0f;         //Used in density calculation
    public const float B                        = 1.2E-5f;      //Used in critical mass calculation
    public const float K                        = 50.0f;        //K = gas/dust ratio
    //public const float K                        = 150.0f;        //K = gas/dust ratio - Testing to see viable range for editor...
    public const float ALPHA                    = 5.0f;         //Used in density calculation
    public const float PROTOPLANET_MASS         = 1.0E-15f;
    public const float CLOUD_ECCENTRICITY       = 0.2f;         //W in Dole paper
    //public const float CLOUD_ECCENTRICITY       = 0.65f;         //W in Dole paper - Testing to see viable range for editor...
    public const float ECCENTRICITY_COEFFICIENT = 0.077f;
    public const float DUST_DENSITY_COEFFICIENT = 1.5E-3f;      //A in Dole's Paper
    //public const float DUST_DENSITY_COEFFICIENT = 1.5E-2f;      //A in Dole's Paper - Testing to see viable range for editor...
    //public const float DUST_DENSITY_COEFFICIENT = 3.0E-3f;      //A in Dole's Paper - Testing to see viable range for editor...
    //public const float DUST_DENSITY_COEFFICIENT = 6.0E-3f;      //A in Dole's Paper - Testing to see viable range for editor...

    public const float SUN_MIN_AGE             = 1.0E9f;
    public const float SUN_MAX_AGE             = 6.0E9f;

    public const float DAYS_IN_A_YEAR           = 365.256f;     //Earth days per year...
    public const float EARTH_AXIAL_TILT         = 23.4f;        //Degrees
    public const float EARTH_EXOSPHERE_TEMP     = 1273.0f;      //Degrees kelvin
    public const float MOLAR_GAS_CONSTANT       = 8314.41f;     //g*m^2 / (sec^2 * K * mol)
    public const float SOLAR_MASS_IN_GRAMS      = 1.989E33f;
    public const float SUN_MASS_IN_EARTH_MASSES = 332775.64f;
    public const float GRAV_CONSTANT            = 6.672E-8f;    //dyne cm^2 / g^2
    public const float EARTH_ACCELERATION       = 980.7f;       //cm/sec^2
    public const float GAS_RETENTION_THRESHOLD  = 6.0f;

    //For the Kothari radius calc...
    public const float A1_20                    = 6.485E12f;                       //cgs system: cm, g, dynes, etc...
    public const float A2_20                    = 4.0032E-8f;
    public const float BETA_20                  = 5.71E12f;

    public const float FUDGE_FACTOR             = 1.004f;       //Original programmers fudge factor...


    public const float CM_PER_METER             = 100.0f;
    public const float CM_PER_KM                = 1.0E5f;

    /// Molecular weights (used for RMS velocity calcs)...from Habitable Planets for Man, p.38 by Dole
    public const float MOL_NITROGEN = 28.0f;

    //--- Environment chain (Fogg 1985 / starform-stargen lineage) ---
    public const float J_ANGULAR_MOMENTUM        = 1.46E-19f;   //cm^2/sec^2 g — Fogg eq. 12
    public const float CHANGE_IN_EARTH_ANG_VEL   = -1.3E-15f;   //rad/sec/year — tidal despin rate
    public const float EARTH_MASS_IN_GRAMS       = 5.977E27f;
    public const float EARTH_RADIUS_CM           = 6.378E8f;
    public const float EARTH_RADIUS_KM           = 6378.0f;
    public const float EARTH_DENSITY             = 5.52f;       //g/cc
    public const float SECONDS_PER_HOUR          = 3600.0f;

    public const float EARTH_ALBEDO              = 0.3f;
    public const float GREENHOUSE_TRIGGER_ALBEDO = 0.20f;
    public const float FREEZING_POINT_OF_WATER   = 273.15f;     //K
    public const float EARTH_EFFECTIVE_TEMP      = 250.0f;      //K
    public const float EARTH_SURF_PRES_IN_MILLIBARS = 1000.0f;
    public const float MILLIBARS_PER_BAR         = 1013.25f;

    public const float CLOUD_COVERAGE_FACTOR     = 1.839E-8f;   //km^2/kg
    public const float EARTH_WATER_MASS_PER_AREA = 3.83E15f;    //g/km^2
    public const float Q2_36                     = 0.0698f;     //1/K — cloud vapor exponent
    public const float EARTH_CONVECTION_FACTOR   = 0.43f;
    public const float WATER_VAPOR_WEIGHT        = 18.0f;

    public const float CLOUD_ALBEDO              = 0.52f;
    public const float ROCKY_ALBEDO              = 0.15f;
    public const float ROCKY_AIRLESS_ALBEDO      = 0.07f;
    public const float WATER_ALBEDO              = 0.04f;
    public const float ICE_ALBEDO                = 0.7f;
    public const float AIRLESS_ICE_ALBEDO        = 0.5f;
    public const float GAS_GIANT_ALBEDO          = 0.5f;

    public const float INCREDIBLY_LARGE_NUMBER   = 9.9999E37f;
}
