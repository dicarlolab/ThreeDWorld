using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using LitJson;

/// <summary>
/// Represents a type of semantic relation between two SemanticObject's
/// This is the abstract base class that should be overridden by the implementation
/// of the specific relationship and operations.
/// </summary>
[System.Serializable]
public abstract class SemanticRelationship
{
#region Fields
    // Name used when describing the relationship in JSON
    public string name = "";
    public NetMessenger _myMessenger = null;
#endregion

#region Properties
#endregion

#region Unity callbacks
    void Start()
    {
        _myMessenger = GameObject.FindObjectOfType<NetMessenger>();
    }
#endregion

#region Functions
    public abstract bool Evaluate(SemanticObject subject, SemanticObject obj);

    // Abstract function that evaluations the relationship value of a given object
    // over all other objects that it needs to be tested against.
    public abstract void Evaluate(List<SemanticObject> affectedNodes, out Dictionary<SemanticObject, List<SemanticObject>> foundObjs);

    // Utility function that takes a list of SemanticObject's evaluated as 
    // having a relationship with the given object and outputs as a JSON object
    public virtual JsonData GetJsonString(List<SemanticObject> affectedNodes)
    {
        if (_myMessenger.logTimeInfo)
            Debug.LogFormat("Get Json for relationship {1}, {0}", Utils.GetTimeStamp(), name);
        JsonData retClass = new JsonData(JsonType.Object);
        Dictionary<SemanticObject, List<SemanticObject>> relationMap;
        Evaluate(affectedNodes, out relationMap);
        if (_myMessenger.logTimeInfo)
            Debug.LogFormat("  Finished evaluate relationships {0}", Utils.GetTimeStamp());
        foreach(SemanticObject obj in relationMap.Keys)
        {
            JsonData lst = new JsonData(JsonType.Array);
            foreach(SemanticObject entry in relationMap[obj])
                lst.Add(entry.identifier);
            retClass[obj.identifier] = lst;
        }
        if (_myMessenger.logTimeInfo)
            Debug.LogFormat("  Finished creating relationships json map {0} with {1} keys", Utils.GetTimeStamp(), relationMap.Keys.Count);
        return retClass;
    }


    // Virtual function that does evaluations over all objects
    // This should be used to evaluate relationships for low-cost operation or for
    // partial evaluations that can streamline the operation when evaluating all the combinations
    // Can also set/clear caches for some evaluation results(e.g. commutative relationships)
    public virtual void Setup(HashSet<SemanticObject> allObservedObjects)
    {
    }

    // Utility function that converts a mapping of all active relations to a mapping
    // of all active relations over a smaller subset of objects
    // This is primarily used when we have multiple avatars that can observe different sets of
    // objects. This allows evaluating relationships once while sending only relevant information
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
