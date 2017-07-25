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
}
