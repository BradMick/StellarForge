using UnityEngine;
using System.Collections;
using System.Collections.Generic;

                                                                   //Gas Giant                       //Sub Gas Giant    //Sub Sub Gas Giant
public enum SF_PLANET_TYPE { UNKNOWN, ROCK, VENUSIAN, TERRESTRIAL, JOVIAN,      MARTIAN, WATER, ICE, SUB_JOVIAN,        GAS_DWARF,          ASTEROIDS, ONE_FACE }

public class SFGas
{
    SFGas()
    {
        i = 0;
        s_psi = 0.0f;
    }

    public int GasIndex             { get { return i; }     set { i = value; } }
    public float SurfacePressure    { get { return s_psi; } set { s_psi = value; } }

    private int i;
    private float s_psi;
}

public class SFPlanet
{
    public SFPlanet()
    {

    }

    public List<SFGas> Atmosphere           { get { return gasList; }          set { gasList = value; } }

    public SF_PLANET_TYPE PlanetType        { get { return planetType; }       set { planetType = value; } }

    public bool GasGiant                    { get { return gasGiant; }         set { gasGiant = value; } }
    public bool ResonantPeriod              { get { return resonantPeriod; }   set { resonantPeriod = value; } }
    public bool GreenhouseEffect            { get { return greenHouseEffect; } set { greenHouseEffect = value; } }

    public int OrbitalZone                  { get { return orbitalZone; } set { orbitalZone = value; } }
    public int PlanetIndex                  { get { return i; }     set { i = value; } }
    public int GasCount                     { get { return i_g; }   set { i_g = value; } }

    public float Axis                       { get { return a; }     set { a = value; } }
    public float Mass                       { get { return m; }     set { m = value; } }
    public float Eccen                      { get { return e; }     set { e = value; } }
    public float GasMass                    { get { return m_g; }   set { m_g = value; } }
    public float DustMass                   { get { return m_d; }   set { m_d = value; } }
    public float AxialTilt                  { get { return at; }    set { at = value; } }
    public float CoreRadius                 { get { return r_c; }   set { r_c = value; } }
    public float LengthOfDay                { get { return p_d; }   set { p_d = value; } }
    public float OrbitalPeriod              { get { return p_o; }   set { p_o = value; } }
    public float EquitorialRadius           { get { return r; }     set { r = value; } }
    public float EscapeVelocity             { get { return v_e; }   set { v_e = value; } }
    public float RMSVelocity                { get { return v_rms; } set { v_rms = value; } }
    public float SurfaceAcceleration        { get { return a_s; }   set { a_s = value; } }
    public float SurfaceGravity             { get { return a_g; }   set { a_g = value; } }
    public float MolecularWeightRetained    { get { return w_mol; } set { w_mol = value; } }
    public float VolatileGasInventory       { get { return vgi; }   set { vgi = value; } }
    public float SurfacePressure            { get { return s_psi; } set { s_psi = value; } }
    public float BoilingPoint               { get { return bp; }    set { bp = value; } }
    public float Albedo                     { get { return alb; }   set { alb = value; } }
    public float ExosphericTemp             { get { return t_exo; } set { t_exo = value; } }
    public float EstimatedTemp              { get { return t_est; } set { t_est = value; } }
    public float EstimatedTempError         { get { return t_err; } set { t_err = value; } }
    public float SurfaceTemp                { get { return t_srf; } set { t_srf = value; } }
    public float GreenhouseTempRise         { get { return t_grn; } set { t_grn = value; } }
    public float HighTemp                   { get { return t_hi; }  set { t_hi = value; } }
    public float LowTemp                    { get { return t_lo; }  set { t_lo = value; } }
    public float MaxTemp                    { get { return t_max; } set { t_max = value; } }
    public float MinTemp                    { get { return t_min; } set { t_min = value; } }
    public float HydrosphereCoverage        { get { return hyd; }   set { hyd = value; } }
    public float CloudCoverage              { get { return cld; }   set { cld = value; } }
    public float IceCoverage                { get { return ice; }   set { ice = value; } }
    public float Density                    { get { return d; }     set { d = value; } }

    private List<SFGas> gasList = new List<SFGas>();

    private SF_PLANET_TYPE planetType;

    private bool gasGiant,
                 resonantPeriod,        //True if in resonant rotation...
                 greenHouseEffect;      //Runaway greenhouse effect?  

    private int i,                      //Planet Index #
                i_g,                    //Count of gases in the atmosphere
                orbitalZone;

    private float a,                    //Semi-major axis in AU
                  e,                    //Eccentricity
                  m, m_g, m_d,          //Mass / Mass of gas, Mass of dust
                  at,                   //Axial tilt
                  r, r_c,               //Equitorial radius, Radius of the planets core
                  d,                    //Density in cm^3
                  p_o, p_d,             //Oribital period in years, Day period in hours
                  v_e, v_rms,           //Escape velocity in cm/s, rms velocity in cm/s
                  a_s, a_g,             //Surface acceleration in cm/s^2, Gravitational acceleration in units of Earth gravitities
                  w_mol,                //Smallest molecular weight retained
                  vgi,                          //Volatile gas inventory
                  s_psi,                        //Surface pressure in millibars (mb)
                  bp,                           //Boiling point of water in kelvin
                  alb,                          //Albedo of hte planet
                  t_exo, t_est, t_err,          //Exospheric temp in kelvin, Estimated temp in kelvin, 
                  t_srf, t_grn,                  //Surface temperature in kelvin, Temperature rise due to greenhouse
                  t_hi, t_lo, t_max, t_min,     //Day time temp, Night time temp,  Summer/Day, Winter/Night
                  hyd, cld, ice;                //Water coverage % (hydrosphere), Cloud coverage %, Ice coverage %
          
}
