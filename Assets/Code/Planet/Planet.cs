using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CUBE_INDEX { TOP, BOTTOM, LEFT, RIGHT, FRONT, BACK }

public class Planet : MonoBehaviour
{
    //Planet details...
    public float            planetRadius    = 1.0f;

    //Mesh details...
    public int              meshResolution  = 8;

    //Surfaces...
    public List<Surface> surfaceList     = new List<Surface>();

    //LOD Details
    public int              LOD             = 0;
    public int              maxLOD          = 3;
    public Transform        LODTarget;
    public float[]          LODDistances     = { 8.0f, 6.0f, 4.0f };

	void Start ()
    {
        SubDivisionQueue    = new List<Surface>();
        GenerationQueue     = new List<Surface>();

        Generate();

        //surfaceList[0].SubDivideSurface();
        //surfaceList[0].subSurfaces[0].SubDivideSurface();
        //surfaceList[0].subSurfaces[0].subSurfaces[2].SubDivideSurface();
	}

    private void Update()
    {
        //for (int i = 0; i < 6; i++)
            StartCoroutine(UpdateLOD(2));

        /*if (Time.time > lastUpdateTime + LODUpdateInterval)
        {
            lastUpdateTime = Time.time;

            Debug.Log("Updating!");

            /*for (int i = 0; i < surfaceList.Count; i++)
            {
                surfaceList[i].UpdateLOD();
            }

            surfaceList[2].UpdateLOD();
        }

        SubDivisionQueue.RemoveAll(item => item == null);

        GenerationQueue.RemoveAll(item => item == null);

        for (int i = 0; i < simultaneousSubdivisions; i++)
        {
            Debug.Log("Generation Queue");

            if (GenerationQueue.Count < simultaneousSubdivisions && SubDivisionQueue.Count > 0 && SubDivisionQueue[0] != null)
            {
                GenerationQueue.Add(SubDivisionQueue[0]);
                SubDivisionQueue[0].SubDivisionComplete += RemoveWhenSubDivided;
                SubDivisionQueue[0].SubDivideSurface();
                SubDivisionQueue.RemoveAt(0);
            }
            else
                break;
        }*/
    }

    #region Generate Planet Surfaces

    public void Generate()
    {
        for (int index = 0; index < 6; index++)
        {
            for (int i = 0; i < (int)Mathf.Pow(2, LOD); i++)
            {
                for (int j = 0; j < (int)Mathf.Pow(2, LOD); j++)
                {
                    Surface surface = new Surface(this, null, (CUBE_INDEX)index, j, i, LOD, meshResolution, planetRadius);
                    surface.surfaceObject.transform.parent = this.transform;

                    surfaceList.Add(surface);
                }
            }
        }
    }

    #endregion

    #region Sub-Division Handling

    private void GetActiveSurfaces(Surface _surface, ref List<Surface> _active)
    {
        if (_surface.subSurfaces.Count == 0)
            _active.Add(_surface);
        else
            for (int i = 0; i < _surface.subSurfaces.Count; i++)
                GetActiveSurfaces(_surface.subSurfaces[i], ref _active);
    }

    public IEnumerator UpdateLOD(int _index)
    {
        LODTarget = Camera.main.transform;

        if (LODTarget == null)
            yield break;

        List<Surface> activeList = new List<Surface>();
        GetActiveSurfaces(surfaceList[_index], ref activeList);

        for (int i = 0; i < activeList.Count; i++)
        {
            Debug.Log("Checking active list!");

            float distance = Vector3.Distance(LODTarget.transform.position, activeList[i].surfaceMesh.bounds.ClosestPoint(LODTarget.position));

            if (activeList[i].LOD < maxLOD)
            {
                Debug.Log(activeList[i].surfaceObject.name + " LOD > maxLOD");

                if (distance > LODDistances[activeList[i].LOD])
                {
                    Debug.Log("distance > LODDistance + " + LODDistances[activeList[i].LOD]);
                    activeList[i].DestroySubSurfaces();
                }
                else if (distance < LODDistances[activeList[i].LOD])
                {
                    Debug.Log("distance < LODDistance + " + LODDistances[activeList[i].LOD]);
                    activeList[i].SubDivideSurface();
                }
                else
                {
                    if (activeList[i].gameObject.tag != "Planet")
                        activeList[i].gameObject.tag = "Planet";
                }
            }
        }
    }

    #endregion

    #region Alternate SubDivision Handling

    public int simultaneousSubdivisions = 1;

    List<Surface> SubDivisionQueue, GenerationQueue;

    public float LODUpdateInterval = 0.0f;
    public float lastUpdateTime = 0.0f;

    public void AddToSubDivisionQueue(Surface _surface)
    {
        if (!SubDivisionQueue.Contains(_surface))
            SubDivisionQueue.Add(_surface);
    }

    public void RemoveFromSubDivisionQueue(Surface _surface)
    {
        if (SubDivisionQueue.Contains(_surface) && !GenerationQueue.Contains(_surface))
            SubDivisionQueue.Remove(_surface);
    }

    public void RemoveWhenSubDivided(Surface _surface)
    {
        GenerationQueue.Remove(_surface);
        _surface.SubDivisionComplete -= RemoveWhenSubDivided;
    }

    #endregion
}
