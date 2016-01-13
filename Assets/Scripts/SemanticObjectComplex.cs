using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Representation for a collection of rigidbodies(each with their own SemanticObject)
/// that together represent a single semantic idea of an object.
/// </summary>
public class SemanticObjectComplex : SemanticObject
{
#region Fields
    // Keep track of currently active collisions on this object for evaluating various SemanticRelationships
    public List<SemanticObject> mySubObjects = new List<SemanticObject>();
    // Each physics frame, mark the current collision list as dirty
    private bool needClearCollisions = true;
    // Keep track of currently active collisions on this object for evaluating various SemanticRelationships
    public List<Collision> activeCollisions = new List<Collision>();
#endregion

#region Unity Callbacks
    private void FixedUpdate()
    {
        needClearCollisions = true;
    }

    // Called once per object after the instance is initialized
    private void Start()
    {
        // Populate links for all subobjects
        foreach(SemanticObject child in mySubObjects)
        {
            List<SemanticObjectComplex> s = child.GetParentObjects();
            if (!s.Contains(this))
                s.Add(this);
        }
    }
#endregion

#region Internal Functions
    private void AssimilateCollisions(Collision toRemove = null)
    {
        if (needClearCollisions)
            activeCollisions.Clear();
        else if (toRemove != null)
        {
            activeCollisions.RemoveAll((Collision other)=>{
                return other.collider == toRemove.collider;
            });
        }
        needClearCollisions = false;
    }
#endregion

#region Public Functions
    public override List<Collision> GetActiveCollisions()
    {
        if (needClearCollisions)
        {
            activeCollisions.Clear();
            foreach(SemanticObjectSimple obj in mySubObjects)
                activeCollisions.AddRange(obj.GetActiveCollisions());
            needClearCollisions = false;
        }
        return activeCollisions;
    }

    public void GetDefaultLayout()
    {
        mySubObjects.Clear();
        SemanticObject[] all = GetComponentsInChildren<SemanticObject>();
        mySubObjects.AddRange(all);
        mySubObjects.Remove(this);
    }
#endregion
}
