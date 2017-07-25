using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class StellarForge : MonoBehaviour
{
    //SystemSeed  31546;        //T1, T2, T3, G4, G5, G6, T7
    //SystemSeed  2167;         //T1, T2, T3, G4, G5, G6, T7
    //SystemSeed  90132;        //T1, T2, T3, G4, G5, G6, T7, T8
    //SystemSeed  57911;        //T1, T2, T3, T4, G5, G6, G7, G8, T9
    //SystemSeed  491578;       //T1, T2, T3, G4, G5, G6, T7, G8
    //SystemSeed  683475;       //T1, T2, G3, G4, G5, G6, G7, T8
    //SystemSeed  1234567890;   //T1, G2, G3, G4, G5, G6, T7
    //SystemSeed  276811547     //T1, T2, T3, T4, G5, G6, G7, G8, T9
    public int    SystemSeed    = 90132;
    public float  Luminosity    = 1.0f;      //Sol = 1.0f
    public float  Mass1         = 1.0f;      //Sol = 1.0f;
    public float  Mass2         = 0.0f;      //Sol is a non-binary system...so this is 0...
    public float  Eccentricity  = 0.0f;      //Again, Sol is non-binary...so this is also 0...
    public float  Axis          = 0.0f;                   //You guessed it...Sol is non-binary...0!
    public string Designation   = "Sol";                  //Catalogue designation of the Star...
    public string Name          = "The Solar System";     //Local name of the Star...


    private SFSun     Sun              = new SFSun();
    private SFAccrete Accrete          = new SFAccrete();
    private Transform SunTransform;
        
    private List<SFNuclei> NUCLEI_LIST = new List<SFNuclei>();
    private List<SFPlanet> PLANET_LIST = new List<SFPlanet>();

    void Start()
    {
        TheForge();

        Debug.Log("--------------------------------------------------");
        //for (int i = 0; i < DUST_LIST.Count; i++)
        //    Debug.Log("Dust band " + i + " has dust? " + DUST_LIST[i].DustPresent + " / has gas? " + DUST_LIST[i].GasPresent + " / Has Planet? " + DUST_LIST[i].HasPlanet + " / inner edge: " + DUST_LIST[i].InnerEdge.ToString("0.00") + " / outer edge: " + DUST_LIST[i].OuterEdge.ToString("0.00"));

        Debug.Log("Seed " + SystemSeed + " run Complete, " + Accrete.NUCLEI_USED + " nuclei injected into the cloud resulting in: " + NUCLEI_LIST.Count + " planets...");
        for (int i = 0; i < PLANET_LIST.Count; i++)
        { 
            //Debug.Log("Nuclei " + i + " x_p: " + NUCLEI_LIST[i].X_p.ToString("0.00") + " / axis: " + NUCLEI_LIST[i].Axis.ToString("0.00") + " / x_a: " + NUCLEI_LIST[i].X_a.ToString("0.00"));
            //Debug.Log("Nuclei " + i + " s_p: " + NUCLEI_LIST[i].S_p.ToString("0.00") + " / axis: " + NUCLEI_LIST[i].Axis.ToString("0.00") + " / s_a: " + NUCLEI_LIST[i].S_a.ToString("0.00"));
            GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.transform.parent = SunTransform;
            if (PLANET_LIST[i].GasGiant == false)
                planet.name = "Terrestrial Planet # " + (i + 1);
            else
                planet.name = "Gas Giant # " + (i + 1);
            planet.transform.position = new Vector3(SunTransform.position.x, SunTransform.position.y, SunTransform.position.z + PLANET_LIST[i].Axis);
            //planet.transform.localScale = new Vector3(PLANET_LIST[i].EquitorialRadius, PLANET_LIST[i].EquitorialRadius, PLANET_LIST[i].EquitorialRadius);
            //Debug.Log("Planet " + (i + 1) + " radius: " + PLANET_LIST[i].EquitorialRadius + " km.");
        }
    }

    void InitializeTheSun()
    {
        Sun.Mass = Mass1;

        if (Sun.Mass < 0.2f || Sun.Mass > 1.5f)
            Sun.Mass = Random.Range(0.7f, 1.4f);

        Sun.OuterDustLimit = SFUtilities.StellarDustLimit(Sun.Mass);

        Sun.Luminosity = Luminosity;

        if (Sun.Luminosity == 0.0f)
            Sun.Luminosity = SFEnvironment.CalculateLuminosity(Sun.Mass);

        Sun.EcosphereRadius = Mathf.Sqrt(Sun.Luminosity);
        Sun.Life            = 1.0E10f * (Sun.Mass / Sun.Luminosity);

        float sunMaxAge = SFConstants.SUN_MAX_AGE;

        if (Sun.Life < SFConstants.SUN_MAX_AGE)
            sunMaxAge = Sun.Life;

        Sun.Age = Random.Range(SFConstants.SUN_MIN_AGE, sunMaxAge);

        if (Mass2 > 0.001f)
            Sun.OuterPlanetBoundary = SFUtilities.CalculateOuterPlanetLimitBinarySystem(Sun.Mass, Mass2, Eccentricity, Axis);
        else
            Sun.OuterPlanetBoundary = 0.0f;
    }

    void GeneratePlanet(int _i, SFNuclei _nuclei)
    {
        SFPlanet temp = new SFPlanet();

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
        temp.OrbitalZone            = SFEnvironment.CalculateOrbitalZone(Sun.Luminosity, _nuclei.Axis);
        temp.OrbitalPeriod          = SFEnvironment.CalculateOrbitalPeriod(_nuclei.Axis, _nuclei.Mass, Sun.Mass);
        temp.AxialTilt              = SFEnvironment.CalculateInclination(_nuclei.Axis);
        temp.ExosphericTemp         = SFConstants.EARTH_EXOSPHERE_TEMP / Mathf.Pow(_nuclei.Axis / Sun.EcosphereRadius, 2.0f);
        temp.RMSVelocity            = SFEnvironment.CalculateRMSVelocity(SFConstants.MOL_NITROGEN, temp.ExosphericTemp);
        temp.CoreRadius             = SFEnvironment.CalculateKothariCoreRadius(_nuclei.DustMass, false, temp.OrbitalZone);

        temp.Density                = SFEnvironment.CalculateEmpiricalDensity(_nuclei.Mass, _nuclei.Axis, Sun.EcosphereRadius, true);
        temp.EquitorialRadius       = SFEnvironment.CalculateVolumeRadius(_nuclei.Mass, temp.Density);
        Debug.Log("Planet " + _i + " Radius: " + temp.EquitorialRadius);

        temp.SurfaceAcceleration    = SFEnvironment.CalculateSurfaceAcceleration(_nuclei.Mass, temp.EquitorialRadius);
        temp.SurfaceGravity         = SFEnvironment.CalculateSurfaceGravity(temp.SurfaceAcceleration);

        //temp.MolecularWeightRetained = SFEnvironment.CalculateMinMolecularWeight(temp); //Write me!!

        //if ((temp.Mass * SFConstants.SUN_MASS_IN_EARTH_MASSES) > 1.0f && (temp.GasMass / temp.Mass) > 0.05f && (SFEnvironment.CalculateMinMolecularWeight(temp) <= 4.0f))

        //Temp!!
        temp.Axis               = _nuclei.Axis;

        PLANET_LIST.Add(temp);
        NUCLEI_LIST.RemoveAt(_i);
    }

    void TheForge()
    {
        Random.seed = SystemSeed;

        SunTransform = this.transform;

        InitializeTheSun();

        Accrete.SetInitialConditions(0.0f, Sun.OuterDustLimit);
        NUCLEI_LIST = Accrete.DistributePlanetaryMasses(Sun.Mass, Sun.Luminosity, Sun.OuterPlanetBoundary);

        for (int i = 0; i < NUCLEI_LIST.Count; i++)
        {
            GeneratePlanet(i, NUCLEI_LIST[i]);
        }
    }
}