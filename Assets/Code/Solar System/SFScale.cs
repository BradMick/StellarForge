//How the universe's real quantities become world units.
//
//Scale is UNIVERSAL. An AU is the same number of world units in every system, or nothing
//lines up: a ship's speed, a jump drive's range, the size a planet reads at from orbit, and
//the distance the anchor layer has to rebase across are all measured against it. Two systems
//disagreeing about how big an AU is would be a bug with no local symptom.
//
//So this is a static constant, not an asset and not a component field. There is nothing to
//create, nothing to assign, and no null reference that could silently fall back to different
//numbers. Changing scale is a deliberate edit to one file that moves the whole game at once.
//
//Sizing bodies together is the genuinely hard part: a star's radius is ~700,000 km, a
//terrestrial planet's ~6,000 km, and an orbit ~150,000,000 km. No single multiplier makes
//all three readable at once, which is why distance, star radius and planet radius each carry
//their own exaggeration factor rather than deriving from one number
public static class SFScale
{
    //--- Distance ---

    //World units per AU. True scale (1 unit = 1 m) would be 1.496e11; gameplay scale
    //compresses hard so a system is traversable in reasonable time
    public const double WORLD_UNITS_PER_AU = 10000.0;

    //--- Stars ---

    //The true figure — one solar radius in kilometres
    public const double WORLD_UNITS_PER_SOLAR_RADIUS = 696340.0;

    //Stars are tiny beside their own orbits, so they are exaggerated to stay visible at
    //system scale. At 0.05 a Sol-radius star renders ~35,000 units across against a
    //10,000-unit AU — a readable disc from a neighbouring orbit
    public const float STAR_RADIUS_SCALE = 0.05f;

    //--- Planets ---

    //Planet radii arrive from the generator in kilometres
    public const double WORLD_UNITS_PER_PLANET_KILOMETRE = 1.0;

    //Planets are exaggerated far less than stars: at 0.1 an Earth-radius world is ~640
    //units, which still reads as a body rather than a dot without swamping its own orbit
    public const float PLANET_RADIUS_SCALE = 0.1f;

    //Peak terrain height as a fraction of planet radius. Real worlds are far smoother than
    //this; a little exaggeration is what makes mountains read from orbit
    public const float TERRAIN_HEIGHT_SCALE = 0.08f;

    //--- Time ---

    //Simulated days per real second — how fast orbits advance by default
    public const double DAYS_PER_SECOND = 0.5;

    //--- Conversions ---

    public static float StarRadiusToWorld(float _solarRadii)
    {
        return (float)(_solarRadii * WORLD_UNITS_PER_SOLAR_RADIUS * STAR_RADIUS_SCALE);
    }

    public static float PlanetRadiusToWorld(float _radiusKm)
    {
        return (float)(_radiusKm * WORLD_UNITS_PER_PLANET_KILOMETRE * PLANET_RADIUS_SCALE);
    }

    public static UnityEngine.Vector3 PositionToWorld(Vector3d _positionAU)
    {
        return _positionAU.ToVector3(WORLD_UNITS_PER_AU);
    }
}
