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
}

[System.Serializable]
public class HeightPlane
{
    public float planeHeight;
    public int dimWidth;
    public int dimLength;
    public Vector3 cornerPos;
    public float gridDim;
    public GeneratablePrefab.AttachAnchor anchorType = GeneratablePrefab.AttachAnchor.Ground;
    public List<GridInfo> myGridSpots = new List<GridInfo>();    

    public GridInfo this[int indexer]
    {
        get{ return myGridSpots[indexer]; }
        set { myGridSpots[indexer] = value; }
    }


    public int Index(int x, int y)
    {
        return (dimLength * x ) + y;
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
        Vector3 minVec = bounds.center - bounds.extents - cornerPos;
        Vector3 maxVec = bounds.center + bounds.extents - cornerPos;
        int gridMinX = Mathf.Clamp(Mathf.FloorToInt(minVec.x / gridDim), 0, dimWidth - 1);
        int gridMaxX = Mathf.Clamp(Mathf.CeilToInt(maxVec.x / gridDim), 0, dimWidth - 1);
        int gridMinZ = Mathf.Clamp(Mathf.FloorToInt(minVec.z / gridDim), 0, dimLength - 1);
        int gridMaxZ = Mathf.Clamp(Mathf.CeilToInt(maxVec.z / gridDim), 0, dimLength - 1);
        StripGrid(gridMinX, gridMaxX, gridMinZ, gridMaxZ);

    }

    // Invalidate all squares for placement for the given grid coordinates
    public void StripGrid(int startX, int maxX, int startY, int maxY)
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
            {
                myGridSpots[Index(startX + dimX + i, j + startY)].leftSquares = i - 1;
            }
        }
        StripGrid(startX, startX + dimX, startY, startY + dimY);
    }

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