using UnityEngine;

//How a generated system's real quantities become world units.
//The generator works in AU, solar masses and kilometres; the scene needs world units.
//A profile owns that translation so the same system can be built at true scale or
//compressed for gameplay without touching the physics.
//Create via Assets ▸ Create ▸ StellarForge ▸ System Scale Profile
[CreateAssetMenu(fileName = "SystemScaleProfile", menuName = "StellarForge/System Scale Profile")]
public class SFSystemScaleProfile : ScriptableObject
{
    [Header("Distance")]
    //World units per AU. Gameplay scale compresses this hard so a system is traversable;
    //true scale (1 unit = 1 m) would be 1.496e11
    public double worldUnitsPerAU = 100000.0;

    [Header("Bodies")]
    //Stars are tiny beside their orbits, so they get their own exaggerated scale to stay
    //visible. worldUnitsPerSolarRadius is the true kilometre figure; starRadiusScale
    //compresses or inflates it
    public double worldUnitsPerSolarRadius = 696340.0;
    [Range(0.0001f, 100.0f)]
    public float starRadiusScale = 0.05f;

    //Planet radii arrive from the generator in kilometres
    public double worldUnitsPerPlanetKilometre = 1.0;
    [Range(0.0001f, 1000.0f)]
    public float planetRadiusScale = 0.1f;

    [Header("Time")]
    //Simulated days per real second — how fast orbits advance
    public double daysPerSecond = 0.5;

    [Header("Terrain")]
    //Peak terrain height as a fraction of planet radius
    [Range(0.001f, 0.3f)]
    public float terrainHeightScale = 0.08f;

    //--- Conversions ---

    public float StarRadiusToWorld(float _solarRadii)
    {
        return (float)(_solarRadii * worldUnitsPerSolarRadius * starRadiusScale);
    }

    public float PlanetRadiusToWorld(float _radiusKm)
    {
        return (float)(_radiusKm * worldUnitsPerPlanetKilometre * planetRadiusScale);
    }

    public Vector3 PositionToWorld(Vector3d _positionAU)
    {
        return _positionAU.ToVector3(worldUnitsPerAU);
    }
}
