using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SimpleJSON;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class Utils
{
    public static string GetTimeStamp()
    {
        return string.Format("@{0}.{1} seconds", System.DateTime.Now.Second, System.DateTime.Now.Millisecond);
    }
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

    public static JSONClass ReadJson(this MyMessageInfo me, out string messageType)
    {
        messageType = null;
        if (me != null && me.text != null)
        {
            JSONClass ret = JSONData.Parse(me.text) as JSONClass;
            if (ret != null)
                messageType = ret["msg_type"].ReadString(messageType);
            return ret;
        }
        return null;
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

    public static JSONArray ToJson(this Vector3 v)
    {
        JSONArray ret = new JSONArray();
        ret.Add(new JSONData(v.x));
        ret.Add(new JSONData(v.y));
        ret.Add(new JSONData(v.z));
        return ret;
    }

    public static JSONArray ToJson(this Quaternion q)
    {
        JSONArray ret = new JSONArray();
        ret.Add(new JSONData(q.w));
        ret.Add(new JSONData(q.x));
        ret.Add(new JSONData(q.y));
        ret.Add(new JSONData(q.z));
        return ret;
    }

    public static Bounds Rotate(this Bounds b, Quaternion rot)
    {
        Bounds newBounds = new Bounds();
        newBounds.center = rot * b.center;
        Vector3 newExtents = (rot * b.extents).Abs();
        newExtents = Vector3.Max(newExtents, (rot * new Vector3(-b.extents.x, b.extents.y, b.extents.z)).Abs());
        newExtents = Vector3.Max(newExtents, (rot * new Vector3(b.extents.x, -b.extents.y, b.extents.z)).Abs());
        newExtents = Vector3.Max(newExtents, (rot * new Vector3(-b.extents.x, -b.extents.y, b.extents.z)).Abs());
        newBounds.size = newExtents * 2f;
        return newBounds;
    }

#if UNITY_EDITOR
    public static HashSet<T> GetAllChildrenAssets<T>(this DefaultAsset folderAsset) where T : Object
    {
        HashSet<T> ret = new HashSet<T>();
        string assetPath = AssetDatabase.GetAssetPath(folderAsset);
        string testVal = AssetDatabase.GenerateUniqueAssetPath(assetPath);
        if (!string.IsNullOrEmpty(testVal))
        {
            // Strip out the Assets folder which is included in both paths
            string fullPath = Application.dataPath + assetPath.Substring(6);
            string [] subFiles = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories);
            foreach(string childFullPath in subFiles)
            {
                string childAssetPath = childFullPath.Substring(Application.dataPath.Length - 6);
                T foundObj = AssetDatabase.LoadAssetAtPath(childAssetPath, typeof(T)) as T;
                if (foundObj != null)
                    ret.Add(foundObj);
            }
        }
        return ret;
    }
#endif

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
        if (node == null || !node.IsNumeric())
            return defaultValue;
        return node.AsInt;
    }

    public static bool ReadInt(this SimpleJSON.JSONNode node, ref int overwriteValue)
    {
        if (node == null || !node.IsNumeric())
            return false;
        overwriteValue = node.AsInt;
        return true;
    }

    public static float ReadFloat(this SimpleJSON.JSONNode node, float defaultValue = 0.0f)
    {
        if (node == null || !node.IsNumeric())
            return defaultValue;
        return node.AsFloat;
    }

    public static bool ReadFloat(this SimpleJSON.JSONNode node, ref float overwriteValue)
    {
        if (node == null || !node.IsNumeric())
            return false;
        overwriteValue = node.AsFloat;
        return true;
    }

    public static Vector3 ReadVector3(this SimpleJSON.JSONNode node, Vector3 defaultValue = new Vector3())
    {
        ReadVector3(node, ref defaultValue);
        return defaultValue;
    }

    public static bool ReadVector3(this SimpleJSON.JSONNode node, ref Vector3 overwriteValue)
    {
        if (node == null || node.Tag != SimpleJSON.JSONBinaryTag.Array || node.Count != 3)
            return false;
        Vector3 placeholder = overwriteValue;
        if (node[0].ReadFloat(ref placeholder.x) && node[1].ReadFloat(ref placeholder.y) && node[2].ReadFloat(ref placeholder.z))
        {
            overwriteValue.x = placeholder.x;
            overwriteValue.y = placeholder.y;
            overwriteValue.z = placeholder.z;
            return true;
        }
        return false;
    }

    public static bool ReadList(this SimpleJSON.JSONNode node, ref List<string> overwriteValue)
    {
        if (node == null || node.Tag != SimpleJSON.JSONBinaryTag.Array)
            return false;
        overwriteValue.Clear();
        string newVal = null;
        for(int i = 0; i < node.Count; ++i)
        {
            if (node[i].ReadString(ref newVal))
                overwriteValue.Add(newVal);
        }
        return true;
    }

    public static bool ReadBool(this SimpleJSON.JSONNode node, bool defaultValue = false)
    {
        if (node == null || node.Tag != SimpleJSON.JSONBinaryTag.BoolValue)
            return defaultValue;
        return node.AsBool;
    }

    public static bool ReadBool(this SimpleJSON.JSONNode node, ref bool overwriteValue)
    {
        if (node == null || node.Tag != SimpleJSON.JSONBinaryTag.BoolValue)
            return false;
        overwriteValue = node.AsBool;
        return true;        
    }
}