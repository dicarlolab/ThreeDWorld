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
    public int _curRandSeed = 0;
    public int complexityLevelToCreate = 100;
    public int numCeilingLights = 10;
    public int maxPlacementAttempts = 300;
    public SemanticObjectSimple wallPrefab;
    public SemanticObjectSimple floorPrefab;
    public SemanticObjectSimple ceilingPrefab;
    public GameObject DEBUG_testCubePrefab = null;
    public TextMesh DEBUG_testGridPrefab = null;
    public Vector3 roomDim = new Vector3(10f, 10f, 10f);
    public List<PrefabInfo> availablePrefabs = new List<PrefabInfo>();
    public List<PrefabInfo> ceilingLightPrefabs = new List<PrefabInfo>();
    public List<PrefabInfo> groundPrefabs = new List<PrefabInfo>();
    public float gridDim = 0.4f;
    public bool shouldUseGivenSeed = false;
    public int desiredRndSeed = -1;

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
            complexityLevelToCreate = json["complexity"].ReadInt(complexityLevelToCreate);
            numCeilingLights = json["num_ceiling_lights"].ReadInt(numCeilingLights);
            roomDim.x = json["room_width"].ReadFloat(roomDim.x);
            roomDim.y = json["room_height"].ReadFloat(roomDim.y);
            roomDim.z = json["room_length"].ReadFloat(roomDim.z);
            gridDim = json["grid_size"].ReadFloat(gridDim);
            shouldUseGivenSeed = json["random_seed"].ReadInt(ref desiredRndSeed) || shouldUseGivenSeed;
            maxPlacementAttempts = json["max_placement_attempts"].ReadInt(maxPlacementAttempts);
        }

        if (shouldUseGivenSeed)
            Random.seed = desiredRndSeed;
        else
            Random.seed = Random.Range(int.MinValue, int.MaxValue);
        _curRandSeed = Random.seed;
        Debug.LogWarning("Using random seed: " + _curRandSeed);
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
        for(int i = 0; i < numCeilingLights && failures < maxPlacementAttempts; ++i)
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

    public Vector3 startPoint = Vector3.zero;
    public Vector3 startSize = Vector3.one;
    public Material testMat;
    public Mesh testMesh;
    public Mesh lastCreated = null;
    public List<Vector3> testVerts;
    public List<Vector2> testUV;
    public List<int> testIndices;
    public void TestMesh()
    {
        if (testMesh == null && GetComponent<MeshFilter>() != null)
        {
            testMesh = GetComponent<MeshFilter>().sharedMesh;
        }
        if (testMesh == null)
            return;
        testVerts.Clear();
        testIndices.Clear();
        testUV.Clear();
        testVerts.AddRange(testMesh.vertices);
        testIndices.AddRange(testMesh.GetIndices(0));
        testUV.AddRange(testMesh.uv);
    }

    public void TestMesh2()
    {
        CreateWallMesh(startPoint, startSize);
    }

    public void CreateWallMesh(Vector3 start, Vector3 size, Transform parentObj = null)
    {
        int[] indexArray = {
            // Left/Right faces
            2,1,3,
            2,0,1,
//            10,1,3,
//            10,8,1,
            6,7,5,
            6,5,4,

            // Top/Bottom faces
            12,13,9,
            12,9,8,
            14,11,15,
            14,10,11,

            // Front/Back faces
            5,7,3,
            5,3,1,
            4,2,6,
            4,0,2
        };

        const int vertCount = 16;
        const int indexCount = 3 * 2 * 6;
        Vector3[] newVerts = new Vector3[vertCount];
        int[]     newIndices  = new int[indexCount];
        List<Vector2> newUVs = new List<Vector2>();
        Vector2 startUV = new Vector2(start.x + start.z, start.y);


        // Create vertices
        for(int isTopBottom = 0; isTopBottom < 2; isTopBottom++)
        {
            for(int i = 0; i < 2; ++i)
            {
                for(int j = 0; j < 2; ++j)
                {
                    for(int k = 0; k < 2; ++k)
                    {
                        newVerts[8 * isTopBottom + 4 * i + 2 * j + k] = start + new Vector3((i == 1) ? size.x : 0, (j == 1) ? size.y : 0, (k == 1) ? size.z : 0);
                        Vector2 newUV = Vector2.zero;
                        if (isTopBottom == 0)
                        {
                            newUV.y = startUV.y + (j * size.y);
                            newUV.x = startUV.x + (i * size.x) + (k * size.z);
                        }
                        else
                        {
                            newUV.y = start.x+start.y + (i * size.x) + (j * size.y);
                            newUV.x = start.z + 4f+(k * size.z);
                        }
                        newUVs.Add(newUV);
                    }
                }
            }
        }

        // Create faces
        for(int i = 0; i < indexArray.Length; ++i)
        {
            int index = indexArray[i];
            newIndices[i] = index;
        }

        Mesh newMesh = new Mesh();
        newMesh.vertices  = newVerts;
        newMesh.triangles = newIndices;
        newMesh.SetUVs(0, newUVs);
        lastCreated = newMesh;

        GameObject newObj = new GameObject(string.Format("Created Mesh @{0} with size{1}", start, size));
        if (parentObj != null)
            newObj.transform.SetParent(parentObj, false);
        MeshFilter meshFilter = newObj.AddComponent<MeshFilter>();
        meshFilter.mesh = newMesh;
        newObj.AddComponent<MeshRenderer>().material = testMat;
        BoxCollider col = newObj.AddComponent<BoxCollider>();
        col.size = size;
        col.center = start + (0.5f * size);
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
        GameObject ceilingObj = GameObject.Instantiate(ceilingPrefab.gameObject);
        ceilingObj.transform.localScale = new Vector3(roomDimensions.x, 1.0f, roomDimensions.z);
        ceilingObj.transform.position = roomCenter + roomDimensions.y * Vector3.up;
        ceilingObj.name = "Ceiling: " + ceilingObj.name;
        ceilingObj.transform.SetParent(curRoom);
    }

    public bool IsDone()
    {
        // TODO: Find a better metric for completion
        return curComplexity >= complexityLevelToCreate || failures > maxPlacementAttempts;
    }
}