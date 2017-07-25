using UnityEngine;
using System.Collections;

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

    private float l,        //Luminosity
                  m,        //Mass
                  a,        //Age
                  li,       //Life
                  r_e,      //Ecosphere radius
                  r_d,      //Outer dust limit
                  r_p;      //Planetary Boundary...for Binary systems
}
