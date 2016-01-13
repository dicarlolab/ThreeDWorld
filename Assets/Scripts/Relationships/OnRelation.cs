using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Implementation for evaluating if an object is "on" another object
/// </summary>
public class OnRelation : SemanticRelationship
{
    public OnRelation()
    {
        name = "ON";
    }

    /// Saved mapping of all objects with an "On" relationship
    public Dictionary<SemanticObject, List<SemanticObject>> foundObjs = new Dictionary<SemanticObject, List<SemanticObject>>();

    public override bool Evaluate(SemanticObject subject, SemanticObject obj)
    {
        return foundObjs.ContainsKey(subject) && foundObjs[subject].Contains(obj) && !subject.IsChildObjectOf(obj);
    }

    public override void Evaluate(List<SemanticObject> affectedNodes, out Dictionary<SemanticObject, List<SemanticObject>> ret)
    {
        ret = LimitSet(affectedNodes, foundObjs);
    }

    public override void Setup(HashSet<SemanticObject> allObservedObjects)
    {
        // Compare all the objects in the list
        foundObjs.Clear();
        foreach(SemanticObject obj in allObservedObjects)
        {
            List<SemanticObject> listObjs = PerformTest(obj, allObservedObjects);
            if (listObjs != null)
                foundObjs[obj] = listObjs;
        }
    }

    private List<SemanticObject> PerformTest(SemanticObject obj1, HashSet<SemanticObject> allObservedObjects)
    {
        List<SemanticObject> retList = new List<SemanticObject>();
        foreach(Collision col in obj1.GetActiveCollisions())
        {
            SemanticObjectSimple hitObj = col.rigidbody.GetComponent<SemanticObjectSimple>();
            // Ensures sufficiently low vertical relative velocity to be treated as "at rest"
            if (hitObj != null && Mathf.Abs(col.relativeVelocity.y) < 0.1f && !retList.Contains(hitObj) && !hitObj.IsChildObjectOf(obj1))
            {
                // Check to see if there exists a contact where this object is above the other object
                foreach(ContactPoint pt in col.contacts)
                {
                    if (pt.normal.y > 0.1f)
                    {
                        retList.Add(hitObj);
                        foreach(SemanticObjectComplex parentObj in hitObj.GetParentObjects())
                        {
                            // Add parent objects, unless we are also a child object of that parent
                            if (!retList.Contains(parentObj) && !obj1.IsChildObjectOf(parentObj))
                                retList.Add(parentObj);
                        }
                        break;
                    }
                }
            }
        }
        if (retList.Count > 0)
            return retList;
        return null;
    }
}
