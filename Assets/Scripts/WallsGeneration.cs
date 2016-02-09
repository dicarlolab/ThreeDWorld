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
    public bool isReversed = false;
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
    public int DEBUG_Spacing;
    static public float WALL_WIDTH = 1.0f;
    static public float WALL_HEIGHT = 1.0f;
    static public int MIN_SPACING = 1;
    static public int NUM_TWISTS = 1;
    static public Material WALL_MATERIAL = null;
#endregion

    static private bool HelperFunction(int testVal, ref bool foundFirstZero, ref Point2 testDir, int x, int y)
    {
        if (testVal <= MIN_SPACING)
        {
            if (testVal == 0 && !foundFirstZero)
            {
                foundFirstZero = true;
                testDir.x = x;
                testDir.y = y;
            }
            else
                return true;
        }
        return false;
    }

    static private System.Predicate<GridInfo> HelperFunction3(HeightPlane givenPlane)
    {
        return (GridInfo test) => {
            Point2 testDir = new Point2();
            bool foundFirstZero = false;
            if (HelperFunction(test.downSquares, ref foundFirstZero, ref testDir, 0, 1))
                return false;
            if (HelperFunction(test.upSquares, ref foundFirstZero, ref testDir, 0, -1))
                return false;
            if (HelperFunction(test.rightSquares, ref foundFirstZero, ref testDir, -1, 0))
                return false;
            if (HelperFunction(test.leftSquares, ref foundFirstZero, ref testDir, 1, 0))
                return false;
            return foundFirstZero && TestPlaceWall(givenPlane, testDir, new Point2(test.x, test.y));
        };
    }

    static private void UpdateGridForSegment(Point3 lastPoint, int dx, int dy, HeightPlane givenPlane)
    {
        givenPlane.UpdateGrid(lastPoint.x + (dx < 0 ? -lastPoint.z : 0), lastPoint.y + (dy < 0 ? -lastPoint.z : 0), (dx == 0) ? 0 : lastPoint.z, ((dy == 0) ? 0 : lastPoint.z));
    }

    static private bool TestPlaceWall(HeightPlane givenPlane, Point2 testDir, Point2 startPoint)
    {
//        if (allowReverseDirection)
//            return TestPlaceWall(givenPlane, testDir, startPoint) || TestPlaceWall(givenPlane, -testDir, startPoint);
        if (givenPlane[startPoint].GetDistInDir(testDir) <= MIN_SPACING)
            return false;
        Point2 perpDir1 = new Point2(testDir.y, testDir.x), perpDir2 = -perpDir1, testPoint = startPoint;
        for(int i = 0; i <= MIN_SPACING; ++i)
        {
            testPoint = testPoint + testDir;
            GridInfo info = givenPlane[testPoint];
            if (info.GetDistInDir(perpDir1) <= MIN_SPACING || info.GetDistInDir(perpDir2) <= MIN_SPACING)
                return false;
        }
        return true;
    }

    static public WallArray PlotNewWallArray(HeightPlane givenPlane, string newName = "")
    {
        WallArray newWallSet = new WallArray();
            newWallSet.DEBUG_Spacing = MIN_SPACING; // TODO: Remove this
        newWallSet.name = newName;
        newWallSet.myPlane = givenPlane;

        // Find start spot
        List<GridInfo> possibleStartPoints = givenPlane.myGridSpots.FindAll(HelperFunction3(givenPlane));
        if (possibleStartPoints.Count == 0)
            return null;

        int randIndex = Random.Range(0, possibleStartPoints.Count);
        GridInfo wallStart = possibleStartPoints[randIndex];
        Point2 buildDir = new Point2();
        int count = 0;
        if (wallStart.leftSquares== 0)
        {
            buildDir.x = 1;
            count = wallStart.rightSquares;
        }
        if (wallStart.rightSquares == 0)
        {
            buildDir.x = -1;
            count = wallStart.leftSquares;
        }
        if (wallStart.downSquares == 0)
        {
            buildDir.y = -1;
            count = wallStart.upSquares;
        }
        if (wallStart.upSquares == 0)
        {
            buildDir.y = 1;
            count = wallStart.downSquares;
        }

        List<Point3> toAdd = new List<Point3>();
        toAdd.Add(new Point3(wallStart.x, wallStart.y, count));
        for(int i = 0; i < NUM_TWISTS; ++i)
        {
            Point3 lastPoint = toAdd[toAdd.Count - 1];

            int forceTurn = -1;
            bool forceDirection = false;
            int minRange = (MIN_SPACING + 1);
            int maxRange = lastPoint.z - (MIN_SPACING);
            buildDir.Normalize();
            Point2 perpDir1 = new Point2(buildDir.y, buildDir.x), perpDir2 = new Point2(-buildDir.y, -buildDir.x);
            List<int> turnPoints = new List<int>();
            for(int j = 1; j < lastPoint.z; ++j)
            {
                Point2 testPoint = (Point2)lastPoint + buildDir;
                GridInfo info = givenPlane[testPoint];
                bool mustTurn1 = info.GetDistInDir(perpDir1) < MIN_SPACING;
                bool mustTurn2 = info.GetDistInDir(perpDir2) < MIN_SPACING;

                if (mustTurn1 || mustTurn2)
                {
                    if (turnPoints.Count == 0)
                    {
                        // If we don't have an option to turn beforehand, force a turn here to close the gap
                        forceTurn = j;
                        forceDirection = mustTurn1;
                    }
                    break;
                }
                else if (j >= minRange && j < maxRange)
                    turnPoints.Add(j);
            }

            // Try to turn if we have a point where we can.
            if (turnPoints.Count > 0 || forceTurn >= 0)
            {
                // Update Grid
                int dist = forceTurn;
                if (forceTurn < 0)
                    dist = turnPoints[Random.Range(0, turnPoints.Count)];
                // Adjust distance of old point
                lastPoint.z = dist - 1;
                toAdd[toAdd.Count - 1] = lastPoint;
                UpdateGridForSegment(toAdd[toAdd.Count - 1], buildDir.x, buildDir.y, givenPlane);

                Point3 testPoint = lastPoint;
                testPoint.x += dist * buildDir.x;
                testPoint.y += dist * buildDir.y;
                GridInfo testInfo = givenPlane.myGridSpots[givenPlane.Index(testPoint.x, testPoint.y)];

                // Pick a new direction
                // TODO: Decide between shorter/longer paths depending on how many twists remaining
                if (forceTurn >= 0)
                    buildDir = forceDirection ? perpDir1 : perpDir2;
                else
                    buildDir = (Random.Range(0, 2) == 1) ? perpDir1 : perpDir2;

                // Add this point to the end
                testPoint.z = testInfo.GetDistInDir(buildDir.x, buildDir.y);
//                Debug.LogFormat(newWallSet.name + ": Turning at {0}/{10} of range({11},{12}) toward ({1},{2}) for {9} info: d{3},u{4},l{5},r{6} @({7},{8})", 
//                    dist, buildDir.x, buildDir.y, testInfo.downSquares, testInfo.upSquares, 
//                    testInfo.leftSquares, testInfo.rightSquares, testInfo.x, testInfo.y, testPoint.z,
//                    originalDist, minRange, maxRange);
                toAdd.Add(testPoint);
            }
            else
                break;
        }
        UpdateGridForSegment(toAdd[toAdd.Count - 1], buildDir.x, buildDir.y, givenPlane);
        toAdd.Add(new Point3(toAdd[toAdd.Count - 1].x + buildDir.x, toAdd[toAdd.Count - 1].y + buildDir.y, 0));

        for(int i = 0; i < toAdd.Count - 1; ++i)
        {
            buildDir.x = (toAdd[i+1].x - toAdd[i].x);
            buildDir.y = (toAdd[i+1].y - toAdd[i].y);
//            Debug.LogFormat(newWallSet.name + ": Going from ({0},{1})=>({2},{3})", toAdd[i].x, toAdd[i].y, toAdd[i+1].x, toAdd[i+1].y);
            WallInfo newWall = newWallSet.BuildInteriorWallSegment(toAdd[i].x, toAdd[i].y, buildDir.x, buildDir.y, toAdd[i].z, givenPlane, i != 0); 
            newWallSet.myWalls.Add(newWall);
            newWall.name = string.Format("{0} #{1}", newName, newWallSet.myWalls.Count);
        }

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
        // Mark start/ending point as intersections on whichever wall we hit
        System.Func<WallInfo, bool, System.Predicate<WallInfo>> testFunc;
        testFunc = (WallInfo segment, bool isFirst)=>{
            int testX = segment.startGridX, testY = segment.startGridY;
            int diffLength = (segment.isReversed ^ isFirst) ? -1 : (segment.gridLength + 1);
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
            Debug.LogWarningFormat("Couldn't find start point! {0}", myWalls[0].name);
        if (foundEnd == null)
            Debug.LogWarningFormat("Couldn't find end point! {0}", myWalls[Count - 1].name);
    }

    private WallInfo BuildInteriorWallSegment(int startX, int startY, int dx, int dy, int gridLength, HeightPlane curPlane, bool shortStart)
    {
        // Update Grid
        bool didReverse = false;
        if (dx < 0)
        {
            startX -= gridLength;
            didReverse = true;
        }
        if (dy < 0)
        {
            startY -= gridLength;
            didReverse = true;
        }
        bool shortEnd = shortStart && (dx < 0 || dy < 0);
        shortStart = shortStart && !shortEnd;
//        curPlane.UpdateGrid(startX, startY, (dx == 0) ? 0 : gridLength, ((dy == 0) ? 0 : gridLength));

        // Create wall info
        WallInfo newWall = new WallInfo();
        newWall.wallMat = WALL_MATERIAL;
        newWall.isNorthSouth = dx == 0;
        newWall.gridLength = gridLength;
        newWall.isReversed = didReverse;
        newWall.startGridX = startX;
        newWall.startGridY = startY;
        newWall.length = (gridLength + 1) * curPlane.gridDim;
        newWall.startPos = curPlane.cornerPos + (curPlane.gridDim * new Vector3(startX, 0, startY));
        if (newWall.isNorthSouth)
        {
            newWall.startPos.z -= 0.5f * curPlane.gridDim;
            newWall.startPos.x -= 0.5f * WALL_WIDTH;
            if (startY != 0 || shortStart)
            {
                float extendDist = (shortStart ? -0.5f : 0.5f) * (curPlane.gridDim - WALL_WIDTH);
                newWall.startPos.z -= extendDist;
                newWall.length += extendDist;
            }
            if (startY+gridLength+1 != curPlane.dimLength || shortEnd)
                newWall.length += (shortEnd ? -0.5f : 0.5f) * (curPlane.gridDim - WALL_WIDTH);
        }
        else
        {
            newWall.startPos.x -= 0.5f * curPlane.gridDim;
            newWall.startPos.z -= 0.5f * WALL_WIDTH;
            if (startX != 0 || shortStart)
            {
                float extendDist = (shortStart ? -0.5f : 0.5f) * (curPlane.gridDim - WALL_WIDTH);
                newWall.startPos.x -= extendDist;
                newWall.length += extendDist;
            }
            if (startX+gridLength+1 != curPlane.dimWidth || shortEnd)
                newWall.length += (shortEnd ? -0.5f : 0.5f) * (curPlane.gridDim - WALL_WIDTH);
        }
//        Debug.LogFormat("Creating for grid: ({0},{1}) for {2} corner for real({3} for {4}), name: {5}, short:{6}, dir:({7},{8})", startX, startY, gridLength, 
//            newWall.startPos, newWall.length, name, shortStart || shortEnd, dx, dy);
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