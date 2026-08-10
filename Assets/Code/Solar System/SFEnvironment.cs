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

    //--- Stellar physicals: everything a star's visuals need, derived from mass/luminosity ---

    //Main-sequence mass-radius relation (solar radii)
    public static float CalculateStellarRadius(float _stellarMassRatio)
    {
        return _stellarMassRatio < 1.0f
            ? Mathf.Pow(_stellarMassRatio, 0.8f)
            : Mathf.Pow(_stellarMassRatio, 0.57f);
    }

    //Effective surface temperature in kelvin, from Stefan-Boltzmann: L = R^2 * (T/Tsun)^4
    public static float CalculateStellarTemperature(float _luminosityRatio, float _radiusRatio)
    {
        float radius = Mathf.Max(_radiusRatio, 0.0001f);
        return 5778.0f * Mathf.Pow(_luminosityRatio / (radius * radius), 0.25f);
    }

    //Blackbody colour for a star of this temperature — the visual + light colour.
    //Piecewise approximation of the Planckian locus over the main-sequence range
    public static Color CalculateStellarColor(float _temperatureK)
    {
        float t = Mathf.Clamp(_temperatureK, 2000.0f, 40000.0f);

        float r, g, b;

        if (t < 6600.0f)
        {
            r = 1.0f;
            g = Mathf.Clamp01(0.39f * Mathf.Log(t / 100.0f) - 0.634f);
            b = t <= 2000.0f ? 0.0f : Mathf.Clamp01(0.543f * Mathf.Log((t / 100.0f) - 10.0f) - 1.196f);
        }
        else
        {
            r = Mathf.Clamp01(1.293f * Mathf.Pow((t / 100.0f) - 60.0f, -0.1332f));
            g = Mathf.Clamp01(1.13f * Mathf.Pow((t / 100.0f) - 60.0f, -0.0755f));
            b = 1.0f;
        }

        return new Color(r, g, b, 1.0f);
    }

    //Harvard spectral class from effective temperature
    public static string CalculateSpectralClass(float _temperatureK)
    {
        if (_temperatureK >= 30000.0f) return "O";
        if (_temperatureK >= 10000.0f) return "B";
        if (_temperatureK >= 7500.0f)  return "A";
        if (_temperatureK >= 6000.0f)  return "F";
        if (_temperatureK >= 5200.0f)  return "G";
        if (_temperatureK >= 3700.0f)  return "K";
        return "M";
    }

    //Frost line: beyond this, water ice survives and giants can form (Hayashi 1981)
    public static float CalculateFrostLine(float _luminosityRatio)
    {
        return 4.85f * Mathf.Sqrt(_luminosityRatio);
    }

    //Habitable zone bounds in AU (conservative Kasting-style scaling of the ecosphere)
    public static float CalculateHabitableZoneInner(float _luminosityRatio)
    {
        return 0.95f * Mathf.Sqrt(_luminosityRatio);
    }

    public static float CalculateHabitableZoneOuter(float _luminosityRatio)
    {
        return 1.37f * Mathf.Sqrt(_luminosityRatio);
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

    //Smallest molecular weight the planet can hold onto for its lifetime. Bisection on
    //gas life: too-light molecules escape in far less than the star's age, heavy ones
    //stay forever — the crossing point is what the atmosphere can retain
    public static float CalculateMinMolecularWeight(SFPlanetData _planet, float _sunAge)
    {
        float mass = _planet.Mass,
              radius = _planet.EquitorialRadius,
              exoTemp = _planet.ExosphericTemp;

        float guess1 = CalculateMoleculeLimit(mass, radius, exoTemp);
        float guess2 = guess1;

        //Bracket the answer: push the upper guess up until gas lives long enough
        int loops = 0;
        while (CalculateGasLife(guess2, _planet) < _sunAge && loops++ < 64)
            guess2 *= 2.0f;

        //...and the lower guess down until it does not
        loops = 0;
        while (CalculateGasLife(guess1, _planet) > _sunAge && loops++ < 64)
            guess1 *= 0.5f;

        //Bisect to a tight bracket
        for (int i = 0; i < 32 && (guess2 - guess1) > 0.1f; i++)
        {
            float mid = (guess1 + guess2) * 0.5f;

            if (CalculateGasLife(mid, _planet) < _sunAge)
                guess1 = mid;
            else
                guess2 = mid;
        }

        return guess2;
    }

    //Time for a gas of this molecular weight to bleed off the exosphere (Fogg eq. 15-16)
    public static float CalculateGasLife(float _molecularWeight, SFPlanetData _planet)
    {
        float v = CalculateRMSVelocity(_molecularWeight, _planet.ExosphericTemp);
        float g = _planet.SurfaceAcceleration;
        float r = _planet.EquitorialRadius * SFConstants.CM_PER_KM;

        if (v <= 0.0f)
            return SFConstants.INCREDIBLY_LARGE_NUMBER;

        float t = (Mathf.Pow(v, 3.0f) / (2.0f * g * g * r))
                * Mathf.Exp((3.0f * g * r) / (v * v));

        //Years, capped — practically infinite retention for heavy molecules
        float years = t / (SFConstants.SECONDS_PER_HOUR * 24.0f * SFConstants.DAYS_IN_A_YEAR);

        if (float.IsInfinity(years) || float.IsNaN(years) || years > SFConstants.INCREDIBLY_LARGE_NUMBER)
            return SFConstants.INCREDIBLY_LARGE_NUMBER;

        return years;
    }

    //Length of the planet's day in hours, accounting for tidal despin over the star's
    //lifetime. Returns the orbital period (tidally locked / resonant) when the star has
    //had time to stop the planet's rotation
    public static float CalculateDayLength(SFPlanetData _planet, float _sunMass, float _sunAge, out bool _resonant)
    {
        _resonant = false;

        float planetMassGrams = _planet.Mass * SFConstants.SOLAR_MASS_IN_GRAMS;
        float equatorialRadiusCM = _planet.EquitorialRadius * SFConstants.CM_PER_KM;
        float yearInHours = _planet.OrbitalPeriod * 24.0f;

        bool giant = _planet.GasGiant;

        //Fogg eq. 12: initial angular velocity from the body's angular momentum
        float k2 = giant ? 0.24f : 0.33f;
        float baseAngularVelocity = Mathf.Sqrt(2.0f * SFConstants.J_ANGULAR_MOMENTUM * planetMassGrams
                                  / (k2 * Mathf.Pow(equatorialRadiusCM, 2.0f)));

        //Tidal braking accumulated over the star's lifetime
        float changeInAngularVelocity = SFConstants.CHANGE_IN_EARTH_ANG_VEL
                                      * (_planet.Density / SFConstants.EARTH_DENSITY)
                                      * (equatorialRadiusCM / SFConstants.EARTH_RADIUS_CM)
                                      * (SFConstants.EARTH_MASS_IN_GRAMS / planetMassGrams)
                                      * Mathf.Pow(_sunMass, 2.0f)
                                      * (1.0f / Mathf.Pow(_planet.Axis, 6.0f));

        float angularVelocity = baseAngularVelocity + changeInAngularVelocity * _sunAge;

        bool stopped = false;
        float dayInHours;

        if (angularVelocity <= 0.0f)
        {
            stopped = true;
            dayInHours = SFConstants.INCREDIBLY_LARGE_NUMBER;
        }
        else
            dayInHours = (2.0f * Mathf.PI) / (SFConstants.SECONDS_PER_HOUR * angularVelocity);

        //Spun down past its own year: locked, or caught in a 2:3 resonance if eccentric
        if (dayInHours >= yearInHours || stopped)
        {
            if (_planet.Eccen > 0.1f)
            {
                _resonant = true;
                return (yearInHours * (1.0f - _planet.Eccen)) / (1.0f + _planet.Eccen);
            }

            return yearInHours;
        }

        return dayInHours;
    }

    //Fogg eq. 17: volatile gas inventory retained by the planet
    public static float CalculateVolatileGasInventory(float _mass, float _escapeVelocity, float _rmsVelocity,
                                                      float _sunMass, int _orbitalZone, bool _greenhouse, bool _accretedGas)
    {
        float velocityRatio = _escapeVelocity / _rmsVelocity;

        if (velocityRatio < SFConstants.GAS_RETENTION_THRESHOLD)
            return 0.0f;

        float proportionConstant;

        switch (_orbitalZone)
        {
            case 1:  proportionConstant = 100000.0f; break;   //inner zone: volatiles boiled away
            case 2:  proportionConstant = 75000.0f;  break;
            default: proportionConstant = 250.0f;    break;   //outer zone: ices survive
        }

        float earthUnits = _mass * SFConstants.SUN_MASS_IN_EARTH_MASSES;
        float temp = (proportionConstant * earthUnits) / _sunMass;
        temp = SFUtilities.About(temp, 0.2f);

        if (_greenhouse || _accretedGas)
            return temp;

        return temp / 100.0f;
    }

    //Fogg eq. 18: surface pressure in millibars
    public static float CalculateSurfacePressure(float _volatileGasInventory, float _equatorialRadius, float _surfaceGravity)
    {
        float radiusRatio = SFConstants.EARTH_RADIUS_KM / _equatorialRadius;

        return _volatileGasInventory * _surfaceGravity
             * (SFConstants.EARTH_SURF_PRES_IN_MILLIBARS / SFConstants.MILLIBARS_PER_BAR)
             / Mathf.Pow(radiusRatio, 2.0f);
    }

    //Boiling point of water at this pressure (Fogg eq. 21)
    public static float CalculateBoilingPoint(float _surfacePressureMb)
    {
        if (_surfacePressureMb <= 0.0f)
            return 0.0f;

        float surfacePressureInBars = _surfacePressureMb / SFConstants.MILLIBARS_PER_BAR;

        return 1.0f / ((Mathf.Log(surfacePressureInBars) / -5050.5f) + (1.0f / 373.0f));
    }

    //Fraction of the surface covered by liquid water (Fogg eq. 22)
    public static float CalculateHydrosphereFraction(float _volatileGasInventory, float _equatorialRadius)
    {
        float temp = (0.71f * _volatileGasInventory / 1000.0f)
                   * Mathf.Pow(SFConstants.EARTH_RADIUS_KM / _equatorialRadius, 2.0f);

        return Mathf.Clamp01(temp);
    }

    //Cloud cover from available water vapor and temperature (Fogg eq. 23)
    public static float CalculateCloudFraction(float _surfaceTemp, float _smallestMolecularWeight,
                                               float _equatorialRadius, float _hydrosphereFraction)
    {
        if (_smallestMolecularWeight > SFConstants.WATER_VAPOR_WEIGHT)
            return 0.0f;

        float surfaceArea = 4.0f * Mathf.PI * Mathf.Pow(_equatorialRadius, 2.0f);
        float hydrosphereMass = _hydrosphereFraction * surfaceArea * SFConstants.EARTH_WATER_MASS_PER_AREA;
        float waterVaporKG = (0.00000001f * hydrosphereMass)
                           * Mathf.Exp(SFConstants.Q2_36 * (_surfaceTemp - SFConstants.FREEZING_POINT_OF_WATER));

        float fraction = SFConstants.CLOUD_COVERAGE_FACTOR * waterVaporKG / surfaceArea;

        return Mathf.Clamp01(fraction);
    }

    //Ice cover from temperature and available water (Fogg eq. 24)
    public static float CalculateIceFraction(float _hydrosphereFraction, float _surfaceTemp)
    {
        if (_surfaceTemp > 328.0f)
            return 0.0f;

        float temp = Mathf.Pow((328.0f - _surfaceTemp) / 90.0f, 5.0f);

        if (temp > 1.5f * _hydrosphereFraction)
            temp = 1.5f * _hydrosphereFraction;

        return Mathf.Clamp01(temp);
    }

    //Effective temperature from stellar flux and albedo (Fogg eq. 19)
    public static float CalculateEffectiveTemp(float _ecosphereRadius, float _axis, float _albedo)
    {
        return Mathf.Sqrt(_ecosphereRadius / _axis)
             * Mathf.Pow((1.0f - _albedo) / (1.0f - SFConstants.EARTH_ALBEDO), 0.25f)
             * SFConstants.EARTH_EFFECTIVE_TEMP;
    }

    //Greenhouse warming (Fogg eq. 20) — opacity rises with pressure and water vapor
    public static float CalculateGreenhouseRise(float _opticalDepth, float _effectiveTemp, float _surfacePressureMb)
    {
        float convectionFactor = SFConstants.EARTH_CONVECTION_FACTOR
                               * Mathf.Pow(_surfacePressureMb / SFConstants.EARTH_SURF_PRES_IN_MILLIBARS, 0.4f);

        float rise = (Mathf.Pow(1.0f + 0.75f * _opticalDepth, 0.25f) - 1.0f) * _effectiveTemp * convectionFactor;

        return Mathf.Max(rise, 0.0f);
    }

    //Atmospheric opacity from retained gases and pressure (Fogg eq. 20 support)
    public static float CalculateOpacity(float _molecularWeight, float _surfacePressureMb)
    {
        float opticalDepth = 0.0f;

        if (_molecularWeight >= 0.0f && _molecularWeight < 10.0f)
            opticalDepth += 3.0f;
        else if (_molecularWeight < 20.0f)
            opticalDepth += 2.34f;
        else if (_molecularWeight < 30.0f)
            opticalDepth += 1.0f;
        else if (_molecularWeight < 45.0f)
            opticalDepth += 0.15f;
        else if (_molecularWeight < 100.0f)
            opticalDepth += 0.05f;

        float pressureRatio = _surfacePressureMb / SFConstants.EARTH_SURF_PRES_IN_MILLIBARS;

        if (pressureRatio >= 70.0f)
            opticalDepth *= 8.333f;
        else if (pressureRatio >= 50.0f)
            opticalDepth *= 6.666f;
        else if (pressureRatio >= 30.0f)
            opticalDepth *= 3.333f;
        else if (pressureRatio >= 10.0f)
            opticalDepth *= 2.0f;
        else if (pressureRatio >= 5.0f)
            opticalDepth *= 1.5f;

        return opticalDepth;
    }

    //Planetary albedo from surface composition and cloud cover (Fogg eq. 25)
    public static float CalculatePlanetAlbedo(float _waterFraction, float _cloudFraction, float _iceFraction, float _surfacePressureMb)
    {
        float rockFraction = 1.0f - _waterFraction - _iceFraction;
        float components = 0.0f;

        if (_waterFraction > 0.0f) components += 1.0f;
        if (_iceFraction > 0.0f) components += 1.0f;
        if (rockFraction > 0.0f) components += 1.0f;

        float cloudAdjustment = components > 0.0f ? _cloudFraction / components : 0.0f;

        float rock = Mathf.Max(rockFraction - cloudAdjustment, 0.0f);
        float water = Mathf.Max(_waterFraction - cloudAdjustment, 0.0f);
        float ice = Mathf.Max(_iceFraction - cloudAdjustment, 0.0f);

        float cloudContribution = _cloudFraction * SFUtilities.About(SFConstants.CLOUD_ALBEDO, 0.2f);

        if (_surfacePressureMb <= 0.0f)
        {
            //Airless: bare rock and exposed ice only
            return cloudContribution
                 + rock * SFUtilities.About(SFConstants.ROCKY_AIRLESS_ALBEDO, 0.3f)
                 + ice * SFUtilities.About(SFConstants.AIRLESS_ICE_ALBEDO, 0.4f)
                 + water * SFUtilities.About(SFConstants.WATER_ALBEDO, 0.2f);
        }

        return cloudContribution
             + rock * SFUtilities.About(SFConstants.ROCKY_ALBEDO, 0.1f)
             + ice * SFUtilities.About(SFConstants.ICE_ALBEDO, 0.1f)
             + water * SFUtilities.About(SFConstants.WATER_ALBEDO, 0.2f);
    }

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

    //Iterated climate solution (Fogg's "iterate_surface_temp"): temperature, albedo,
    //hydrosphere, clouds and ice all depend on each other, so relax them together until
    //the surface temperature settles
    public static void IterateSurfaceTemperature(SFPlanetData _planet, float _ecosphereRadius)
    {
        float effectiveTemp = CalculateEffectiveTemp(_ecosphereRadius, _planet.Axis, SFConstants.EARTH_ALBEDO);
        float opticalDepth = CalculateOpacity(_planet.MolecularWeightRetained, _planet.SurfacePressure);
        float greenhouseRise = CalculateGreenhouseRise(opticalDepth, effectiveTemp, _planet.SurfacePressure);

        _planet.SurfaceTemp = effectiveTemp + greenhouseRise;

        SetPlanetTemperatureBands(_planet);

        //Relax: each pass recomputes surface fractions from the current temperature,
        //derives a new albedo, and feeds that back into the temperature
        float previousTemp = _planet.SurfaceTemp - 5.0f;
        int iterations = 0;

        while (Mathf.Abs(_planet.SurfaceTemp - previousTemp) > 0.25f && iterations++ < 25)
        {
            previousTemp = _planet.SurfaceTemp;

            _planet.HydrosphereCoverage = CalculateHydrosphereFraction(_planet.VolatileGasInventory, _planet.EquitorialRadius);
            _planet.CloudCoverage = CalculateCloudFraction(_planet.SurfaceTemp, _planet.MolecularWeightRetained,
                                                           _planet.EquitorialRadius, _planet.HydrosphereCoverage);
            _planet.IceCoverage = CalculateIceFraction(_planet.HydrosphereCoverage, _planet.SurfaceTemp);

            //A runaway-hot world boils its oceans away entirely
            if (_planet.SurfaceTemp >= _planet.BoilingPoint && _planet.BoilingPoint > 0.0f)
                _planet.HydrosphereCoverage = 0.0f;

            _planet.Albedo = CalculatePlanetAlbedo(_planet.HydrosphereCoverage, _planet.CloudCoverage,
                                                   _planet.IceCoverage, _planet.SurfacePressure);

            effectiveTemp = CalculateEffectiveTemp(_ecosphereRadius, _planet.Axis, _planet.Albedo);
            opticalDepth = CalculateOpacity(_planet.MolecularWeightRetained, _planet.SurfacePressure);
            greenhouseRise = CalculateGreenhouseRise(opticalDepth, effectiveTemp, _planet.SurfacePressure);

            _planet.SurfaceTemp = effectiveTemp + greenhouseRise;
            _planet.GreenhouseTempRise = greenhouseRise;
        }

        SetPlanetTemperatureBands(_planet);
    }

    //Day/night and seasonal swings around the mean. Thin air and long days mean wild
    //ranges; thick atmospheres and oceans damp them (Fogg eq. 26-27)
    private static void SetPlanetTemperatureBands(SFPlanetData _planet)
    {
        float pressureMod = _planet.SurfacePressure <= 0.0f
            ? 1.0f
            : Mathf.Clamp(1.0f / (1.0f + 0.6f * (_planet.SurfacePressure / SFConstants.EARTH_SURF_PRES_IN_MILLIBARS)), 0.1f, 1.0f);

        //Water is a heat reservoir; clouds trap night-side warmth
        float waterMod = 1.0f - 0.5f * _planet.HydrosphereCoverage - 0.2f * _planet.CloudCoverage;
        waterMod = Mathf.Clamp(waterMod, 0.2f, 1.0f);

        //Long days bake and freeze; fast rotators stay even
        float dayMod = _planet.LengthOfDay <= 0.0f
            ? 1.0f
            : Mathf.Clamp(Mathf.Pow(_planet.LengthOfDay / 24.0f, 0.25f), 0.5f, 4.0f);

        float dailySwing = 40.0f * pressureMod * waterMod * dayMod;

        //Axial tilt drives the seasonal spread
        float seasonalSwing = 30.0f * pressureMod * waterMod * Mathf.Sin(Mathf.Abs(_planet.AxialTilt) * Mathf.Deg2Rad);

        _planet.HighTemp = _planet.SurfaceTemp + dailySwing * 0.5f;
        _planet.LowTemp = _planet.SurfaceTemp - dailySwing * 0.5f;
        _planet.MaxTemp = _planet.HighTemp + seasonalSwing * 0.5f;
        _planet.MinTemp = _planet.LowTemp - seasonalSwing * 0.5f;
    }

    //Final classification from the settled environment — this is what selects the
    //planet archetype (terrain preset, ramp, biomes, shells) at spawn time
    public static SF_PLANET_TYPE ClassifyPlanet(SFPlanetData _planet)
    {
        if (_planet.GasGiant)
        {
            float earthMasses = _planet.Mass * SFConstants.SUN_MASS_IN_EARTH_MASSES;

            if (earthMasses > 200.0f)
                return SF_PLANET_TYPE.JOVIAN;
            if (earthMasses > 50.0f)
                return SF_PLANET_TYPE.SUB_JOVIAN;

            return SF_PLANET_TYPE.GAS_DWARF;
        }

        //Tidally locked worlds bake on one face and freeze on the other
        if (_planet.ResonantPeriod || (_planet.LengthOfDay >= _planet.OrbitalPeriod * 24.0f && _planet.SurfacePressure < 1.0f))
            return SF_PLANET_TYPE.ONE_FACE;

        //No meaningful atmosphere: bare rock, or ice if it is cold and icy
        if (_planet.SurfacePressure < 1.0f)
        {
            if (_planet.IceCoverage > 0.5f)
                return SF_PLANET_TYPE.ICE;

            return _planet.Mass * SFConstants.SUN_MASS_IN_EARTH_MASSES < 0.1f
                ? SF_PLANET_TYPE.ASTEROIDS
                : SF_PLANET_TYPE.ROCK;
        }

        //Runaway greenhouse
        if (_planet.SurfaceTemp > _planet.BoilingPoint && _planet.BoilingPoint > 0.0f)
            return SF_PLANET_TYPE.VENUSIAN;

        //Ocean world
        if (_planet.HydrosphereCoverage > 0.95f)
            return SF_PLANET_TYPE.WATER;

        //Frozen world
        if (_planet.IceCoverage > 0.5f || _planet.SurfaceTemp < SFConstants.FREEZING_POINT_OF_WATER - 30.0f)
            return SF_PLANET_TYPE.ICE;

        //Earthlike: liquid water, breathable-scale pressure, temperate
        if (_planet.HydrosphereCoverage > 0.05f && _planet.SurfacePressure > 250.0f
            && _planet.SurfaceTemp > 255.0f && _planet.SurfaceTemp < 320.0f)
            return SF_PLANET_TYPE.TERRESTRIAL;

        //Thin cold atmosphere over rock — the Mars case
        if (_planet.SurfacePressure < 250.0f)
            return SF_PLANET_TYPE.MARTIAN;

        return SF_PLANET_TYPE.ROCK;
    }
}
