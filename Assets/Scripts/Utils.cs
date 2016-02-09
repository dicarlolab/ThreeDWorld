using UnityEngine;
using System.Collections;

public class Utils
{
}

[System.Serializable]
public struct Point2
{
    public int x;
    public int y;
    public Point2(int x, int y)
    {
        this.x = x;
        this.y = y;
    }
    public static Point2 operator -(Point2 pt)
    {
        return new Point2(-pt.x, -pt.y);
    }
    public static Point2 operator +(Point2 pt1, Point2 pt2)
    {
        return new Point2(pt1.x + pt2.x, pt1.y + pt2.y);
    }
    public static bool operator ==(Point2 pt1, Point2 pt2)
    {
        return pt1.x == pt2.x && pt1.y == pt2.y;
    }
    public static bool operator !=(Point2 pt1, Point2 pt2)
    {
        return pt1.x != pt2.x || pt1.y != pt2.y;
    }
    public void Normalize()
    {
        if (x > 0)
            x = 1;
        else if (x < 0)
            x = -1;
        if (y > 0)
            y = 1;
        else if (y < 0)
            y = -1;
    }
    public override string ToString()
    {
        return string.Format("({0},{1})", x, y);
    }
    public override bool Equals(object obj)
    {
        if (obj != null && obj is Point2)
        {
            Point2 pt2 = (Point2)obj;
            return this == pt2;
        }
        return false;
    }
    public override int GetHashCode()
    {
        return x + 3131 * y;
    }
}

[System.Serializable]
public struct Point3
{
    public int x;
    public int y;
    public int z;
    public Point3(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
    public static Point3 operator -(Point3 pt)
    {
        return new Point3(-pt.x, -pt.y, -pt.z);
    }
    public static Point3 operator +(Point3 pt1, Point3 pt2)
    {
        return new Point3(pt1.x + pt2.x, pt1.y + pt2.y, pt1.z + pt2.z);
    }
    public static bool operator ==(Point3 pt1, Point3 pt2)
    {
        return pt1.x == pt2.x && pt1.y == pt2.y && pt1.z == pt2.z;
    }
    public static bool operator !=(Point3 pt1, Point3 pt2)
    {
        return pt1.x != pt2.x || pt1.y != pt2.y || pt1.z == pt2.z;
    }
    public static implicit operator Point2(Point3 pt1)
    {
        return new Point2(pt1.x, pt1.y);
    }
    public override string ToString()
    {
        return string.Format("({0},{1},{2})", x, y, z);
    }
    public override bool Equals(object obj)
    {
        if (obj != null && obj is Point3)
        {
            Point3 pt2 = (Point3)obj;
            return this == pt2;
        }
        return false;
    }
    public override int GetHashCode()
    {
        return x + 313 * y + 73737 * z;
    }
}


public static class UtilExtensionMethods
{
    public static string FullPath(this Transform xfm)
    {
        if (xfm == null || xfm.gameObject == null)
            return "";
        if (xfm.parent == null)
            return xfm.name;
        return xfm.parent + "/" + xfm.name;
    }

    public static Vector3 Abs(this Vector3 v)
    {
        if (v.x < 0)
            v.x = -v.x;
        if (v.y < 0)
            v.y = -v.y;
        if (v.z < 0)
            v.z = -v.z;
        return v;
    }

    public static bool IsNumeric(this SimpleJSON.JSONNode node)
    {
        SimpleJSON.JSONBinaryTag tag = node.Tag;
        switch(tag)
        {
            case SimpleJSON.JSONBinaryTag.DoubleValue:
            case SimpleJSON.JSONBinaryTag.FloatValue:
            case SimpleJSON.JSONBinaryTag.IntValue:
                return true;
        }
        return false;
    }

    public static string ReadString(this SimpleJSON.JSONNode node, string defaultValue = "")
    {
        if (node == null || node.Tag != SimpleJSON.JSONBinaryTag.Value)
            return defaultValue;
        return node.Value;
    }

    public static bool ReadString(this SimpleJSON.JSONNode node, ref string overwriteValue)
    {
        if (node == null || node.Tag != SimpleJSON.JSONBinaryTag.Value)
            return false;
        overwriteValue = node.Value;
        return true;
    }

    public static int ReadInt(this SimpleJSON.JSONNode node, int defaultValue = 0)
    {
        if (node == null || node.IsNumeric())
            return defaultValue;
        return node.AsInt;
    }

    public static bool ReadInt(this SimpleJSON.JSONNode node, ref int overwriteValue)
    {
        if (node == null || node.IsNumeric())
            return false;
        overwriteValue = node.AsInt;
        return true;
    }

    public static float ReadFloat(this SimpleJSON.JSONNode node, float defaultValue = 0.0f)
    {
        if (node == null || node.IsNumeric())
            return defaultValue;
        return node.AsFloat;
    }

    public static bool ReadFloat(this SimpleJSON.JSONNode node, ref float overwriteValue)
    {
        if (node == null || node.IsNumeric())
            return false;
        overwriteValue = node.AsFloat;
        return true;
    }

    public static bool ReadBool(this SimpleJSON.JSONNode node, bool defaultValue = false)
    {
        if (node == null || node.Tag != SimpleJSON.JSONBinaryTag.Value)
            return defaultValue;
        return node.AsBool;
    }

    public static bool ReadBool(this SimpleJSON.JSONNode node, ref bool overwriteValue)
    {
        if (node == null || node.Tag != SimpleJSON.JSONBinaryTag.Value)
            return false;
        overwriteValue = node.AsBool;
        return true;        
    }
}