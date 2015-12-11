using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;

[System.Serializable]
public abstract class SemanticRelationship
{
#region Fields
    public string name = "";
#endregion

#region Properties
#endregion

#region Functions
    public abstract bool Evaluate(SemanticObject subject, SemanticObject obj);
    public abstract void Evaluate(List<SemanticObject> affectedNodes, out Dictionary<SemanticObject, List<SemanticObject>> foundObjs);
    public virtual JSONNode GetJsonString(List<SemanticObject> affectedNodes)
    {
        JSONClass retClass = new JSONClass();
        Dictionary<SemanticObject, List<SemanticObject>> relationMap;
        Evaluate(affectedNodes, out relationMap);
        foreach(SemanticObject obj in relationMap.Keys)
        {
            JSONArray lst = new JSONArray();
            foreach(SemanticObject entry in relationMap[obj])
                lst.Add(entry.identifier);
            retClass.Add(obj.identifier, lst);
        }
        return retClass;
    }

    public virtual void Setup(HashSet<SemanticObject> allObservedObjects)
    {
    }

    public static Dictionary<SemanticObject, List<SemanticObject>> LimitSet(List<SemanticObject> subset, Dictionary<SemanticObject, List<SemanticObject>> unrestrictedList)
    {
        Dictionary<SemanticObject, List<SemanticObject>> ret = new Dictionary<SemanticObject, List<SemanticObject>>();
        foreach(SemanticObject obj in unrestrictedList.Keys)
        {
            if (subset.Contains(obj))
            {
                List<SemanticObject> nodes = unrestrictedList[obj].FindAll((SemanticObject test)=>{
                    return subset.Contains(test);
                });
                if (nodes.Count > 0)
                    ret[obj] = nodes;
            }
        }
        return ret;
    }
#endregion
}
