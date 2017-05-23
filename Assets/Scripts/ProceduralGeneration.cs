
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
        public bool isLight;
        public GeneratablePrefab.AttachAnchor anchorType;
        public Bounds bounds;
        public List<GeneratablePrefab.StackableInfo> stackableAreas = new List<GeneratablePrefab.StackableInfo>();
    }

    [System.Serializable]
    public class Random_help{
        public System.Random _rand;
        public Random_help (int rand_seed){
            _rand   = new System.Random(rand_seed);
        }
        public float Next_Gaussian(float mean, float stdDev){
            
            double randNormal   = -1f;
            int try_times       = 0;
            while (randNormal <= 0f && try_times < 200){
                double u1 = _rand.NextDouble(); //these are uniform(0,1) random doubles
                double u2 = _rand.NextDouble();
                double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                             Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
                randNormal =
                             mean + stdDev * randStdNormal; //random normal(mean,stdDev^2)
                try_times   = try_times + 1;
            }
            if (try_times==200)
                randNormal  = 1;
            return (float) randNormal;
        }
    }


#region Fields
    // The number of physics collisions to create
    public int numCeilingLights = 10;
    public float intensityCeilingLights = 1.0f;
	public bool useStandardShader = false;
    public int minStackingBases = 0;
    public int forceStackedItems = 5;
    public int maxPlacementAttempts = 300;
    public bool randomMaterials = false;
    public SemanticObjectSimple floorPrefab;
    public SemanticObjectSimple ceilingPrefab;
    public GameObject DEBUG_testCubePrefab = null;
    public TextMesh DEBUG_testGridPrefab = null;
    public Vector3 roomDim = new Vector3(10f, 10f, 10f);
	public List<PrefabDatabase.PrefabInfo> availablePrefabs = new List<PrefabDatabase.PrefabInfo>();
    public List<string> disabledItems = new List<string>();
    public List<string> permittedItems = new List<string>();
    public float gridDim = 0.4f;
    public int use_cache_self   = 0; // 0 is for not (using Loadfromcacheordownload, 1 is for using)
    public int disable_rand_stacking = 1; //0 is for not disabling
    public int enable_global_unit_scale = 0; //1 is for enabling
    public string cache_folder  = "/Users/chengxuz/3Dworld/ThreeDWorld/Assets/PrefabDatabase/AssetBundles/file_cache_test/";
    public bool shouldUseStandardizedSize = false;
    public Vector3 standardizedSize = Vector3.one;
    public bool shouldUseGivenSeed = false;
    public int desiredRndSeed = -1;
	public System.Random _rand;

    // Relate to global scale control
    public int scene_scale_seed     = 0;
    public string scene_scale_con   = "NULL";
    public float scene_scale_mean   = 1f;
    public float scene_scale_var    = 0f;

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
    public Material skyboxMaterial = null;
    public Material windowTrimMaterial = null;
    public PhysicMaterial physicsMaterial = null;
    public bool showProcGenDebug = false;
    public List<Random_help> list_rands    = new List<Random_help>();

    private int _curRandSeed = 0;
    private int _curRoomWidth = 0;
    private int _curRoomLength = 0;
    private float _curRoomHeight = 0f;
    private Vector3 _roomCornerPos = Vector3.zero;
    private Transform _curRoom = null;
    private int _failures = 0; // Counter to avoid infinite loops if we can't place anything
    private List<WallArray> wallSegmentList = new List<WallArray>();
    //private List<PrefabDatabase.PrefabInfo> ceilingLightPrefabs = new List<PrefabDatabase.PrefabInfo>();
    private List<PrefabDatabase.PrefabInfo> groundPrefabs = new List<PrefabDatabase.PrefabInfo>();
    private List<PrefabDatabase.PrefabInfo> stackingPrefabs = new List<PrefabDatabase.PrefabInfo>();
	private List<PrefabDatabase.PrefabInfo> stackablePrefabs = new List<PrefabDatabase.PrefabInfo>();
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

    public void gen_rand_forinfo(ref PrefabDatabase.PrefabInfo info){
        if (!info.use_global_rand){
            list_rands.Add(new Random_help(info.scale_seed));
            info.rand_index     = list_rands.Count - 1;
            info.first_rand     = list_rands[info.rand_index].Next_Gaussian(info.dynamic_scale, info.scale_var);
        }
    }

    public void Init()
    {
    	// Reset the UID Color counter
    	resetUIDColor();

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
            randomMaterials = json["random_materials"].ReadBool(false);
            numCeilingLights = json["num_ceiling_lights"].ReadInt(numCeilingLights);
            useStandardShader = json["use_standard_shader"].ReadBool(false);
            intensityCeilingLights = json["intensity_ceiling_lights"].ReadFloat(intensityCeilingLights);
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
            use_cache_self      = json["use_cache_self"].ReadInt(use_cache_self);
            cache_folder        = json["cache_folder"].ReadString(cache_folder);
            disable_rand_stacking   = json["disable_rand_stacking"].ReadInt(disable_rand_stacking);
            enable_global_unit_scale    = json["enable_global_unit_scale"].ReadInt(enable_global_unit_scale);
            try {
                scene_scale_seed    = json["global_scale_dict"]["seed"].ReadInt(scene_scale_seed);
                scene_scale_mean    = json["global_scale_dict"]["scale"].ReadFloat(scene_scale_mean);
                scene_scale_var     = json["global_scale_dict"]["var"].ReadFloat(scene_scale_var);
                scene_scale_con     = json["global_scale_dict"]["option"].ReadString(scene_scale_con);
            }
            catch{}
        }

        list_rands.Clear();
        list_rands.Add(new Random_help(scene_scale_seed));

		_curRandSeed = UnityEngine.Random.Range (int.MinValue, int.MaxValue);
        if (shouldUseGivenSeed) {
            _rand = new System.Random (desiredRndSeed);
            _curRandSeed = desiredRndSeed;
        } else {
            _rand = new System.Random (_curRandSeed);
        }

        // Create grid to populate objects
        _failures = 0;

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

		LitJson.JsonData prefabRounds = json["rounds"];
		if (prefabRounds != null) {
			for(int r = 0; r < prefabRounds.Count; r++)
			{
				availablePrefabs = new List<PrefabDatabase.PrefabInfo>();
				int num_items = prefabRounds[r]["num_items"].ReadInt(0);
				LitJson.JsonData items = prefabRounds[r]["items"];
				if(items != null) {
					for(int i = 0; i < items.Count; i++)
					{
						PrefabDatabase.PrefabInfo newInfo = new PrefabDatabase.PrefabInfo();
						newInfo.fileName = items[i]["aws_address"].ReadString(newInfo.fileName);

						newInfo.bounds.center = items[i]["center_pos"].ReadVector3(new Vector3(0f, 0f, 0f));
						newInfo.bounds.extents = items[i]["boundb_pos"].ReadVector3(new Vector3(0f, 0f, 0f));
						newInfo._id_str  = items[i]["_id_str"].ReadString(newInfo._id_str);
						newInfo.aws_version = items[i]["aws_version"].ReadString(newInfo.aws_version);

						newInfo.mass = items[i]["mass"].ReadFloat(1.0f);
						newInfo.set_scale(items[i]["scale"]);
						newInfo.round_num = r;
						gen_rand_forinfo(ref newInfo);

						availablePrefabs.Add(newInfo);
					}
				}
				groundPrefabs = availablePrefabs.FindAll(((PrefabDatabase.PrefabInfo info)=>
				        {return info.anchorType == GeneratablePrefab.AttachAnchor.Ground;}));
				Debug.Log ("About to add objects");
				for(int i = 0; i < num_items; i++)
				{
					if (!AddObjects (groundPrefabs)) {
						Debug.LogWarning(String.Format("Not enough objects spawned. {0} out of {1}", i, num_items));
						break;
					}
				}
			}
		}
    }

    private bool TryPlaceGroundObject(Bounds bounds, float modScale, GeneratablePrefab.AttachAnchor anchorType, out int finalX, out int finalY, out HeightPlane whichPlane, out float offset_height)
    {
        finalX = 0;
        finalY = 0;
        offset_height = 0.1f;
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

        bool foundValid = false;
        foreach(int planeNum in randomPlanesOrder)
        {
            HeightPlane curHeightPlane = _allHeightPlanes[planeNum];
            // Make sure we aren't hitting the ceiling
            if (boundsHeight >= curHeightPlane.planeHeight || curHeightPlane.cornerPos.y + boundsHeight >= _curRoomHeight) {
                continue;
            }
            // Only get grid squares which are valid to place on.
            
            // List<GridInfo> validValues = curHeightPlane.myGridSpots.FindAll((GridInfo info)=>{return info.rightSquares >= (boundsWidth-1) && info.downSquares > (boundsLength-1) && !info.inUse;});
            List<GridInfo> validValues = curHeightPlane.myGridSpots.FindAll((GridInfo info)=>{return info.rightSquares >= (boundsWidth-1) && info.downSquares > (boundsLength-1);});
            //Debug.Log("Valid positions:" + validValues.Count);
            while(validValues.Count > 0 && !foundValid)
            {
				int randIndex = _rand.Next(0, validValues.Count);
                GridInfo testInfo = validValues[randIndex];
                validValues.RemoveAt(randIndex);
                if (curHeightPlane.TestGrid(testInfo, boundsWidth, boundsLength))
                {
                    Vector3 centerPos = curHeightPlane.cornerPos + new Vector3(gridDim * (testInfo.x + (0.5f * boundsWidth)), 0.1f+boundsHeight, gridDim * (testInfo.y + (0.5f * boundsLength)));
                    if (anchorType == GeneratablePrefab.AttachAnchor.Ceiling)
                    {
                        centerPos.y = _roomCornerPos.y + _curRoomHeight - (0.1f+boundsHeight);
                    }
                    if (Physics.CheckBox(centerPos, testBounds.extents) && (disable_rand_stacking == 1))
                    //if (false)
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
                    if (Physics.CheckBox(centerPos, testBounds.extents) && (disable_rand_stacking == 0)){

                        float max_height = 0f;
                        int try_times = 0;
                        while (Physics.CheckBox(centerPos, testBounds.extents) &&  try_times < 20){
                            Collider[] hitObjs = Physics.OverlapBox(centerPos, testBounds.extents);
                            HashSet<string> hitObjNames = new HashSet<string>();
                            foreach(Collider col in hitObjs)
                            {
                                if (col.attachedRigidbody != null)
                                    hitObjNames.Add(col.attachedRigidbody.name);
                                else
                                    hitObjNames.Add(col.gameObject.name );
                                if (max_height < col.bounds.center.y + col.bounds.extents.y){
                                    max_height  = col.bounds.center.y + col.bounds.extents.y;
                                };
                            }
                            centerPos.y     = max_height + 0.1f+boundsHeight;
                            try_times       = try_times + 1;
                        }
//                        Debug.Log("Choosing highest height:" + max_height + " " + try_times);
                        finalX = testInfo.x;
                        finalY = testInfo.y;
                        offset_height = max_height + 0.1f;
                        whichPlane = curHeightPlane;
                        foundValid = true;
                        return foundValid;
                    }

                    if (!Physics.CheckBox(centerPos, testBounds.extents) || (disable_rand_stacking == 0))
                    {
//                        Debug.LogFormat("Selecting ({0},{1}) which has ({2},{3}) to place ({4},{5})", testInfo.x, testInfo.y, testInfo.rightSquares, testInfo.downSquares, boundsWidth, boundsLength);
                        finalX = testInfo.x;
                        finalY = testInfo.y;
                        offset_height = 0.1f;
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

	public static void resetUIDColor() {
		UID_BY_INDEX = 0x3;
	}

	public static Color getNewUIDColor() {
		if (UID_BY_INDEX >= 0x1000000)
			Debug.LogError ("UID's has exceeded 256^3, the current max limit of objects which can be formed!");
		float r = (float) (UID_BY_INDEX / 0x10000) / 255f;
		float g = (float) ((UID_BY_INDEX / 0x100) % 0x100) / 255f;
		float b = (float) (UID_BY_INDEX % 0x100) / 255f;
		UID_BY_INDEX += 0x1;
		return new Color (r, g, b);
	}

    private bool AddObjects(List<PrefabDatabase.PrefabInfo> prefabList)
    {
        if (prefabList.Count == 0)
        {
            return false;
        }

        // Randomly add next one?
        int temp_rand_index     = _rand.Next(0, prefabList.Count);
        PrefabDatabase.PrefabInfo info = prefabList[temp_rand_index];

        //float modScale = prefabDatabase.GetSceneScale (info);
        float modScale = 1.0f;
        float offset_y = 0.1f;

        // For global setting
        if (enable_global_unit_scale==1){
            float longest_axis = info.bounds.size.magnitude;
            modScale = 1/longest_axis;
        }

        //TODO Verify that we want to scale by the longest_axis here first (DOSCH very big otherwise)
        if (scene_scale_con=="Multi_size") 
        {
			float longest_axis = info.bounds.size.magnitude;
            modScale = list_rands[0].Next_Gaussian(scene_scale_mean, scene_scale_var);
			modScale = modScale/longest_axis;
        }

        if (scene_scale_con=="Absol_size"){
            float longest_axis      = info.bounds.size.magnitude;
            modScale                = list_rands[0].Next_Gaussian(scene_scale_mean, scene_scale_var)/longest_axis;
        }

        // For option "Multi_size"
        if (info.option_scale=="Multi_size"){
            if (info.apply_to_inst)
                modScale    = list_rands[info.rand_index].Next_Gaussian(info.dynamic_scale, info.scale_var);
            else
                modScale    = info.first_rand;
        }

        // For option "Absol_size"
        if (info.option_scale=="Absol_size"){
            float longest_axis      = info.bounds.size.magnitude;
			if (info.apply_to_inst) {
				modScale = list_rands [info.rand_index].Next_Gaussian (info.dynamic_scale, info.scale_var) / longest_axis;
			} else {
				modScale = info.first_rand / longest_axis;
			}
		}

        HeightPlane targetHeightPlane;
        Quaternion modifiedRotation = Quaternion.identity;

        if (info.stackableAreas.Count == 0)
            modifiedRotation = Quaternion.Euler(new Vector3(0, (float) _rand.NextDouble() * 360f,0));
        Bounds modifiedBounds = info.bounds.Rotate(modifiedRotation);

		// Find a spot to place this object
        int spawnX, spawnZ;
        if (TryPlaceGroundObject(modifiedBounds, modScale, info.anchorType, out spawnX, out spawnZ, out targetHeightPlane, out offset_y))
        {
            int boundsWidth = Mathf.CeilToInt(modScale* 2f * modifiedBounds.extents.x / gridDim) - 1;
            int boundsLength = Mathf.CeilToInt(modScale* 2f * modifiedBounds.extents.z / gridDim) - 1;
            float modHeight = offset_y+(modifiedBounds.extents.y * modScale);
            Vector3 centerPos = targetHeightPlane.cornerPos + new Vector3(gridDim * (spawnX + (0.5f * boundsWidth)), modHeight, gridDim * (spawnZ + (0.5f * boundsLength)));
            if (info.anchorType == GeneratablePrefab.AttachAnchor.Ceiling)
                centerPos.y = _roomCornerPos.y + _curRoomHeight - modHeight;

            GameObject newPrefab;
            if (info.fileName.ToLowerInvariant().Contains("http://")) {
                if (use_cache_self==1) {
                    //I need to do this here becuase StartCoroutine can not be correctly used in PrefabDatabase
                    string cache_fileName   = cache_folder + info._id_str + "_" + info.aws_version + ".bundle";
                    newPrefab   = PrefabDatabase.LoadAssetFromBundle(cache_fileName);
                    if (newPrefab==null){
                        // Loading it twice now, might influence the efficiency, TODO: load only once
                        // Currently the WWW can not be wrote when used for assetbundle
                        StartCoroutine(PrefabDatabase.LoadAssetFromBundleWWW_cached_self(info.fileName, cache_fileName));
                        newPrefab = PrefabDatabase.LoadAssetFromBundleWWW(info.fileName);
                    }
                } else {
                    newPrefab = PrefabDatabase.LoadAssetFromBundleWWW(info.fileName);
                }
            } else {
                newPrefab = PrefabDatabase.LoadAssetFromBundle(info.fileName);
            }

            GameObject newInstance = UnityEngine.Object.Instantiate<GameObject>(newPrefab.gameObject);
            newInstance.transform.position = centerPos - (modifiedBounds.center * modScale);
            newInstance.transform.localScale = newInstance.transform.localScale * modScale;
            newInstance.transform.rotation = modifiedRotation * newInstance.transform.rotation;
  
            newInstance.name = string.Format("{0}, {1}, {2}", info.fileName, 
                    newPrefab.name, (_curRoom != null) ? _curRoom.childCount.ToString() : "?");
            newInstance.GetComponent<SemanticObject>().initLocalScale = newInstance.transform.localScale;
			newInstance.GetComponent<SemanticObject>().isStatic = false;
			newInstance.GetComponent<SemanticObject>().extents = modifiedBounds.extents;
			newInstance.GetComponent<SemanticObject> ().center = modifiedBounds.center;
			newInstance.GetComponent<SemanticObject> ().round_num = info.round_num;
			newInstance.GetComponent<Rigidbody> ().mass = info.mass;

			// Add physics material
			Collider[] colliders = newInstance.GetComponentsInChildren<Collider>();
			foreach(Collider collider in colliders)
			{
				collider.material = physicsMaterial;
			}

            Renderer[] RendererList = newInstance.GetComponentsInChildren<Renderer>();
            Color colorID = getNewUIDColor ();
            foreach (Renderer _rend in RendererList)
            {
                    foreach (Material _mat in _rend.materials)
                    {
                    		// This is just to make objects easier identifiable 
                    		// by using the "Get Identity" shader
							Shader originalShader = _mat.shader;
							_mat.shader = Shader.Find("Get Identity");
							_mat.SetColor("_idval", colorID);
							_mat.shader = originalShader;

							// Always use standard shader
							if(useStandardShader) 
								_mat.shader = Shader.Find("Standard");

                            // Set glossiness and metallic randomly
                            if(randomMaterials) {
								float GLOSS_MEAN = 0.588270935961f;
								float GLOSS_STD = 0.265175303096f;
								float METALLIC_MEAN = 0.145517241379f;
								float METALLIC_STD = 0.271416832554f;

								// Set glossiness using statistical properties derived from a test set
								Random_help random_gloss = new Random_help(_rand.Next());
								float glossiness = GLOSS_MEAN;
								if(_mat.HasProperty("_Glossiness"))
									glossiness = _mat.GetFloat("_Glossiness");
								//glossiness = glossiness + Convert.ToSingle(_rand.NextDouble()) * GLOSS_STD * 2 - GLOSS_STD;
								glossiness = random_gloss.Next_Gaussian(glossiness, GLOSS_STD);
								glossiness = Mathf.Min(glossiness, 1.0f);
								glossiness = Mathf.Max(glossiness, 0.0f);
								_mat.SetFloat("_Glossiness", glossiness);

								// Set metallic using statistical properties derived from a test set
								Random_help random_metallic = new Random_help(_rand.Next());
								float metallic = METALLIC_MEAN;
								if(_mat.HasProperty("_Metallic"))
									metallic = _mat.GetFloat("_Metallic");
								//metallic = metallic + Convert.ToSingle(_rand.NextDouble()) * METALLIC_STD * 2 - METALLIC_STD;
								metallic = random_metallic.Next_Gaussian(metallic, METALLIC_STD);
								metallic = Mathf.Min(metallic, 1.0f);
								metallic = Mathf.Max(metallic, 0.0f);
								_mat.SetFloat("_Metallic", metallic);	
							}

							// Add idval to shader
							_mat.SetColor("_idval", colorID);
                    }
            }

            if (_curRoom != null)
                newInstance.transform.SetParent(_curRoom);
        }
        else
        {
            // TODO: Mark item as unplaceable and continue with smaller objects?
            prefabList.Remove(info);
        }
		return true;
    }

	public void CreateLightingSetup(Vector3 roomDimensions, Vector3 roomCenter)
    {
    	// roomDimensions are the dimensions of the room, with the y coordinate describing the height of the room
    	// roomCenter describes the center of the room
		Vector3 ceilingSize = new Vector3(roomDimensions.x, 0, roomDimensions.z);
		Vector3 ceilingStart = roomCenter + new Vector3(-0.5f * roomDimensions.x, roomDimensions.y, -0.5f * roomDimensions.z);

		// set skybox to Material assigned to ProceduralGeneration prefab
        RenderSettings.skybox = skyboxMaterial;

        /* 
         *  We want to keep the total light energy constant in a given volume. 
         *  Therefore, we base the number of lights and their intensities on the volume of each room.
         *  At first the intensity of each light is assumed to be constant.
         */
		double scalingFactor = 0.0156;
		int totalNumberOfLights = Convert.ToInt32(scalingFactor * roomDimensions.x * roomDimensions.z);

		if(numCeilingLights > 0)
			totalNumberOfLights = numCeilingLights;

		int widthNumberOfLights = Convert.ToInt32(Math.Sqrt(totalNumberOfLights * roomDimensions.x / roomDimensions.z));
		int lengthNumberOfLights = Convert.ToInt32(Math.Sqrt(totalNumberOfLights * roomDimensions.z / roomDimensions.x));

		float widthLightDistance = ceilingSize.x / widthNumberOfLights;
		float lengthLightDistance = ceilingSize.z / lengthNumberOfLights;

		float intensity = 1.0f;

		if(intensityCeilingLights > 0.0f)
			intensity = intensityCeilingLights;

		int iter_light = 0;
		for(float i = ceilingStart.x + widthLightDistance / 2.0f; i <= ceilingStart.x + ceilingSize.x; i = i + widthLightDistance)
        {
			for(float j = ceilingStart.z + lengthLightDistance / 2.0f; j <= ceilingStart.x + ceilingSize.z; j = j + lengthLightDistance)
        	{
				GameObject spotLightGameObject = new GameObject("Spot Light " + iter_light.ToString());
        		Light spotLight = spotLightGameObject.AddComponent<Light>();
        		spotLight.type = LightType.Spot;
				spotLight.color = Color.white;
				spotLight.range = 30;
				spotLight.intensity = intensity;
				spotLight.spotAngle = 145.0f;
				spotLight.renderMode = LightRenderMode.ForcePixel;
				spotLight.shadows = LightShadows.Soft;
				spotLightGameObject.transform.position = new Vector3(i, ceilingStart.y * 0.8f, j);
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
}
