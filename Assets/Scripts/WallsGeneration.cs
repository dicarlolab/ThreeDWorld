using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class WallInfo
{
    public string name = "";
    public Vector3 startPos = Vector3.zero;
    public float length = 1f;
    public bool isNorthSouth = false;
    public int startGridX;
    public int startGridY;
    public int gridLength;
    public Material wallMat = null;

    [System.Serializable]
    public struct HoleInfo
    {
        public Vector2 bottomCorner;
        public Vector2 size;
    };

    public List<HoleInfo> holes = new List<HoleInfo>();
    public List<float> intersectionPoints = new List<float>();


    public void SortHoles()
    {
        holes.Sort((HoleInfo x, HoleInfo y) => {
            return x.bottomCorner.x.CompareTo(y.bottomCorner.x);
        });
    }

    public void MarkIntersection(float lengthPos)
    {
        intersectionPoints.Add(lengthPos);
        intersectionPoints.Sort();
    }

    public void AddDoor()
    {
        // TODO: Add multiple doors?
        const float DOOR_WIDTH = 1.5f;
        const float DOOR_HEIGHT = 3.0f;
        HoleInfo doorHole = new HoleInfo();
        doorHole.size = new Vector2(DOOR_WIDTH, DOOR_HEIGHT);
        float midPoint = length * 0.5f, longestSection = 0.0f, curSection = 0f, prevPoint = 0f;
        foreach (float point in intersectionPoints)
        {
            curSection = point - prevPoint;
            if (curSection > longestSection)
            {
                longestSection = curSection;
                midPoint = prevPoint + (0.5f * curSection);
            }
            prevPoint = point;
        }
        doorHole.bottomCorner = new Vector2(midPoint - (0.5f * DOOR_WIDTH), 0.0f);
        holes.Add(doorHole);
    }


    public GameObject ConstructWallSegment(float wallWidth, float wallHeight, Transform parentObj = null)
    {
        Vector3 baseSize = isNorthSouth ? new Vector3(wallWidth, wallHeight, 0f) : new Vector3(0f, wallHeight, wallWidth);
        Vector3 lengthDir = isNorthSouth ? new Vector3(0f, 0f, 1f) : new Vector3(1f, 0f, 0f);
        SortHoles();
        float curX = 0.0f, finalX = length;

        // Create base object properties
        GameObject wallBase = new GameObject(name);
        if (parentObj != null)
            wallBase.transform.SetParent(parentObj);
        wallBase.AddComponent<SemanticObjectSimple>();
        Rigidbody rb = wallBase.GetComponent<Rigidbody>();
        rb.isKinematic = true;

        // Fill in segments for holes
        Vector3 newSize, newStartPos;
        // TODO: If we have vertically overlapping windows/doors we'll need to adjust this logic!
        for (int i = 0; i < holes.Count; ++i)
        {
            HoleInfo holeInfo = holes[i];

            newSize = baseSize + (holeInfo.bottomCorner.x - curX) * lengthDir;
            if (holeInfo.bottomCorner.x - curX > 0)
                CreateWallMesh(startPos + curX * lengthDir, newSize, wallBase.transform);
            curX = holeInfo.bottomCorner.x;
            newSize = baseSize + (holeInfo.size.x * lengthDir);

            // Below
            newStartPos = startPos + curX * lengthDir;
            newSize.y = holeInfo.bottomCorner.y;
            if (newSize.y > 0)
                CreateWallMesh(newStartPos, newSize, wallBase.transform);

            // Above
            newStartPos.y = startPos.y + holeInfo.bottomCorner.y + holeInfo.size.y;
            newSize.y = baseSize.y - (holeInfo.bottomCorner.y + holeInfo.size.y);
            if (newSize.y > 0)
                CreateWallMesh(newStartPos, newSize, wallBase.transform);

            curX = holeInfo.bottomCorner.x + holeInfo.size.x;
        }

        // Final segment
        newSize = baseSize + (finalX - curX) * lengthDir;
        if (finalX - curX > 0)
            CreateWallMesh(startPos + curX * lengthDir, newSize, wallBase.transform);

        return wallBase;
    }

    public GameObject CreateWallMesh(Vector3 start, Vector3 size, Transform parentObj = null)
    {
        int[] indexArray = {
            // Left/Right faces
            2, 1, 3,
            2, 0, 1,
//            10,1,3,
//            10,8,1,
            6, 7, 5,
            6, 5, 4,

            // Top/Bottom faces
            12, 13, 9,
            12, 9, 8,
            14, 11, 15,
            14, 10, 11,

            // Front/Back faces
            5, 7, 3,
            5, 3, 1,
            4, 2, 6,
            4, 0, 2
        };

        const int vertCount = 16;
        const int indexCount = 3 * 2 * 6;
        Vector3[] newVerts = new Vector3[vertCount];
        int[] newIndices = new int[indexCount];
        List<Vector2> newUVs = new List<Vector2>();
        Vector2 startUV = new Vector2(start.x + start.z, start.y);


        // Create vertices
        for (int isTopBottom = 0; isTopBottom < 2; isTopBottom++)
        {
            for (int i = 0; i < 2; ++i)
            {
                for (int j = 0; j < 2; ++j)
                {
                    for (int k = 0; k < 2; ++k)
                    {
                        newVerts[8 * isTopBottom + 4 * i + 2 * j + k] = start + new Vector3((i == 1) ? size.x : 0, (j == 1) ? size.y : 0, (k == 1) ? size.z : 0);
                        Vector2 newUV = Vector2.zero;
                        if (isTopBottom == 0)
                        {
                            newUV.y = startUV.y + (j * size.y);
                            newUV.x = startUV.x + (i * size.x) + (k * size.z);
                        } else
                        {
                            newUV.y = start.x + start.y + (i * size.x) + (j * size.y);
                            newUV.x = start.z + 4f + (k * size.z);
                        }
                        newUVs.Add(newUV);
                    }
                }
            }
        }

        // Create faces
        for (int i = 0; i < indexArray.Length; ++i)
        {
            int index = indexArray[i];
            newIndices[i] = index;
        }

        Mesh newMesh = new Mesh();
        newMesh.vertices = newVerts;
        newMesh.triangles = newIndices;
        newMesh.SetUVs(0, newUVs);

        GameObject newObj = new GameObject(string.Format("Created Mesh @{0} with size{1}", start, size));
        if (parentObj != null)
            newObj.transform.SetParent(parentObj, false);
        MeshFilter meshFilter = newObj.AddComponent<MeshFilter>();
        meshFilter.mesh = newMesh;
        newObj.AddComponent<MeshRenderer>().material = wallMat;
        BoxCollider col = newObj.AddComponent<BoxCollider>();
        col.size = size;
        col.center = start + (0.5f * size);
        return newObj;
    }
};


[System.Serializable]
public class WallArray : IEnumerable<WallInfo>
{
#region Fields
    public string name;
    public List<WallInfo> myWalls = new List<WallInfo>();
    public HeightPlane myPlane;
    static public float WALL_WIDTH = 1.0f;
    static public float WALL_HEIGHT = 1.0f;
    static public float MIN_SPACING = 1.0f;
    static public Material WALL_MATERIAL = null;
#endregion


    static private bool HelperFunction(int testVal, ref bool foundFirstZero)
    {
        if (testVal <= MIN_SPACING)
        {
            if (testVal == 0 && !foundFirstZero)
                foundFirstZero = true;
            else
                return true;
        }
        return false;
    }
    static private bool HelperFunction2(GridInfo test)
    {
        bool foundFirstZero = false;
        if (HelperFunction(test.downSquares, ref foundFirstZero))
            return false;
        if (HelperFunction(test.upSquares, ref foundFirstZero))
            return false;
        if (HelperFunction(test.rightSquares, ref foundFirstZero))
            return false;
        if (HelperFunction(test.leftSquares, ref foundFirstZero))
            return false;
        return foundFirstZero;
    }

    static public WallArray PlotNewWallArray(HeightPlane givenPlane, string newName = "")
    {
        WallArray newWallSet = new WallArray();
        newWallSet.name = newName;
        newWallSet.myPlane = givenPlane;

        // Find start spot
        List<GridInfo> possibleStartPoints = givenPlane.myGridSpots.FindAll(HelperFunction2);
        if (possibleStartPoints.Count == 0)
            return null;

        int randIndex = Random.Range(0, possibleStartPoints.Count);
        GridInfo wallStart = possibleStartPoints[randIndex];
        int dx = 0, dy = 0;
        int count = 0;
        if (wallStart.leftSquares== 0)
        {
            dx = 1;
            count = wallStart.rightSquares;
        }
        if (wallStart.rightSquares == 0)
        {
            dx = -1;
            count = wallStart.leftSquares;
        }
        if (wallStart.downSquares == 0)
        {
            dy = -1;
            count = wallStart.upSquares;
        }
        if (wallStart.upSquares == 0)
        {
            dy = 1;
            count = wallStart.downSquares;
        }

        // TODO: Add in twists?
        WallInfo newWall = newWallSet.BuildInteriorWallSegment(wallStart.x, wallStart.y, dx, dy, count, givenPlane); 
        newWallSet.myWalls.Add(newWall);
        newWall.name = string.Format("{0} #{1}", newName, newWallSet.myWalls.Count);
        return newWallSet;
    }

    static public WallArray CreateRoomOuterWalls(HeightPlane curPlane)
    {
        WallArray baseRoom = new WallArray();
        baseRoom.name = "Outer Wall";
        // Build initial walls
        for(int i = 0; i < 4; ++i)
        {
            WallInfo newWall = new WallInfo();
            newWall.name = "Outer Wall " + i;
            newWall.isNorthSouth = i % 2 == 1;
            newWall.startGridX = -1;
            newWall.startGridY = -1;
            newWall.wallMat = WALL_MATERIAL;
            if (!newWall.isNorthSouth)
            {
                newWall.gridLength = curPlane.dimWidth;
                newWall.length = (2*WALL_WIDTH) + (curPlane.dimWidth * curPlane.gridDim);
                newWall.startPos.x = -WALL_WIDTH;
                newWall.startPos.z = (i == 0) ? -WALL_WIDTH : (curPlane.gridDim * curPlane.dimLength);
                newWall.startGridX = 0;
                if (i != 0)
                    newWall.startGridY = curPlane.dimLength;
            }
            else
            {
                newWall.gridLength = curPlane.dimLength;
                newWall.length = curPlane.dimLength * curPlane.gridDim;
//                newWall.startPos.x = curPlane.gridDim * ((i == 1) ? 0 : curPlane.dimWidth);
                newWall.startPos.x = (i == 1) ? (-WALL_WIDTH) : (curPlane.gridDim * curPlane.dimWidth);
                newWall.startPos.z = 0f;
                newWall.startGridY = 0;
                if (i != 1)
                    newWall.startGridX = curPlane.dimWidth;
            }
            newWall.startPos.x -= curPlane.gridDim * 0.5f;
            newWall.startPos.z -= curPlane.gridDim * 0.5f;

            newWall.startPos = curPlane.cornerPos + newWall.startPos;
            baseRoom.Add(newWall);
        }
        return baseRoom;
    }

    public void PlaceDoorsAndWindows(bool shouldPlaceDoors)
    {
        // Add Doors
        // Sort so we add the door to the longest wall segment
        myWalls.Sort((WallInfo x, WallInfo y) => {
            return -(x.length.CompareTo(y.length));
        });

        // Don't create a door for the outer wall (the first index).
        if (shouldPlaceDoors)
            myWalls[0].AddDoor();

        // TODO: Add Windows        
    }

    public void MarkIntersectionPoints(List<WallArray> listOfWalls)
    {
        // Mark start/ending point in 
        System.Func<WallInfo, bool, System.Predicate<WallInfo>> testFunc;
        testFunc = (WallInfo segment, bool isFirst)=>{
            int testX = segment.startGridX, testY = segment.startGridY;
            int diffLength = isFirst ? -1 : (segment.gridLength + 1);
            if (segment.isNorthSouth)
                testY += diffLength;
            else
                testX += diffLength;

            return (WallInfo testInfo)=> 
            {
                bool ret = false;
                if(testInfo.isNorthSouth)
                    ret = testInfo.startGridX == testX && testInfo.startGridY <= testY && (testInfo.startGridY + testInfo.gridLength) >= testY;
                else
                    ret = testInfo.startGridY == testY && testInfo.startGridX <= testX && (testInfo.startGridX + testInfo.gridLength) >= testX;
                if (ret)
                    testInfo.MarkIntersection((testInfo.isNorthSouth ? (testY - testInfo.startGridY): (testX - testInfo.startGridX)) * myPlane.gridDim);
                return ret;
            };
        };
        System.Predicate<WallInfo> SearchFuncStart = testFunc(myWalls[0], true);
        System.Predicate<WallInfo> SearchFuncEnd = testFunc(myWalls[Count - 1], false);
        WallInfo foundStart = null, foundEnd = null;
        for(int j = 0; j < listOfWalls.Count && (foundEnd == null || foundStart == null); ++j)
        {
            WallArray curWallSeg = listOfWalls[j];
            if (foundStart == null)
                foundStart = curWallSeg.myWalls.Find(SearchFuncStart);
            if (foundEnd == null)
                foundEnd = curWallSeg.myWalls.Find(SearchFuncEnd);
        }

        // Sanity check
        if (foundStart == null)
            Debug.LogWarningFormat("Couldn't find start point! ");
        if (foundEnd == null)
            Debug.LogWarningFormat("Couldn't find end point! ");        
    }

    private WallInfo BuildInteriorWallSegment(int startX, int startY, int dx, int dy, int gridLength, HeightPlane curPlane)
    {
//        int startX = wallStart.x, startY = wallStart.y;

        // Update Grid
        if (dx < 0)
            startX -= gridLength;
        if (dy < 0)
            startY -= gridLength;
        curPlane.UpdateGrid(startX, startY, (dx == 0) ? 0 : gridLength, ((dy == 0) ? 0 : gridLength));
//        int endX = startX + ((dx == 0) ? 0 : (dx * gridLength));
//        int endY = startY + ((dy == 0) ? 0 : (dy * gridLength));

        // Create wall info
        WallInfo newWall = new WallInfo();
        newWall.wallMat = WALL_MATERIAL;
        newWall.isNorthSouth = dx == 0;
        newWall.gridLength = gridLength;
        newWall.startGridX = startX;
        newWall.startGridY = startY;
        newWall.length = (gridLength + 1) * curPlane.gridDim;
        newWall.startPos = curPlane.cornerPos + (curPlane.gridDim * new Vector3(startX, 0, startY));
        if (newWall.isNorthSouth)
        {
            newWall.startPos.z -= 0.5f * curPlane.gridDim;
            newWall.startPos.x -= 0.5f * WALL_WIDTH;
            if (startY != 0)
            {
                newWall.startPos.z -= 0.5f * (curPlane.gridDim - WALL_WIDTH);
                newWall.length += 0.5f * (curPlane.gridDim - WALL_WIDTH);
            }
            if (startY+gridLength+1 != curPlane.dimLength)
                newWall.length += 0.5f * (curPlane.gridDim - WALL_WIDTH);
        }
        else
        {
            newWall.startPos.x -= 0.5f * curPlane.gridDim;
            newWall.startPos.z -= 0.5f * WALL_WIDTH;
            if (startX != 0)
            {
                newWall.startPos.x -= 0.5f * (curPlane.gridDim - WALL_WIDTH);
                newWall.length += 0.5f * (curPlane.gridDim - WALL_WIDTH);
            }
            if (startX+gridLength+1 != curPlane.dimWidth)
                newWall.length += 0.5f * (curPlane.gridDim - WALL_WIDTH);
        }
        Debug.LogFormat("Creating for ({0},{1}) for {2} corner for {3} based on room corner {4}, dim: {5}/{6} ({7},{8})", startX, startY, gridLength, 
            newWall.startPos, newWall.length, curPlane.dimWidth, curPlane.dimLength, dx, dy);
        return newWall;
    }

    public void ConstructWallSegments(Transform parentObj = null)
    {
        GameObject containerObj = new GameObject(name);
        if (parentObj != null)
            containerObj.transform.SetParent(parentObj);
        
        foreach (WallInfo info in myWalls)
        {
            info.ConstructWallSegment(WALL_WIDTH, WALL_HEIGHT, containerObj.transform);
        }
    }

#region List Wrapper shortcut functions
    public void RemoveAt(int index)
    {
        myWalls.RemoveAt(index);
    }

    public int Count {
        get {
            return myWalls.Count;
        }
    }

    public WallInfo this[int index] {
        get {
            return myWalls[index];
        }
        set {
            myWalls[index] = value;
        }
    }

    public void Add(WallInfo item)
    {
        myWalls.Add(item);
    }
#endregion

#region IEnumerable implementation
    public IEnumerator<WallInfo> GetEnumerator()
    {
        return myWalls.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return myWalls.GetEnumerator();
    }
#endregion
}