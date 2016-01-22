using UnityEngine;
using System.Collections;

public class Utils
{
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
}