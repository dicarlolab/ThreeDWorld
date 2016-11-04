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
    public Material trimMat = null;
    public Material windowMat = null;
    public Material windowTrimMat = null;
    public static float TRIM_HEIGHT = 0.5f;
    public static float TRIM_THICKNESS = 0.01f;
    public const float MIN_SPACING_FOR_HOLES = 0.1f;
    public static float WINDOW_WIDTH = 2.0f;
    public static float WINDOW_HEIGHT = 2.0f;
    public static float WINDOW_PLACEMENT_HEIGHT = 2.0f;
    public static float WINDOW_SPACING = 6.0f;
    public static float DOOR_WIDTH = 1.5f;
    public static float DOOR_HEIGHT = 3.0f;

    [System.Serializable]
    public struct HoleInfo
    {
        public Vector2 bottomCorner;
        public Vector2 size;
        public bool fillWithGlass;
    };

    public List<HoleInfo> holes = new List<HoleInfo>();
    public List<Vector2> placementSpots = new List<Vector2>();

    public void Init(float newLength)
    {
        length = newLength;
        placementSpots.Clear();
        placementSpots.Add(new Vector2(MIN_SPACING_FOR_HOLES, length - MIN_SPACING_FOR_HOLES));

		wallMat.SetColor ("_idval", ProceduralGeneration.getNewUIDColor ());
		trimMat.SetColor ("_idval", ProceduralGeneration.getNewUIDColor ());

    }

    private static int CompareSpots(Vector2 v1, Vector2 v2)
    {
        return v1.x.CompareTo(v2.x);
    }

    public void SortHoles()
    {
        holes.Sort((HoleInfo x, HoleInfo y) => {
            return x.bottomCorner.x.CompareTo(y.bottomCorner.x);
        });
    }

    public void MarkIntersection(float lengthPos)
    {
        RemovePlacementSpot(lengthPos - (0.5f * WallArray.WALL_WIDTH), lengthPos + (0.5f * WallArray.WALL_WIDTH));
    }

    public void AddWindows()
    {
        float MOD_WINDOW_WIDTH = WINDOW_WIDTH + 2 * MIN_SPACING_FOR_HOLES;

        // First find all valid spots a window could fit in
        List<Vector2> validSpots = new List<Vector2>();
        float totalRange = 0f;
        for(int i = 0; i < placementSpots.Count; ++i)
        {
            Vector2 nextArea = placementSpots[i];
            if (nextArea.y - nextArea.x > MOD_WINDOW_WIDTH)
            {
                validSpots.Add(nextArea);
                totalRange += (nextArea.y - nextArea.x - MOD_WINDOW_WIDTH);
            }
        }

        // TODO: Mark window locations so we can create window meshes there later
        // Place windows until we have enough or we run out of space
        int NUM_TO_PLACE = Mathf.RoundToInt(length / WINDOW_SPACING);
        while (validSpots.Count > 0 && NUM_TO_PLACE > 0)
        {
            float placementLoc = Random.Range(0f, totalRange);
            Vector2 nextArea = validSpots[0];
            for(int i = 0; i < validSpots.Count; ++i)
            {
                nextArea = validSpots[i];
                float placementLength = (nextArea.y - nextArea.x - MOD_WINDOW_WIDTH);
                if (placementLoc > placementLength)
                    placementLoc -= placementLength;
                else
                {
                    validSpots.RemoveAt(i);
                    totalRange -= placementLength;
                    if (placementLength > placementLoc + 2 * MOD_WINDOW_WIDTH)
                    {
                        validSpots.Insert(i, new Vector2(nextArea.x + placementLoc + MOD_WINDOW_WIDTH, nextArea.y));
                        totalRange += (placementLength - (placementLoc + MOD_WINDOW_WIDTH));
                    }
                    if (placementLoc > MOD_WINDOW_WIDTH)
                    {
                        validSpots.Insert(i, new Vector2(nextArea.x, nextArea.x + placementLoc));
                        totalRange += (placementLoc - MOD_WINDOW_WIDTH);
                    }
                    break;
                }
            }
            if (placementLoc + nextArea.x + MOD_WINDOW_WIDTH > nextArea.y)
            {
                Debug.LogErrorFormat("Invalid placement @{0}/{1} on {2} with mww:{3} for #{4}", placementLoc, totalRange, nextArea, MOD_WINDOW_WIDTH, NUM_TO_PLACE);
                NUM_TO_PLACE = 0;
            }
            else
            {
                // TODO: Randomize window height location?
                float placementHeight = WINDOW_PLACEMENT_HEIGHT;//(WallArray.WALL_HEIGHT - WINDOW_HEIGHT)*0.5f;
                float midPoint = placementLoc + nextArea.x + (0.5f * MOD_WINDOW_WIDTH);
                HoleInfo windowHole = new HoleInfo();
                windowHole.fillWithGlass = true;
                windowHole.size = new Vector2(WINDOW_WIDTH, WINDOW_HEIGHT);
                windowHole.bottomCorner = new Vector2(midPoint - (0.5f * WINDOW_WIDTH), placementHeight);
                holes.Add(windowHole);
                RemovePlacementSpot(windowHole.bottomCorner.x - MIN_SPACING_FOR_HOLES, windowHole.bottomCorner.x + windowHole.size.x + MIN_SPACING_FOR_HOLES);
                NUM_TO_PLACE--;
            }
        }
    }

    private void RemovePlacementSpot(float newSpotX, float newSpotY)
    {
        RemovePlacementSpot(new Vector2(newSpotX, newSpotY));
    }
    private void RemovePlacementSpot(Vector2 newSpot)
    {
        for(int i = 0; i < placementSpots.Count && placementSpots[i].x <= newSpot.y; ++i)
        {
            if (placementSpots[i].x < newSpot.y && placementSpots[i].y >= newSpot.x)
            {
                Vector2 before = new Vector2(placementSpots[i].x, newSpot.x);
                Vector2 after = new Vector2(newSpot.y, placementSpots[i].y);
                placementSpots.RemoveAt(i);
                if (before.y > before.x)
                {
                    placementSpots.Insert(i, before);
                    ++i;
                }
                if (after.y > after.x)
                {
                    placementSpots.Insert(i, after);
                    ++i;
                }
            }
        }
    }

    public void AddDoor()
    {
        // TODO: Add multiple doors?
        HoleInfo doorHole = new HoleInfo();
        doorHole.fillWithGlass = false;
        doorHole.size = new Vector2(DOOR_WIDTH, DOOR_HEIGHT);
        float midPoint = length * 0.5f, longestSection = 0.0f, curSection = 0f;
        for(int i = 0; i < placementSpots.Count; ++i)
        {
            Vector2 nextArea = placementSpots[i];
            curSection = nextArea.y - nextArea.x;
            if (curSection > longestSection)
            {
                longestSection = curSection;
                midPoint = (0.5f * nextArea.y + nextArea.x);
            }
        }
        doorHole.bottomCorner = new Vector2(midPoint - (0.5f * DOOR_WIDTH), 0.0f);
        holes.Add(doorHole);
        RemovePlacementSpot(doorHole.bottomCorner.x - MIN_SPACING_FOR_HOLES, doorHole.bottomCorner.x + doorHole.size.x + MIN_SPACING_FOR_HOLES);
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
                CreateWallMesh(startPos + curX * lengthDir, newSize, wallBase.transform, "between ");
            curX = holeInfo.bottomCorner.x;
            newSize = baseSize + (holeInfo.size.x * lengthDir);

            // Below
            newStartPos = startPos + curX * lengthDir;
            newSize.y = holeInfo.bottomCorner.y;
            if (newSize.y > 0)
                CreateWallMesh(newStartPos, newSize, wallBase.transform, "below: ");
            
            // Fill in windows if necessary
            if (holeInfo.fillWithGlass)
            {
                newStartPos.y = startPos.y + holeInfo.bottomCorner.y;
                newSize.y = holeInfo.size.y;
                GameObject windowMesh = CreateBoxMesh(newStartPos, newSize, WallArray.WINDOW_MATERIAL, string.Format("Created Window @{0} with size{1}", newStartPos, newSize), parentObj);
				windowMesh.GetComponent<MeshRenderer> ().material.SetColor ("_idval", ProceduralGeneration.getNewUIDColor ());
                windowMesh.AddComponent<SemanticObjectSimple>();
                Rigidbody windowRB = windowMesh.GetComponent<Rigidbody>();
                windowRB.isKinematic = true;
            }

            // Above
            newStartPos.y = startPos.y + holeInfo.bottomCorner.y + holeInfo.size.y;
            newSize.y = baseSize.y - (holeInfo.bottomCorner.y + holeInfo.size.y);
            if (newSize.y > 0)
                CreateWallMesh(newStartPos, newSize, wallBase.transform, "above: ");


            curX = holeInfo.bottomCorner.x + holeInfo.size.x;
        }

        // Final segment
        newSize = baseSize + (finalX - curX) * lengthDir;
        if (finalX - curX > 0)
            CreateWallMesh(startPos + curX * lengthDir, newSize, wallBase.transform);

        return wallBase;
    }

    public GameObject CreateWallMesh(Vector3 start, Vector3 size, Transform parentObj = null, string namePrefix = "")
    {
        GameObject ret = CreateBoxMesh(start, size, wallMat, string.Format("{0}Created Box @{1} with size{2}", namePrefix, start, size), parentObj);
        // Create trim if necessary
        if (start.y < TRIM_HEIGHT && start.y + size.y >= TRIM_HEIGHT)
        {
            Vector3 trimSize = new Vector3(), trimStart = new Vector3();
            trimSize.x = size.x + 2 * TRIM_THICKNESS;
            trimSize.y = TRIM_HEIGHT - start.y;
            trimSize.z = size.z + 2 * TRIM_THICKNESS;
            trimStart.x = start.x - TRIM_THICKNESS;
            trimStart.y = start.y;
            trimStart.z = start.z - TRIM_THICKNESS;
            CreateBoxMesh(trimStart, trimSize, trimMat, string.Format("{0}Trim Box @{1} with size{2}", namePrefix, trimStart, trimSize), parentObj);
        }
        return ret;
    }
		
	public static GameObject CreateBoxMesh(Vector3 start, Vector3 size, Material mat, string name, Transform parentObj = null) 
	{
		//Code for this method is adapted from a script found here: https://github.com/Dsphar/Cube_Texture_Auto_Repeat_Unity/blob/master/ReCalcCubeTexture.cs
		GameObject box = GameObject.CreatePrimitive (PrimitiveType.Cube);
		//set all wall objects static
		box.isStatic = true;
		box.transform.localScale = size;
		box.transform.position = start + (size / 2f);
		box.GetComponent<MeshRenderer>().material = mat;
		var newUVs = new Vector2[box.GetComponent<MeshFilter> ().mesh.vertices.Length];

		var width = size.x;
		var depth = size.z;
		var height = size.y;

		//Front
		newUVs[2] = new Vector2(0, height);
		newUVs[3] = new Vector2(width, height);
		newUVs[0] = new Vector2(0, 0);
		newUVs[1] = new Vector2(width, 0);

		//Back
		newUVs[7] = new Vector2(0, 0);
		newUVs[6] = new Vector2(width, 0);
		newUVs[11] = new Vector2(0, height);
		newUVs[10] = new Vector2(width, height);

		//Left
		newUVs[19] = new Vector2(depth, 0);
		newUVs[17] = new Vector2(0, height);
		newUVs[16] = new Vector2(0, 0);
		newUVs[18] = new Vector2(depth, height);

		//Right
		newUVs[23] = new Vector2(depth, 0);
		newUVs[21] = new Vector2(0, height);
		newUVs[20] = new Vector2(0, 0);
		newUVs[22] = new Vector2(depth, height);

		//Top
		newUVs[4] = new Vector2(width, 0);
		newUVs[5] = new Vector2(0, 0);
		newUVs[8] = new Vector2(width, depth);
		newUVs[9] = new Vector2(0, depth);

		//Bottom
		newUVs[13] = new Vector2(width, 0);
		newUVs[14] = new Vector2(0, 0);
		newUVs[12] = new Vector2(width, depth);
		newUVs[15] = new Vector2(0, depth);

		box.GetComponent <MeshFilter>().mesh.SetUVs (0, new List<Vector2>(newUVs));
		box.name = name;
		if (parentObj != null)
			box.transform.SetParent(parentObj);
		return box;
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
    static public int MIN_SPACING = 1;
    static public int NUM_TWISTS = 1;
    static public List<Material> WALL_MATERIALS = new List<Material>();
    static public int CURRENT_WALL_MAT_INDEX = 0;
    static public Material TRIM_MATERIAL = null;
    static public Material WINDOW_MATERIAL = null;
    static public Material WINDOW_TRIM_MATERIAL = null;
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
            newWall.wallMat = GetNextWallMat();
            newWall.trimMat = TRIM_MATERIAL;
            if (!newWall.isNorthSouth)
            {
                newWall.gridLength = curPlane.dimWidth;
                newWall.Init((2*WALL_WIDTH) + (curPlane.dimWidth * curPlane.gridDim));
                newWall.startPos.x = -WALL_WIDTH;
                newWall.startPos.z = (i == 0) ? -WALL_WIDTH : (curPlane.gridDim * curPlane.dimLength);
                newWall.startGridX = 0;
                if (i != 0)
                    newWall.startGridY = curPlane.dimLength;
            }
            else
            {
                newWall.gridLength = curPlane.dimLength;
                newWall.Init(curPlane.dimLength * curPlane.gridDim);
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
        foreach(WallInfo info in myWalls)
            info.AddWindows();
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

    private static Material GetNextWallMat()
    {
        return WALL_MATERIALS[++CURRENT_WALL_MAT_INDEX % WALL_MATERIALS.Count];
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
        newWall.wallMat = GetNextWallMat();
        newWall.trimMat = TRIM_MATERIAL;
        newWall.isNorthSouth = dx == 0;
        newWall.gridLength = gridLength;
        newWall.isReversed = didReverse;
        newWall.startGridX = startX;
        newWall.startGridY = startY;
        float newLength = (gridLength + 1) * curPlane.gridDim;
        newWall.startPos = curPlane.cornerPos + (curPlane.gridDim * new Vector3(startX, 0, startY));
        if (newWall.isNorthSouth)
        {
            newWall.startPos.z -= 0.5f * curPlane.gridDim;
            newWall.startPos.x -= 0.5f * WALL_WIDTH;
            if (startY != 0 || shortStart)
            {
                float extendDist = (shortStart ? -0.5f : 0.5f) * (curPlane.gridDim - WALL_WIDTH);
                newWall.startPos.z -= extendDist;
                newLength += extendDist;
            }
            if (startY+gridLength+1 != curPlane.dimLength || shortEnd)
                newLength += (shortEnd ? -0.5f : 0.5f) * (curPlane.gridDim - WALL_WIDTH);
        }
        else
        {
            newWall.startPos.x -= 0.5f * curPlane.gridDim;
            newWall.startPos.z -= 0.5f * WALL_WIDTH;
            if (startX != 0 || shortStart)
            {
                float extendDist = (shortStart ? -0.5f : 0.5f) * (curPlane.gridDim - WALL_WIDTH);
                newWall.startPos.x -= extendDist;
                newLength += extendDist;
            }
            if (startX+gridLength+1 != curPlane.dimWidth || shortEnd)
                newLength += (shortEnd ? -0.5f : 0.5f) * (curPlane.gridDim - WALL_WIDTH);
        }
        newWall.Init(newLength);
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