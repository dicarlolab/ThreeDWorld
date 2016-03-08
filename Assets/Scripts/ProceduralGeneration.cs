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
        public List<GeneratablePrefab.StackableInfo> stackableAreas = new List<GeneratablePrefab.StackableInfo>();
    }


#region Fields
    // The number of physics collisions to create
    public int complexityLevelToCreate = 100;
    public int numCeilingLights = 10;
    public int minStackingBases = 0;
    public int forceStackedItems = 5;
    public int maxPlacementAttempts = 300;
    public SemanticObjectSimple floorPrefab;
    public SemanticObjectSimple ceilingPrefab;
    public GameObject DEBUG_testCubePrefab = null;
    public TextMesh DEBUG_testGridPrefab = null;
    public Vector3 roomDim = new Vector3(10f, 10f, 10f);
    public List<PrefabInfo> availablePrefabs = new List<PrefabInfo>();
    public List<string> disabledItems = new List<string>();
    public List<string> permittedItems = new List<string>();
    public float gridDim = 0.4f;
    public bool shouldUseStandardizedSize = false;
    public Vector3 standardizedSize = Vector3.one;
    public bool shouldUseGivenSeed = false;
    public int desiredRndSeed = -1;

    public float WALL_WIDTH = 1.0f;
    public float DOOR_WIDTH = 1.5f;
    public float DOOR_HEIGHT = 3.0f;
    public float WINDOW_SIZE_WIDTH = 2.0f;
    public float WINDOW_SIZE_HEIGHT = 2.0f;
    public float WINDOW_PLACEMENT_HEIGHT = 2.0f;
    public float WINDOW_SPACING = 6.0f;
    public float WALL_TRIM_HEIGHT = 0.5f;
    public float WALL_TRIM_THICKNESS = 0.01f;
    public float MIN_HALLWAY_SPACING = 5.0f;
    public int NUM_ROOMS = 1;
    public int MAX_NUM_TWISTS = 4;
    public List<Material> wallMaterials = new List<Material>();
    public Material floorMaterial = null;
    public Material ceilingMaterial = null;
    public Material wallTrimMaterial = null;
    public Material windowMaterial = null;
    public Material windowTrimMaterial = null;
    public bool showProcGenDebug = false;

    private int _curRandSeed = 0;
    private int _curComplexity = 0;
    private int _curRoomWidth = 0;
    private int _curRoomLength = 0;
    private bool _forceStackObject = false;
    private float _curRoomHeight = 0f;
    private Vector3 _roomCornerPos = Vector3.zero;
    private Transform _curRoom = null;
    private int _failures = 0; // Counter to avoid infinite loops if we can't place anything
    private List<WallArray> wallSegmentList = new List<WallArray>();
    private List<PrefabInfo> ceilingLightPrefabs = new List<PrefabInfo>();
    private List<PrefabInfo> groundPrefabs = new List<PrefabInfo>();
    private List<PrefabInfo> stackingPrefabs = new List<PrefabInfo>();
    public List<HeightPlane> _allHeightPlanes = new List<HeightPlane>();
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
            showProcGenDebug = json["debug_procedural_generation_logs"].ReadBool(showProcGenDebug);
            shouldUseGivenSeed = json["random_seed"].ReadInt(ref desiredRndSeed) || shouldUseGivenSeed;
            shouldUseStandardizedSize = json["should_use_standardized_size"].ReadBool(shouldUseStandardizedSize);
            standardizedSize = json["standardized_size"].ReadVector3(standardizedSize);
            json["disabled_items"].ReadList(ref disabledItems);
            json["permitted_items"].ReadList(ref permittedItems);
            complexityLevelToCreate = json["complexity"].ReadInt(complexityLevelToCreate);
            numCeilingLights = json["num_ceiling_lights"].ReadInt(numCeilingLights);
            minStackingBases = json["minimum_stacking_base_objects"].ReadInt(minStackingBases);
            forceStackedItems = json["minimum_objects_to_stack"].ReadInt(forceStackedItems);
            roomDim.x = json["room_width"].ReadFloat(roomDim.x);
            roomDim.y = json["room_height"].ReadFloat(roomDim.y);
            roomDim.z = json["room_length"].ReadFloat(roomDim.z);
            WALL_WIDTH = json["wall_width"].ReadFloat(WALL_WIDTH);
            DOOR_WIDTH = json["door_width"].ReadFloat(DOOR_WIDTH);
            DOOR_HEIGHT = json["door_height"].ReadFloat(DOOR_HEIGHT);
            WINDOW_SIZE_WIDTH = json["window_size_width"].ReadFloat(WINDOW_SIZE_WIDTH);
            WINDOW_SIZE_HEIGHT = json["window_size_height"].ReadFloat(WINDOW_SIZE_HEIGHT);
            WINDOW_PLACEMENT_HEIGHT = json["window_placement_height"].ReadFloat(WINDOW_PLACEMENT_HEIGHT);
            WINDOW_SPACING = json["window_spacing"].ReadFloat(WINDOW_SPACING);
            WALL_TRIM_HEIGHT = json["wall_trim_height"].ReadFloat(WALL_TRIM_HEIGHT);
            WALL_TRIM_THICKNESS = json["wall_trim_thickness"].ReadFloat(WALL_TRIM_THICKNESS);
            MIN_HALLWAY_SPACING = json["min_hallway_width"].ReadFloat(MIN_HALLWAY_SPACING);
            NUM_ROOMS = json["number_rooms"].ReadInt(NUM_ROOMS);
            MAX_NUM_TWISTS = json["max_wall_twists"].ReadInt(MAX_NUM_TWISTS);
            maxPlacementAttempts = json["max_placement_attempts"].ReadInt(maxPlacementAttempts);
            gridDim = json["grid_size"].ReadFloat(gridDim);
        }

        if (shouldUseGivenSeed)
            Random.seed = desiredRndSeed;
        else
            Random.seed = Random.Range(int.MinValue, int.MaxValue);
        _curRandSeed = Random.seed;
        Debug.LogWarning("Using random seed: " + _curRandSeed);

        List<PrefabInfo> filteredPrefabs = availablePrefabs.FindAll(((PrefabInfo info)=>{
            // Remove items that have been disallowed
            foreach(string itemName in disabledItems)
            {
                if (info.fileName.ToLowerInvariant().Contains(itemName.ToLowerInvariant()))
                    return false;
            }

            // If we have a list, only use items that are allowed in the list
            if (permittedItems.Count > 0)
            {
                foreach(string itemName in permittedItems)
                {
                    if (info.fileName.ToLowerInvariant().Contains(itemName.ToLowerInvariant()))
                        return true;
                }
                return false;
            }
            return true;
        }));
        // TODO: We're not filtering the ceiling lights, since we currently only have 1 prefab that works
        ceilingLightPrefabs = availablePrefabs.FindAll(((PrefabInfo info)=>{return info.anchorType == GeneratablePrefab.AttachAnchor.Ceiling && info.isLight;}));
        groundPrefabs = filteredPrefabs.FindAll(((PrefabInfo info)=>{return info.anchorType == GeneratablePrefab.AttachAnchor.Ground;}));
        stackingPrefabs = groundPrefabs.FindAll(((PrefabInfo info)=>{return info.stackableAreas.Count > 0;}));
        List<PrefabInfo> itemsForStacking = groundPrefabs.FindAll(((PrefabInfo info)=>{return true;}));

        // Create grid to populate objects
        _curComplexity = 0;
        _failures = 0;
        _forceStackObject = false;

        // Create rooms
        roomDim.x = Mathf.Round(roomDim.x / gridDim) * gridDim;
        roomDim.z = Mathf.Round(roomDim.z / gridDim) * gridDim;
        CreateRoom(roomDim, new Vector3((roomDim.x-1) * 0.5f,0,(roomDim.z-1) * 0.5f));

        _failures = 0;
        // Keep creating objects until we are supposed to stop
        // TODO: Create a separate plane to map ceiling placement
        for(int i = 0; (i - _failures) < numCeilingLights && _failures < maxPlacementAttempts; ++i)
            AddObjects(ceilingLightPrefabs);
        _failures = 0;

        if (showProcGenDebug && minStackingBases > 0)
            Debug.LogFormat("Stacking {0} objects bases: {1} types", minStackingBases, stackingPrefabs.Count);
        // Place stacking bases first so we can have more opportunities to stack on top
        for(int i = 0; (i - _failures) < minStackingBases && stackingPrefabs.Count > 0; ++i)
            AddObjects(stackingPrefabs);
        _failures = 0;

        if (showProcGenDebug && forceStackedItems > 0)
            Debug.LogFormat("Stacking {0} objects: {1} types", forceStackedItems, itemsForStacking.Count);
        // Place stacking bases first so we can have more opportunities to stack on top
        _forceStackObject = true;
        for(int i = 0; (i - _failures) < forceStackedItems && itemsForStacking.Count > 0; ++i)
            AddObjects(itemsForStacking);
        _failures = 0;
        _forceStackObject = false;

        if (showProcGenDebug)
            Debug.Log("Rest of objects");
        while(!IsDone())
            AddObjects(groundPrefabs);

        for(int i = 0; i < _allHeightPlanes.Count; ++i)
            DrawTestGrid(_allHeightPlanes[i]);
        Debug.Log("Final complexity: " + _curComplexity);
    }

    private bool TryPlaceGroundObject(Bounds bounds, GeneratablePrefab.AttachAnchor anchorType, out int finalX, out int finalY, out float modScale, out HeightPlane whichPlane)
    {
        finalX = 0;
        finalY = 0;
        whichPlane = null;
        modScale = 1.0f;
        if (shouldUseStandardizedSize)
        {
            modScale = Mathf.Min(
                standardizedSize.x / bounds.extents.x,
                standardizedSize.y / bounds.extents.y,
                standardizedSize.z / bounds.extents.z);
        }
        Bounds testBounds = new Bounds(bounds.center, modScale * 2f * bounds.extents);
        int boundsWidth = Mathf.CeilToInt(2 * testBounds.extents.x / gridDim);
        int boundsLength = Mathf.CeilToInt(2 * testBounds.extents.z / gridDim);
        float boundsHeight = testBounds.extents.y;

        List<int> randomPlanesOrder = new List<int>();
        int randomOrderValue = Random.Range(0, int.MaxValue);
        for(int i = 0; i < _allHeightPlanes.Count; ++i)
            randomPlanesOrder.Insert(randomOrderValue % (randomPlanesOrder.Count + 1), i);
        if (_forceStackObject)
            randomPlanesOrder.Remove(0);

        bool foundValid = false;
        foreach(int planeNum in randomPlanesOrder)
        {
            HeightPlane curHeightPlane = _allHeightPlanes[planeNum];
            // Make sure we aren't hitting the ceiling
            if (boundsHeight >= curHeightPlane.planeHeight || curHeightPlane.cornerPos.y + boundsHeight >= _curRoomHeight)
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
                    Vector3 centerPos = curHeightPlane.cornerPos + new Vector3(gridDim * (testInfo.x + (0.5f * boundsWidth)), 0.1f+boundsHeight, gridDim * (testInfo.y + (0.5f * boundsLength)));
                    if (anchorType == GeneratablePrefab.AttachAnchor.Ceiling)
                        centerPos.y = _roomCornerPos.y + _curRoomHeight - (0.1f+boundsHeight);
                    if (Physics.CheckBox(centerPos, testBounds.extents))
                    {
                        // Found another object here, let the plane know that there's something above messing with some of the squares
                        string debugText = "";
                        Collider[] hitObjs = Physics.OverlapBox(centerPos, testBounds.extents);
                        HashSet<string> hitObjNames = new HashSet<string>();
                        foreach(Collider col in hitObjs)
                        {
                            if (col.attachedRigidbody != null)
                                hitObjNames.Add(col.attachedRigidbody.name);
                            else
                                hitObjNames.Add(col.gameObject.name );
                            curHeightPlane.RestrictBounds(col.bounds);
                        }
                        foreach(string hitName in hitObjNames)
                            debugText += hitName + ", ";
                        if (showProcGenDebug)
                            Debug.LogFormat("Unexpected objects: ({0}) at ({1},{2}) on plane {3} with test {5} ext: {4}", debugText, testInfo.x, testInfo.y, curHeightPlane.name, testBounds.extents, centerPos);
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
        WallArray.MIN_SPACING = Mathf.RoundToInt(MIN_HALLWAY_SPACING / gridDim);
        WallArray.NUM_TWISTS = MAX_NUM_TWISTS;
        WallArray.WALL_MATERIALS = wallMaterials;
        WallArray.CURRENT_WALL_MAT_INDEX = 0;
        WallArray.TRIM_MATERIAL = wallTrimMaterial ;
        WallArray.WINDOW_MATERIAL = windowMaterial;
        WallArray.WINDOW_TRIM_MATERIAL = windowTrimMaterial;
        WallInfo.WINDOW_WIDTH = WINDOW_SIZE_WIDTH;
        WallInfo.WINDOW_HEIGHT = WINDOW_SIZE_HEIGHT;
        WallInfo.WINDOW_SPACING = WINDOW_SPACING;
        WallInfo.WINDOW_PLACEMENT_HEIGHT = WINDOW_PLACEMENT_HEIGHT;
        WallInfo.DOOR_WIDTH = DOOR_WIDTH;
        WallInfo.DOOR_HEIGHT = DOOR_HEIGHT;
        WallInfo.TRIM_HEIGHT = WALL_TRIM_HEIGHT;
        WallInfo.TRIM_THICKNESS = WALL_TRIM_THICKNESS;

        wallSegmentList.Add(WallArray.CreateRoomOuterWalls(curPlane));

        while(wallSegmentList.Count < NUM_ROOMS && _failures < 300)
        {
            WallArray newWallSet = WallArray.PlotNewWallArray(curPlane, string.Format("Wall Segment {0}", wallSegmentList.Count));
            if (newWallSet == null)
            {
                // No possible segments! Start over!
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
    // Setup a new prefab object from a model
    [MenuItem ("Procedural Generation/Create prefab model folders")]
    private static void CreatePrefabFromModel()
    {
        HashSet<GameObject> allSelected = new HashSet<GameObject>(Selection.gameObjects);
        foreach(Object obj in Selection.objects)
        {
            if (obj is GameObject)
                allSelected.Add(obj as GameObject);
            else if (obj is DefaultAsset)
                allSelected.UnionWith((obj as DefaultAsset).GetAllChildrenAssets<GameObject>());
        }
        if (allSelected == null || allSelected.Count == 0)
            return;

        foreach(GameObject obj in allSelected)
        {
            // Create a single copy of this model
            if (PrefabUtility.GetPrefabType(obj) == PrefabType.ModelPrefab)
                MakeSimplePrefabObj(obj);
        }
    }

    private static void MakeSimplePrefabObj(GameObject obj)
    {
        GameObject instance = GameObject.Instantiate(obj) as GameObject;
        instance.name = obj.name;

        // Remove any old colliders.
        Collider[] foundColliders = instance.transform.GetComponentsInChildren<Collider>();
        foreach(Collider col in foundColliders)
            Object.DestroyImmediate(col, true);

        // Create SemanticObject/Rigidbody
        instance.AddComponent<SemanticObjectSimple>().name = instance.name;

        // Add generatable prefab tags
        instance.AddComponent<GeneratablePrefab>();

        // Save as a prefab
        string prefabAssetPath = string.Format("Assets/Resources/Prefabs/Converted Models/{0}.prefab", obj.name);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
        if (prefab == null)
            prefab = PrefabUtility.CreatePrefab(prefabAssetPath, instance);
        else
            prefab = PrefabUtility.ReplacePrefab(instance, prefab);
        GameObject.DestroyImmediate(instance);

        // Create colliders for the prefab
        // Using reflection to avoid failing when compiling on machine without ConcaveCollider scripts.
        // ConcaveCollider.FH_CreateColliders(prefab, true);
        System.Type t = System.Type.GetType("ConcaveCollider");
        if (t != null)
        {
            System.Reflection.MethodInfo method = t.GetMethod("FH_CreateColliders", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            if (method != null)
            {
                object[] parameters = {prefab, true};
                method.Invoke(null, parameters);
            }
            else
                Debug.LogWarning("ConcaveCollider::FH_CreateColliders method doesn't exist!");
        }
        else
            Debug.LogWarning("ConcaveCollider class doesn't exist!");

        // Save out updated metadata settings
        GeneratablePrefab metaData = prefab.GetComponent<GeneratablePrefab>();
        metaData.ProcessPrefab();
        SetupPrefabs(false);
    }

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
                foreach(GeneratablePrefab.StackableInfo stackRegion in prefab.stackableAreas)
                    newInfo.stackableAreas.Add(stackRegion);
                availablePrefabs.Add(newInfo);
            }
        }
    }
#endif

    private void AddObjects(List<PrefabInfo> prefabList)
    {
        if (prefabList.Count == 0)
        {
            _failures++;
            return;
        }
        PrefabInfo info = prefabList[Random.Range(0, prefabList.Count)];

        // Check for excess complexity
        int maxComplexity = (complexityLevelToCreate - _curComplexity);
        if (info.complexity > maxComplexity)
        {
            prefabList.RemoveAll((PrefabInfo testInfo)=>{
                return testInfo.complexity > maxComplexity;
            });
            if (showProcGenDebug)
                Debug.LogFormat("Filtering for complexity {0} > {1} leaving {2} objects ", info.complexity, maxComplexity, prefabList.Count);
            if (prefabList.Count == 0)
                return;
            info = prefabList[Random.Range(0, prefabList.Count)];
        }

        // Find a spot to place this object
        int spawnX, spawnZ;
        float modScale;
        HeightPlane targetHeightPlane;
        Quaternion modifiedRotation = Quaternion.identity;
        if (info.stackableAreas.Count == 0)
            modifiedRotation = Quaternion.Euler(new Vector3(0,Random.Range(0f,360f),0));
        Bounds modifiedBounds = info.bounds.Rotate(modifiedRotation);

        if (TryPlaceGroundObject(modifiedBounds, info.anchorType, out spawnX, out spawnZ, out modScale, out targetHeightPlane))
        {
            int boundsWidth = Mathf.CeilToInt(modScale* 2f * modifiedBounds.extents.x / gridDim) - 1;
            int boundsLength = Mathf.CeilToInt(modScale* 2f * modifiedBounds.extents.z / gridDim) - 1;
            float modHeight = 0.1f+(modifiedBounds.extents.y * modScale);
            Vector3 centerPos = targetHeightPlane.cornerPos + new Vector3(gridDim * (spawnX + (0.5f * boundsWidth)), modHeight, gridDim * (spawnZ + (0.5f * boundsLength)));
            if (info.anchorType == GeneratablePrefab.AttachAnchor.Ceiling)
                centerPos.y = _roomCornerPos.y + _curRoomHeight - modHeight;

            GameObject newPrefab = Resources.Load<GameObject>(info.fileName);
            // TODO: Factor in complexity to the arrangement algorithm?
            _curComplexity += info.complexity;

            GameObject newInstance = Object.Instantiate<GameObject>(newPrefab.gameObject);
            newInstance.transform.position = centerPos - (modifiedBounds.center * modScale);
            newInstance.transform.localScale = newInstance.transform.localScale * modScale;
            newInstance.transform.rotation = modifiedRotation * newInstance.transform.rotation;
            newInstance.name = string.Format("{0} #{1} on {2}", newPrefab.name, (_curRoom != null) ? _curRoom.childCount.ToString() : "?", targetHeightPlane.name);

			print("LUMP");
			print(newInstance.name);
			print(newPrefab.name);
			print(newInstance.GetComponent<Renderer>());
			if (newInstance.GetComponent<Renderer>() != null)
			{
				print(newInstance.GetComponent<Renderer>().material);
				newInstance.GetComponent<Renderer>().material.SetInt("_idval", _curRoom.childCount);
				print(newInstance.GetComponent<Renderer>().material.GetInt("_idval"));
			} else 
			{
				print("null material");
			}

            // Create test cube
            if (DEBUG_testCubePrefab != null)
            {
                GameObject testCube = Object.Instantiate<GameObject>(DEBUG_testCubePrefab);
                testCube.transform.localScale = modScale * 2f * modifiedBounds.extents;
                testCube.transform.position = centerPos;
                testCube.name = string.Format("Cube {0}", newInstance.name);
                testCube.transform.SetParent(_curRoom);
            }

            if (showProcGenDebug)
                Debug.LogFormat("{0}: @{1} R:{2} G:{3} BC:{4} MS:{5}", info.fileName, newInstance.transform.position, targetHeightPlane.cornerPos, new Vector3(gridDim * spawnX, info.bounds.extents.y, gridDim * spawnZ), info.bounds.center, modScale);
            if (_curRoom != null)
                newInstance.transform.SetParent(_curRoom);

            // For stackable objects, create a new height plane to stack
            if (info.anchorType == GeneratablePrefab.AttachAnchor.Ground)
            {
                targetHeightPlane.UpdateGrid(spawnX, spawnZ, boundsWidth, boundsLength);
                foreach(GeneratablePrefab.StackableInfo stackInfo in info.stackableAreas)
                {
                    int width = Mathf.FloorToInt(stackInfo.dimensions.x / gridDim);
                    int length = Mathf.FloorToInt(stackInfo.dimensions.z / gridDim);
                    if (width <= 0 || length <= 0)
                        continue;
                    HeightPlane newPlane = new HeightPlane();
                    newPlane.gridDim = gridDim;
                    // TODO: Set rotation matrix for new plane?
                    newPlane.rotMat = modifiedRotation;
                    newPlane.cornerPos = newInstance.transform.position + (modifiedRotation * (stackInfo.bottomCenter + info.bounds.center));
                    newPlane.cornerPos = newPlane.GridToWorld(new Vector2((width-1) * -0.5f, (length-1) * -0.5f));
                    newPlane.planeHeight = stackInfo.dimensions.y;
                    if (stackInfo.dimensions.y <= 0)
                        newPlane.planeHeight = _curRoomHeight - newPlane.cornerPos.y;
                    newPlane.Clear(width, length);
                    newPlane.name = string.Format("Plane for {0}", newInstance.name);
                    _allHeightPlanes.Add(newPlane);
                }
            }
        }
        else
        {
            // TODO: Mark item as unplaceable and continue with smaller objects?
            if (showProcGenDebug)
                Debug.LogFormat("Couldn't place: {0}. {1} object types, {2} complexity left", info.fileName, prefabList.Count, complexityLevelToCreate - _curComplexity);
            prefabList.Remove(info);
            ++_failures;
        }
    }

    public void CreateRoom(Vector3 roomDimensions, Vector3 roomCenter)
    {
        _curRoom = new GameObject("New Room").transform;
        _roomCornerPos = (roomCenter - (0.5f *roomDimensions)) + (gridDim * 0.5f * Vector3.one);
        _roomCornerPos.y = 0f;

        // Create floor and ceiling
        Vector3 floorSize = new Vector3(roomDimensions.x, WALL_WIDTH, roomDimensions.z);
        Vector3 floorStart = roomCenter + new Vector3(-0.5f * roomDimensions.x, -WALL_WIDTH, -0.5f * roomDimensions.z);
        Vector3 ceilingStart = floorStart + (roomDimensions.y + WALL_WIDTH) * Vector3.up;

        GameObject floor = WallInfo.CreateBoxMesh(floorStart, floorSize, floorMaterial, "Floor", _curRoom);
        floor.AddComponent<SemanticObjectSimple>();
        floor.GetComponent<Rigidbody>().isKinematic = true;

        GameObject top = WallInfo.CreateBoxMesh(ceilingStart, floorSize, ceilingMaterial, "Ceiling", _curRoom);
        top.AddComponent<SemanticObjectSimple>();
        top.GetComponent<Rigidbody>().isKinematic = true;

        // Setup floor plane
        _allHeightPlanes.Clear();
        HeightPlane basePlane = new HeightPlane();
        basePlane.gridDim = gridDim;
        basePlane.planeHeight = roomDim.y;
        basePlane.name = "Plane Floor";
        _allHeightPlanes.Add(basePlane);
        _curRoomWidth = Mathf.FloorToInt(roomDim.x / gridDim);
        _curRoomLength = Mathf.FloorToInt(roomDim.z / gridDim);
        _curRoomHeight = roomDim.y;
        basePlane.cornerPos = _roomCornerPos;
        basePlane.Clear(_curRoomWidth, _curRoomLength);

        // Create walls
        SubdivideRoom();
    }

    private void DrawTestGrid(HeightPlane plane)
    {
        // Create debug test grid on the floor
        if (DEBUG_testGridPrefab != null)
        {
            GameObject child = new GameObject("TEST GRIDS " + plane.name);
            child.transform.SetParent(_curRoom);
            foreach(GridInfo g in plane.myGridSpots)
            {
                TextMesh test = GameObject.Instantiate<TextMesh>(DEBUG_testGridPrefab);
                test.transform.SetParent(child.transform);
//                test.text = string.Format("  {0}\n{2}  {1}\n  {3}", g.upSquares, g.leftSquares, g.rightSquares, g.downSquares);
                test.text = string.Format("{0},{1}", g.x, g.y);
                test.color = g.inUse ? Color.red: Color.cyan;
                test.transform.position = plane.GridToWorld(new Point2(g.x, g.y));
                test.name = string.Format("{0}: ({1},{2})", DEBUG_testGridPrefab.name, g.x, g.y);
                test.transform.localScale = gridDim * Vector3.one;
            }
        }        
    }

    public bool IsDone()
    {
        // TODO: Find a better metric for completion
        return _curComplexity >= complexityLevelToCreate || _failures > maxPlacementAttempts;
    }
}