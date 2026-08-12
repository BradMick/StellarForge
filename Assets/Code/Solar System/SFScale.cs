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
//WORLDS ARE TAMRIEL-SIZED, NOT EARTH-SIZED. That single choice is what makes everything
//else work, and it is worth stating plainly because it is counter-intuitive.
//
//Walking scale (1 unit = 1 m) is fixed — a metre of ground is a metre. What is NOT fixed is
//how big a world is. A 10 km-radius planet has a 63 km circumference, which is Tamriel: days
//of walking, room for continents, cities and quests. Earth is 640x larger than that, and
//nothing in the design needs it.
//
//The reason it matters: a planet cannot shrink once walking scale is set, so an oversized
//planet ends up competing with its own orbit as orbits compress. With Earth-sized worlds
//that caps compression at ~1000x (Neptune 3.3 hours). With Tamriel-sized worlds a planet is
//a rounding error against its orbit, and orbits can compress 100x harder.
//
//MISSION BUDGET drives the rest. The loop is Privateer/Freelancer: a mission runs 15-30
//minutes end to end, so a one-way leg spends a few minutes, not an evening. Ships are full
//Newtonian at ~12.5 G with no speed cap, so a trip is a brachistochrone — accelerate to the
//midpoint, flip, decelerate — taking 2*sqrt(distance/acceleration). At 100,000x:
//
//    Earth to Moon       11 s        to the Belt (1.7 AU)    5 min
//    inner hop (0.3 AU)   2 min      to Jupiter  (4.2 AU)    8 min
//    Earth to Mars        3 min      to Neptune  (29 AU)    20 min
//
//Inner-system hops are minutes. The outer system is a 20-minute haul — far enough to feel
//like a journey, close enough to be a mission rather than an evening.
//
//WHERE THE CEILING IS. The planet is what breaks, not the star:
//
//    100,000x    a 10 km world is 0.67% of its orbit   fine
//  1,000,000x    a 10 km world is 6.7%  of its orbit   starting to crowd
//
//WHY THE STAR IS NOT THE LIMIT. A star CAN shrink with the orbits, because what a player
//perceives is its apparent size — a ratio of radius to distance — not its absolute radius.
//Shrinking it in step holds that ratio constant for free, which is why STAR_RADIUS_SCALE is
//an art-direction dial rather than a constraint. This is the fix for the bug that prompted
//all of this: a hand-set 0.05 star scale against a 10,000-unit AU gave the sun a 3.5 AU
//radius and put the entire inner system inside the photosphere
public static class SFScale
{
    //--- The three numbers everything else derives from ---

    //Walking scale. 1 unit = 1 m, so a kilometre of ground is 1000 units. This is the one
    //thing that never bends: it is what a player's stride, a building's footprint and a
    //city's extent are all measured in
    public const double WORLD_UNITS_PER_KILOMETRE = 1000.0;

    //How much closer together the orbits sit than reality. Set by the mission budget above,
    //capped by when planets start crowding their own orbits
    public const double ORBIT_COMPRESSION = 100000.0;

    //What an Earth-radius world actually renders at. 10 km radius = 63 km circumference =
    //Tamriel, which is the size the design asks for; Earth's real 6371 km is 640x more world
    //than anything needs and would cap orbit compression at a hundredth of this
    public const double EARTH_RADIUS_KM_IN_GAME = 10.0;

    //--- Distance ---

    //True scale would be 1.496e11 units per AU at 1 unit = 1 m
    public const double WORLD_UNITS_PER_AU = 149597871.0 * WORLD_UNITS_PER_KILOMETRE / ORBIT_COMPRESSION;

    //--- Planets ---

    //Generator radii are real kilometres (Earth = 6371). This maps them onto the game's own
    //size band, preserving RELATIVE size — a Jupiter still dwarfs a Mars — while putting the
    //whole range where walking works and orbits have room
    public const double WORLD_UNITS_PER_PLANET_KILOMETRE =
        EARTH_RADIUS_KM_IN_GAME * WORLD_UNITS_PER_KILOMETRE / 6371.0;

    //Global multiplier on every world, for tuning the whole set at once without touching the
    //Earth reference above
    public const float PLANET_RADIUS_SCALE = 1.0f;

    //--- Stars ---

    //One solar radius, shrunk with the orbits so the star's APPARENT size stays honest.
    //Without the compression term a true-radius star sits 100,000x too large against its own
    //compressed orbits — which is precisely the bug described at the top of this file
    public const double WORLD_UNITS_PER_SOLAR_RADIUS = 696340.0 * WORLD_UNITS_PER_KILOMETRE / ORBIT_COMPRESSION;

    //Pure art direction, and the one number here chosen by eye rather than derived. 1 = the
    //real 0.53 degrees from an Earth-like orbit; 2 = 1.07 degrees, plainly a disc rather than
    //a bright point while space still reads as mostly empty and dark. Raise it with the inner
    //orbits in view — at 8x the sun covers ~9.5% of Mercury's orbit
    public const float STAR_RADIUS_SCALE = 2.0f;

    //--- Terrain ---

    //Peak terrain height as a fraction of planet radius. Earth's real figure is about 0.14%
    //(Everest against 6371 km), but on a 10 km world that would be a 14 m bump — so the
    //exaggeration here is doing real work, not just flattering the silhouette. 2% of a 10 km
    //radius is a 200 m peak: climbable, visible from orbit, not a golf ball
    public const float TERRAIN_HEIGHT_SCALE = 0.02f;

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
