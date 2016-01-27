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
        public bool isLight;
        public GeneratablePrefab.AttachAnchor anchorType;
        public Bounds bounds;
    }


#region Fields
    // The number of physics collisions to create
    public int complexityLevelToCreate = 100;
    public int numCeilingLights = 10;
    public int desiredSpacing = 100;
    public SemanticObjectSimple wallPrefab;
    public SemanticObjectSimple floorPrefab;
    public GameObject DEBUG_testCubePrefab = null;
    public TextMesh DEBUG_testGridPrefab = null;
    public Vector3 roomDim = new Vector3(10f, 10f, 10f);
    public List<PrefabInfo> availablePrefabs = new List<PrefabInfo>();
    public List<PrefabInfo> ceilingLightPrefabs = new List<PrefabInfo>();
    public List<PrefabInfo> groundPrefabs = new List<PrefabInfo>();
    public float gridDim = 0.4f;

    private int curComplexity = 0;
    private int curRoomWidth = 0;
    private int curRoomLength = 0;
    private float curRoomHeight = 0f;
    private Vector3 roomCornerPos = Vector3.zero;
    private Transform curRoom = null;
    private int failures = 0; // Counter to avoid infinite loops if we can't place anything
    private List<HeightPlane> allHeightPlanes = new List<HeightPlane>();
    private static ProceduralGeneration _Instance = null;
#endregion

#region Properties
    public static ProceduralGeneration Instance
    {
        get { return _Instance; }
    }
#endregion

#region Unity Callbacks
    private void Awake()
    {
        _Instance = this;
    }

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
            if (json["num_ceiling_lights"].Tag == SimpleJSON.JSONBinaryTag.IntValue)
                numCeilingLights = json["num_ceiling_lights"].AsInt;
            if (json["room_width"].IsNumeric())
                roomDim.x = json["room_width"].AsFloat;
            if (json["room_height"].IsNumeric())
                roomDim.y = json["room_height"].AsFloat;
            if (json["room_length"].IsNumeric())
                roomDim.z = json["room_length"].AsFloat;
            if (json["desired_spacing"].Tag == SimpleJSON.JSONBinaryTag.IntValue)
                desiredSpacing = json["desired_spacing"].AsInt;
        }

        ceilingLightPrefabs = availablePrefabs.FindAll(((PrefabInfo info)=>{return info.anchorType == GeneratablePrefab.AttachAnchor.Ceiling && info.isLight;}));
        groundPrefabs = availablePrefabs.FindAll(((PrefabInfo info)=>{return info.anchorType == GeneratablePrefab.AttachAnchor.Ground;}));

        // Create rooms
        CreateRoom(roomDim, Vector3.zero);

        // Create grid to populate objects
        curComplexity = 0;
        failures = 0;

        allHeightPlanes.Clear();
        HeightPlane basePlane = new HeightPlane();
        allHeightPlanes.Add(basePlane);
        // TODO: Properly factor in wall/floor/ceiling thickness. Right now just assuming it's 2.0f
        curRoomWidth = Mathf.FloorToInt((roomDim.x - 2.0f) / gridDim);
        curRoomLength = Mathf.FloorToInt((roomDim.z - 2.0f) / gridDim);
        curRoomHeight = roomDim.y - 2.0f;
        basePlane.dimWidth = curRoomWidth;
        basePlane.dimLength = curRoomLength;
        basePlane.cornerPos = roomCornerPos;
        for(int i = 0; i < curRoomWidth; ++i)
        {
            for(int j = 0; j < curRoomLength; ++j)
            {
                GridInfo newGridInfo = new GridInfo();
                newGridInfo.x = i;
                newGridInfo.y = j;
                basePlane.ModifyGrid(newGridInfo, i, j, curRoomWidth, curRoomLength);
                basePlane.myGridSpots.Add(newGridInfo);
            }
        }

        // Keep creating objects until we are supposed to stop
        // TODO: Create a separate plane to map ceiling placement
        for(int i = 0; i < numCeilingLights && failures < 200; ++i)
            AddObjects(ceilingLightPrefabs);
        failures = 0;
        while(!IsDone())
            AddObjects(groundPrefabs);

        if (DEBUG_testGridPrefab != null)
        {
            foreach(GridInfo g in basePlane.myGridSpots)
            {
                TextMesh test = GameObject.Instantiate<TextMesh>(DEBUG_testGridPrefab);
                test.text = string.Format("  {0}\n{2}  {1}\n  {3}", g.upSquares, g.leftSquares, g.rightSquares, g.downSquares);
                test.color = g.inUse ? Color.red: Color.cyan;
                test.transform.position = roomCornerPos + new Vector3(gridDim * g.x, 0.0f, gridDim * g.y);
                test.name = string.Format("{0}: ({1},{2})", DEBUG_testGridPrefab.name, g.x, g.y);
                test.transform.localScale = gridDim * Vector3.one;
            }
        }
    }


    private bool TryPlaceGroundObject(Bounds bounds, GeneratablePrefab.AttachAnchor anchorType, out int finalX, out int finalY, out HeightPlane whichPlane)
    {
        finalX = 0;
        finalY = 0;
        whichPlane = null;
        int boundsWidth = Mathf.CeilToInt(2 * bounds.extents.x / gridDim);
        int boundsLength = Mathf.CeilToInt(2 * bounds.extents.z / gridDim);
        float boundsHeight = bounds.extents.y;

        bool foundValid = false;
        foreach(HeightPlane curHeightPlane in allHeightPlanes)
        {
            // Make sure we aren't hitting the ceiling
            if (curHeightPlane.planeHeight + boundsHeight >= curRoomHeight)
                continue;
            // Only get grid squares which are valid to place on.
            List<GridInfo> validValues = curHeightPlane.myGridSpots.FindAll((GridInfo info)=>{return info.rightSquares >= (boundsWidth-1) && info.downSquares > (boundsLength-1) && !info.inUse;});
            while(validValues.Count > 0 && !foundValid)
            {
                int randIndex = Random.Range(0, validValues.Count);
                GridInfo testInfo = validValues[randIndex];
                validValues.RemoveAt(randIndex);
                if (curHeightPlane.TestGrid(testInfo, boundsWidth, boundsLength))
                {
                    Vector3 centerPos = roomCornerPos + new Vector3(gridDim * (testInfo.x + (0.5f * boundsWidth)), 0.1f+boundsHeight, gridDim * (testInfo.y + (0.5f * boundsLength)));
                    if (anchorType == GeneratablePrefab.AttachAnchor.Ceiling)
                        centerPos.y = roomCornerPos.y + curRoomHeight - (0.1f+boundsHeight);
                    if (Physics.CheckBox(centerPos, bounds.extents))
                    {
                        // Found another object here, let the plane know that there's something above messing with some of the squares
                        // TODO: Figure out which squares to remove from contention on this plane.
                        string debugText = "";
                        Collider[] hitObjs = Physics.OverlapBox(centerPos, bounds.extents);
                        foreach(Collider col in hitObjs)
                        {
                            debugText += col.gameObject.name + ", ";
                            curHeightPlane.RestrictBounds(col.bounds);
                        }
                        Debug.Log("Unexpected objects: (" + debugText + ") at " + testInfo);
                    }
                    else
                    {
//                        Debug.LogFormat("Selecting ({0},{1}) which has ({2},{3}) to place ({4},{5})", testInfo.x, testInfo.y, testInfo.rightSquares, testInfo.downSquares, boundsWidth, boundsLength);
                        finalX = testInfo.x;
                        finalY = testInfo.y;
                        whichPlane = curHeightPlane;
                        foundValid = true;
                        return foundValid;
                    }
                }
            }
        }

        return foundValid;
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

    // Save out core information so we can decide whether to place the objects dynamically even if they aren't loaded yet
    private void CompileListOfProceduralComponents(bool shouldRecomputePrefabInformation)
    {
        GeneratablePrefab [] allThings = Resources.LoadAll<GeneratablePrefab>("");
        const string resPrefix = "Resources/";
        availablePrefabs.Clear();
        foreach(GeneratablePrefab prefab in allThings)
        {
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            if (!string.IsNullOrEmpty(assetPath) && prefab.shouldUse)
            {
                if (shouldRecomputePrefabInformation)
                    prefab.ProcessPrefab();
                PrefabInfo newInfo = new PrefabInfo();
                newInfo.fileName = assetPath.Substring(assetPath.LastIndexOf(resPrefix) + resPrefix.Length);
                newInfo.fileName = newInfo.fileName.Substring(0, newInfo.fileName.LastIndexOf("."));
                newInfo.complexity = prefab.myComplexity;
                newInfo.bounds = prefab.myBounds;
                newInfo.isLight = prefab.isLight;
                newInfo.anchorType = prefab.attachMethod;
                availablePrefabs.Add(newInfo);
            }
        }
    }
#endif

    private void AddObjects(List<PrefabInfo> prefabList)
    {
        // TODO: Seed this random selection?
        PrefabInfo info = prefabList[Random.Range(0, prefabList.Count)];

        // Find a spot to place this object
        int spawnX, spawnZ;
        HeightPlane targetHeightPlane;
        if (TryPlaceGroundObject(info.bounds, info.anchorType, out spawnX, out spawnZ, out targetHeightPlane))
        {
            int boundsWidth = Mathf.CeilToInt(2 * info.bounds.extents.x / gridDim) - 1;
            int boundsLength = Mathf.CeilToInt(2 * info.bounds.extents.z / gridDim) - 1;
            Vector3 centerPos = roomCornerPos + new Vector3(gridDim * (spawnX + (0.5f * boundsWidth)), 0.1f+info.bounds.extents.y, gridDim * (spawnZ + (0.5f * boundsLength)));
            if (info.anchorType == GeneratablePrefab.AttachAnchor.Ceiling)
                centerPos.y = roomCornerPos.y + curRoomHeight - (0.1f+info.bounds.extents.y);

            GameObject newPrefab = Resources.Load<GameObject>(info.fileName);
            // TODO: Factor in complexity to the arrangement algorithm?
            curComplexity += info.complexity;

            GameObject newInstance = Object.Instantiate<GameObject>(newPrefab.gameObject);
            newInstance.transform.position = centerPos - info.bounds.center;
            newInstance.name = string.Format("{0} #{1}", newPrefab.name, (curRoom != null) ? curRoom.childCount.ToString() : "?");

            // Create test cube
            if (DEBUG_testCubePrefab != null)
            {
                GameObject testCube = Object.Instantiate<GameObject>(DEBUG_testCubePrefab);
                testCube.transform.localScale = 2 * info.bounds.extents;
                testCube.transform.position = centerPos;
                testCube.name = string.Format("Cube {0}", newInstance.name);
                testCube.transform.SetParent(curRoom);
            }

            Debug.LogFormat("{0}: @{1} R:{2} G:{3} BC:{4}", info.fileName, newInstance.transform.position, roomCornerPos, new Vector3(gridDim * spawnX, info.bounds.extents.y, gridDim * spawnZ), info.bounds.center);
            if (curRoom != null)
                newInstance.transform.SetParent(curRoom);

            // TODO: For stackable objects, create a new height plane to stack
            if (info.anchorType == GeneratablePrefab.AttachAnchor.Ground)
                targetHeightPlane.UpdateGrid(spawnX, spawnZ, boundsWidth, boundsLength);
        }
        else
            // TODO: Mark item as unplaceable and continue with smaller objects?
            ++failures;
    }

    public void CreateRoom(Vector3 roomDimensions, Vector3 roomCenter)
    {
        curRoom = new GameObject("New Room").transform;
        roomCornerPos = roomCenter - (0.5f *roomDimensions);
        roomCornerPos.x = roomCornerPos.x + 1.0f;
        roomCornerPos.y = 0f;
        roomCornerPos.z = roomCornerPos.z + 1.0f;

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
        GameObject floorObj = GameObject.Instantiate(floorPrefab.gameObject);
        floorObj.transform.localScale = new Vector3(roomDimensions.x, 1.0f, roomDimensions.z);
        floorObj.transform.position = roomCenter;
        floorObj.name = "Floor: " + floorObj.name;
        floorObj.transform.SetParent(curRoom);

        // Create ceiling
        // TODO: Use different prefab for ceiling?
        GameObject ceilingObj = GameObject.Instantiate(floorPrefab.gameObject);
        ceilingObj.transform.localScale = new Vector3(roomDimensions.x, 1.0f, roomDimensions.z);
        ceilingObj.transform.position = roomCenter + roomDimensions.y * Vector3.up;
        ceilingObj.name = "Ceiling: " + ceilingObj.name;
        ceilingObj.transform.SetParent(curRoom);
    }

    public bool IsDone()
    {
        // TODO: Find a better metric for completion
        return curComplexity >= complexityLevelToCreate || failures > 30;
    }
}