using UnityEngine;

//Reusable camera-frustum visibility tester. Attach to any GameObject, point it at a camera
//(falls back to Camera.main), and query IsVisible with world-space bounds, a renderer, or a point.
//Frustum planes are recalculated at most once per frame no matter how many callers query
public class FrustumCuller : MonoBehaviour
{
    //Camera to cull against; Camera.main when left empty
    public Camera targetCamera;

    //World units grown around queried bounds — a safety margin against pop-in at the frustum edge
    public float boundsPadding = 0.0f;

    private Plane[] frustumPlanes = new Plane[6];
    private int lastPlaneFrame = -1;

    private bool TryUpdatePlanes()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
            return false;

        if (lastPlaneFrame != Time.frameCount)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, frustumPlanes);
            lastPlaneFrame = Time.frameCount;
        }

        return true;
    }

    public bool IsVisible(Bounds _worldBounds)
    {
        return IsVisible(_worldBounds, 0.0f);
    }

    //Callers can widen the test per query (e.g. LOD pre-refinement beyond the view edges)
    public bool IsVisible(Bounds _worldBounds, float _extraPadding)
    {
        //No camera to test against — treat everything as visible rather than hiding the world
        if (!TryUpdatePlanes())
            return true;

        float padding = boundsPadding + _extraPadding;
        if (padding > 0.0f)
            _worldBounds.Expand(padding);

        return GeometryUtility.TestPlanesAABB(frustumPlanes, _worldBounds);
    }

    public bool IsVisible(Renderer _renderer)
    {
        return _renderer != null && IsVisible(_renderer.bounds);
    }

    //Convex sample-point test: invisible when every point lies behind a single frustum
    //plane by more than _inflation. Far tighter than an AABB for large curved surface
    //patches, whose bounding boxes can dwarf the geometry they contain
    public bool IsVisible(Vector3[] _worldPoints, int _count, float _inflation)
    {
        if (!TryUpdatePlanes())
            return true;

        float threshold = -(_inflation + boundsPadding);

        for (int p = 0; p < 6; p++)
        {
            bool allBehind = true;

            for (int i = 0; i < _count; i++)
            {
                if (frustumPlanes[p].GetDistanceToPoint(_worldPoints[i]) > threshold)
                {
                    allBehind = false;
                    break;
                }
            }

            if (allBehind)
                return false;
        }

        return true;
    }

    public bool IsVisible(Vector3 _worldPoint)
    {
        return IsVisible(new Bounds(_worldPoint, Vector3.zero));
    }
}
