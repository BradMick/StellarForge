using UnityEngine;

//A star's complete physical description. Mass is the only free parameter of a
//main-sequence star — luminosity, radius, temperature, colour and lifetime all follow
//from it. Used for the primary AND any companion, so both are described identically
[System.Serializable]
public class SFSun
{
    public SFSun()
    {
        l   = 0.0f;
        m   = 0.0f;
        a   = 0.0f;
        li  = 0.0f;
        r_e = 0.0f;
    }

    public float Age                    { get { return a; }   set { a = value; } }
    public float Life                   { get { return li; }  set { li = value; } }
    public float Mass                   { get { return m; }   set { m = value; } }
    public float Luminosity             { get { return l; }   set { l = value; } }
    public float OuterDustLimit         { get { return r_d; } set { r_d = value; } }
    public float EcosphereRadius        { get { return r_e; } set { r_e = value; } }
    public float OuterPlanetBoundary    { get { return r_p; } set { r_p = value; } }
    //Circumbinary systems: nothing survives inside this radius from the barycenter
    public float InnerPlanetBoundary    { get { return r_i; } set { r_i = value; } }

    //Visual/physical properties — what a star body needs to render itself
    public float Radius                 { get { return r_s; } set { r_s = value; } }   //solar radii
    public float Temperature            { get { return t; }   set { t = value; } }     //kelvin
    public Color StarColor              { get { return c; }   set { c = value; } }
    public string SpectralClass         { get { return sc; }  set { sc = value; } }

    //[SerializeField] throughout so a generated star can be saved into an SFSystemAsset.
    //Unity serializes fields, not properties, and these are private — without the
    //attribute the asset would write a star full of zeros
    [SerializeField] private float l,        //Luminosity
                  m,        //Mass
                  a,        //Age
                  li,       //Life
                  r_e,      //Ecosphere radius
                  r_d,      //Outer dust limit
                  r_p,      //Planetary Boundary...for Binary systems
                  r_i,      //Inner planetary boundary...for circumbinary systems
                  r_s,      //Stellar radius in solar radii
                  t;        //Effective surface temperature
    [SerializeField] private Color c;        //Blackbody colour
    [SerializeField] private string sc;      //Spectral class (O B A F G K M)

    //Fill every derived property from mass. _luminosityOverride > 0 substitutes a
    //hand-authored luminosity (evolved stars, art direction) instead of the
    //main-sequence relation; everything else still follows from the pair
    public void DeriveFromMass(float _mass, float _luminosityOverride)
    {
        Mass = Mathf.Clamp(_mass, 0.08f, 50.0f);

        Luminosity = _luminosityOverride > 0.0f
            ? _luminosityOverride
            : SFEnvironment.CalculateLuminosity(Mass);

        Radius = SFEnvironment.CalculateStellarRadius(Mass);
        Temperature = SFEnvironment.CalculateStellarTemperature(Luminosity, Radius);
        StarColor = SFEnvironment.CalculateStellarColor(Temperature);
        SpectralClass = SFEnvironment.CalculateSpectralClass(Temperature);

        //Main-sequence lifetime: massive stars burn out fast
        Life = 1.0E10f * (Mass / Luminosity);

        OuterDustLimit = SFUtilities.StellarDustLimit(Mass);
        EcosphereRadius = Mathf.Sqrt(Luminosity);
    }
}
