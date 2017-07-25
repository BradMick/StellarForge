using UnityEngine;
using System;
using System.Collections;

public class SFNuclei : IComparable<SFNuclei>
{
    public SFNuclei()
    {
        a   = 0.0f;
        m   = 0.0f;
        e   = 0.0f;
        mu  = 0.0f;
        m_c = 0.0f;
        m_g = 0.0f;
        m_d = 0.0f;
        s_p = 0.0f;
        s_a = 0.0f;
        r_p = 0.0f;
        r_a = 0.0f;
        x_p = 0.0f;
        x_a = 0.0f;
        d_d = 0.0f;
        
    }

    public int CompareTo(SFNuclei _planet)
    {
        if (_planet == null)
            return 1;
        else
            return this.a.CompareTo(_planet.a);
    }

    public bool GasGiant        { get { return gasGiant; }  set { gasGiant = value; } }

    public float Axis           { get { return a; }     set { a = value; } }
    public float Mass           { get { return m; }     set { m = value; } }
    public float Eccen          { get { return e; }     set { e = value; } }
    public float GasMass        { get { return m_g; }   set { m_g = value; } }
    public float DustMass       { get { return m_d; }   set { m_d = value; } }
    public float CritMass       { get { return m_c; }   set { m_c = value; } }
    public float MassUnit       { get { return mu; }    set { mu = value; } }
    public float DustDensity    { get { return d_d; }   set { d_d = value; } }

    public float S_p            { get { return s_p; }   set { s_p = value; } }
    public float S_a            { get { return s_a; }   set { s_a = value; } }
    public float R_p            { get { return s_p; }   set { s_p = value; } }
    public float R_a            { get { return r_a; }   set { r_a = value; } }
    public float X_p            { get { return x_p; }   set { x_p = value; } }
    public float X_a            { get { return x_a; }   set { x_a = value; } }

    private bool  gasGiant;

    private float a,                //Axis
                  m,                //Mass
                  e,                //Eccentricity
                  mu,               //Mass unit
                  m_g, m_d, m_c,    //Mass of gas / Mass of Dust / Critical Mass
                  s_p, s_a,         //Swept dust distance at perihelion / Swept dust distance at aphelion
                  r_p, r_a,         //Radius at perihelion / Radius at aphelion
                  x_p, x_a,         //Gravitational effect limit at perihelion / Gravitational effect limit at aphelion
                  d_d;              //Dust density
}
