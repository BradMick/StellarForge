using UnityEngine;
using System;
using System.Collections;

public class SFDust : IComparable<SFDust>
{
    public SFDust()
    {
        hasPlanet   = false;
        gasPresent  = true;
        dustPresent = true;
        innerEdge   = 0.0f;
        outerEdge   = 0.0f;
        index       = 0;
    }

    public int CompareTo(SFDust _dust)
    {
        if (_dust == null)
            return 1;
        else
            return this.innerEdge.CompareTo(_dust.innerEdge);
    }

    public bool  HasPlanet      { get { return hasPlanet; }     set { hasPlanet = value; } }
    public bool  GasPresent     { get { return gasPresent; }    set { gasPresent = value; } }
    public bool  DustPresent    { get { return dustPresent; }   set { dustPresent = value; } }
    
    public float InnerEdge      { get { return innerEdge; }     set { innerEdge = value; } }
    public float OuterEdge      { get { return outerEdge; }     set { outerEdge = value; } }

    public int   Index          { get { return index; }         set { index = value; } }

    private bool   hasPlanet,   
                   gasPresent,
                   dustPresent;

    private float  innerEdge,
                   outerEdge;

    private int    index;


}
