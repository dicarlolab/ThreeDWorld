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
    public List<PrefabInfo> availablePrefabs = new List<PrefabInfo>();
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
        }
		_curRandSeed = UnityEngine.Random.Range (int.MinValue, int.MaxValue);
		if (shouldUseGivenSeed) {
			_rand = new System.Random (desiredRndSeed);
			_curRandSeed = desiredRndSeed;
		} else {
			_rand = new System.Random (_curRandSeed);
		}
        Debug.Log("Using random seed: " + _curRandSeed);

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
    // Setup a new prefab object from a model
    [MenuItem ("Procedural Generation/Create prefab model folders")]
    private static void CreatePrefabFromModel()
    {
		Dictionary<GameObject, string> allSelected = new Dictionary<GameObject, string>();

		foreach (GameObject obj in Selection.gameObjects) {
			allSelected.Add (obj, AssetDatabase.GetAssetPath (obj));
		}

		foreach(UnityEngine.Object obj in Selection.objects)
		{
			if (obj != null)
			{
				if (obj is GameObject) {
					allSelected.Add (obj as GameObject, AssetDatabase.GetAssetPath (obj));
				} else if (obj is DefaultAsset) {
					HashSet<GameObject> children = (obj as DefaultAsset).GetAllChildrenAssets<GameObject> ();
					foreach (GameObject child in children) {
						allSelected.Add (child, AssetDatabase.GetAssetPath (child));
					}
				}
			}
		}
		if (allSelected == null || allSelected.Count == 0)
			return;

		List<GameObject> toRemove = new List<GameObject>();
		foreach (KeyValuePair<GameObject, string> entry in allSelected) {
			if (entry.Key == null || PrefabUtility.GetPrefabType (entry.Key) != PrefabType.ModelPrefab) {
				toRemove.Add (entry.Key);
			}
		}

		foreach (GameObject entry in toRemove) {
			allSelected.Remove (entry);
		}

		foreach (KeyValuePair<GameObject, string> entry in allSelected) {
			string loadingDirectory = "Assets/Models/";

			if (!entry.Value.StartsWith (loadingDirectory)) {
				Debug.LogError ("Selected Assets are not contained within " + loadingDirectory + " directory. Please move or copy assets to this directory, select them from there and try again.");
			} else {
				//get rid of location in hierarchy
				string subPathWithFileType = entry.Value.Remove (0, loadingDirectory.Length);
				//get rid of file type
				string subPathWithoutFileType = subPathWithFileType.Remove (subPathWithFileType.LastIndexOf ('.'));
				//get rid of file name
				string subPathWithoutFilename = subPathWithoutFileType.Remove (subPathWithoutFileType.LastIndexOf ('/'));
	
				//if directory does not already exist, make one
				System.IO.Directory.CreateDirectory(Application.dataPath + "/Resources/Prefabs/Converted Models/" + subPathWithoutFilename);

				Debug.Log (Application.dataPath + "/Resources/Prefabs/Converted Models/" + subPathWithoutFilename);

				//make meta file
				EditorApplication.Step ();
	
				//make prefab
				MakeSimplePrefabObj (entry.Key, subPathWithoutFileType);
	
				//force GC
				EditorApplication.SaveAssets ();
				EditorUtility.UnloadUnusedAssetsImmediate ();
				GC.Collect ();
				EditorApplication.Step ();
			}
		}
    }

	/*
    private static void MakeMultipleSimplePrefabObj(Queue<GameObject> allObjs)
    {
        if (allObjs.Count == 0)
            return;
        GameObject obj = allObjs.Dequeue();
        MakeSimplePrefabObj(obj);
        if (allObjs.Count > 0)
        {
            if (ConcaveCollider.DelayBatchedCalls)
                EditorApplication.delayCall += ()=>{ MakeMultipleSimplePrefabObj(allObjs); };
            else
                MakeMultipleSimplePrefabObj(allObjs);
        }
    }
    */

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

        // Save as a prefab
		string prefabAssetPath = string.Format("Assets/Resources/Prefabs/Converted Models/{0}.prefab", subPath);
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
            replaceIndex = availablePrefabs.FindIndex( (PrefabInfo testInfo)=>{
                return testInfo.fileName == newFileName;
            });
            if (replaceIndex >= 0)
                availablePrefabs.RemoveAt(replaceIndex);
        }

        if (prefab.shouldUse)
        {
            if (shouldRecomputePrefabInformation)
                prefab.ProcessPrefab();
            PrefabInfo newInfo = new PrefabInfo();
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

	private Color getNewUIDColor() {
		if (UID_BY_INDEX >= 0x1000000)
			Debug.LogError ("UID's has exceeded 256^3, the current max limit of objects which can be formed!");
		float r = (float) (UID_BY_INDEX / 0x10000) / 256f;
		float g = (float) ((UID_BY_INDEX / 0x100) % 0x100) / 256f;
		float b = (float) (UID_BY_INDEX % 0x100) / 256f;
		UID_BY_INDEX += 0x1;
		return new Color (r, g, b);
	}

    private void AddObjects(List<PrefabInfo> prefabList)
    {
        if (prefabList.Count == 0)
        {
            _failures++;
            return;
        }
		PrefabInfo info = prefabList[_rand.Next(0, prefabList.Count)];

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
			info = prefabList[_rand.Next(0, prefabList.Count)];
        }

        // Find a spot to place this object
        int spawnX, spawnZ;
        float modScale;
        HeightPlane targetHeightPlane;
        Quaternion modifiedRotation = Quaternion.identity;
        if (info.stackableAreas.Count == 0)
			modifiedRotation = Quaternion.Euler(new Vector3(0, (float) _rand.NextDouble() * 360f,0));
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
        
        Renderer[] RendererList = floor.GetComponentsInChildren<Renderer>();
		foreach (Renderer _rend in RendererList)
		{
			foreach (Material _mat in _rend.materials)
			{
				_mat.SetColor("_idval", new Color(0f,0f,1f/256f));	
			}
		}
		//{
		//	_rend.material.SetInt("_idval", 1);	
		//}	
        //floor.GetComponent<Renderer>().material.SetInt("_idval", 1);

		// Make a spawn plane
		SpawnArea floorSpawn = GameObject.Instantiate<SpawnArea>(Resources.Load<SpawnArea>("Prefabs/PlaneSpawn"));
		floorSpawn.gameObject.transform.position = roomCenter;
       	
		//retrieve ratio between spawn plane and floor
		float xRatio = floorSize.x / floorSpawn.gameObject.GetComponent<Collider>().bounds.size.x;
		float zRatio = floorSize.z / floorSpawn.gameObject.GetComponent<Collider>().bounds.size.z;

		floorSpawn.gameObject.transform.localScale = new Vector3 (xRatio * floorSpawn.gameObject.transform.localScale.x, 0, zRatio * floorSpawn.gameObject.transform.localScale.z);

		Debug.Log (floorSpawn.name);

        GameObject top = WallInfo.CreateBoxMesh(ceilingStart, floorSize, ceilingMaterial, "Ceiling", _curRoom);
        top.AddComponent<SemanticObjectSimple>();
        top.GetComponent<Rigidbody>().isKinematic = true;
        RendererList = top.GetComponentsInChildren<Renderer>();
		foreach (Renderer _rend in RendererList)
		{
			foreach (Material _mat in _rend.materials)
			{
				_mat.SetColor("_idval", new Color(0f,0f,2f/256f));	
			}
		}
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