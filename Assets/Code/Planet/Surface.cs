using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Surface : MonoBehaviour
{
    public Planet planet;
    public Surface surface;
    public int xIndex;
    public int zIndex;
    public CUBE_INDEX cubeIndex;
    public bool generated = false;

    //Planet details...
    public float planetRadius;

    //Mesh details...
    public int meshResolution;

    //LOD Details...
    public int LOD;
    public bool queuedForSubDivision = false;


    public delegate void SurfaceDelegate(Surface _surface);
    public event SurfaceDelegate GenerationComplete, SubDivisionComplete, SurfaceDestroyed;

    public Vector3[] vertexArray;

    public GameObject surfaceObject;

    public Mesh surfaceMesh;
    public MeshCollider surfaceMeshCollider;
    public MeshRenderer surfaceMeshRenderer;
    public MeshFilter surfaceMeshFilter;

    #region Surface Generation

    public Surface(Planet _planet, Surface _surface, CUBE_INDEX _cubeIndex, int _xIndex, int _zIndex, int _LOD, int _meshResolution, float _planetRadius)
    {
        planet          = _planet;
        surface         = _surface;
        cubeIndex       = _cubeIndex;
        xIndex          = _xIndex;
        zIndex          = _zIndex;
        LOD             = _LOD;
        meshResolution  = _meshResolution;
        planetRadius    = _planetRadius;

        subSurfaces = new List<Surface>();

        surfaceObject = new GameObject("Surface " + (int)cubeIndex + " - LOD " + LOD + " - (" + zIndex + ", " + xIndex + ")");

        surfaceMeshFilter = surfaceObject.AddComponent<MeshFilter>();
        surfaceMeshCollider = surfaceObject.AddComponent<MeshCollider>();
        surfaceMeshRenderer = surfaceObject.AddComponent<MeshRenderer>();

        GenerateSurfaceCoordinates();
    }

    private void GenerateSurfaceCoordinates()
    {
        //Create and size the Vertex Array
        vertexArray = new Vector3[meshResolution * meshResolution];

        //Calculate the increment and keep the vertices confined to a 1x1 cube
        float increment = (1.0f / ((float)meshResolution - 1)) / (int)Mathf.Pow(2, LOD);

        for (int i = 0, index = 0; i < meshResolution; i++)
        {
            for (int j = 0; j < meshResolution; j++, index++)
            {
                float xPos = (float)j * increment - 0.5f + ((float)xIndex / Mathf.Pow(2, LOD));
                float yPos = 0.5f;
                float zPos = (float)i * increment - 0.5f + ((float)zIndex / Mathf.Pow(2, LOD));

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

                //Sperify!
                vertexArray[index] = vertexArray[index].normalized;

                //Change the radius...
                vertexArray[index] *= planetRadius;
            }
        }

        GenerateSurface();
    }//End of GenerateSurfaceCoordinates()

    private void GenerateSurface()
    {
        if (generated)
        {
            surfaceMeshRenderer.enabled = true;
            surfaceMeshCollider.enabled = true;
            return;
        }

        //Create and size the vertex buffer
        int vertexBufferSize    = meshResolution - 1;
        int[] vertexBuffer      = new int[(vertexBufferSize * vertexBufferSize) * 6];

        //Create a new mesh object
        //Mesh surfaceMesh;
        //Create a new Mesh using the Objects MeshFilter
        surfaceMesh         = surfaceMeshFilter.sharedMesh = new Mesh();
        surfaceMesh.name    = surfaceObject.name;

        for (int triIndex = 0, vertIndex = 0, i = 0; i < vertexBufferSize; i++, vertIndex++)
        {
            for (int j = 0; j < vertexBufferSize; j++, triIndex += 6, vertIndex++)
            {
                //  1
                //  | \
                //  |  \
                //  |   \
                //  0----2
                vertexBuffer[triIndex] = vertIndex;
                vertexBuffer[triIndex + 1] = vertIndex + meshResolution;
                vertexBuffer[triIndex + 2] = vertIndex + 1;
                //  1----3
                //    \  |
                //     \ |
                //      \|
                //       2
                vertexBuffer[triIndex + 3] = vertIndex + meshResolution;
                vertexBuffer[triIndex + 4] = vertIndex + meshResolution + 1;
                vertexBuffer[triIndex + 5] = vertIndex + 1;
            }
        }
        //Assign the Vertex Array from the surface to the mesh vertex array
        surfaceMesh.vertices = vertexArray;
        //Assign the Vertex Buffer
        surfaceMesh.triangles = vertexBuffer;
        //Recalculate the Normals
        //surfaceMesh.normals = RecalculateNormals(vertexArray);
        surfaceMesh.RecalculateNormals();
        //Recalculate bounds
        surfaceMesh.RecalculateBounds();
        //After the Mesh has been created, pass it back to the MeshCollider
        surfaceMeshCollider.sharedMesh = surfaceMesh;

        if (GenerationComplete != null)
            GenerationComplete(this);

        generated = true;
    }//End of GenerateSurface()

    #endregion

    #region Alternate SubDivision Handling

    public List<Surface> subSurfaces;
    public bool hasSubSurfaces = false;
    private int generatedCount = 0;
    private Transform LODTarget;
    private float distance;

    public void UpdateLOD()
    {
        distance = GetDistance();
        //Debug.Log(distance);

        if (LOD < planet.maxLOD)
        {
            Debug.Log(this.surfaceObject.name + " LOD < planet.maxLOD");
            if (!queuedForSubDivision)
            {
                Debug.Log(this.surfaceObject.name + " !queuedForSubDivision");
                if (!hasSubSurfaces && generated)
                {
                    float LODDistance = planet.LODDistances[LOD];

                    Debug.Log(this.surfaceObject.name + " !hasSubSurfaces && generated + " + LODDistance + " " + distance);
                    if (distance < LODDistance)
                    {
                        Debug.Log(this.surfaceObject.name +  " Added to queue!");
                        planet.AddToSubDivisionQueue(this);
                        queuedForSubDivision = true;
                    }
                }
                else
                {
                    float LODDistance = planet.LODDistances[LOD] * 2.0f;

                    Debug.Log(this.surfaceObject.name + " else !queuedForSubDivision");
                    if (distance > LODDistance)
                    {
                        Debug.Log("Destroying SubSurfaces");
                        DestroySubSurfaces();
                    }
                    else
                    {
                        Debug.Log(this.surfaceObject.name +  " Updating LOD's");
                        for (int i = 0; i < subSurfaces.Count; i++)
                        {
                            subSurfaces[i].UpdateLOD();
                        }
                    }
                }
            }
            else
            {
                Debug.Log(this.surfaceObject.name +  " else !queuedForSubDivision");
                if (distance > planet.LODDistances[LOD])
                {
                    Debug.Log(this.surfaceObject.name + " distance > planet.LODDistance, REMOVED FROM QUEUE");
                    planet.RemoveFromSubDivisionQueue(this);
                    queuedForSubDivision = false;
                }
            }
        }
    }

    public void CleanAndDestroy()
    {
        if (this != null)
        {
            if (this.surfaceMesh != null)
                DestroyImmediate(this.surfaceMesh, false);
            if (this.surfaceObject != null)
                DestroyImmediate(this.surfaceObject);
        }

        if (SurfaceDestroyed != null)
            SurfaceDestroyed(this);
    }

    public void DestroySubSurfaces()
    {
        if (subSurfaces.Count > 0)
        {
            Debug.Log("subSurfaces.Count > 0");

            if (subSurfaces[0].hasSubSurfaces)
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
                Surface subSurface = new Surface(planet, this, cubeIndex, (xIndex * 2) + j, (zIndex * 2) + i, LOD + 1, meshResolution, planetRadius);

                subSurface.surfaceObject.transform.parent = surfaceObject.transform;

                subSurfaces.Add(subSurface);
            }
        }

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
        surfaceMeshCollider.enabled = true;
        generated = true;
    }

    private float  GetDistance()
    {
        LODTarget = Camera.main.transform;

        return Vector3.Distance(LODTarget.position, surfaceMesh.bounds.ClosestPoint(LODTarget.position));
    }

    private Vector3[] RecalculateNormals(Vector3[] _vertexArray)
    {
        Vector3[] normals = new Vector3[_vertexArray.Length];

        for (int i = 0; i < normals.Length; i++)
            normals[i] = _vertexArray[i].normalized;

        return normals;
    }


    #endregion
}
