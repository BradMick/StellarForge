using UnityEngine;

//Height-to-color ramp for planetary terrain, as a reusable project asset:
//create via Assets ▸ Create ▸ StellarForge ▸ Terrain Color Ramp and share between planets.
//t = 0 is the deepest seabed, t = 0.5 is ALWAYS the waterline (sea level tracks the
//terrain's oceanLevel automatically), t = 1 the highest peak
[CreateAssetMenu(fileName = "SFTerrainColorRamp", menuName = "StellarForge/Terrain Color Ramp")]
public class SFTerrainColorRamp : ScriptableObject
{
    public Gradient gradient = new Gradient();

    //Bumped on every inspector edit so live planets know to recolor
    [System.NonSerialized] private int version;
    public int Version { get { return version; } }

    private void OnValidate()
    {
        version++;
    }

    public Color Evaluate(float _t)
    {
        return gradient.Evaluate(Mathf.Clamp01(_t));
    }

    //Editor default when the asset is created: ocean → beach → lowlands → mountains → snow
    private void Reset()
    {
        gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.05f, 0.20f, 0.45f), 0.00f), //deep ocean
                new GradientColorKey(new Color(0.10f, 0.35f, 0.60f), 0.48f), //shallows
                new GradientColorKey(new Color(0.80f, 0.75f, 0.50f), 0.52f), //beach
                new GradientColorKey(new Color(0.20f, 0.45f, 0.15f), 0.65f), //lowlands
                new GradientColorKey(new Color(0.45f, 0.38f, 0.30f), 0.85f), //mountains
                new GradientColorKey(Color.white,                    1.00f), //peaks
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(1.0f, 1.0f),
            });
    }
}
