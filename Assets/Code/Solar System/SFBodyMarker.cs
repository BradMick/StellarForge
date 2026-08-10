using UnityEngine;

//Makes a generated body clickable and framable in the Scene view.
//Planet surfaces are transient hidden tiles, so a spawned planet has no renderer of its
//own — without a marker there is nothing to pick, and Frame Selected has no bounds to
//zoom to. The wire sphere is editor-only and never appears in the game
[ExecuteAlways]
public class SFBodyMarker : MonoBehaviour
{
    public float radius = 1.0f;
    public Color markerColor = Color.white;
    public string label;

    //Draw the outline even when the object is not selected, so bodies can be found
    public bool alwaysShow = true;

    private void OnDrawGizmos()
    {
        if (!alwaysShow)
            return;

        //A filled wire sphere is what Unity's picking uses, and what Frame Selected
        //zooms to — so this both marks the body and makes it navigable
        Gizmos.color = new Color(markerColor.r, markerColor.g, markerColor.b, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = markerColor;
        Gizmos.DrawWireSphere(transform.position, radius);

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(label))
        {
            UnityEditor.Handles.color = markerColor;
            UnityEditor.Handles.Label(transform.position + Vector3.up * radius * 1.4f, label);
        }
#endif
    }
}
