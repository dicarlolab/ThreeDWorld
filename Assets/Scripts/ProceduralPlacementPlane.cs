using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class GridInfo
{
    public bool inUse = false;
    public int x;
    public int y;
    public int leftSquares;
    public int rightSquares;
    public int upSquares;
    public int downSquares;

    public int GetDistInDir(Point2 pt)
    {
        return GetDistInDir(pt.x, pt.y);
    }
    public int GetDistInDir(int dx, int dy)
    {
        if (dy < 0)
            return upSquares;
        if (dy > 0)
            return downSquares;
        if (dx < 0)
            return leftSquares;
        if (dx > 0)
            return rightSquares;
        return 0;
    }
}

[System.Serializable]
public class HeightPlane
{
    public string name = "HeightPlane";
    public float planeHeight = 0f;
    public int dimWidth;
    public int dimLength;
    public Vector3 cornerPos;
    public float gridDim;
    public GeneratablePrefab.AttachAnchor anchorType = GeneratablePrefab.AttachAnchor.Ground;
    public List<GridInfo> myGridSpots = new List<GridInfo>();
    public Vector3 upDir = new Vector3(0,1f,0);
    public Vector3 widthDir = new Vector3(1f,0,0);
    public Vector3 lengthDir = new Vector3(0,0,1f);
    public Quaternion rotMat = Quaternion.identity;

    public GridInfo this[int indexer]
    {
        get{ return myGridSpots[indexer]; }
        set { myGridSpots[indexer] = value; }
    }

    public GridInfo this[Point2 indexer]
    {
        get{ return myGridSpots[Index(indexer)]; }
        set { myGridSpots[Index(indexer)] = value; }
    }

    public Vector3 GridToWorld(Point2 pt)
    {
        return rotMat * (cornerPos + new Vector3(gridDim * pt.x, 0.0f, gridDim * pt.y));
    }

    public Vector3 GridToWorld(Vector2 vec)
    {
        return rotMat * (cornerPos + new Vector3(gridDim * vec.x, 0.0f, gridDim * vec.y));
    }

    public void InitForWall(WallInfo wall, bool whichSide)
    {
        if (wall.isNorthSouth)
        {
            if (whichSide)
                rotMat = Quaternion.Euler(new Vector3(0f, 0f, 90f));
            else
                rotMat = Quaternion.Euler(new Vector3(0f, 0f, -90f));
        }
        else
        {
            if (whichSide)
                rotMat = Quaternion.Euler(new Vector3(90f, 0f, 0f));
            else
                rotMat = Quaternion.Euler(new Vector3(-90f, 0f, 0f));
        }
        upDir = rotMat * new Vector3(0,1f,0);
        widthDir = rotMat * new Vector3(1f,0,0);
        lengthDir = rotMat * new Vector3(0,0,1f);
        anchorType = GeneratablePrefab.AttachAnchor.Wall;

        // TODO: Adjust dimensions and corner pos

    }

    public void Clear()
    {
        Clear(dimWidth, dimLength);
    }

    public void Clear(int width, int length)
    {
        dimWidth = width;
        dimLength = length;
        myGridSpots.Clear();
        for(int i = 0; i < width; ++i)
        {
            for(int j = 0; j < length; ++j)
            {
                GridInfo newGridInfo = new GridInfo();
                newGridInfo.x = i;
                newGridInfo.y = j;
                ModifyGrid(newGridInfo, i, j, width, length);
                myGridSpots.Add(newGridInfo);
            }
        }        
    }

    public int Index(int x, int y)
    {
        if (x >= dimWidth || y >= dimLength)
            Debug.LogErrorFormat("XY values out of bounds for plane! ({0},{1}) for bounds: ({2} x {3})", x, y, dimWidth, dimLength);
        return (dimLength * x ) + y;
    }

    public int Index(Point2 pt)
    {
        if (pt.x >= dimWidth || pt.y >= dimLength)
            Debug.LogErrorFormat("Point out of bounds for plane! {0} for bounds: ({1} x {2})", pt, dimWidth, dimLength);
        return (dimLength * pt.x ) + pt.y;
    }

    public void ModifyGrid(GridInfo info, int i, int j, int width, int length)
    {
        info.leftSquares = i;
        info.rightSquares = width - i - 1;
        info.upSquares = j;
        info.downSquares= length - j - 1;
    }

    // Helper function to invalidate all squares covered by these bounds
    public void RestrictBounds(Bounds bounds)
    {
        Vector3 minVec = rotMat * (bounds.center - bounds.extents - cornerPos);
        Vector3 maxVec = rotMat * (bounds.center + bounds.extents - cornerPos);
        int gridMinX = Mathf.Clamp(Mathf.FloorToInt(minVec.x / gridDim), 0, dimWidth - 1);
        int gridMaxX = Mathf.Clamp(Mathf.CeilToInt(maxVec.x / gridDim), 0, dimWidth - 1);
        int gridMinZ = Mathf.Clamp(Mathf.FloorToInt(minVec.z / gridDim), 0, dimLength - 1);
        int gridMaxZ = Mathf.Clamp(Mathf.CeilToInt(maxVec.z / gridDim), 0, dimLength - 1);
        UpdateGrid(gridMinX, gridMinZ, gridMaxX-gridMinX, gridMaxZ-gridMinZ);
    }

    // Mark a section of the grid as used and update surrounding squares to say how far they are from a boundary
    public void UpdateGrid(int startX, int startY, int dimX, int dimY)
    {
//        Debug.LogFormat("UpdateGrid({0},{1},{2},{3},{4})", startX, startY, dimX, dimY, newHeight);
        int numToCheck;
        for(int i = 0; i <= dimX; ++i)
        {
            // check up
            numToCheck = myGridSpots[Index(i + startX, startY)].upSquares;
            for(int j = 1; j <= numToCheck; ++j)
                myGridSpots[Index(i + startX, startY - j)].downSquares = j - 1;
            // check down
            numToCheck = myGridSpots[Index(i + startX, startY + dimY)].downSquares;
            for(int j = 1; j <= numToCheck; ++j)
                myGridSpots[Index(i + startX, startY + dimY + j)].upSquares = j - 1;
        }
        for(int j = 0; j <= dimY; ++j)
        {
            // check left
            numToCheck = myGridSpots[Index(startX, j + startY)].leftSquares;
            for(int i = 1; i <= numToCheck; ++i)
                myGridSpots[Index(startX - i, j + startY)].rightSquares = i - 1;
            // check right
            numToCheck = myGridSpots[Index(startX + dimX, j + startY)].rightSquares;
            for(int i = 1; i <= numToCheck; ++i)
                myGridSpots[Index(startX + dimX + i, j + startY)].leftSquares = i - 1;
        }
        StripGrid(startX, startX + dimX, startY, startY + dimY);
    }

    // Invalidate all squares for placement for the given grid coordinates
    private void StripGrid(int startX, int maxX, int startY, int maxY)
    {
        for(int i = startX; i <= maxX; ++i)
        {
            for(int j = startY; j <= maxY; ++j)
            {
                int index = Index(i, j);
                myGridSpots[index].inUse = true;
                ModifyGrid(myGridSpots[index], 0, 0, 1, 1);
            }
        }        
    }

    // Tests to ensure that it is valid to place an object over the given grid rectangle
    public bool TestGrid(GridInfo info, int dimX, int dimY)
    {
        for(int i = 0; i < dimX; ++i)
        {
            if (myGridSpots[Index(info.x + i, info.y)].downSquares < (dimY-1))
                return false;
        }
        return true;
    }
}