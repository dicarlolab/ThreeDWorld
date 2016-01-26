using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class GridInfo
{
    public float height;
    public bool inUse;
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
    public float height;
    public int dimWidth;
    public int dimLength;
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

    public void UpdateGrid(int startX, int startY, int dimX, int dimY, float newHeight)
    {
        try
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
            for(int i = 0; i <= dimX; ++i)
            {
                for(int j = 0; j <= dimY; ++j)
                {
                    int index = Index(i + startX, j + startY);
                    myGridSpots[index].height = newHeight;
                    // TODO: For stackable objects, add raised platform stuff
                    ModifyGrid(myGridSpots[index], 0, 0, 1, 1);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarningFormat("Got exception {0}", e.ToString());
        }
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