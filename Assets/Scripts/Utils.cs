using UnityEngine;
using System.Collections;

public class Utils
{
}

[System.Serializable]
public struct Point2
{
    int x;
    int y;
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