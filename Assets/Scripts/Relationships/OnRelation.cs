using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class OnRelation : SemanticRelationship
{
    public OnRelation()
    {
        name = "ON";
    }

    public Dictionary<SemanticObject, List<SemanticObject>> foundObjs = new Dictionary<SemanticObject, List<SemanticObject>>();

    public override bool Evaluate(SemanticObject subject, SemanticObject obj)
    {
        return foundObjs.ContainsKey(subject) && foundObjs[subject].Contains(obj);
    }

    public override void Evaluate(List<SemanticObject> affectedNodes, out Dictionary<SemanticObject, List<SemanticObject>> ret)
    {
        ret = LimitSet(affectedNodes, foundObjs);
    }

    public override void Setup(HashSet<SemanticObject> allObservedObjects)
    {
        // Compare all the objects in the list
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
        foreach(Collision col in obj1.activeCollisions)
        {
            SemanticObject hitObj = col.rigidbody.GetComponent<SemanticObject>();
            if (hitObj != null && Mathf.Abs(col.relativeVelocity.y) < 0.1f)
            {
                // Check to see if there exists a contact where this object is above the other object
                foreach(ContactPoint pt in col.contacts)
                {
                    if (pt.normal.y > 0.1f)
                    {
                        retList.Add(hitObj);
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
