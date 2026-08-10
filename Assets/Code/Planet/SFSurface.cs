using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

//Plain class, not a MonoBehaviour — instances are created with new SFSurface(...),
//which Unity does not support for components
public class SFSurface
{
    public SFPlanet planet;
    public SFSurface surface;
    public int xIndex;
    public int zIndex;
    public CUBE_INDEX cubeIndex;
    public bool generated = false;

    //SFPlanet details...
    public float planetRadius;

    //Mesh details...
    public int meshResolution;

    //LOD Details...
    public int LOD;

    //Stitching — bitmask of edges (see EDGE_INDEX) currently collapsed to match a coarser neighbor
    public int currentStitchMask = -1;

    //Radial spread of this tile's actual vertices (max - min distance from center) —
    //per-tile slack for conservative visibility tests
    public float radialVariation;


    public delegate void SurfaceDelegate(SFSurface _surface);
    public event SurfaceDelegate GenerationComplete, SurfaceDestroyed;

    public Vector3[] vertexArray;
    public Vector3[] normalArray;
    public Vector2[] uvArray;

    //Geomorphing: whether this tile carries parent-shape morph targets in UV2/UV3
    private bool hasMorphData;

    public GameObject surfaceObject;

    public Mesh surfaceMesh;
    public MeshCollider surfaceMeshCollider;
    public MeshRenderer surfaceMeshRenderer;
    public MeshFilter surfaceMeshFilter;

    #region SFSurface Generation

    public SFSurface(SFPlanet _planet, SFSurface _surface, CUBE_INDEX _cubeIndex, int _xIndex, int _zIndex, int _LOD, int _meshResolution, float _planetRadius)
    {
        planet          = _planet;
        surface         = _surface;
        cubeIndex       = _cubeIndex;
        xIndex          = _xIndex;
        zIndex          = _zIndex;
        LOD             = _LOD;
        meshResolution  = _meshResolution;
        planetRadius    = _planetRadius;

        subSurfaces = new List<SFSurface>();

        planet.RegisterSurface(this);

        surfaceObject = new GameObject("Surface " + (int)cubeIndex + " - LOD " + LOD + " - (" + zIndex + ", " + xIndex + ")");

        //Editor preview tiles are transient: never saved into scenes/prefabs, hidden from
        //the hierarchy, cleaned up by SFPlanet's preview lifecycle
        if (!Application.isPlaying)
            surfaceObject.hideFlags = HideFlags.HideAndDontSave;

        surfaceMeshFilter = surfaceObject.AddComponent<MeshFilter>();
        surfaceMeshCollider = surfaceObject.AddComponent<MeshCollider>();
        surfaceMeshRenderer = surfaceObject.AddComponent<MeshRenderer>();

        surfaceMeshRenderer.sharedMaterial = planet.GetSurfaceMaterial();
        ApplyColorMap();

        if (planet.UseGPUBaker)
        {
            //Mesh shell now; vertex data arrives from the GPU baker — same tick in the
            //editor (synchronous bake), a frame or two later at runtime (async readback)
            CreateEmptyMesh();
            planet.Baker.RequestBake(this);
        }
        else
            GenerateSurfaceCoordinates();
    }

    private void CreateEmptyMesh()
    {
        if (surfaceMesh != null)
            return;

        surfaceMesh = surfaceMeshFilter.sharedMesh = new Mesh();
        surfaceMesh.name = surfaceObject.name;

        if (!Application.isPlaying)
            surfaceMesh.hideFlags = HideFlags.HideAndDontSave;
    }

    private void GenerateSurfaceCoordinates()
    {
        ComputeVertexCoordinates();
        GenerateSurface();
    }

    private void ComputeVertexCoordinates()
    {
        //Create and size the Vertex Array
        if (vertexArray == null)
            vertexArray = new Vector3[meshResolution * meshResolution];
        //Cube-face UVs: the whole face spans [0,1], each tile its sub-rectangle. Unused by
        //the current shaders, but they make every mesh ready for the per-pixel color bake
        //(GPU milestone) with no mesh changes later
        if (uvArray == null)
            uvArray = new Vector2[meshResolution * meshResolution];

        //One integer division per coordinate, so vertices shared between neighboring tiles
        //(at any LOD) are bit-identical — accumulating a float increment leaves hairline gaps
        int quads = meshResolution - 1;
        float denominator = quads * (1 << LOD);

        for (int i = 0, index = 0; i < meshResolution; i++)
        {
            for (int j = 0; j < meshResolution; j++, index++)
            {
                float xPos = (j + xIndex * quads) / denominator - 0.5f;
                float yPos = 0.5f;
                float zPos = (i + zIndex * quads) / denominator - 0.5f;

                uvArray[index] = new Vector2(xPos + 0.5f, zPos + 0.5f);

                switch (cubeIndex)
                {
                    case CUBE_INDEX.TOP:
                        //Assign Vertex Coordinates
                        vertexArray[index] = new Vector3(xPos, yPos, zPos);
                        break;

                    case CUBE_INDEX.BOTTOM:
                        //Assign Vertex Coordinates
                        vertexArray[index] = new Vector3(xPos, -yPos, -zPos);
                        break;

                    case CUBE_INDEX.LEFT:
                        //Assign Vertex Coordinates
                        vertexArray[index] = new Vector3(-yPos, zPos, -xPos);
                        break;

                    case CUBE_INDEX.RIGHT:
                        //Assign Vertex Coordinates
                        vertexArray[index] = new Vector3(yPos, zPos, xPos);
                        break;

                    case CUBE_INDEX.FRONT:
                        //Assign Vertex Coordinates
                        vertexArray[index] = new Vector3(-xPos, zPos, yPos);
                        break;

                    case CUBE_INDEX.BACK:
                        //Assign Vertex Coordinates
                        vertexArray[index] = new Vector3(xPos, zPos, -yPos);
                        break;
                }

                //Spherify, then displace along the planet's surface function
                //(radius + terrain height)
                vertexArray[index] = planet.GetSurfacePoint(vertexArray[index].normalized);
            }
        }
    }//End of ComputeVertexCoordinates()

    private void GenerateSurface()
    {
        if (generated)
        {
            surfaceMeshRenderer.enabled = true;
            RefreshCollider();
            return;
        }

        //Create (or reuse) the Mesh on the Objects MeshFilter
        CreateEmptyMesh();

        //Assign the Vertex Array from the surface to the mesh vertex array
        surfaceMesh.vertices = vertexArray;
        //Triangle buffers are shared per-planet and precomputed per stitch mask;
        //a fresh tile starts unstitched, the planet's stitch pass corrects it the same frame
        currentStitchMask = 0;
        surfaceMesh.triangles = planet.GetTriangleBuffer(0);

        ApplyVertexAttributes();
        PrepareMorphData();

        //Bake the collider only where physics needs it — cooking is expensive
        RefreshCollider();

        if (GenerationComplete != null)
            GenerationComplete(this);

        generated = true;
    }//End of GenerateSurface()

    //Normals come from the planet's surface function, not from this tile's triangles —
    //mesh-based normals disagree along tile borders and cause lighting seams.
    //Colors carry the terrain ramp. Displacement is radial, so normalizing a vertex
    //recovers its sphere direction and its magnitude recovers the terrain height
    private void ApplyVertexAttributes()
    {
        Vector3[] normals = new Vector3[vertexArray.Length];
        Color[] colors = new Color[vertexArray.Length];

        float minRadius = float.MaxValue, maxRadius = float.MinValue;

        for (int i = 0; i < vertexArray.Length; i++)
        {
            normals[i] = planet.GetSurfaceNormal(vertexArray[i].normalized);
            colors[i] = planet.GetVertexColor(vertexArray[i], normals[i]);

            float radius = vertexArray[i].magnitude;
            if (radius < minRadius) minRadius = radius;
            if (radius > maxRadius) maxRadius = radius;
        }

        radialVariation = maxRadius - minRadius;
        normalArray = normals;

        surfaceMesh.normals = normals;
        surfaceMesh.colors = colors;
        surfaceMesh.uv = uvArray;
        surfaceMesh.RecalculateBounds();
    }

    //Real-time terrain editing: recompute this tile's geometry against the planet's current
    //height field and appearance. Triangles and stitch mask are untouched
    public void RebuildGeometry()
    {
        surfaceMeshRenderer.sharedMaterial = planet.GetSurfaceMaterial();

        if (planet.UseGPUBaker)
        {
            planet.Baker.RequestBake(this);
            return;
        }

        ComputeVertexCoordinates();
        surfaceMesh.vertices = vertexArray;

        ApplyVertexAttributes();
        RefreshCollider();
    }

    //CPU escape hatch used by the GPU baker when a readback errors
    public void GenerateCPUFallback()
    {
        GenerateSurfaceCoordinates();
    }

    //Vertex data arriving from the GPU baker (first bake or rebuild). On first bake the
    //tile also gets its base triangulation; the stitch pass corrects the mask afterward.
    //Rebuilds keep the existing (possibly stitched) triangles — vertex counts never change
    public void ApplyBakedData(Vector3[] _positions, Vector3[] _normals, Color[] _colors, Vector2[] _uvs)
    {
        if (surfaceObject == null)
            return;

        //Stale in-flight bake from before a resolution change — drop it; the planet's
        //resolution watch has already torn down and requeued this region
        if (_positions.Length != planet.meshResolution * planet.meshResolution)
            return;

        CreateEmptyMesh();

        vertexArray = _positions;
        uvArray = _uvs;

        float minRadius = float.MaxValue, maxRadius = float.MinValue;
        for (int i = 0; i < _positions.Length; i++)
        {
            float radius = _positions[i].magnitude;
            if (radius < minRadius) minRadius = radius;
            if (radius > maxRadius) maxRadius = radius;
        }
        radialVariation = maxRadius - minRadius;

        surfaceMesh.vertices = _positions;

        bool firstBake = !generated;
        if (firstBake)
        {
            currentStitchMask = 0;
            surfaceMesh.triangles = planet.GetTriangleBuffer(0);
        }

        surfaceMesh.normals = _normals;
        surfaceMesh.colors = _colors;
        surfaceMesh.uv = _uvs;
        surfaceMesh.RecalculateBounds();
        normalArray = _normals;

        if (firstBake)
            PrepareMorphData();

        RefreshCollider();

        generated = true;

        if (firstBake && GenerationComplete != null)
            GenerationComplete(this);
    }

    #region Geomorphing

    //Morph targets: this tile's surface as its PARENT rendered it — positions and normals
    //bilinearly interpolated on the parent's coarser grid, stored in UV2/UV3. The shader
    //starts a freshly revealed tile at the parent's shape and blends to the true one,
    //turning LOD refinement into a smooth transition instead of a pop
    private void PrepareMorphData()
    {
        hasMorphData = false;

        if (surface == null || surface.vertexArray == null || surface.normalArray == null || vertexArray == null)
            return;

        int R = meshResolution;
        if (surface.vertexArray.Length != R * R || vertexArray.Length != R * R || surface.normalArray.Length != R * R)
            return;

        Vector3[] parentPositions = new Vector3[R * R];
        Vector3[] parentNormals = new Vector3[R * R];

        int quads = R - 1;
        float offsetX = (xIndex & 1) * quads * 0.5f;
        float offsetZ = (zIndex & 1) * quads * 0.5f;

        for (int i = 0, index = 0; i < R; i++)
        {
            for (int j = 0; j < R; j++, index++)
            {
                //This vertex's position on the parent's grid (this tile covers one quadrant)
                float pj = offsetX + j * 0.5f;
                float pi = offsetZ + i * 0.5f;

                int j0 = (int)pj;
                int i0 = (int)pi;
                int j1 = Mathf.Min(j0 + 1, quads);
                int i1 = Mathf.Min(i0 + 1, quads);
                float fj = pj - j0;
                float fi = pi - i0;

                parentPositions[index] = Bilerp(surface.vertexArray, R, i0, j0, i1, j1, fi, fj);
                parentNormals[index] = Bilerp(surface.normalArray, R, i0, j0, i1, j1, fi, fj).normalized;
            }
        }

        surfaceMesh.SetUVs(1, parentPositions);
        surfaceMesh.SetUVs(2, parentNormals);
        hasMorphData = true;

        //Distance band: parent-shaped at this tile's spawn/merge threshold, fully
        //detailed by 60% of it. Set once — the shader evaluates per vertex, per camera
        if (LOD > 0)
        {
            float spawnDistance = planet.SplitDistance(LOD - 1);
            SetMorphDistances(spawnDistance, spawnDistance * 0.6f);
        }
    }

    private static Vector3 Bilerp(Vector3[] _grid, int _R, int _i0, int _j0, int _i1, int _j1, float _fi, float _fj)
    {
        Vector3 a = Vector3.Lerp(_grid[_i0 * _R + _j0], _grid[_i0 * _R + _j1], _fj);
        Vector3 b = Vector3.Lerp(_grid[_i1 * _R + _j0], _grid[_i1 * _R + _j1], _fj);
        return Vector3.Lerp(a, b, _fi);
    }

    private void SetMorphDistances(float _start, float _end)
    {
        if (surfaceMeshRenderer == null)
            return;

        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        surfaceMeshRenderer.GetPropertyBlock(properties);
        properties.SetFloat("_MorphStart", _start);
        properties.SetFloat("_MorphEnd", _end);
        surfaceMeshRenderer.SetPropertyBlock(properties);
    }

    #endregion

    #endregion

    #region SubDivision

    public List<SFSurface> subSurfaces;
    public bool hasSubSurfaces = false;
    private int generatedCount = 0;

    //Bind this tile's face color map (per-pixel surface color) via a property block —
    //shared material stays untouched, and rebakes update the texture contents in place
    public void ApplyColorMap()
    {
        if (surfaceMeshRenderer == null)
            return;

        if (planet.UseGPUBaker && planet.terrain != null)
        {
            //At impostor range the planet uses the far maps (ocean painted in, shell hidden)
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            surfaceMeshRenderer.GetPropertyBlock(properties);
            properties.SetTexture("_ColorMap", planet.ImpostorRange
                ? planet.Baker.GetFarMap((int)cubeIndex)
                : planet.Baker.GetFaceMap((int)cubeIndex));
            properties.SetFloat("_UseColorMap", 1.0f);
            surfaceMeshRenderer.SetPropertyBlock(properties);
        }
    }

    //Visual-only culling — the collider stays active so physics never depends on the camera.
    //generated guards against re-enabling a parent that is structurally hidden
    public void SetCulled(bool _culled)
    {
        surfaceMeshRenderer.enabled = generated && !_culled;
    }

    public void ApplyStitch(int _mask, int[] _triangleBuffer)
    {
        //A resolution change can leave this mesh a frame behind the shared buffers —
        //skip rather than assign out-of-range indices; the planet rebuilds it next frame
        if (surfaceMesh == null || surfaceMesh.vertexCount != planet.meshResolution * planet.meshResolution)
            return;

        currentStitchMask = _mask;
        surfaceMesh.triangles = _triangleBuffer;

        RefreshCollider();
    }

    //MeshCollider cooking costs milliseconds per mesh, so colliders are only baked on tiles
    //that need physics: the finest LOD (where the target actually is), or everything when
    //the planet's collidersAtMaxLODOnly is off
    private bool WantsCollider()
    {
        return planet.generateColliders && (!planet.collidersAtMaxLODOnly || LOD == planet.maxLOD);
    }

    private void RefreshCollider()
    {
        if (WantsCollider())
        {
            surfaceMeshCollider.enabled = true;
            //Reassign so the collider re-cooks from the current triangulation
            surfaceMeshCollider.sharedMesh = null;
            surfaceMeshCollider.sharedMesh = surfaceMesh;
        }
        else
        {
            surfaceMeshCollider.enabled = false;
            surfaceMeshCollider.sharedMesh = null;
        }
    }

    public void CleanAndDestroy()
    {
        planet.UnregisterSurface(this);

        //Runtime: deferred Destroy (DestroyImmediate mid-frame stalls, and merge waves destroy
        //many at once). Edit mode: Destroy is not allowed — DestroyImmediate is required
        if (Application.isPlaying)
        {
            if (surfaceMesh != null)
                UnityEngine.Object.Destroy(surfaceMesh);
            if (surfaceObject != null)
                UnityEngine.Object.Destroy(surfaceObject);
        }
        else
        {
            if (surfaceMesh != null)
                UnityEngine.Object.DestroyImmediate(surfaceMesh, false);
            if (surfaceObject != null)
                UnityEngine.Object.DestroyImmediate(surfaceObject);
        }

        if (SurfaceDestroyed != null)
            SurfaceDestroyed(this);
    }

    public void DestroySubSurfaces()
    {
        if (subSurfaces.Count > 0)
        {
            //Only merge one level at a time — every child must itself be a leaf,
            //otherwise grandchild meshes would leak when the child objects are destroyed
            for (int i = 0; i < subSurfaces.Count; i++)
                if (subSurfaces[i].hasSubSurfaces)
                    return;

            for (int i = 0; i < subSurfaces.Count; i++)
                subSurfaces[i].CleanAndDestroy();

            subSurfaces.Clear();

            hasSubSurfaces = false;

            ShowParentSurfaces();
        }
    }

    public void SubDivideSurface()
    {
        if (hasSubSurfaces)
            return;

        hasSubSurfaces = true;
        generatedCount = 0;

        for (int i = 0, index = 0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++, index++)
            {
                SFSurface subSurface = new SFSurface(planet, this, cubeIndex, (xIndex * 2) + j, (zIndex * 2) + i, LOD + 1, meshResolution, planetRadius);

                subSurface.surfaceObject.transform.parent = surfaceObject.transform;

                subSurfaces.Add(subSurface);
            }
        }

        //Hide the parent only once every child has geometry — with the async GPU baker
        //children arrive a few frames later and holes would flash otherwise. On the CPU
        //path children generate synchronously, so this hides immediately as before
        for (int c = 0; c < subSurfaces.Count; c++)
        {
            if (subSurfaces[c].generated)
                generatedCount++;
            else
                subSurfaces[c].GenerationComplete += OnChildGenerated;
        }

        if (generatedCount >= subSurfaces.Count)
            RevealChildren();
    }

    private void OnChildGenerated(SFSurface _child)
    {
        _child.GenerationComplete -= OnChildGenerated;
        generatedCount++;

        if (hasSubSurfaces && generatedCount >= subSurfaces.Count)
            RevealChildren();
    }

    //Swap moment: parent hides, children appear. Distance-based morphing means the
    //children render at (nearly) the parent's exact shape at this range — the swap
    //itself changes nothing visible
    private void RevealChildren()
    {
        HideParentSurfaces();
    }

    #endregion

    #region UTILITIES

    private void HideParentSurfaces()
    {
        surfaceMeshRenderer.enabled = false;
        surfaceMeshCollider.enabled = false;
        generated = false;
    }

    private void ShowParentSurfaces()
    {
        surfaceMeshRenderer.enabled = true;
        generated = true;
        RefreshCollider();
    }

    #endregion
}
