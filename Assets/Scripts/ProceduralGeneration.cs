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

    [System.Serializable]
    public class GridInfo
    {
        public float height;
        public int x;
        public int y;
        public int leftSquares;
        public int rightSquares;
        public int upSquares;
        public int downSquares;
    }

#region Fields
    // The number of physics collisions to create
    public int complexityLevelToCreate = 100;
    public int desiredSpacing = 100;
    public SemanticObjectSimple wallPrefab;
    public SemanticObjectSimple floorPrefab;
    public GameObject DEBUG_testCubePrefab;
    public TextMesh DEBUG_testGridPrefab;
    public Vector3 roomDim = new Vector3(10f, 10f, 10f);
    public List<PrefabInfo> availablePrefabs = new List<PrefabInfo>();
    public float gridDim = 0.4f;

    private int curComplexity = 0;
    private int curRoomWidth = 0;
    private int curRoomLength = 0;
    private Vector3 roomCornerPos = Vector3.zero;
    private Transform curRoom = null;
    private int failures = 0; // Counter to avoid infinite loops if we can't place anything
    // TODO: Move this into its own data structure!
    // TODO: Change this to be a list of said data structure, for multiple placement subsections
    private List<GridInfo> availableGridSpots = new List<GridInfo>();
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
        failures = 0;
//        oldAvailableGridSpots.Clear();
//        float xDist = roomDim.x / desiredSpacing;
//        float xStart = (roomDim.x * -0.5f) + (0.5f * xDist);
//        float zDist = roomDim.z / desiredSpacing;
//        float zStart = (roomDim.z * -0.5f) + (0.5f * zDist);
//        for(int i = 0; i < desiredSpacing; ++i)
//        {
//            for(int j = 0; j < desiredSpacing; ++j)
//            {
//                oldAvailableGridSpots.Add(new Vector3(xStart + (xDist * i), 1.0f, zStart + (zDist * j)));
//            }
//        }

        availableGridSpots.Clear();
        curRoomWidth = Mathf.FloorToInt((roomDim.x - 2.0f) / gridDim);
        curRoomLength = Mathf.FloorToInt((roomDim.z - 2.0f) / gridDim);
        for(int i = 0; i < curRoomWidth; ++i)
        {
            for(int j = 0; j < curRoomLength; ++j)
            {
                GridInfo newGridInfo = new GridInfo();
                newGridInfo.x = i;
                newGridInfo.y = j;
                ModifyGrid(newGridInfo, i, j, curRoomWidth, curRoomLength);
                availableGridSpots.Add(newGridInfo);
            }
        }

        // Keep creating objects until we are supposed to stop
        while(!IsDone())
            AddObjects();

        foreach(GridInfo g in availableGridSpots)
        {
            TextMesh test = GameObject.Instantiate<TextMesh>(DEBUG_testGridPrefab);
            test.text = string.Format("  {0}\n{2}  {1}\n  {3}", g.upSquares, g.leftSquares, g.rightSquares, g.downSquares);
            test.color = (g.height > 0) ? Color.red: Color.cyan;
            test.transform.position = roomCornerPos + new Vector3(gridDim * g.x, 0.0f, gridDim * g.y);
            test.name = string.Format("{0}: ({1},{2})", DEBUG_testGridPrefab.name, g.x, g.y);
            test.transform.localScale = gridDim * Vector3.one;
        }
    }

    private int Index(int x, int y)
    {
        return (curRoomLength * x ) + y;
    }

    private void ModifyGrid(GridInfo info, int i, int j, int width, int length)
    {
        info.leftSquares = i;
        info.rightSquares = width - i - 1;
        info.upSquares = j;
        info.downSquares= length - j - 1;
    }

    private void UpdateGrid(int startX, int startY, int dimX, int dimY, float newHeight)
    {
        try
        {
//        Debug.LogFormat("UpdateGrid({0},{1},{2},{3},{4})", startX, startY, dimX, dimY, newHeight);
        int numToCheck;
        for(int i = 0; i <= dimX; ++i)
        {
            // check up
            numToCheck = availableGridSpots[Index(i + startX, startY)].upSquares;
            for(int j = 1; j <= numToCheck; ++j)
                availableGridSpots[Index(i + startX, startY - j)].downSquares = j - 1;
            // check down
            numToCheck = availableGridSpots[Index(i + startX, startY + dimY)].downSquares;
            for(int j = 1; j <= numToCheck; ++j)
                availableGridSpots[Index(i + startX, startY + dimY + j)].upSquares = j - 1;
        }
        for(int j = 0; j <= dimY; ++j)
        {
            // check left
            numToCheck = availableGridSpots[Index(startX, j + startY)].leftSquares;
            for(int i = 1; i <= numToCheck; ++i)
                availableGridSpots[Index(startX - i, j + startY)].rightSquares = i - 1;
            // check right
            numToCheck = availableGridSpots[Index(startX + dimX, j + startY)].rightSquares;
//            GridInfo refPoint = availableGridSpots[Index(startX + dimX, j + startY)];
//            Debug.LogFormat("{0} ({1},{2}) instead of ({3},{4})", refPoint.rightSquares, refPoint.x, refPoint.y, startX + dimX, j + startY);
            for(int i = 1; i <= numToCheck; ++i)
            {
//                int curIndex = Index(startX + dimX + i, j + startY);
//                if (curIndex >= availableGridSpots.Count)
//                {
//                    Debug.LogFormat("{0} ({1},{2}) instead of ({3},{4})", refPoint.rightSquares, refPoint.x, refPoint.y, startX + dimX, j + startY);
//                    Debug.LogFormat("Overflow with {0}/{1} sx{2} dx{3} i{4} num{5} j{6} sy:{7} dim: ({8},{9})", curIndex, availableGridSpots.Count, startX, dimX, i, numToCheck, j, startY, curRoomWidth, curRoomLength);
//                }
                availableGridSpots[Index(startX + dimX + i, j + startY)].leftSquares = i - 1;
            }
        }
        for(int i = 0; i <= dimX; ++i)
        {
            for(int j = 0; j <= dimY; ++j)
            {
                int index = Index(i + startX, j + startY);
                availableGridSpots[index].height = newHeight;
                // TODO: For stackable objects, add raised platform stuff
                ModifyGrid(availableGridSpots[index], 0, 0, 1, 1);
            }
        }
        }
        catch (System.Exception e)
        {
            Debug.LogWarningFormat("Got exception {0}", e.ToString());
        }
    }

    private bool TestGrid(GridInfo info, int dimX, int dimY)
    {
        for(int i = 0; i < dimX; ++i)
        {
            if (availableGridSpots[Index(info.x + i, info.y)].downSquares < (dimY-1))
                return false;
        }
        return true;
    }

    private bool TryPlaceGroundObject(Bounds bounds, out int finalX, out int finalY)
    {
        finalX = 0;
        finalY = 0;
        int boundsWidth = Mathf.CeilToInt(2 * bounds.extents.x / gridDim);
        int boundsLength = Mathf.CeilToInt(2 * bounds.extents.z / gridDim);
//        float newHeight = bounds.extents.y;

        // TODO: Filter values here!
        List<GridInfo> validValues = availableGridSpots.FindAll((GridInfo info)=>{return info.rightSquares >= (boundsWidth-1) && info.downSquares > (boundsLength-1) && info.height <= 0;});
        bool foundValid = false;
        while(validValues.Count > 0 && !foundValid)
        {
            int randIndex = Random.Range(0, validValues.Count);
            GridInfo testInfo = validValues[randIndex];
            validValues.RemoveAt(randIndex);
            if (TestGrid(testInfo, boundsWidth, boundsLength))
            {
//                Debug.LogFormat("Selecting ({0},{1}) which has ({2},{3}) to place ({4},{5})", testInfo.x, testInfo.y, testInfo.rightSquares, testInfo.downSquares, boundsWidth, boundsLength);
                finalX = testInfo.x;
                finalY = testInfo.y;
                foundValid = true;
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
                    prefab.ProcessPrefab();
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

        // Find a spot to place this object
        int spawnX, spawnZ;
        if (TryPlaceGroundObject(info.bounds, out spawnX, out spawnZ))
        {
            int boundsWidth = Mathf.CeilToInt(2 * info.bounds.extents.x / gridDim) - 1;
            int boundsLength = Mathf.CeilToInt(2 * info.bounds.extents.z / gridDim) - 1;
            Vector3 centerPos = roomCornerPos + new Vector3(gridDim * (spawnX + (0.5f * boundsWidth)), 0.1f+info.bounds.extents.y, gridDim * (spawnZ + (0.5f * boundsLength)));

            GameObject newPrefab = Resources.Load<GameObject>(info.fileName);
            // TODO: Factor in complexity to the arrangement algorithm?
            curComplexity += info.complexity;

            GameObject newInstance = Object.Instantiate<GameObject>(newPrefab.gameObject);
            newInstance.transform.position = centerPos - info.bounds.center;
            newInstance.name = string.Format("{0} #{1}", newPrefab.name, (curRoom != null) ? curRoom.childCount.ToString() : "?");

            // Create test cube
            GameObject testCube = Object.Instantiate<GameObject>(DEBUG_testCubePrefab);
            testCube.transform.localScale = 2 * info.bounds.extents;
            testCube.transform.position = centerPos;
            testCube.name = string.Format("Cube {0}", newInstance.name);
            testCube.transform.SetParent(curRoom);

            Debug.LogFormat("{0}: @{1} R:{2} G:{3} BC:{4}", info.fileName, newInstance.transform.position, roomCornerPos, new Vector3(gridDim * spawnX, info.bounds.extents.y, gridDim * spawnZ), info.bounds.center);
            if (curRoom != null)
                newInstance.transform.SetParent(curRoom);

            UpdateGrid(spawnX, spawnZ, boundsWidth, boundsLength, 2 * info.bounds.extents.z);
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
        GameObject floor = GameObject.Instantiate(floorPrefab.gameObject);
        floor.transform.localScale = new Vector3(roomDimensions.x, 1.0f, roomDimensions.z);
        floor.transform.position = roomCenter;
        floor.transform.SetParent(curRoom);
    }

    public bool IsDone()
    {
        return curComplexity >= complexityLevelToCreate || failures > 10;
    }
}
