using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TouchingRelation : SemanticRelationship {

	public TouchingRelation()
	{
		name = "TOUCHING";
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
		List<SemanticObject> retList = new List<SemanticObject> ();
		foreach (Collision col in obj1.GetActiveCollisions()) {
			SemanticObjectSimple hitObj = col.rigidbody.GetComponent<SemanticObjectSimple> ();
			if (!retList.Contains(hitObj))
				retList.Add (hitObj);
		}
		if (retList.Count > 0) {
			return retList;
		}
		return null;
	}
}