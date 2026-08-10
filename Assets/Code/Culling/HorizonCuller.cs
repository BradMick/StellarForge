using UnityEngine;

//Reusable horizon (self-occlusion) tester for spherical bodies. A point on or near a sphere
//is hidden from a viewer once it passes beyond the sphere's horizon — the circle where sight
//lines graze the surface. The visible cap shrinks as the viewer approaches the surface.
//Attach anywhere; point sphereCenter at the body (falls back to this object's transform)
public class HorizonCuller : MonoBehaviour
{
    public Transform sphereCenter;
    public float sphereRadius = 1.0f;

    //Fraction the occluding sphere is shrunk by for the test — small slack that keeps points
    //right on the horizon rim visible. Queried points are tested at their TRUE position
    //(terrain samples include elevation), so mountains peeking over the horizon are already
    //handled exactly — keep this near zero; large values keep a wasteful band of geometry
    //beyond the limb alive
    [Range(0.0f, 0.5f)]
    public float horizonMargin = 0.01f;

    public bool IsVisible(Vector3 _viewerPosition, Vector3 _worldPoint)
    {
        Vector3 center = sphereCenter != null ? sphereCenter.position : transform.position;

        Vector3 toViewer = _viewerPosition - center;
        float viewerDistance = toViewer.magnitude;

        float occluderRadius = sphereRadius * (1.0f - horizonMargin);

        //Viewer inside the (shrunk) sphere — no meaningful horizon, cull nothing
        if (viewerDistance <= occluderRadius)
            return true;

        //Visible while the angle between the point's and the viewer's radial directions stays
        //inside the horizon cap: cos(angle) >= occluderRadius / viewerDistance.
        //Far away that approaches the full near hemisphere; at the surface it collapses to a point
        Vector3 pointDirection = (_worldPoint - center).normalized;
        return Vector3.Dot(pointDirection, toViewer / viewerDistance) >= occluderRadius / viewerDistance;
    }
}
