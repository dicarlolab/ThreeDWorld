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
    public int maxPlacementAttempts = 300;
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

    public List<WallArray> wallSegmentList = new List<WallArray>();
    public float WALL_WIDTH = 1.0f;
    public int NUM_ROOMS = 1;
    public int MAX_NUM_TWISTS = 4;
    public int MIN_SPACING = 10;
    public Material wallMaterial = null;
    public Material wallTrimMaterial = null;
    public Material windowMaterial = null;
    public Material windowTrimMaterial = null;

    private int _curRandSeed = 0;
    private int _curComplexity = 0;
    private int _curRoomWidth = 0;
    private int _curRoomLength = 0;
    private float _curRoomHeight = 0f;
    private Vector3 _roomCornerPos = Vector3.zero;
    private Transform _curRoom = null;
    private int _failures = 0; // Counter to avoid infinite loops if we can't place anything
    private List<HeightPlane> _allHeightPlanes = new List<HeightPlane>();
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
        roomDim.x = Mathf.Round(roomDim.x / gridDim) * gridDim;
        roomDim.z = Mathf.Round(roomDim.z / gridDim) * gridDim;
        CreateRoom(roomDim, new Vector3((roomDim.x-1) * 0.5f,0,(roomDim.z-1) * 0.5f));

        // Create grid to populate objects
        _curComplexity = 0;
        _failures = 0;

        _allHeightPlanes.Clear();
        HeightPlane basePlane = new HeightPlane();
        basePlane.gridDim = gridDim;
        _allHeightPlanes.Add(basePlane);
        _curRoomWidth = Mathf.FloorToInt(roomDim.x / gridDim);
        _curRoomLength = Mathf.FloorToInt(roomDim.z / gridDim);
        _curRoomHeight = roomDim.y;
        basePlane.cornerPos = _roomCornerPos;
        basePlane.Clear(_curRoomWidth, _curRoomLength);

        SubdivideRoom();

        // Keep creating objects until we are supposed to stop
        // TODO: Create a separate plane to map ceiling placement
        for(int i = 0; i < numCeilingLights && _failures < maxPlacementAttempts; ++i)
            AddObjects(ceilingLightPrefabs);
        _failures = 0;
        while(!IsDone())
            AddObjects(groundPrefabs);

        if (DEBUG_testGridPrefab != null)
        {
            GameObject child = new GameObject("TEST GRIDS");
            child.transform.SetParent(_curRoom);
            foreach(GridInfo g in basePlane.myGridSpots)
            {
                TextMesh test = GameObject.Instantiate<TextMesh>(DEBUG_testGridPrefab);
                test.transform.SetParent(child.transform);
//                test.text = string.Format("  {0}\n{2}  {1}\n  {3}", g.upSquares, g.leftSquares, g.rightSquares, g.downSquares);
                test.text = string.Format("{0},{1}", g.x, g.y);
                test.color = g.inUse ? Color.red: Color.cyan;
                test.transform.position = _roomCornerPos + new Vector3(gridDim * g.x, 0.0f, gridDim * g.y);
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
        foreach(HeightPlane curHeightPlane in _allHeightPlanes)
        {
            // Make sure we aren't hitting the ceiling
            if (curHeightPlane.planeHeight + boundsHeight >= _curRoomHeight)
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
                    Vector3 centerPos = _roomCornerPos + new Vector3(gridDim * (testInfo.x + (0.5f * boundsWidth)), 0.1f+boundsHeight, gridDim * (testInfo.y + (0.5f * boundsLength)));
                    if (anchorType == GeneratablePrefab.AttachAnchor.Ceiling)
                        centerPos.y = _roomCornerPos.y + _curRoomHeight - (0.1f+boundsHeight);
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

    public void SubdivideRoom()
    {
        wallSegmentList.Clear();
        HeightPlane curPlane = _allHeightPlanes[0];

        // Build initial walls
        _failures = 0;
        WallArray.WALL_HEIGHT = _curRoomHeight;
        WallArray.WALL_WIDTH = WALL_WIDTH;
        WallArray.MIN_SPACING = Mathf.RoundToInt(MIN_SPACING / gridDim);
        WallArray.NUM_TWISTS = MAX_NUM_TWISTS;
        WallArray.WALL_MATERIAL = wallMaterial;
        WallArray.TRIM_MATERIAL = wallTrimMaterial ;
        WallArray.WINDOW_MATERIAL = windowMaterial;
        WallArray.WINDOW_TRIM_MATERIAL = windowTrimMaterial ;
        wallSegmentList.Add(WallArray.CreateRoomOuterWalls(curPlane));

        while(wallSegmentList.Count < NUM_ROOMS && _failures < 300)
        {
            WallArray newWallSet = WallArray.PlotNewWallArray(curPlane, string.Format("Wall Segment {0}", wallSegmentList.Count));
            if (newWallSet == null)
            {
                // No possible segments! Start over!
                Debug.LogWarning("Starting over!!!");
                curPlane.Clear();
                _failures++;
                while(wallSegmentList.Count > 1)
                    wallSegmentList.RemoveRange(1, wallSegmentList.Count - 1);
                if (_failures > 300)
                    break;
                continue;
            }
            else
            {
                wallSegmentList.Add(newWallSet);
                newWallSet.MarkIntersectionPoints(wallSegmentList);
            }
        }

        for(int i = 0; i < wallSegmentList.Count; ++i)
        {
            WallArray curWallSeg = wallSegmentList[i];
            curWallSeg.PlaceDoorsAndWindows(i != 0);

            // Actually build object meshes
            curWallSeg.ConstructWallSegments(_curRoom);
        }
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
            Vector3 centerPos = _roomCornerPos + new Vector3(gridDim * (spawnX + (0.5f * boundsWidth)), 0.1f+info.bounds.extents.y, gridDim * (spawnZ + (0.5f * boundsLength)));
            if (info.anchorType == GeneratablePrefab.AttachAnchor.Ceiling)
                centerPos.y = _roomCornerPos.y + _curRoomHeight - (0.1f+info.bounds.extents.y);

            GameObject newPrefab = Resources.Load<GameObject>(info.fileName);
            // TODO: Factor in complexity to the arrangement algorithm?
            _curComplexity += info.complexity;

            GameObject newInstance = Object.Instantiate<GameObject>(newPrefab.gameObject);
            newInstance.transform.position = centerPos - info.bounds.center;
            newInstance.name = string.Format("{0} #{1}", newPrefab.name, (_curRoom != null) ? _curRoom.childCount.ToString() : "?");

            // Create test cube
            if (DEBUG_testCubePrefab != null)
            {
                GameObject testCube = Object.Instantiate<GameObject>(DEBUG_testCubePrefab);
                testCube.transform.localScale = 2 * info.bounds.extents;
                testCube.transform.position = centerPos;
                testCube.name = string.Format("Cube {0}", newInstance.name);
                testCube.transform.SetParent(_curRoom);
            }

            Debug.LogFormat("{0}: @{1} R:{2} G:{3} BC:{4}", info.fileName, newInstance.transform.position, _roomCornerPos, new Vector3(gridDim * spawnX, info.bounds.extents.y, gridDim * spawnZ), info.bounds.center);
            if (_curRoom != null)
                newInstance.transform.SetParent(_curRoom);

            // TODO: For stackable objects, create a new height plane to stack
            if (info.anchorType == GeneratablePrefab.AttachAnchor.Ground)
                targetHeightPlane.UpdateGrid(spawnX, spawnZ, boundsWidth, boundsLength);
        }
        else
            // TODO: Mark item as unplaceable and continue with smaller objects?
            ++_failures;
    }

    public void CreateRoom(Vector3 roomDimensions, Vector3 roomCenter)
    {
        _curRoom = new GameObject("New Room").transform;
        _roomCornerPos = (roomCenter - (0.5f *roomDimensions)) + (gridDim * 0.5f * Vector3.one);
        _roomCornerPos.y = 0f;

        // Create floor
        GameObject floorObj = GameObject.Instantiate(floorPrefab.gameObject);
        floorObj.transform.localScale = new Vector3(roomDimensions.x, 1.0f, roomDimensions.z);
        floorObj.transform.position = roomCenter;
        floorObj.name = "Floor: " + floorObj.name;
        floorObj.transform.SetParent(_curRoom);

        // Create ceiling
        // TODO: Use different prefab for ceiling?
        GameObject ceilingObj = GameObject.Instantiate(ceilingPrefab.gameObject);
        ceilingObj.transform.localScale = new Vector3(roomDimensions.x, 1.0f, roomDimensions.z);
        ceilingObj.transform.position = roomCenter + roomDimensions.y * Vector3.up;
        ceilingObj.name = "Ceiling: " + ceilingObj.name;
        ceilingObj.transform.SetParent(_curRoom);
    }

    public bool IsDone()
    {
        // TODO: Find a better metric for completion
        return _curComplexity >= complexityLevelToCreate || _failures > maxPlacementAttempts;
    }
}