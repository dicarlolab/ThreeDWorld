//TODO:  -- more generic way to deterine object identity
//       -- assign different identity to different walls
 
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

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
	public List<PrefabDatabase.PrefabInfo> availablePrefabs = new List<PrefabDatabase.PrefabInfo>();
    public List<string> disabledItems = new List<string>();
    public List<string> permittedItems = new List<string>();
    public float gridDim = 0.4f;
    public bool shouldUseStandardizedSize = false;
    public Vector3 standardizedSize = Vector3.one;
    public bool shouldUseGivenSeed = false;
    public int desiredRndSeed = -1;
	public System.Random _rand;

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
    public LitJson.JsonData scaleRelatDict = new LitJson.JsonData();

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
    private List<PrefabDatabase.PrefabInfo> ceilingLightPrefabs = new List<PrefabDatabase.PrefabInfo>();
    private List<PrefabDatabase.PrefabInfo> groundPrefabs = new List<PrefabDatabase.PrefabInfo>();
    private List<PrefabDatabase.PrefabInfo> stackingPrefabs = new List<PrefabDatabase.PrefabInfo>();
    public List<HeightPlane> _allHeightPlanes = new List<HeightPlane>();
    private static ProceduralGeneration _Instance = null;
	private static int UID_BY_INDEX = 0x3;
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

	private void Start() {
		SceneManager.SetActiveScene (this.gameObject.scene);
		Init ();
	}
#endregion

    public void Init()
    {
        LitJson.JsonData json = SimulationManager.argsConfig;
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
            // scaleRelatDict = new LitJson.JsonData(json["scale_relat_dict"]);
            //scaleRelatDict = json["scale_relat_dict"];
        }

        _curRandSeed = UnityEngine.Random.Range (int.MinValue, int.MaxValue);
        if (shouldUseGivenSeed) {
            _rand = new System.Random (desiredRndSeed);
            _curRandSeed = desiredRndSeed;
        } else {
            _rand = new System.Random (_curRandSeed);
        }

        Debug.Log("Using random seed: " + _curRandSeed);

        //var database_bundle = AssetBundle.LoadFromFile("Assets/ScenePrefabs/PrefabDatabase.prefab");
        //PrefabDatabase database =  Resources.Load("Assets/ScenePrefabs/PrefabDatabase.prefab") as PrefabDatabase;
        //PrefabDatabase database =  AssetDatabase.LoadAssetAtPath<PrefabDatabase> 
        //        ("Assets/ScenePrefabs/PrefabDatabase.prefab");

        /*
        var database_list = database_bundle.LoadAllAssets<PrefabDatabase>();
        Debug.Log("Test output: " + database_list);
        var database = database_list[0];
        if (database==null)
        {
            Debug.Log("Null database!" + database_list);
        }
        */
        PrefabDatabase database = GameObject.FindObjectOfType<PrefabDatabase>();
        availablePrefabs = database.prefabs;

        Debug.Log (availablePrefabs.Count);

        List<PrefabDatabase.PrefabInfo> filteredPrefabs = availablePrefabs.FindAll(((PrefabDatabase.PrefabInfo info)=>{
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
                    {
                        // Get the option and scale from json message

                        try {
                            info.option_scale   = json["scale_relat_dict"][itemName]["option"].ReadString(info.option_scale);
                            info.dynamic_scale  = json["scale_relat_dict"][itemName]["scale"].ReadFloat(info.dynamic_scale);
                        } catch {
                        }

                        // Get the option and scale via the Filename (recommend for http files)
                        try {
                            info.option_scale   = json["scale_relat_dict"][info.fileName]["option"].ReadString(info.option_scale);
                            info.dynamic_scale  = json["scale_relat_dict"][info.fileName]["scale"].ReadFloat(info.dynamic_scale);
                        } catch {
                        }

                        return true;
                    }
                }
                return false;
            }

            // Get the option and scale via the Filename (recommend for http files)

            try {
                info.option_scale   = json["scale_relat_dict"][info.fileName]["option"].ReadString(info.option_scale);
                info.dynamic_scale  = json["scale_relat_dict"][info.fileName]["scale"].ReadFloat(info.dynamic_scale);
            } catch {
            }

            return true;
        }));
        // TODO: We're not filtering the ceiling lights, since we currently only have 1 prefab that works
        ceilingLightPrefabs = availablePrefabs.FindAll(((PrefabDatabase.PrefabInfo info)=>{return info.anchorType == GeneratablePrefab.AttachAnchor.Ceiling && info.isLight;}));
        groundPrefabs = filteredPrefabs.FindAll(((PrefabDatabase.PrefabInfo info)=>{return info.anchorType == GeneratablePrefab.AttachAnchor.Ground;}));
        stackingPrefabs = groundPrefabs.FindAll(((PrefabDatabase.PrefabInfo info)=>{return info.stackableAreas.Count > 0;}));
        List<PrefabDatabase.PrefabInfo> itemsForStacking = groundPrefabs.FindAll(((PrefabDatabase.PrefabInfo info)=>{return true;}));

        // Create grid to populate objects
        _curComplexity = 0;
        _failures = 0;
        _forceStackObject = false;

        // Create rooms
        roomDim.x = Mathf.Round(roomDim.x / gridDim) * gridDim;
        roomDim.z = Mathf.Round(roomDim.z / gridDim) * gridDim;
		Debug.Log ("Starting to create rooms...");
        CreateRoom(roomDim, new Vector3((roomDim.x-1) * 0.5f,0,(roomDim.z-1) * 0.5f));
		Debug.Log ("...created!");

		// Create lighting setup
		Debug.Log("Creating lights...");
		CreateLightingSetup(roomDim, new Vector3((roomDim.x-1) * 0.5f,0,(roomDim.z-1) * 0.5f));
		Debug.Log("...created!");

        _failures = 0;
        // Keep creating objects until we are supposed to stop
        // TODO: Create a separate plane to map ceiling placement
        // TODO: Replace ceilingLightPrefabs with Lighting setup
        /*for(int i = 0; (i - _failures) < numCeilingLights && _failures < maxPlacementAttempts; ++i)
            AddObjects(ceilingLightPrefabs);
        _failures = 0;*/

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
		while (!IsDone ())
			if (!AddObjects (groundPrefabs)) {
				break;
			}

        for(int i = 0; i < _allHeightPlanes.Count; ++i)
            DrawTestGrid(_allHeightPlanes[i]);
        Debug.Log("Final complexity: " + _curComplexity);
    }

	//TODO: why is this called try place ground if you can specify the anchor type?

	/// <summary>
	/// Tries to place object on the ground.
	/// </summary>
	/// <returns><c>true</c>, if location to spawn object is found, <c>false</c> otherwise.</returns>
	/// <param name="bounds">Bounds.</param>
	/// <param name="anchorType">Anchor type, can be Ground, Ceiling, or Wall.</param>
	/// <param name="finalX">x coord where object should spawn.</param>
	/// <param name="finalY">y coord where object should spawn.</param>
	/// <param name="modScale">Modified scale.</param>
	/// <seealso cref="PrefabDatabase.GetSceneScale">modScale is retrieved via this method.</seealso>
	/// <param name="whichPlane">Which height plane the object should spawn on.</param>
        ///

    private bool TryPlaceGroundObject(Bounds bounds, float modScale, GeneratablePrefab.AttachAnchor anchorType, out int finalX, out int finalY, out HeightPlane whichPlane)
    {
        finalX = 0;
        finalY = 0;
        whichPlane = null;

        /*
        modScale = 1.0f;
        if (shouldUseStandardizedSize)
        {
            modScale = Mathf.Min(
                standardizedSize.x / bounds.extents.x,
                standardizedSize.y / bounds.extents.y,
                standardizedSize.z / bounds.extents.z);
        }
        */

        Bounds testBounds = new Bounds(bounds.center, modScale * 2f * bounds.extents);
        int boundsWidth = Mathf.CeilToInt(2 * testBounds.extents.x / gridDim);
        int boundsLength = Mathf.CeilToInt(2 * testBounds.extents.z / gridDim);
        float boundsHeight = testBounds.extents.y;

        List<int> randomPlanesOrder = new List<int>();
        int randomOrderValue = _rand.Next(0, int.MaxValue);

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
				int randIndex = _rand.Next(0, validValues.Count);
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
	private static void MakeSimplePrefabObj(GameObject obj, string subPath)
    {
        Debug.LogFormat("MakeSimplePrefabObj {0}", obj.name);
        // Find vrml text if we have it
        string vrmlText = null;
        string vrmlPath = AssetDatabase.GetAssetPath(obj);
        if (vrmlPath != null)
        {
            vrmlPath = vrmlPath.Replace(".obj", ".wrl");
            string fullPath = System.IO.Path.Combine(Application.dataPath, vrmlPath.Substring(7));
            if (!System.IO.File.Exists(fullPath))
                fullPath = fullPath.Replace(".wrl", "_vhacd.wrl");
            if (System.IO.File.Exists(fullPath))
            {
                using (System.IO.StreamReader reader = new System.IO.StreamReader(fullPath))
                {
                    vrmlText = reader.ReadToEnd();
                }
            }
            else
                Debug.LogFormat("Cannot find {0}\n{1}", vrmlPath, fullPath);
        }

        GameObject instance = GameObject.Instantiate(obj) as GameObject;
        instance.name = obj.name;

        // Remove any old colliders.
        Collider[] foundColliders = instance.transform.GetComponentsInChildren<Collider>();
        foreach(Collider col in foundColliders)
            UnityEngine.Object.DestroyImmediate(col, true);

        // Create SemanticObject/Rigidbody
        instance.AddComponent<SemanticObjectSimple>().name = instance.name;

        // Add generatable prefab tags
        instance.AddComponent<GeneratablePrefab>();

		//NormalSolver.RecalculateNormals (instance.GetComponentInChildren<MeshFilter> ().sharedMesh, 60);

        // Save as a prefab
		string prefabAssetPath = string.Format("Assets/PrefabDatabase/GeneratedPrefabs/{0}.prefab", subPath);
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
        if (prefab == null)
            prefab = PrefabUtility.CreatePrefab(prefabAssetPath, instance);
        else
            prefab = PrefabUtility.ReplacePrefab(instance, prefab);
        GameObject.DestroyImmediate(instance);


        // Create colliders for the prefab
        ConcaveCollider.FH_CreateColliders(prefab, vrmlText, true);

        // Save out updated metadata settings
        GeneratablePrefab metaData = prefab.GetComponent<GeneratablePrefab>();
        metaData.ProcessPrefab();
        SetupPrefabs(false);

		EditorUtility.SetDirty (prefab);
		try {
			EditorUtility.SetDirty (instance);
		} catch {
			//do nothing
		}
    }
		

    public static void SetupPrefabs(bool shouldRecompute)
    {
        ProceduralGeneration [] allThings = Resources.LoadAll<ProceduralGeneration>("");
        if (allThings != null && allThings.Length > 0)
            allThings[0].CompileListOfProceduralComponents(shouldRecompute);
    }

    public void SavePrefabInformation(GeneratablePrefab prefab, bool shouldRecomputePrefabInformation, bool replaceOld = true)
    {
        const string resPrefix = "Resources/";
        string assetPath = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(assetPath))
            return;
        string newFileName = assetPath.Substring(assetPath.LastIndexOf(resPrefix) + resPrefix.Length);
        newFileName = newFileName.Substring(0, newFileName.LastIndexOf("."));
        int replaceIndex = -1;
        if (replaceOld)
        {
            replaceIndex = availablePrefabs.FindIndex( (PrefabDatabase.PrefabInfo testInfo)=>{
                return testInfo.fileName == newFileName;
            });
            if (replaceIndex >= 0)
                availablePrefabs.RemoveAt(replaceIndex);
        }

        if (prefab.shouldUse)
        {
            if (shouldRecomputePrefabInformation)
                prefab.ProcessPrefab();
            PrefabDatabase.PrefabInfo newInfo = new PrefabDatabase.PrefabInfo();
            newInfo.fileName = newFileName;
            newInfo.complexity = prefab.myComplexity;
            newInfo.bounds = prefab.myBounds;
            newInfo.isLight = prefab.isLight;
            newInfo.anchorType = prefab.attachMethod;
            foreach(GeneratablePrefab.StackableInfo stackRegion in prefab.stackableAreas)
                newInfo.stackableAreas.Add(stackRegion);
            if (replaceIndex < 0)
                availablePrefabs.Add(newInfo);
            else
                availablePrefabs.Insert(replaceIndex, newInfo);
        }        
        EditorUtility.SetDirty(this);
    }

    // Save out core information so we can decide whether to place the objects dynamically even if they aren't loaded yet
    private void CompileListOfProceduralComponents(bool shouldRecomputePrefabInformation)
    {
        GeneratablePrefab [] allThings = Resources.LoadAll<GeneratablePrefab>("");
        availablePrefabs.Clear();
        foreach(GeneratablePrefab prefab in allThings)
            SavePrefabInformation(prefab, shouldRecomputePrefabInformation, false);
        EditorUtility.SetDirty(this);
    }
#endif

	public static Color getNewUIDColor() {
		if (UID_BY_INDEX >= 0x1000000)
			Debug.LogError ("UID's has exceeded 256^3, the current max limit of objects which can be formed!");
		float r = (float) (UID_BY_INDEX / 0x10000) / 256f;
		float g = (float) ((UID_BY_INDEX / 0x100) % 0x100) / 256f;
		float b = (float) (UID_BY_INDEX % 0x100) / 256f;
		UID_BY_INDEX += 0x1;
		return new Color (r, g, b);
	}

    private bool AddObjects(List<PrefabDatabase.PrefabInfo> prefabList)
    {
        if (prefabList.Count == 0)
        {
            _failures++;
            return false;
        }

        // Randomly add next one?
        int temp_rand_index     = _rand.Next(0, prefabList.Count);
        PrefabDatabase.PrefabInfo info = prefabList[temp_rand_index];

        if (info.loaded==0) {
            // Load it now
            Debug.Log("From http loaded!");
            var www = WWW.LoadFromCacheOrDownload (info.fileName, 0);
            var loadedAssetBundle = www.assetBundle;
            string[] assetList = loadedAssetBundle.GetAllAssetNames ();

            foreach (string asset in assetList) { 

                GameObject gObj = loadedAssetBundle.LoadAsset<GameObject> (asset);
                GeneratablePrefab[] prefab = gObj.GetComponents<GeneratablePrefab> ();
                if (prefab.GetLength (0) == 0) {
                    Debug.LogFormat ("Cannot load GeneratablePrefab component on {0}", gObj);
                    continue;
                }
                GeneratablePrefab prefab_temp   = prefab [0];
                info.loaded     = 1;
                info.complexity = prefab_temp.myComplexity;
                info.bounds     = prefab_temp.myBounds;
                info.isLight    = prefab_temp.isLight;
                info.anchorType = prefab_temp.attachMethod;
                foreach (GeneratablePrefab.StackableInfo stackRegion in prefab_temp.stackableAreas)
                    info.stackableAreas.Add (stackRegion);
                prefabList[temp_rand_index]     = info;

            }
            loadedAssetBundle.Unload (false);
        }

        // Check for excess complexity
        int maxComplexity = (complexityLevelToCreate - _curComplexity);
        if (info.complexity > maxComplexity)
        {
            // Change for lazy loading
            /*
            prefabList.RemoveAll((PrefabDatabase.PrefabInfo testInfo)=>{
				return (testInfo.complexity > maxComplexity) | (testInfo.complexity == 0);
            });
            if (showProcGenDebug)
                Debug.LogFormat("Filtering for complexity {0} > {1} leaving {2} objects ", info.complexity, maxComplexity, prefabList.Count);
            if (prefabList.Count == 0)
                return false;
            */

            prefabList.Remove(info);
            return true;
            //info = prefabList[_rand.Next(0, prefabList.Count)];
        }

        // Find a spot to place this object
        int spawnX, spawnZ;
        //float modScale = prefabDatabase.GetSceneScale (info);
        float modScale = 1.0f;

        // For option "Multi_size"
        if (info.option_scale=="Multi_size")
        {
            modScale    = modScale*info.dynamic_scale;
        }

        // For option "Absol_size"
        if (info.option_scale=="Absol_size")
        {
            float longest_axis      = info.bounds.size.magnitude;
            Debug.Log("Longest axis: " + longest_axis.ToString());
            modScale                = info.dynamic_scale/longest_axis;
        }

        HeightPlane targetHeightPlane;
        Quaternion modifiedRotation = Quaternion.identity;

        if (info.stackableAreas.Count == 0)
            modifiedRotation = Quaternion.Euler(new Vector3(0, (float) _rand.NextDouble() * 360f,0));
        Bounds modifiedBounds = info.bounds.Rotate(modifiedRotation);

        if (TryPlaceGroundObject(modifiedBounds, modScale, info.anchorType, out spawnX, out spawnZ, out targetHeightPlane))
        {
            int boundsWidth = Mathf.CeilToInt(modScale* 2f * modifiedBounds.extents.x / gridDim) - 1;
            int boundsLength = Mathf.CeilToInt(modScale* 2f * modifiedBounds.extents.z / gridDim) - 1;
            float modHeight = 0.1f+(modifiedBounds.extents.y * modScale);
            Vector3 centerPos = targetHeightPlane.cornerPos + new Vector3(gridDim * (spawnX + (0.5f * boundsWidth)), modHeight, gridDim * (spawnZ + (0.5f * boundsLength)));
            if (info.anchorType == GeneratablePrefab.AttachAnchor.Ceiling)
                centerPos.y = _roomCornerPos.y + _curRoomHeight - modHeight;

//            GameObject newPrefab = Resources.Load<GameObject>(info.
            GameObject newPrefab;
            if (info.fileName.ToLowerInvariant().Contains("http://")) {
                Debug.Log("From http");
                newPrefab = PrefabDatabase.LoadAssetFromBundleWWW(info.fileName);
            } else {
                newPrefab = PrefabDatabase.LoadAssetFromBundle(info.fileName);
            }
			// TODO: Factor in complexity to the arrangement algorithm?
            _curComplexity += info.complexity;

            GameObject newInstance = UnityEngine.Object.Instantiate<GameObject>(newPrefab.gameObject);
            newInstance.transform.position = centerPos - (modifiedBounds.center * modScale);
            newInstance.transform.localScale = newInstance.transform.localScale * modScale;
            newInstance.transform.rotation = modifiedRotation * newInstance.transform.rotation;
            //newInstance.name = string.Format("{0} #{1} on {2}", newPrefab.name, (_curRoom != null) ? _curRoom.childCount.ToString() : "?", targetHeightPlane.name);
            
            newInstance.name = string.Format("{0}, {1}, {2}", info.fileName, newPrefab.name, (_curRoom != null) ? _curRoom.childCount.ToString() : "?");
            Renderer[] RendererList = newInstance.GetComponentsInChildren<Renderer>();
            Color colorID = getNewUIDColor ();
            foreach (Renderer _rend in RendererList)
            {
                    foreach (Material _mat in _rend.materials)
                    {
                            _mat.SetColor("_idval", colorID);	
                    }
            }	
	
            // Create test cube
            if (DEBUG_testCubePrefab != null)
            {
                GameObject testCube = UnityEngine.Object.Instantiate<GameObject>(DEBUG_testCubePrefab);
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
		return true;
    }

	public void CreateLightingSetup(Vector3 roomDimensions, Vector3 roomCenter)
    {
    	// roomDimensions are the dimensions of the room, with the y coordinate describing the height of the room
    	// roomCenter describes the center of the room
		Vector3 ceilingSize = new Vector3(roomDimensions.x, 0, roomDimensions.z);
		Vector3 ceilingStart = roomCenter + new Vector3(-0.5f * roomDimensions.x, roomDimensions.y, -0.5f * roomDimensions.z);

        Debug.Log("SETTING SKYBOX!");
		var bundle = AssetBundle.LoadFromFile("Assets/Scenes/Lighting/" + "sunnyskybox");
		Material levelMat = bundle.LoadAsset(bundle.GetAllAssetNames()[0]) as Material;
        RenderSettings.skybox = levelMat;

        /* 
         *  We want to keep the total light energy constant in a given volume. 
         *  Therefore, we base the number of lights and their intensities on the volume of each room.
         *  At first the intensity of each light is assumed to be constant.
         */
		double scalingFactor = 0.0106;
		int totalNumberOfLights = Convert.ToInt32(scalingFactor * roomDimensions.x * roomDimensions.z);
		int widthNumberOfLights = Convert.ToInt32(Math.Sqrt(totalNumberOfLights * roomDimensions.x / roomDimensions.z));
		int lengthNumberOfLights = Convert.ToInt32(Math.Sqrt(totalNumberOfLights * roomDimensions.z / roomDimensions.x));

		float widthLightDistance = ceilingSize.x / widthNumberOfLights;
		float lengthLightDistance = ceilingSize.z / lengthNumberOfLights;

		float intensity = 1.0f;
		int iter_light = 0;
		for(float i = ceilingStart.x + widthLightDistance / 2.0f; i <= ceilingStart.x + ceilingSize.x; i = i + widthLightDistance)
        {
			for(float j = ceilingStart.z + lengthLightDistance / 2.0f; j <= ceilingStart.x + ceilingSize.z; j = j + lengthLightDistance)
        	{
        		/*GameObject lightGameObject = new GameObject("Point Light " + iter_light.ToString());
        		Light lightComp = lightGameObject.AddComponent<Light>();
        		lightComp.color = Color.white;
				lightComp.range = 1.7f * roomDimensions.y;;
        		lightComp.intensity = intensity;
				lightComp.renderMode = LightRenderMode.ForcePixel;
				lightComp.shadows = LightShadows.Soft;
        		lightGameObject.transform.position = new Vector3(i, ceilingStart.y * 0.7f, j);*/

				GameObject spotLightGameObject = new GameObject("Spot Light " + iter_light.ToString());
        		Light spotLight = spotLightGameObject.AddComponent<Light>();
        		spotLight.type = LightType.Spot;
				spotLight.color = Color.white;
				spotLight.range = 30;
				spotLight.intensity = intensity;
				spotLight.spotAngle = 145.0f;
				spotLight.renderMode = LightRenderMode.ForcePixel;
				spotLight.shadows = LightShadows.Soft;
				spotLightGameObject.transform.position = new Vector3(i, ceilingStart.y * 0.7f, j);
				Quaternion rot = Quaternion.identity;
				rot.eulerAngles = new Vector3(90, 0, 0);
				spotLightGameObject.transform.rotation = rot;

        		iter_light++; 
        	}
        }

		GameObject directLightGameObject = new GameObject("Directional Light");
        Light directLightComp = directLightGameObject.AddComponent<Light>();
        directLightComp.color = Color.white;
        directLightComp.type = LightType.Directional;
        directLightComp.intensity = 0.85f;
        Quaternion target = Quaternion.identity;
		target.eulerAngles = new Vector3(30, 0, 0);
		directLightComp.transform.rotation = target;
		directLightComp.transform.position = new Vector3(100, 100, 100);
		directLightComp.shadows = LightShadows.Soft;

		GameObject reflectionProbeObject = new GameObject("Reflection Probe");
		ReflectionProbe reflectionProbe = reflectionProbeObject.AddComponent<ReflectionProbe>();
		reflectionProbe.transform.position = new Vector3(roomCenter.x, roomDimensions.y / 2.0f, roomCenter.z);
		reflectionProbe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
		reflectionProbe.hdr = true;
		reflectionProbe.size = new Vector3(roomDimensions.x, roomDimensions.y, roomDimensions.z);
    }

    public void CreateRoom(Vector3 roomDimensions, Vector3 roomCenter)
    {
		Debug.Log ("Create 1.");
        _curRoom = new GameObject("New Room").transform;
        _roomCornerPos = (roomCenter - (0.5f *roomDimensions)) + (gridDim * 0.5f * Vector3.one);
        _roomCornerPos.y = 0f;
		Debug.Log ("Create 2.");
        // Create floor and ceiling
        Vector3 floorSize = new Vector3(roomDimensions.x, WALL_WIDTH, roomDimensions.z);
        Vector3 floorStart = roomCenter + new Vector3(-0.5f * roomDimensions.x, -WALL_WIDTH, -0.5f * roomDimensions.z);
        Vector3 ceilingStart = floorStart + (roomDimensions.y + WALL_WIDTH) * Vector3.up;
		Debug.Log ("Create 3.");
        GameObject floor = WallInfo.CreateBoxMesh(floorStart, floorSize, floorMaterial, "Floor", _curRoom);
        floor.AddComponent<SemanticObjectSimple>();
        floor.GetComponent<Rigidbody>().isKinematic = true;
		Debug.Log ("Create 4.");
        Renderer[] RendererList = floor.GetComponentsInChildren<Renderer>();
		foreach (Renderer _rend in RendererList)
		{
			foreach (Material _mat in _rend.materials)
			{
				_mat.SetColor("_idval", new Color(0f,0f,1f/256f));	
			}
		}
		Debug.Log ("Create 5.");
		//{
		//	_rend.material.SetInt("_idval", 1);	
		//}	
        //floor.GetComponent<Renderer>().material.SetInt("_idval", 1);

		GameObject borktest = Resources.Load<GameObject> ("Prefabs/PointSpawn");
		Debug.Log ("borktest");
		if (borktest == null) {
			Debug.Log ("its null");
		}
		Debug.Log(borktest.name);
		// Make a spawn plane
		SpawnArea floorSpawn = GameObject.Instantiate<SpawnArea>(Resources.Load<SpawnArea>("Prefabs/PlaneSpawn"));
		Debug.Log ("Create 6.");
		floorSpawn.gameObject.transform.position = roomCenter;
		Debug.Log ("Create 7.");
		//retrieve ratio between spawn plane and floor
		float xRatio = floorSize.x / floorSpawn.gameObject.GetComponent<Collider>().bounds.size.x;
		float zRatio = floorSize.z / floorSpawn.gameObject.GetComponent<Collider>().bounds.size.z;
		Debug.Log ("Create 8.");
		floorSpawn.gameObject.transform.localScale = new Vector3 (xRatio * floorSpawn.gameObject.transform.localScale.x, 0, zRatio * floorSpawn.gameObject.transform.localScale.z);
		Debug.Log ("Create 9.");
		Debug.LogFormat ("Created floor: {0}", floorSpawn.name);
		Debug.Log ("Create 10.");
        GameObject top = WallInfo.CreateBoxMesh(ceilingStart, floorSize, ceilingMaterial, "Ceiling", _curRoom);
		Debug.Log ("Create 11.");
        top.AddComponent<SemanticObjectSimple>();
		Debug.Log ("Create 12.");
        top.GetComponent<Rigidbody>().isKinematic = true;
		Debug.Log ("Create 13.");
        RendererList = top.GetComponentsInChildren<Renderer>();
		foreach (Renderer _rend in RendererList)
		{
			foreach (Material _mat in _rend.materials)
			{
				_mat.SetColor("_idval", new Color(0f,0f,2f/256f));	
			}
		}
		Debug.Log ("Create 14.");
		//{
		//	_rend.material.SetInt("_idval", 2);	
		//}
        
        //top.GetComponent<Renderer>().material.SetInt("_idval", 2);

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
		Debug.Log ("Create 15.");

        // Create walls
        SubdivideRoom();
		Debug.Log ("Create 16.");
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
