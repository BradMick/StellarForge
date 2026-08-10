using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CUBE_INDEX { TOP, BOTTOM, LEFT, RIGHT, FRONT, BACK }

//Tile edges in grid space: SOUTH = z min, NORTH = z max, WEST = x min, EAST = x max
public enum EDGE_INDEX { SOUTH, NORTH, WEST, EAST }

//Valid tile mesh densities (quads per tile edge). Values outside this set break stitching
//pairing or degenerate the mesh/bake pipeline, so the inspector only offers these
public enum SF_QUADS_PER_EDGE { Quads4 = 4, Quads8 = 8, Quads16 = 16, Quads32 = 32 }

[ExecuteAlways]
public class SFPlanet : MonoBehaviour
{
    //Planet details...
    public float            planetRadius    = 1.0f;

    //Mesh details...
    //Quads per tile edge — restricted to vetted power-of-two values: stitching needs border
    //quads to pair up, and powers of two keep tile borders and texel alignment bit-exact
    public SF_QUADS_PER_EDGE quadsPerEdgeSetting = SF_QUADS_PER_EDGE.Quads16;
    public int              quadsPerEdge    { get { return (int)quadsPerEdgeSetting; } }
    public int              meshResolution  { get { return quadsPerEdge + 1; } }

    //Surfaces... (never serialized — tiles are generated, not authored)
    [System.NonSerialized]
    public List<SFSurface> surfaceList     = new List<SFSurface>();

    //LOD Details — thresholds and depth derive from radius and the detail target;
    //nothing here needs hand-tuning per planet.
    //LOD = the BASE subdivision the planet rests and far-freezes at. Raise it (2 is a good
    //default) so the whole-disc early splits never happen — the coarser the base, the
    //bigger the visible pop when the first split repaints the entire planet
    public int              LOD             = 2;
    public Transform        LODTarget;

    //Finest ground detail: meters per quad at max LOD under the player. Drives maxLOD
    public float            targetGroundResolution = 1.5f;
    //Split when closer than this factor × tile size — a constant screen-density ladder
    //that scales with any radius. Higher = more detail retained at distance
    public float            lodSplitFactor = 4.0f;
    //Optional ceiling on the derived depth (water shells stay coarse via this)
    public int              maxLODCap = 26;

    //Derived — recomputed from radius/resolution, never authored or serialized
    [System.NonSerialized]
    public int              maxLOD;

    //Optional companions — auto-found on this GameObject if left empty
    public FrustumCuller    frustumCuller;
    public HorizonCuller    horizonCuller;
    public SFPlanetTerrain    terrain;
    //When true, tiles outside the frustum stop subdividing and keep their current LOD
    //until they come back into view. Merging stays distance-driven either way
    public bool             cullSubdivision = true;

    //Performance
    //Cap on distance-driven splits per frame. A camera swing onto unrefined terrain then
    //spreads its subdivision burst over several frames instead of spiking one.
    //With the GPU baker a split is cheap bookkeeping — 16 is comfortable (the old 4 dates
    //from the CPU era of per-split noise evaluation and collider cooking)
    public int              maxSplitsPerFrame = 16;
    private int             splitsThisFrame = 0;

    //Pre-refine this many degrees BEYOND the frustum edges (split gate only — render
    //culling stays tight). Fast turns then land on terrain that is already refined
    //instead of watching tiles ladder in
    public float            subdivisionFrustumMargin = 20.0f;

    //MeshCollider cooking is expensive — only the finest tiles (the ones things actually
    //land on, which exist exactly around the LOD target) carry live colliders
    public bool             collidersAtMaxLODOnly = true;

    //Master collider switch — off for surfaces that never need physics (e.g. water shells)
    public bool             generateColliders = true;

    //Visual-only shells (atmosphere): excluded from camera collision, altitude queries,
    //and clip-plane logic — nothing should ever treat them as a surface
    public bool             intangible = false;

    //When set, overrides every other material source (used by SFWaterShell to give the
    //water sphere its own material without a SFPlanetTerrain)
    public Material         surfaceMaterialOverride;

    //Uniform subdivision level for the edit-mode preview (0 = 6 tiles, 2 = 96, 3 = 384).
    //The preview regenerates automatically whenever a relevant setting changes
    public int              editorPreviewLOD = 2;

    //Bake tile geometry and colors in a compute shader — batches of tiles per dispatch,
    //async readback at runtime, synchronous for editor previews. Automatically falls back
    //to the CPU path when compute shaders are unavailable
    public bool             useGPUBaker = true;
    private SFPlanetGPUBaker baker;

    public bool UseGPUBaker { get { return useGPUBaker && SFPlanetGPUBaker.Supported; } }

    public SFPlanetGPUBaker Baker
    {
        get
        {
            if (baker == null)
                baker = new SFPlanetGPUBaker(this);
            return baker;
        }
    }

    //Real-time terrain editing: tiles rebuilt per frame when terrain settings change.
    //Higher = faster visual convergence, bigger frame cost while converging
    public int              maxRebuildsPerFrame = 24;
    private int             appliedTerrainVersion = 0;
    private int             appliedMeshResolution = -1;
    private List<SFSurface>   rebuildQueue = new List<SFSurface>();

    private void OnValidate()
    {
        targetGroundResolution = Mathf.Max(targetGroundResolution, 0.01f);
        lodSplitFactor = Mathf.Max(lodSplitFactor, 1.0f);
        maxLODCap = Mathf.Clamp(maxLODCap, 0, 26);

        RecomputeDerivedLOD();
    }

    //maxLOD = smallest depth whose quads reach the target ground resolution.
    //Face edge arc = (π/2)·R; tiles per edge double each level; quads per tile edge fixed
    private void RecomputeDerivedLOD()
    {
        float faceArc = Mathf.PI * 0.5f * planetRadius;
        float needed = faceArc / (quadsPerEdge * targetGroundResolution);
        maxLOD = Mathf.Clamp(Mathf.CeilToInt(Mathf.Log(Mathf.Max(needed, 1.0f), 2.0f)), 0, maxLODCap);
    }

    //Split threshold for a tile at this LOD: proportional to the tile's own arc size,
    //so the ladder is geometric and radius-independent. Merging uses 2× for hysteresis
    public float SplitDistance(int _lod)
    {
        return lodSplitFactor * (Mathf.PI * 0.5f * planetRadius) / (1 << _lod);
    }

	void Start ()
    {
        //Edit mode uses the editor preview path instead (see Editor Preview region)
        if (!Application.isPlaying)
            return;

        RecomputeDerivedLOD();
        appliedMeshResolution = meshResolution;

        //Terrain must be resolved before Generate() so the root tiles get heights
        if (terrain == null)
            terrain = GetComponent<SFPlanetTerrain>();

        if (frustumCuller == null)
            frustumCuller = GetComponent<FrustumCuller>();

        if (horizonCuller == null)
            horizonCuller = GetComponent<HorizonCuller>();
        if (horizonCuller != null && horizonCuller.sphereCenter == null)
        {
            //A culler living on this GameObject configures itself from the planet
            horizonCuller.sphereCenter = transform;
            horizonCuller.sphereRadius = planetRadius;
        }

        Generate(LOD);

        //Initial color maps bake synchronously during load — tiles are never gray-flashed
        if (UseGPUBaker && terrain != null)
        {
            Baker.RequestFaceBake();
            Baker.ProcessFaceBakes(true);
        }

        //Tiles just generated against the current settings — only later edits need rebuilds
        appliedTerrainVersion = terrain != null ? terrain.CombinedVersion : 0;

        //surfaceList[0].SubDivideSurface();
        //surfaceList[0].subSurfaces[0].SubDivideSurface();
        //surfaceList[0].subSurfaces[0].subSurfaces[2].SubDivideSurface();
	}

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (Camera.main == null)
            return;

        LODTarget = Camera.main.transform;

        //Radius can change at runtime (water shells track sea level) — keep depth in sync
        RecomputeDerivedLOD();

        //Mesh resolution changes invalidate every existing mesh (vertex counts no longer
        //match the shared triangle buffers) — tear down and regenerate cleanly
        if (appliedMeshResolution != meshResolution)
        {
            appliedMeshResolution = meshResolution;
            DestroyAllSurfaces();
            Generate(LOD);
        }

        //Far freeze: beyond the distance where every tile has merged back to the roots
        //anyway, the planet is just six textured meshes — skip the per-frame LOD, stitch
        //and cull passes entirely. Threshold derives from the split ladder, no tuning
        if (UpdateFarFreeze())
        {
            UpdateTerrainRebuilds();

            if (UseGPUBaker)
            {
                Baker.Process(false);
                Baker.ProcessFaceBakes(false);
            }
            return;
        }

        splitsThisFrame = 0;

        for (int i = 0; i < surfaceList.Count; i++)
            UpdateSurfaceLOD(surfaceList[i]);

        //After the tree has settled for this frame, restitch any leaf whose border situation changed
        for (int i = 0; i < surfaceList.Count; i++)
            ApplyStitchMasks(surfaceList[i]);

        for (int i = 0; i < surfaceList.Count; i++)
            ApplyFrustumCulling(surfaceList[i]);

        UpdateTerrainRebuilds();

        if (UseGPUBaker)
        {
            Baker.Process(false);
            Baker.ProcessFaceBakes(false);   //one face per frame while a rebake is queued
        }
    }

    private void OnDestroy()
    {
        if (baker != null)
        {
            baker.Dispose();
            baker = null;
        }
    }

    #region SFSurface Function

    //Step size in direction space for normal sampling. Keep well below the angular size
    //of one quad at maxLOD, and above float precision noise
    private const float normalSampleEpsilon = 0.001f;

    //Radial height offset for a point on the unit sphere. Depends ONLY on _direction so that
    //neighboring tiles at different LODs agree exactly. No terrain component = smooth sphere
    public float GetHeight(Vector3 _direction)
    {
        if (terrain != null)
            return terrain.GetHeight(_direction, planetRadius);

        return 0.0f;
    }

    //World-space-shaped surface point for a unit sphere direction
    public Vector3 GetSurfacePoint(Vector3 _direction)
    {
        return _direction * (planetRadius + GetHeight(_direction));
    }

    //Unit sphere direction for a point on a cube face, using the same face mappings
    //as SFSurface.GenerateSurfaceCoordinates. _u and _v are the grid coordinates in [-0.5, 0.5]
    public static Vector3 CubePointToDirection(CUBE_INDEX _face, float _u, float _v)
    {
        switch (_face)
        {
            case CUBE_INDEX.TOP:    return new Vector3(_u, 0.5f, _v).normalized;
            case CUBE_INDEX.BOTTOM: return new Vector3(_u, -0.5f, -_v).normalized;
            case CUBE_INDEX.LEFT:   return new Vector3(-0.5f, _v, -_u).normalized;
            case CUBE_INDEX.RIGHT:  return new Vector3(0.5f, _v, _u).normalized;
            case CUBE_INDEX.FRONT:  return new Vector3(-_u, _v, 0.5f).normalized;
            default:                return new Vector3(_u, _v, -0.5f).normalized;
        }
    }

    //Normal from central differences of the surface function itself, not from mesh triangles.
    //Border vertices shared by neighboring tiles produce identical normals at any LOD,
    //and terrain displacement is picked up automatically through GetSurfacePoint
    public Vector3 GetSurfaceNormal(Vector3 _direction)
    {
        //Face-independent tangent basis so tiles on different cube faces agree too
        Vector3 reference = Mathf.Abs(_direction.y) < 0.99f ? Vector3.up : Vector3.right;
        Vector3 tangent = Vector3.Cross(reference, _direction).normalized;
        Vector3 bitangent = Vector3.Cross(_direction, tangent);

        Vector3 deltaTangent = GetSurfacePoint((_direction + tangent * normalSampleEpsilon).normalized)
                             - GetSurfacePoint((_direction - tangent * normalSampleEpsilon).normalized);
        Vector3 deltaBitangent = GetSurfacePoint((_direction + bitangent * normalSampleEpsilon).normalized)
                               - GetSurfacePoint((_direction - bitangent * normalSampleEpsilon).normalized);

        return Vector3.Cross(deltaTangent, deltaBitangent).normalized;
    }

    //Vertex color for a planet-local surface vertex: ramp + cliffs + biomes + vegetation.
    //Displacement is radial, so the vertex magnitude recovers its height above the sphere
    public Color GetVertexColor(Vector3 _localVertex, Vector3 _normal)
    {
        if (terrain == null)
            return Color.white;

        return terrain.GetSurfaceColor(_localVertex.normalized, _normal, _localVertex.magnitude - planetRadius, planetRadius);
    }

    //Tile material: the terrain's, or a shared fallback built on the bundled
    //vertex-color shader so the ramp shows even with nothing assigned
    private Material fallbackMaterial;

    public Material GetSurfaceMaterial()
    {
        if (surfaceMaterialOverride != null)
            return surfaceMaterialOverride;

        if (terrain != null && terrain.surfaceMaterial != null)
            return terrain.surfaceMaterial;

        if (fallbackMaterial == null)
        {
            Shader shader = Shader.Find("StellarForge/SFPlanet SFSurface");
            fallbackMaterial = new Material(shader != null ? shader : Shader.Find("Diffuse"));
        }

        return fallbackMaterial;
    }

    #endregion

    #region Real-Time Terrain Rebuild

    //Push the current face color maps to every live tile's renderer — called by the baker
    //when the maps are (re)created. Contents update in place on rebakes, so per-tile
    //application is only needed here and at tile creation
    public void ApplyColorMapsToTiles()
    {
        foreach (SFSurface surface in surfaceRegistry.Values)
            surface.ApplyColorMap();
    }

    //Queue every live tile for regeneration — used when something outside the terrain
    //version system changes the surface (e.g. SFWaterShell adjusting the radius or material)
    public void RequestFullRebuild()
    {
        rebuildQueue.Clear();
        rebuildQueue.AddRange(surfaceRegistry.Values);
    }

    //When any terrain setting, ramp, or material changes at runtime, every live tile is
    //queued and rebuilt over the following frames — newly split tiles pick up the current
    //settings automatically, so only pre-existing geometry needs the sweep
    private void UpdateTerrainRebuilds()
    {
        int version = terrain != null ? terrain.CombinedVersion : 0;
        if (version != appliedTerrainVersion)
        {
            appliedTerrainVersion = version;
            rebuildQueue.Clear();
            rebuildQueue.AddRange(surfaceRegistry.Values);

            //Recolor the face maps along with the geometry
            if (UseGPUBaker)
                Baker.RequestFaceBake();
        }

        int budget = maxRebuildsPerFrame;
        while (budget > 0 && rebuildQueue.Count > 0)
        {
            SFSurface surface = rebuildQueue[rebuildQueue.Count - 1];
            rebuildQueue.RemoveAt(rebuildQueue.Count - 1);

            //Tiles may have merged away while queued
            if (surface == null || surface.surfaceObject == null)
                continue;

            surface.RebuildGeometry();
            budget--;
        }
    }

    #endregion

    #region Analytic Raycast

    //Raycast against the planet with no colliders involved: closed-form ray/sphere
    //intersection against the terrain's outer shell, then a bounded march + bisection
    //against the height field itself. Works at any distance regardless of LOD or colliders.
    //A hit inside terrain (origin underground) reports the ray origin itself.
    //For the surface normal at the hit, pass the result to GetSurfaceNormal(hit.normalized)
    public bool Raycast(Ray _ray, out Vector3 _hitPoint)
    {
        _hitPoint = Vector3.zero;

        //SFPlanet-local space so a moving or rotating planet stays correct
        Vector3 origin    = transform.InverseTransformPoint(_ray.origin);
        Vector3 direction = transform.InverseTransformDirection(_ray.direction).normalized;

        float maxHeight   = terrain != null ? terrain.MaxHeight(planetRadius) : 0.0f;
        float shellRadius = planetRadius + maxHeight;

        //Closed-form intersection with the shell sphere: t^2 + 2bt + c = 0
        float b = Vector3.Dot(origin, direction);
        float c = origin.sqrMagnitude - shellRadius * shellRadius;
        float discriminant = b * b - c;

        if (discriminant < 0.0f)
            return false;                                   //ray line never meets the shell

        float sqrtD  = Mathf.Sqrt(discriminant);
        float tEnter = -b - sqrtD;
        float tExit  = -b + sqrtD;

        if (tExit < 0.0f)
            return false;                                   //planet entirely behind the ray

        if (terrain == null)
        {
            //Smooth sphere — the closed-form answer is exact (no hit from inside)
            if (tEnter < 0.0f)
                return false;

            _hitPoint = transform.TransformPoint(origin + direction * tEnter);
            return true;
        }

        //March the chord through the shell until the ray dips below the terrain, then bisect.
        //Step is tied to terrain amplitude so features can't be tunneled through; the chord
        //divisor bounds the iteration count for grazing rays
        float tStart = Mathf.Max(tEnter, 0.0f);
        float step   = Mathf.Max(maxHeight * 0.5f, (tExit - tStart) / 512.0f);
        if (step <= 0.0f)
            step = Mathf.Max(planetRadius, 1.0f) * 0.0001f;

        float tPrev = tStart;
        if (AltitudeAboveTerrain(origin + direction * tStart) <= 0.0f)
        {
            _hitPoint = transform.TransformPoint(origin + direction * tStart);
            return true;
        }

        for (float t = tStart + step; tPrev < tExit; t += step)
        {
            float tClamped = Mathf.Min(t, tExit);

            if (AltitudeAboveTerrain(origin + direction * tClamped) <= 0.0f)
            {
                //SFSurface crossed between tPrev and tClamped — bisect to precision
                float lo = tPrev, hi = tClamped;
                for (int i = 0; i < 16; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (AltitudeAboveTerrain(origin + direction * mid) > 0.0f)
                        lo = mid;
                    else
                        hi = mid;
                }

                _hitPoint = transform.TransformPoint(origin + direction * hi);
                return true;
            }

            tPrev = tClamped;
        }

        return false;
    }

    //Signed altitude of a planet-local point above the terrain surface directly beneath it
    private float AltitudeAboveTerrain(Vector3 _localPoint)
    {
        return _localPoint.magnitude - (planetRadius + GetHeight(_localPoint.normalized));
    }

    #endregion

    #region Generate SFPlanet Surfaces

    public void Generate(int _baseLOD)
    {
        int tilesPerEdge = 1 << _baseLOD;

        for (int index = 0; index < 6; index++)
        {
            for (int i = 0; i < tilesPerEdge; i++)
            {
                for (int j = 0; j < tilesPerEdge; j++)
                {
                    SFSurface surface = new SFSurface(this, null, (CUBE_INDEX)index, j, i, _baseLOD, meshResolution, planetRadius);
                    surface.surfaceObject.transform.parent = this.transform;

                    surfaceList.Add(surface);
                }
            }
        }
    }

    //Tear down every live tile (both play-mode trees and editor previews)
    public void DestroyAllSurfaces()
    {
        for (int i = 0; i < surfaceList.Count; i++)
            DestroySurfaceRecursive(surfaceList[i]);

        surfaceList.Clear();
        surfaceRegistry.Clear();
        rebuildQueue.Clear();
    }

    private void DestroySurfaceRecursive(SFSurface _surface)
    {
        for (int i = 0; i < _surface.subSurfaces.Count; i++)
            DestroySurfaceRecursive(_surface.subSurfaces[i]);

        _surface.subSurfaces.Clear();
        _surface.CleanAndDestroy();
    }

    #endregion

    #region Sub-Division Handling

    //Closest direction within a tile's spherical patch to a planet-local position: project the
    //position onto the tile's cube face and clamp into the tile's rectangle. Used for LOD
    //distance (bounding boxes overshoot curved tiles unevenly and skew the LOD footprint)
    //and for horizon culling (it finds the tile's most-visible sample)
    private Vector3 ClosestTileDirection(SFSurface _surface, Vector3 _localPosition)
    {
        //Project onto the tile's face: w = component along the face normal,
        //u/v = tangential components matching the face's grid axes
        float w, u, v;
        switch (_surface.cubeIndex)
        {
            case CUBE_INDEX.TOP:    w = _localPosition.y;  u = _localPosition.x;  v = _localPosition.z;  break;
            case CUBE_INDEX.BOTTOM: w = -_localPosition.y; u = _localPosition.x;  v = -_localPosition.z; break;
            case CUBE_INDEX.LEFT:   w = -_localPosition.x; u = -_localPosition.z; v = _localPosition.y;  break;
            case CUBE_INDEX.RIGHT:  w = _localPosition.x;  u = _localPosition.z;  v = _localPosition.y;  break;
            case CUBE_INDEX.FRONT:  w = _localPosition.z;  u = -_localPosition.x; v = _localPosition.y;  break;
            default:                w = -_localPosition.z; u = _localPosition.x;  v = _localPosition.y;  break;
        }

        //The tile's rectangle in face grid coordinates
        int n = 1 << _surface.LOD;
        float u0 = (float)_surface.xIndex / n - 0.5f, u1 = (float)(_surface.xIndex + 1) / n - 0.5f;
        float v0 = (float)_surface.zIndex / n - 0.5f, v1 = (float)(_surface.zIndex + 1) / n - 0.5f;

        if (w > 1e-5f)
            return CubePointToDirection(_surface.cubeIndex,
                Mathf.Clamp(0.5f * u / w, u0, u1),
                Mathf.Clamp(0.5f * v / w, v0, v1));

        //Position is on the far side of this face's hemisphere — the tile is distant anyway,
        //so its center is an adequate stand-in
        return CubePointToDirection(_surface.cubeIndex, (u0 + u1) * 0.5f, (v0 + v1) * 0.5f);
    }

    //Distance from a world-space position to a tile's actual spherical patch (see above)
    private float DistanceToSurfaceTile(SFSurface _surface, Vector3 _worldPosition)
    {
        //SFPlanet-local space, so a moved or rotated planet still measures correctly
        Vector3 p = transform.InverseTransformPoint(_worldPosition);

        return Vector3.Distance(p, GetSurfacePoint(ClosestTileDirection(_surface, p)));
    }

    private void UpdateSurfaceLOD(SFSurface _surface)
    {
        float distance = DistanceToSurfaceTile(_surface, LODTarget.position);

        if (_surface.subSurfaces.Count == 0)
        {
            //Self-heal: re-request any leaf still waiting on geometry (e.g. a bake lost to
            //a disable/enable cycle) — the baker's request queue dedupes
            if (!_surface.generated && UseGPUBaker)
                Baker.RequestBake(_surface);

            //Unbaked tiles (GPU data still in flight) don't split — their children would
            //only queue behind them anyway
            if (_surface.generated && _surface.LOD < maxLOD && distance < SplitDistance(_surface.LOD))
            {
                //Skip refining tiles the camera can't see; they catch up when they re-enter view.
                //Neighbor-enforcement splits from SplitSurface bypass this on purpose — stitching
                //correctness must hold whether or not a tile is visible.
                //The per-frame budget only gates NEW distance-driven splits; a split that goes
                //ahead always completes its enforcement cascade so the tree stays consistent.
                //The angular margin pre-refines beyond the view edges so turns land on ready terrain
                float margin = distance * Mathf.Tan(subdivisionFrustumMargin * Mathf.Deg2Rad);
                if (splitsThisFrame < maxSplitsPerFrame && (!cullSubdivision || IsTileVisible(_surface, margin)))
                    SplitSurface(_surface);
            }
        }
        else
        {
            //Merge threshold sits above the split threshold so tiles don't flicker at the boundary
            if (distance > SplitDistance(_surface.LOD) * 2.0f)
            {
                //Recurse first so the deepest levels collapse bottom-up — DestroySubSurfaces
                //refuses to merge while any child still has children of its own
                for (int i = 0; i < _surface.subSurfaces.Count; i++)
                    UpdateSurfaceLOD(_surface.subSurfaces[i]);

                if (CanMerge(_surface))
                    _surface.DestroySubSurfaces();
            }
            else
                for (int i = 0; i < _surface.subSurfaces.Count; i++)
                    UpdateSurfaceLOD(_surface.subSurfaces[i]);
        }
    }

    #endregion

    #region SFSurface Registry & Neighbors

    //Every live SFSurface (leaves AND hidden parents), keyed by (face, LOD, x, z)
    private Dictionary<long, SFSurface> surfaceRegistry = new Dictionary<long, SFSurface>();

    private static long SurfaceKey(CUBE_INDEX _face, int _LOD, int _x, int _z)
    {
        return ((long)_face << 60) | ((long)_LOD << 52) | ((long)_x << 26) | (long)_z;
    }

    public void RegisterSurface(SFSurface _surface)
    {
        surfaceRegistry[SurfaceKey(_surface.cubeIndex, _surface.LOD, _surface.xIndex, _surface.zIndex)] = _surface;
    }

    public void UnregisterSurface(SFSurface _surface)
    {
        surfaceRegistry.Remove(SurfaceKey(_surface.cubeIndex, _surface.LOD, _surface.xIndex, _surface.zIndex));
    }

    public SFSurface FindSurface(CUBE_INDEX _face, int _LOD, int _x, int _z)
    {
        SFSurface surface;
        surfaceRegistry.TryGetValue(SurfaceKey(_face, _LOD, _x, _z), out surface);
        return surface;
    }

    //Cross-face adjacency, derived from each face's grid-to-cube mapping in
    //SFSurface.GenerateSurfaceCoordinates. [face, edge] → the face it touches,
    //which of THAT face's edges is shared, and whether the along-edge coordinate runs reversed
    private static readonly CUBE_INDEX[,] adjacentFace = new CUBE_INDEX[6, 4]
    {
        //SOUTH              NORTH              WEST               EAST
        { CUBE_INDEX.BACK,   CUBE_INDEX.FRONT,  CUBE_INDEX.LEFT,   CUBE_INDEX.RIGHT }, //TOP
        { CUBE_INDEX.FRONT,  CUBE_INDEX.BACK,   CUBE_INDEX.LEFT,   CUBE_INDEX.RIGHT }, //BOTTOM
        { CUBE_INDEX.BOTTOM, CUBE_INDEX.TOP,    CUBE_INDEX.FRONT,  CUBE_INDEX.BACK  }, //LEFT
        { CUBE_INDEX.BOTTOM, CUBE_INDEX.TOP,    CUBE_INDEX.BACK,   CUBE_INDEX.FRONT }, //RIGHT
        { CUBE_INDEX.BOTTOM, CUBE_INDEX.TOP,    CUBE_INDEX.RIGHT,  CUBE_INDEX.LEFT  }, //FRONT
        { CUBE_INDEX.BOTTOM, CUBE_INDEX.TOP,    CUBE_INDEX.LEFT,   CUBE_INDEX.RIGHT }, //BACK
    };

    private static readonly EDGE_INDEX[,] adjacentEdge = new EDGE_INDEX[6, 4]
    {
        { EDGE_INDEX.NORTH, EDGE_INDEX.NORTH, EDGE_INDEX.NORTH, EDGE_INDEX.NORTH }, //TOP
        { EDGE_INDEX.SOUTH, EDGE_INDEX.SOUTH, EDGE_INDEX.SOUTH, EDGE_INDEX.SOUTH }, //BOTTOM
        { EDGE_INDEX.WEST,  EDGE_INDEX.WEST,  EDGE_INDEX.EAST,  EDGE_INDEX.WEST  }, //LEFT
        { EDGE_INDEX.EAST,  EDGE_INDEX.EAST,  EDGE_INDEX.EAST,  EDGE_INDEX.WEST  }, //RIGHT
        { EDGE_INDEX.SOUTH, EDGE_INDEX.NORTH, EDGE_INDEX.EAST,  EDGE_INDEX.WEST  }, //FRONT
        { EDGE_INDEX.NORTH, EDGE_INDEX.SOUTH, EDGE_INDEX.EAST,  EDGE_INDEX.WEST  }, //BACK
    };

    private static readonly bool[,] adjacentReversed = new bool[6, 4]
    {
        { false, true,  true,  false }, //TOP
        { true,  false, false, true  }, //BOTTOM
        { false, true,  false, false }, //LEFT
        { true,  false, false, false }, //RIGHT
        { true,  true,  false, false }, //FRONT
        { false, false, false, false }, //BACK
    };

    private static EDGE_INDEX OppositeEdge(EDGE_INDEX _edge)
    {
        switch (_edge)
        {
            case EDGE_INDEX.SOUTH: return EDGE_INDEX.NORTH;
            case EDGE_INDEX.NORTH: return EDGE_INDEX.SOUTH;
            case EDGE_INDEX.WEST:  return EDGE_INDEX.EAST;
            default:               return EDGE_INDEX.WEST;
        }
    }

    //Coordinates of the same-LOD neighbor across the given edge, handling cube-face wrap.
    //_nEdge is the shared edge as seen from the NEIGHBOR's grid
    public void GetNeighborCoordinates(SFSurface _surface, EDGE_INDEX _edge, out CUBE_INDEX _nFace, out int _nX, out int _nZ, out EDGE_INDEX _nEdge)
    {
        int n = 1 << _surface.LOD;
        int sx = _surface.xIndex + (_edge == EDGE_INDEX.EAST ? 1 : _edge == EDGE_INDEX.WEST ? -1 : 0);
        int sz = _surface.zIndex + (_edge == EDGE_INDEX.NORTH ? 1 : _edge == EDGE_INDEX.SOUTH ? -1 : 0);

        if (sx >= 0 && sx < n && sz >= 0 && sz < n)
        {
            _nFace = _surface.cubeIndex;
            _nX = sx;
            _nZ = sz;
            _nEdge = OppositeEdge(_edge);
            return;
        }

        _nFace = adjacentFace[(int)_surface.cubeIndex, (int)_edge];
        _nEdge = adjacentEdge[(int)_surface.cubeIndex, (int)_edge];

        int t = (_edge == EDGE_INDEX.EAST || _edge == EDGE_INDEX.WEST) ? _surface.zIndex : _surface.xIndex;
        if (adjacentReversed[(int)_surface.cubeIndex, (int)_edge])
            t = n - 1 - t;

        switch (_nEdge)
        {
            case EDGE_INDEX.SOUTH: _nX = t;     _nZ = 0;     break;
            case EDGE_INDEX.NORTH: _nX = t;     _nZ = n - 1; break;
            case EDGE_INDEX.WEST:  _nX = 0;     _nZ = t;     break;
            default:               _nX = n - 1; _nZ = t;     break;
        }
    }

    #endregion

    #region Stitching

    //The neighbor's two children that touch the shared edge, per EDGE_INDEX,
    //using subSurfaces order from SubDivideSurface: [0]=(x0,z0) [1]=(x1,z0) [2]=(x0,z1) [3]=(x1,z1)
    private static readonly int[,] edgeChildIndices = new int[4, 2] { { 0, 1 }, { 2, 3 }, { 0, 2 }, { 1, 3 } };

    //Split with the restricted-quadtree guarantee: every edge neighbor is brought up to this
    //tile's LOD first, so adjacent leaves never differ by more than one level — the most a
    //stitch pattern can bridge
    public void SplitSurface(SFSurface _surface)
    {
        if (_surface.hasSubSurfaces)
            return;

        splitsThisFrame++;

        for (int edge = 0; edge < 4; edge++)
        {
            CUBE_INDEX nFace; int nX, nZ; EDGE_INDEX nEdge;
            GetNeighborCoordinates(_surface, (EDGE_INDEX)edge, out nFace, out nX, out nZ, out nEdge);
            EnsureSurfaceExists(nFace, _surface.LOD, nX, nZ);
        }

        _surface.SubDivideSurface();
    }

    private SFSurface EnsureSurfaceExists(CUBE_INDEX _face, int _LOD, int _x, int _z)
    {
        SFSurface surface = FindSurface(_face, _LOD, _x, _z);
        if (surface != null)
            return surface;

        if (_LOD <= 0)
            return null;

        SFSurface parent = EnsureSurfaceExists(_face, _LOD - 1, _x / 2, _z / 2);
        if (parent == null)
            return null;

        SplitSurface(parent);

        return FindSurface(_face, _LOD, _x, _z);
    }

    //Merging is blocked while any neighbor still shows grandchildren along the shared edge —
    //otherwise the merged leaf would border tiles two LODs finer than itself
    private bool CanMerge(SFSurface _surface)
    {
        for (int edge = 0; edge < 4; edge++)
        {
            CUBE_INDEX nFace; int nX, nZ; EDGE_INDEX nEdge;
            GetNeighborCoordinates(_surface, (EDGE_INDEX)edge, out nFace, out nX, out nZ, out nEdge);

            SFSurface neighbor = FindSurface(nFace, _surface.LOD, nX, nZ);
            if (neighbor == null || !neighbor.hasSubSurfaces)
                continue;

            if (neighbor.subSurfaces[edgeChildIndices[(int)nEdge, 0]].hasSubSurfaces ||
                neighbor.subSurfaces[edgeChildIndices[(int)nEdge, 1]].hasSubSurfaces)
                return false;
        }

        return true;
    }

    //An edge needs stitching when no same-LOD neighbor exists there — with the ≤1-level
    //constraint that means the neighbor is exactly one LOD coarser
    private int ComputeStitchMask(SFSurface _surface)
    {
        int mask = 0;

        for (int edge = 0; edge < 4; edge++)
        {
            CUBE_INDEX nFace; int nX, nZ; EDGE_INDEX nEdge;
            GetNeighborCoordinates(_surface, (EDGE_INDEX)edge, out nFace, out nX, out nZ, out nEdge);

            if (FindSurface(nFace, _surface.LOD, nX, nZ) == null)
                mask |= 1 << edge;
        }

        return mask;
    }

    private bool farFrozen;
    private bool impostorRange;

    //Far-frozen: per-frame LOD/stitch/cull work stops, visuals unchanged (crossing it is invisible)
    public bool FarFrozen { get { return farFrozen; } }
    //Impostor range: the planet is tens of pixels — painted-ocean far maps, water shell hidden.
    //Representation swaps only happen here, where they cannot be perceived
    public bool ImpostorRange { get { return impostorRange; } }

    //True while the planet is far enough to be a static textured sphere. Entering the
    //frozen state requires the tree to have collapsed to the roots (which the normal
    //merge logic does on its own past 2× the root split distance)
    private bool UpdateFarFreeze()
    {
        float anchorDistance = Vector3.Distance(LODTarget.position, transform.position);
        float freezeDistance = SplitDistance(0) * 2.05f;
        bool wantFar = anchorDistance > freezeDistance;

        //Appearance swaps only at impostor range (~8× the freeze distance): the planet is
        //a handful of pixels there, so switching maps / hiding the shell cannot pop
        bool wantImpostor = anchorDistance > freezeDistance * 8.0f;
        if (wantImpostor != impostorRange)
        {
            impostorRange = wantImpostor;
            ApplyColorMapsToTiles();
            Debug.Log("[SFPlanet " + name + "] impostor range " + (impostorRange ? "ENTER" : "EXIT")
                + " at distance " + anchorDistance.ToString("F0") + " (threshold " + (freezeDistance * 8.0f).ToString("F0") + ")");
        }

        if (!wantFar)
        {
            if (farFrozen)
                Debug.Log("[SFPlanet " + name + "] far freeze EXIT at distance " + anchorDistance.ToString("F0"));
            farFrozen = false;
            return false;
        }

        if (!farFrozen)
        {
            for (int i = 0; i < surfaceList.Count; i++)
                if (surfaceList[i].hasSubSurfaces)
                    return false;   //still collapsing — keep running normal passes

            //Fully collapsed: show every root plainly (GPU backface culling handles the
            //far side; six draw calls is cheaper than per-frame culling math)
            for (int i = 0; i < surfaceList.Count; i++)
                surfaceList[i].SetCulled(false);

            farFrozen = true;
            Debug.Log("[SFPlanet " + name + "] far freeze ENTER at distance " + anchorDistance.ToString("F0")
                + " (threshold " + freezeDistance.ToString("F0") + ")");
        }

        return true;
    }

    //Scratch buffer for per-tile visibility samples (single-threaded use only)
    private readonly Vector3[] tileSamples = new Vector3[5];

    //Combined visual visibility: inside the camera frustum AND not hidden behind the planet's
    //own horizon. Either culler is optional — absent means that test always passes.
    //_extraPadding widens only the frustum tests (used by the split gate's pre-refine margin)
    private bool IsTileVisible(SFSurface _surface)
    {
        return IsTileVisible(_surface, 0.0f);
    }

    private bool IsTileVisible(SFSurface _surface, float _extraPadding)
    {
        if (frustumCuller != null)
        {
            //Both tests are conservative, so a tile is culled if EITHER proves it invisible.
            //The AABB test governs coarse tiles (small point-sample sets represent huge
            //curved patches poorly); the corner-vertex test governs fine tiles, where
            //bounding boxes are much looser than the actual shell geometry
            if (!frustumCuller.IsVisible(_surface.surfaceMeshRenderer.bounds, _extraPadding))
                return false;

            Vector3[] vertices = _surface.vertexArray;
            if (vertices != null && vertices.Length == meshResolution * meshResolution)
            {
                int last = vertices.Length - 1;
                tileSamples[0] = transform.TransformPoint(vertices[0]);
                tileSamples[1] = transform.TransformPoint(vertices[meshResolution - 1]);
                tileSamples[2] = transform.TransformPoint(vertices[last - meshResolution + 1]);
                tileSamples[3] = transform.TransformPoint(vertices[last]);
                tileSamples[4] = transform.TransformPoint(vertices[vertices.Length / 2]);

                //Conservative slack: curvature sag between samples plus THIS tile's own
                //measured height variation (tiny for fine tiles, where this test matters)
                float halfAngle = (Mathf.PI * 0.25f) / (1 << _surface.LOD);
                float inflation = planetRadius * (1.0f - Mathf.Cos(halfAngle)) + _surface.radialVariation + _extraPadding;

                if (!frustumCuller.IsVisible(tileSamples, 5, inflation))
                    return false;
            }
        }

        //Coarse tiles (LOD 0-1 span 45-90° of arc) are judged by only 5 samples — the
        //verdict flips near the limb and whole faces pop. Culling them saves almost
        //nothing (a handful of draws; backface culling eats the far side), so skip
        if (horizonCuller != null && _surface.LOD >= 2 && !IsTileAboveHorizon(_surface))
            return false;

        return true;
    }

    //A tile survives horizon culling if any of its samples — the patch point nearest the
    //viewer plus the four corners — is still on the viewer's side of the horizon
    private bool IsTileAboveHorizon(SFSurface _surface)
    {
        Vector3 viewer = LODTarget.position;

        if (HorizonSampleVisible(viewer, ClosestTileDirection(_surface, transform.InverseTransformPoint(viewer))))
            return true;

        int n = 1 << _surface.LOD;
        float u0 = (float)_surface.xIndex / n - 0.5f, u1 = (float)(_surface.xIndex + 1) / n - 0.5f;
        float v0 = (float)_surface.zIndex / n - 0.5f, v1 = (float)(_surface.zIndex + 1) / n - 0.5f;

        return HorizonSampleVisible(viewer, CubePointToDirection(_surface.cubeIndex, u0, v0))
            || HorizonSampleVisible(viewer, CubePointToDirection(_surface.cubeIndex, u1, v0))
            || HorizonSampleVisible(viewer, CubePointToDirection(_surface.cubeIndex, u0, v1))
            || HorizonSampleVisible(viewer, CubePointToDirection(_surface.cubeIndex, u1, v1));
    }

    private bool HorizonSampleVisible(Vector3 _viewer, Vector3 _direction)
    {
        return horizonCuller.IsVisible(_viewer, transform.TransformPoint(GetSurfacePoint(_direction)));
    }

    //Visual culling for leaves. Runs after the LOD and stitch passes so it sees the final tree;
    //parents stay hidden regardless, and colliders are never culled
    private void ApplyFrustumCulling(SFSurface _surface)
    {
        if (_surface.subSurfaces.Count == 0)
            _surface.SetCulled(!IsTileVisible(_surface));
        else
            for (int i = 0; i < _surface.subSurfaces.Count; i++)
                ApplyFrustumCulling(_surface.subSurfaces[i]);
    }

    private void ApplyStitchMasks(SFSurface _surface)
    {
        if (_surface.subSurfaces.Count == 0)
        {
            //Unbaked tiles have no vertices yet — assigning triangles would error.
            //They take the mask-0 buffer at bake time and get corrected here next frame
            if (!_surface.generated)
                return;

            int mask = ComputeStitchMask(_surface);
            if (mask != _surface.currentStitchMask)
                _surface.ApplyStitch(mask, GetTriangleBuffer(mask));
        }
        else
            for (int i = 0; i < _surface.subSurfaces.Count; i++)
                ApplyStitchMasks(_surface.subSurfaces[i]);
    }

    #endregion

    #region Triangle Buffers

    //16 possible border configurations per resolution, shared by every tile on the planet
    private Dictionary<int, int[]> triangleBufferCache = new Dictionary<int, int[]>();
    private int triangleBufferResolution = -1;

    public int[] GetTriangleBuffer(int _mask)
    {
        if (triangleBufferResolution != meshResolution)
        {
            triangleBufferCache.Clear();
            triangleBufferResolution = meshResolution;
        }

        int[] buffer;
        if (!triangleBufferCache.TryGetValue(_mask, out buffer))
        {
            buffer = BuildTriangleBuffer(_mask);
            triangleBufferCache[_mask] = buffer;
        }

        return buffer;
    }

    private static void AddQuad(List<int> _tris, int _R, int _i, int _j)
    {
        int v = _i * _R + _j;
        _tris.Add(v);      _tris.Add(v + _R);     _tris.Add(v + 1);
        _tris.Add(v + _R); _tris.Add(v + _R + 1); _tris.Add(v + 1);
    }

    private static void AddTriangle(List<int> _tris, int _a, int _b, int _c)
    {
        _tris.Add(_a); _tris.Add(_b); _tris.Add(_c);
    }

    //Builds the triangulation for one stitch mask. Stitched edges collapse border quads in
    //pairs into a fan that skips the odd edge vertex (the one the coarser neighbor lacks).
    //Where two stitched edges meet, each drops its corner filler triangle — the other edge's
    //fan covers that area, tiling the corner exactly
    private int[] BuildTriangleBuffer(int _mask)
    {
        int R = meshResolution;
        int Q = R - 1;
        bool sS = (_mask & 1) != 0, sN = (_mask & 2) != 0, sW = (_mask & 4) != 0, sE = (_mask & 8) != 0;

        List<int> tris = new List<int>();

        //Interior — untouched by any stitch configuration
        for (int i = 1; i <= Q - 2; i++)
            for (int j = 1; j <= Q - 2; j++)
                AddQuad(tris, R, i, j);

        //South row (quads i = 0; edge vertices on row 0)
        if (sS)
        {
            for (int t = 0; t < Q / 2; t++)
            {
                int j = 2 * t;
                int a = j, c = j + 2;
                int d = R + j, e = R + j + 1, f = R + j + 2;

                AddTriangle(tris, a, e, c);
                if (!(t == 0 && sW))         AddTriangle(tris, a, d, e);
                if (!(t == Q / 2 - 1 && sE)) AddTriangle(tris, e, f, c);
            }
        }
        else
            for (int j = (sW ? 1 : 0); j <= (sE ? Q - 2 : Q - 1); j++)
                AddQuad(tris, R, 0, j);

        //North row (quads i = Q-1; edge vertices on row Q)
        if (sN)
        {
            for (int t = 0; t < Q / 2; t++)
            {
                int j = 2 * t;
                int a = Q * R + j, c = Q * R + j + 2;
                int d = (Q - 1) * R + j, e = (Q - 1) * R + j + 1, f = (Q - 1) * R + j + 2;

                AddTriangle(tris, a, c, e);
                if (!(t == 0 && sW))         AddTriangle(tris, a, e, d);
                if (!(t == Q / 2 - 1 && sE)) AddTriangle(tris, c, f, e);
            }
        }
        else
            for (int j = (sW ? 1 : 0); j <= (sE ? Q - 2 : Q - 1); j++)
                AddQuad(tris, R, Q - 1, j);

        //West column (quads j = 0; edge vertices on column 0). Corner quads belong to the rows
        if (sW)
        {
            for (int t = 0; t < Q / 2; t++)
            {
                int i = 2 * t;
                int a = i * R, c = (i + 2) * R;
                int d = i * R + 1, e = (i + 1) * R + 1, f = (i + 2) * R + 1;

                AddTriangle(tris, a, c, e);
                if (!(t == 0 && sS))         AddTriangle(tris, a, e, d);
                if (!(t == Q / 2 - 1 && sN)) AddTriangle(tris, c, f, e);
            }
        }
        else
            for (int i = 1; i <= Q - 2; i++)
                AddQuad(tris, R, i, 0);

        //East column (quads j = Q-1; edge vertices on column Q)
        if (sE)
        {
            for (int t = 0; t < Q / 2; t++)
            {
                int i = 2 * t;
                int a = i * R + Q, c = (i + 2) * R + Q;
                int d = i * R + Q - 1, e = (i + 1) * R + Q - 1, f = (i + 2) * R + Q - 1;

                AddTriangle(tris, a, e, c);
                if (!(t == 0 && sS))         AddTriangle(tris, a, d, e);
                if (!(t == Q / 2 - 1 && sN)) AddTriangle(tris, c, e, f);
            }
        }
        else
            for (int i = 1; i <= Q - 2; i++)
                AddQuad(tris, R, i, Q - 1);

        return tris.ToArray();
    }

    #endregion

    #region Debug Overlay

    //On-screen state readout (Game view, play mode) for diagnosing distance behavior:
    //distance, state, thresholds, live tile count. Toggle on the inspector
    public bool debugOverlay = false;

    private void OnGUI()
    {
        if (!debugOverlay || !Application.isPlaying || LODTarget == null)
            return;

        float distance = Vector3.Distance(LODTarget.position, transform.position);
        float freezeDistance = SplitDistance(0) * 2.05f;
        string state = impostorRange ? "IMPOSTOR" : farFrozen ? "FROZEN" : "ACTIVE";

        GUILayout.Label("[" + name + "]  r=" + planetRadius.ToString("F0")
            + "  d=" + distance.ToString("F0")
            + "  " + state
            + "  tiles=" + surfaceRegistry.Count
            + "  maxLOD=" + maxLOD
            + "  split0@" + SplitDistance(0).ToString("F0")
            + "  freeze@" + freezeDistance.ToString("F0")
            + "  impostor@" + (freezeDistance * 8.0f).ToString("F0"));
    }

    #endregion

    #region Editor Preview

#if UNITY_EDITOR
    //Edit-mode planets render as a fixed uniform subdivision (editorPreviewLOD) that
    //regenerates whenever a relevant setting changes — no play mode needed to design a
    //world. Preview tiles are HideAndDontSave: never serialized into scenes or prefabs,
    //so a planet prefab stays just its components. A uniform grid needs no stitching,
    //LOD, or culling passes

    private int editorPreviewHash;

    private void OnEnable()
    {
        if (Application.isPlaying)
            return;

        //Domain reloads orphan preview tiles (surfaceList is intentionally not serialized) —
        //sweep any DontSave children left behind before generating fresh ones
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if ((child.hideFlags & HideFlags.DontSave) != 0)
                DestroyImmediate(child);
        }

        editorPreviewHash = 0;
        UnityEditor.EditorApplication.update -= EditorPreviewTick;
        UnityEditor.EditorApplication.update += EditorPreviewTick;
    }

    private void OnDisable()
    {
        //Buffers must release here as well — domain reloads skip OnDestroy, and leaked
        //ComputeBuffers trip Unity's leak detection. The baker recreates lazily on demand
        if (baker != null)
        {
            baker.Dispose();
            baker = null;
        }

        if (Application.isPlaying)
            return;

        UnityEditor.EditorApplication.update -= EditorPreviewTick;
        DestroyAllSurfaces();
    }

    private void EditorPreviewTick()
    {
        if (this == null)
        {
            UnityEditor.EditorApplication.update -= EditorPreviewTick;
            return;
        }

        if (terrain == null)
            terrain = GetComponent<SFPlanetTerrain>();

        RecomputeDerivedLOD();

        int hash = ComputeEditorPreviewHash();
        if (hash != editorPreviewHash || surfaceList.Count == 0)
        {
            editorPreviewHash = hash;
            DestroyAllSurfaces();
            Generate(Mathf.Clamp(editorPreviewLOD, 0, 4));
        }

        //Editor bakes synchronously — the whole preview lands in one dispatch, same tick
        if (UseGPUBaker)
        {
            Baker.Process(true);

            if (terrain != null && appliedTerrainVersion != terrain.CombinedVersion)
            {
                appliedTerrainVersion = terrain.CombinedVersion;
                Baker.RequestFaceBake();
            }
            Baker.ProcessFaceBakes(true);
        }
    }

    private int ComputeEditorPreviewHash()
    {
        int hash = 17;
        hash = hash * 31 + planetRadius.GetHashCode();
        hash = hash * 31 + quadsPerEdge;
        hash = hash * 31 + editorPreviewLOD;
        hash = hash * 31 + (terrain != null ? terrain.CombinedVersion : 0);
        hash = hash * 31 + (surfaceMaterialOverride != null ? surfaceMaterialOverride.GetInstanceID() : 0);
        return hash;
    }
#endif

    #endregion
}
