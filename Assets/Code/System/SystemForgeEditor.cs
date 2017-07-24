using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class SystemForgeEditor : EditorWindow
{
    private Vector2 mousePos;
    private Vector2 mapCoord;   //This stores the pixel position of an object added to the map...
    private Vector2 sysCoord;   //This is the position of the planet relative to the center of the map in AU...

    //Map editor stuff...
    private int         mapScaleSliderValue     = 2;
    private int         mapScaleSliderMinValue  = 0;
    private int         mapScaleSLiderMaxValue  = 3;

    private int         mapSize                 = 200;          //au
    private int         scaledMapSize           = 0;            //pixels
    private int         mapEditorSize           = 800;          //pixels
    private float[]     scaleValues             = { 0.0001f,    //au/pixel
                                                    0.001f,
                                                    0.01f,
                                                    0.1f };
    private float[]     gridLineSpacing         = { 0.1f,       //au
                                                    0.1f,       
                                                    1.0f,
                                                    10.0f };
    private int         scaledGridLineSpacing   = 0;            //pixels

    //These are for defining  the sizes of labels, sliders, etc...all the cosmetic stuff
    private Vector2     labelSize               = new Vector2(256, 32);             //pixels
    private Vector2     mapScaleSliderSize      = new Vector2(800, 32);             //pixels


    private Vector2     mapEditorPos            = new Vector2(320, 16);
    private Vector2     mapScaleSliderPos       = new Vector2(320, 820);



    private Vector2     mapScrollBarPosition;

    private Rect        mapRect;               //The map is where planet placement takes place.
    private Rect        mapEditorRect;         //The map editor is a scrollable window that houses the map.
    private Rect        mapScaleSliderBarRect;

    Event currentEvent;

    List<Rect>          planetRectList     = new List<Rect>();
    List<GameObject>    planetObjectList   = new List<GameObject>();

    [MenuItem("Window/System Forge")]
    public static void ShowWindow()
    {
        SystemForgeEditor editor = (SystemForgeEditor)EditorWindow.GetWindow(typeof(SystemForgeEditor));
        editor.Init();
        editor.title            = "System Forge";
        editor.minSize          = new Vector2(1124, 840);
        editor.wantsMouseMove   = true;
    }

    void Init()
    {
        //The map editors scroll bars need to be initialized to the center of the map...
        scaledMapSize         = Mathf.RoundToInt(mapSize / scaleValues[mapScaleSliderValue]);
        scaledGridLineSpacing = Mathf.RoundToInt(gridLineSpacing[mapScaleSliderValue] / scaleValues[mapScaleSliderValue]);
        mapScrollBarPosition  = new Vector2((scaledMapSize / 2.0f) - (mapEditorSize / 2.0f), (scaledMapSize / 2.0f) - (mapEditorSize / 2.0f));
    }

    void OnGUI()
    {
        currentEvent = Event.current;

       if (currentEvent.type == EventType.MouseMove)
            Repaint();

        UpdateScaledValues();

        DrawMapEditor();

        MouseInMap();

        GUI.Label(new Rect(8, 8, labelSize.x, labelSize.y), "Map Scale Slider Value: " + mapScaleSliderValue.ToString());
        GUI.Label(new Rect(8, 28, labelSize.x, labelSize.y), "Editor Mouse Position: " + Event.current.mousePosition.ToString());
        GUI.Label(new Rect(8, 48, labelSize.x, labelSize.y), "Pixel Coord: " + mapCoord.ToString());
        GUI.Label(new Rect(8, 68, labelSize.x, labelSize.y), "System Coord in AU : " + new Vector2(sysCoord.x, sysCoord.y).ToString("0.####"));
        GUI.Label(new Rect(8, 88, labelSize.x, labelSize.y), "Scaled Map Size: " + scaledMapSize.ToString());
        GUI.Label(new Rect(8, 108, labelSize.x, labelSize.y), "Slider Bar Value: " + mapScrollBarPosition.ToString());
    }

    void MouseInMap()
    {
        mousePos = currentEvent.mousePosition;
        mapCoord = new Vector2(currentEvent.mousePosition.x + mapScrollBarPosition.x - mapEditorPos.x, currentEvent.mousePosition.y + mapScrollBarPosition.y - mapEditorPos.y);
        sysCoord = new Vector2((mapCoord.x - mapRect.center.x) * scaleValues[mapScaleSliderValue], (mapRect.center.y - mapCoord.y) * scaleValues[mapScaleSliderValue]);

        if (!mapEditorRect.Contains(mousePos))
        {
            mapCoord.x = 0;
            sysCoord.x = 0;
        }

        if (!mapEditorRect.Contains(mousePos))
        {
            mapCoord.y = 0;
            sysCoord.y = 0;
        }
    }

    void UpdateScaledValues()
    {
        scaledMapSize         = Mathf.RoundToInt(mapSize / scaleValues[mapScaleSliderValue]);
        scaledGridLineSpacing = Mathf.RoundToInt(gridLineSpacing[mapScaleSliderValue] / scaleValues[mapScaleSliderValue]);
    }

    void DrawMapEditor()
    {
        mapRect               = new Rect(new Vector2(0.0f, 0.0f), new Vector2(scaledMapSize, scaledMapSize));
        mapEditorRect         = new Rect(mapEditorPos, new Vector2(mapEditorSize, mapEditorSize));
        mapScaleSliderBarRect = new Rect(mapScaleSliderPos, mapScaleSliderSize);

        mapScrollBarPosition = GUI.BeginScrollView(mapEditorRect, mapScrollBarPosition, mapRect, true, true);

        GUI.Box(mapRect, "Map Editor");
        //Since the map has been created, draw the grid...

        DrawMapGrid();

        GUI.EndScrollView();

        mapScaleSliderValue = Mathf.RoundToInt(GUI.HorizontalSlider(mapScaleSliderBarRect, mapScaleSliderValue, mapScaleSliderMinValue, mapScaleSLiderMaxValue));
    }

    void DrawMapGrid()
    {
        int numberOfLines = Mathf.RoundToInt(scaledMapSize / scaledGridLineSpacing);

        for (int i = 1; i < numberOfLines; i++)
        {
            if ((i % 5) == 0)
                Handles.color = Color.red;
            else
                Handles.color = Color.grey;

            //Horizontal Lines
            Handles.DrawLine(new Vector3(mapRect.xMin, scaledGridLineSpacing * i, 0.0f), new Vector3(mapRect.xMax, scaledGridLineSpacing * i, 0.0f));
            //Vertical Lines
            Handles.DrawLine(new Vector3(scaledGridLineSpacing * i, mapRect.yMin, 0.0f), new Vector3(scaledGridLineSpacing * i, mapRect.yMax, 0.0f));
        }
    }


    /*public void Init()
{
    mapEditor = new Rect(mapEditorPos, mapEditorSize);

    map = new Rect(mapEditorPos, mapSize);

    lineTexture = new Texture2D(1, 1);
    lineTexture.SetPixel(0, 0, Color.grey);
    lineTexture.Apply();

    sunTexture = Resources.Load("sun_01") as Texture;

    mapScrollBarPosition = new Vector2((mapSize.x / 2.0f) - (mapEditorSize.x / 2.0f), (mapSize.y / 2.0f) - (mapEditorSize.y / 2.0f));
}*/


    /*evt = Event.current;

    mousePos = evt.mousePosition;
    mapCoord = new Vector2(evt.mousePosition.x + mapScrollBarPosition.x - mapEditorPos.x, evt.mousePosition.y + mapScrollBarPosition.y - mapEditorPos.y);
    sysCoord = new Vector2((mapCoord.x - (map.center.x - mapEditorPos.x)) * 0.01f, ((map.center.y - mapEditorPos.y) - mapCoord.y) * 0.01f);

    BeginWindows();

    MouseInMap();

    if(GUILayout.Button("Create System!", GUILayout.Width(312)))
    {
        Debug.Log("planet object #: " + planetRectList.Count);
        for (int i = 0; i < planetRectList.Count; i++)
        {
            planetObjectList.Add(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            planetObjectList[i].name = "Planet " + i;
            planetObjectList[i].transform.position = new Vector3(planetRectList[i].position.x, 0.0f, planetRectList[i].position.y);
        }
    }

    if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseUp)
        Repaint();

    GUILayout.Label("Star Details", EditorStyles.boldLabel);
    //mass = EditorGUILayout.FloatField("Mass " + i + ": ", mass, GUILayout.Width(256));
    //radius = EditorGUILayout.FloatField("Radius " + i + ": ", radius, GUILayout.Width(256));
    //luminosity = EditorGUILayout.FloatField("Luminosity " + i + ": ", luminosity, GUILayout.Width(256));

    mapScrollBarPosition = GUI.BeginScrollView(mapEditor, mapScrollBarPosition, map, true, true);

    GUI.Box(map, "Map");
    DrawMapGrid();

    AddEditorObject(mapCoord.x + mapEditorPos.x - 16, mapCoord.y + mapEditorPos.y - 16);

    DrawObject();

    GUI.EndScrollView();

    EndWindows();

    EditorGUILayout.LabelField("Editor Mouse Position: ", Event.current.mousePosition.ToString());
    EditorGUILayout.LabelField("Pixel Coord: ", mapCoord.ToString());
    EditorGUILayout.LabelField("System Coord in AU : ", new Vector2(sysCoord.x, sysCoord.y).ToString("0.##"));
    EditorGUILayout.LabelField("Rect Object: ", planetRectList.Count.ToString());*/
    //}

    /*void MouseInMap()
    {
        if (!mapEditor.Contains(mousePos))
        {
            mapCoord.x  = 0;
            sysCoord.x = 0;
        }

        if (!mapEditor.Contains(mousePos))
        {
            mapCoord.y  = 0;
            sysCoord.y = 0;
        }
    }

    void AddEditorObject(float _x, float _z)
    {
        if (evt.type == EventType.mouseDown && evt.button == 0 && mapEditor.Contains(mousePos))
        {
            Debug.Log("True");

            Rect temp = new Rect(_x, _z, 32, 32);   
            
            planetRectList.Add(temp);

            GameObject systemObject = new GameObject("Object", typeof(SpriteRenderer));
            systemObject.transform.position = new Vector3(sysCoord.x * 0.01f, 0.0f, sysCoord.y * 0.01f);

            planetObjectList.Add(systemObject);
        }
    }

    void AddPlanetObject(float _x, float _z)
    {
        if (evt.type == EventType.MouseDown && mapEditor.Contains(mousePos))
        {
            Debug.Log("True");

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            temp.hideFlags = HideFlags.HideInHierarchy;
            temp.transform.position = new Vector3(_x * 0.01f, 0.0f, _z * 0.01f);

            planetObjectList.Add(temp);
        }
    }

    void DrawObject()
    {
        for (int i = 0; i < planetRectList.Count; i++)
        {
            GUI.DrawTexture(planetRectList[i], sunTexture);
        }
    }

    void DrawMapGrid()
    {
        map = new Rect(mapEditor.xMin, mapEditor.yMin, mapSize.x, mapSize.y);

        int numberOfLines = (int)(mapSize.x / gridLineSpacing);

        for (int i = 1; i < numberOfLines; i++)
        {
            if ((i % 5) == 0)
                Handles.color = Color.red;
            else
                Handles.color = Color.grey;

            //Horizontal Lines
            Handles.DrawLine(new Vector3(map.xMin, mapEditor.position.y + (gridLineSpacing * i), 0.0f), new Vector3(map.xMax, mapEditor.position.y + (gridLineSpacing * i), 0.0f));
            //Vertical Lines
            Handles.DrawLine(new Vector3(mapEditor.position.x + (gridLineSpacing * i), map.yMin, 0.0f), new Vector3(mapEditor.position.x + (gridLineSpacing * i), map.yMax, 0.0f));
        }
    }*/
}
