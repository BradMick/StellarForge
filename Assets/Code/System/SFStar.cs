using UnityEngine;
using System.Collections;

public class SFStar : MonoBehaviour
{
    public SFStar(float luminosity, float mass1, float mass2, float eccentricity, float axis, string designation, string name)
    {
        l = luminosity;
        m1 = mass1;
        m2 = mass2;
        e = eccentricity;
        a = axis;
        d = designation;
        n = name;
    }

    public float    Luminosity;
    public float    Mass1;
    public float    Mass2;
    public float    Eccentricity;
    public float    Axis;  
    public string   Designation;
    public string   Name;

    private float   l,  //luminosity of primary star
                    m1, //mass of primary star
                    m2, //mass of secondary star
                    e,  //eccentricity
                    a;  //semi-major axis
    private string  d,  //designation
                    n;  //name
}
