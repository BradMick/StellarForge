using UnityEngine;

//The set of archetypes a system draws from. One asset on the spawner covers every planet
//type the generator can produce, so adding a new world flavour is authoring an archetype
//rather than touching code.
//Create via Assets ▸ Create ▸ StellarForge ▸ Archetype Library
[CreateAssetMenu(fileName = "ArchetypeLibrary", menuName = "StellarForge/Archetype Library")]
public class SFArchetypeLibrary : ScriptableObject
{
    public SFPlanetArchetype[] archetypes;

    //Used when nothing in the library claims a planet's type — better a plain world than
    //a missing one
    public SFPlanetArchetype fallback;

    public SFPlanetArchetype Find(SF_PLANET_TYPE _type)
    {
        if (archetypes != null)
        {
            for (int i = 0; i < archetypes.Length; i++)
                if (archetypes[i] != null && archetypes[i].Matches(_type))
                    return archetypes[i];
        }

        return fallback;
    }
}
