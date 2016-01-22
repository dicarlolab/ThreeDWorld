using UnityEngine;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ProceduralGeneration : MonoBehaviour
{
    [System.Serializable]
    public class PrefabInfo
    {
        public string fileName;
        public int complexity;
        public Bounds bounds;
    }

#region Fields
    // The number of physics collisions to create
    public int complexityLevelToCreate = 100;
    public int desiredSpacing = 100;
    public SemanticObjectSimple wallPrefab;
    public SemanticObjectSimple floorPrefab;
    public Vector3 roomDim = new Vector3(10f, 10f, 10f);
    public List<PrefabInfo> availablePrefabs = new List<PrefabInfo>();

    private int curComplexity = 0;
    private Transform curRoom = null;
    private List<Vector3> availableGridSpots = new List<Vector3>();
#endregion

#region Unity Callbacks
    private void Start()
    {
        Init();
    }
#endregion

    public void Init()
    {
        SimpleJSON.JSONClass json = SimulationManager.argsConfig;
        if (json != null)
        {
            // Override settings with those in config
            if (json["complexity"].Tag == SimpleJSON.JSONBinaryTag.IntValue)
                complexityLevelToCreate = json["complexity"].AsInt;
            if (json["room_width"].IsNumeric())
                roomDim.x = json["room_width"].AsFloat;
            if (json["room_height"].IsNumeric())
                roomDim.y = json["room_height"].AsFloat;
            if (json["room_length"].IsNumeric())
                roomDim.z = json["room_length"].AsFloat;
            if (json["desired_spacing"].Tag == SimpleJSON.JSONBinaryTag.IntValue)
                desiredSpacing = json["desired_spacing"].AsInt;
        }

        // Create rooms
        CreateRoom(roomDim, Vector3.zero);

        // Create grid to populate objects
        curComplexity = 0;
        availableGridSpots.Clear();
        float xDist = roomDim.x / desiredSpacing;
        float xStart = (roomDim.x * -0.5f) + (0.5f * xDist);
        float zDist = roomDim.z / desiredSpacing;
        float zStart = (roomDim.z * -0.5f) + (0.5f * zDist);
        for(int i = 0; i < desiredSpacing; ++i)
        {
            for(int j = 0; j < desiredSpacing; ++j)
            {
                availableGridSpots.Add(new Vector3(xStart + (xDist * i), 1.0f, zStart + (zDist * j)));
            }
        }

        // Keep creating objects until we are supposed to stop
        while(!IsDone())
            AddObjects();
    }

#if UNITY_EDITOR
    // Finds all prefabs that we can use and create a lookup table with relevant information
    [MenuItem ("Procedural Generation/SetupPrefabs Quick")]
    private static void SetupPrefabsQuick()
    {
        SetupPrefabs(false);
    }

    [MenuItem ("Procedural Generation/SetupPrefabs Full")]
    private static void SetupPrefabsFull()
    {
        SetupPrefabs(true);
    }

    public static void SetupPrefabs(bool shouldRecompute)
    {
        ProceduralGeneration [] allThings = Resources.LoadAll<ProceduralGeneration>("");
        if (allThings != null && allThings.Length > 0)
            allThings[0].CompileListOfProceduralComponents(shouldRecompute);
    }

    private void CompileListOfProceduralComponents(bool shouldRecomputePrefabInformation)
    {
        GeneratablePrefab [] allThings = Resources.LoadAll<GeneratablePrefab>("");
        const string resPrefix = "Resources/";
        availablePrefabs.Clear();
        foreach(GeneratablePrefab prefab in allThings)
        {
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            if (!string.IsNullOrEmpty(assetPath))
            {
                if (shouldRecomputePrefabInformation)
                    prefab.Process();
                PrefabInfo newInfo = new PrefabInfo();
                newInfo.fileName = assetPath.Substring(assetPath.LastIndexOf(resPrefix) + resPrefix.Length);
                newInfo.fileName = newInfo.fileName.Substring(0, newInfo.fileName.LastIndexOf("."));
                newInfo.complexity = prefab.myComplexity;
                newInfo.bounds = prefab.myBounds;
                availablePrefabs.Add(newInfo);
            }
        }
    }
#endif

    private void AddObjects()
    {
        // TODO: Seed this random selection?
        PrefabInfo info = availablePrefabs[Random.Range(0, availablePrefabs.Count)];
        GameObject newPrefab = Resources.Load<GameObject>(info.fileName);
        // TODO: Factor in complexity to the arrangement algorithm?
        curComplexity += info.complexity;

        // TODO: Figure out a smart way to place parts
        // For now we just place them in the center of a subdivided grid.
        GameObject newInstance = Object.Instantiate<GameObject>(newPrefab.gameObject);
        int gridIndex = Random.Range(0, availableGridSpots.Count);
        newInstance.transform.position = availableGridSpots[gridIndex];
        if (curRoom != null)
            newInstance.transform.SetParent(curRoom);
        availableGridSpots.RemoveAt(gridIndex);

    }

    public void CreateRoom(Vector3 roomDimensions, Vector3 roomCenter)
    {
        curRoom = new GameObject("New Room").transform;

        // Create walls
        Vector3 [] directions = {Vector3.forward, Vector3.back, Vector3.left, Vector3.right};
        for(int i = 0; i < 4; ++i)
        {
            Vector3 curDir = directions[i];
            Vector3 normalizedDir = (Vector3.Dot(roomDimensions, curDir) > 0) ? curDir : -curDir;
            GameObject wall = GameObject.Instantiate(wallPrefab.gameObject);
            wall.transform.position = roomCenter + (0.5f * Vector3.Dot(roomDimensions, curDir) * normalizedDir);
            Vector3 newScale = normalizedDir + roomDimensions - (Vector3.Dot(roomDimensions, curDir) * curDir);
            wall.transform.localScale = newScale;
            wall.transform.SetParent(curRoom);
        }

        // Create floor
        GameObject floor = GameObject.Instantiate(floorPrefab.gameObject);
        floor.transform.localScale = new Vector3(roomDimensions.x, 1.0f, roomDimensions.z);
        floor.transform.position = roomCenter;
        floor.transform.SetParent(curRoom);
    }

    public bool IsDone()
    {
        return curComplexity >= complexityLevelToCreate || availableGridSpots.Count == 0;
    }
}
