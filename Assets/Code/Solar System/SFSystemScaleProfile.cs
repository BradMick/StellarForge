using UnityEngine;

//How a generated system's real quantities become world units.
//The generator works in AU, solar masses and kilometres; the game needs metres. A profile
//owns that translation so the same system can be built at true scale or compressed for
//gameplay without touching the physics.
//Create via Assets ▸ Create ▸ StellarForge ▸ System Scale Profile
[CreateAssetMenu(fileName = "SystemScaleProfile", menuName = "StellarForge/System Scale Profile")]
public class SFSystemScaleProfile : ScriptableObject
{
    [Header("Distance")]
    //World units per AU. 1 unit = 1 metre, so true scale is 1.496e11.
    //Gameplay scale compresses this hard so transfers take minutes rather than weeks
    public double worldUnitsPerAU = 1.496e8;    //1:1000 of reality

    [Header("Bodies")]
    //Planet radii come from the generator in kilometres
    public double worldUnitsPerPlanetKilometre = 1.0;
    //...compressed by this, so a 6371 km Earth becomes a walkable ~6.4 km world
    [Range(0.0001f, 1.0f)]
    public float planetRadiusScale = 0.001f;

    //Stars are tiny beside their orbits; exaggerate them so they read from a distance
    public double worldUnitsPerSolarRadius = 696340.0;   //true kilometres per solar radius
    [Range(0.0001f, 100.0f)]
    public float starRadiusScale = 0.01f;

    [Header("Time")]
    //Simulated days per real second. 1 = real time; larger values speed the system up
    public double daysPerSecond = 0.02;
    //A planet's rotation is compressed separately so a 24 h day can run in ~30 min
    [Range(1.0f, 200.0f)]
    public float dayLengthCompression = 48.0f;

    [Header("Terrain")]
    //Peak terrain height as a fraction of planet radius. Real relief is ~0.001; gameplay
    //worlds want dramatic mountains on a small sphere
    [Range(0.001f, 0.3f)]
    public float terrainHeightScale = 0.08f;

    //--- Conversions ---

    public double AUToWorld(double _au)
    {
        return _au * worldUnitsPerAU;
    }

    public Vector3 PositionToWorld(Vector3d _positionAU)
    {
        return _positionAU.ToVector3(worldUnitsPerAU);
    }

    //Planet radius in world units, from the generator's kilometres
    public float PlanetRadiusToWorld(float _radiusKm)
    {
        return (float)(_radiusKm * worldUnitsPerPlanetKilometre * planetRadiusScale);
    }

    //Star radius in world units, from solar radii
    public float StarRadiusToWorld(float _solarRadii)
    {
        return (float)(_solarRadii * worldUnitsPerSolarRadius * starRadiusScale);
    }

    //Rotation period in seconds of real time, from the generator's hours
    public float DayLengthToSeconds(float _hours)
    {
        return _hours * 3600.0f / Mathf.Max(dayLengthCompression, 0.01f);
    }

    //A ready-made gameplay profile: ~1:1000 planets, compressed orbits, brisk days
    public void ResetToGameplayScale()
    {
        worldUnitsPerAU = 1.496e8;
        planetRadiusScale = 0.001f;
        starRadiusScale = 0.01f;
        daysPerSecond = 0.02;
        dayLengthCompression = 48.0f;
        terrainHeightScale = 0.08f;
    }

    //True scale: every distance and body at its real size in metres. Requires the
    //double-precision anchor layer to be playable, but the data is honest
    public void ResetToRealScale()
    {
        worldUnitsPerAU = 1.496e11;
        planetRadiusScale = 1000.0f;        //km -> metres
        starRadiusScale = 1000.0f;
        daysPerSecond = 1.0 / 86400.0;      //real time
        dayLengthCompression = 1.0f;
        terrainHeightScale = 0.001f;
    }
}
