#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

//THE single edit-mode update loop for the whole project.
//
//Every generated thing in a StellarForge scene — the system map, star bodies, planet
//surfaces, water and atmosphere shells — used to maintain its own EditorApplication.update
//subscription and rebuild itself whenever it noticed something had changed. Four
//independent loops mutating the same hierarchy, each able to trigger the others, with no
//defined order. That is what destroyed and rebuilt bodies every frame and made planet
//spawning unshippable.
//
//The rule this file exists to enforce: NOTHING creates, destroys or regenerates scene
//objects in edit mode except inside this driver's tick. Components no longer watch for
//their own changes. OnValidate calls MarkDirty and returns; the driver decides when the
//rebuild happens and in what order.
//
//Order matters and is explicit (see SF_REBUILD_ORDER): the generator produces the physical
//system, stars and planets are built from it, and shells attach to planets that already
//exist. Last session's headline bug was an SFPlanet built before its SFPlanetTerrain, which
//left it with nothing to generate from — dependency order is not a nicety here.
[InitializeOnLoad]
public static class SFEditorDriver
{
    //Dependency order for a tick. Lower runs first; a client's rebuild may assume
    //everything at a lower order is already up to date this tick
    public enum SF_REBUILD_ORDER
    {
        //Generates the physical system. Everything else reads its output
        GENERATOR = 0,
        //Scene bodies built from the generated system
        STAR      = 10,
        PLANET    = 20,
        //Attach to a planet that must already exist and be generated
        SHELL     = 30
    }

    //Implemented by anything the driver rebuilds. Components must not rebuild themselves
    public interface ISFEditorClient
    {
        //Where this client sits in the dependency order
        SF_REBUILD_ORDER RebuildOrder { get; }

        //Rebuild this client's generated scene objects. Called only from the driver tick,
        //only when the client has been marked dirty, and never re-entrantly
        void EditorRebuild();
    }

    //Marked dirty and awaiting a rebuild. A set, so marking the same client twenty times
    //while a slider is dragged still costs exactly one rebuild
    private static readonly HashSet<ISFEditorClient> dirty = new HashSet<ISFEditorClient>();

    //Scratch list for the sorted pass. Reused so a tick allocates nothing
    private static readonly List<ISFEditorClient> pending = new List<ISFEditorClient>();

    //Re-entrancy guard. A rebuild that marks something dirty (shells legitimately do this)
    //must not recurse into the driver — the mark lands in the set and is picked up by the
    //NEXT tick instead
    private static bool ticking;

    static SFEditorDriver()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    //Queue a client for rebuild on the next tick. Cheap and idempotent: safe to call from
    //OnValidate, from an inspector button, or repeatedly while a value is being dragged
    public static void MarkDirty(ISFEditorClient _client)
    {
        if (_client == null)
            return;

        dirty.Add(_client);
    }

    //Drop a client that is going away, so a destroyed object is never rebuilt
    public static void Forget(ISFEditorClient _client)
    {
        if (_client == null)
            return;

        dirty.Remove(_client);
    }

    private static void Tick()
    {
        //Play mode runs the real Update loops; edit-mode previews stay out of the way
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
            return;

        //Rebuilding during a compile or scene load races the domain reload that follows
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        if (ticking || dirty.Count == 0)
            return;

        ticking = true;

        try
        {
            //Snapshot and clear before rebuilding: anything marked dirty DURING this pass
            //is left in the set for the next tick rather than extending this one, which is
            //what keeps a self-triggering client from spinning the editor
            pending.Clear();
            pending.AddRange(dirty);
            dirty.Clear();

            //Unity nulls destroyed components without removing them from managed
            //collections — a client can be non-null here and still be a dead object
            pending.RemoveAll(IsDestroyed);

            pending.Sort(CompareOrder);

            for (int i = 0; i < pending.Count; i++)
            {
                ISFEditorClient client = pending[i];

                //Rebuilding an earlier client can destroy a later one (a planet rebuild
                //takes its shells with it), so re-check rather than trusting the snapshot
                if (IsDestroyed(client))
                    continue;

                //One client throwing must not strand the rest of the tick, or a single bad
                //planet silently freezes every preview in the scene
                try
                {
                    client.EditorRebuild();
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e, client as Object);
                }
            }

            pending.Clear();
        }
        finally
        {
            ticking = false;
        }
    }

    private static bool IsDestroyed(ISFEditorClient _client)
    {
        //The Unity-lifetime check needs the Object overload of ==, not reference equality
        Object asObject = _client as Object;
        return asObject == null;
    }

    private static int CompareOrder(ISFEditorClient _a, ISFEditorClient _b)
    {
        return ((int)_a.RebuildOrder).CompareTo((int)_b.RebuildOrder);
    }
}
#endif
