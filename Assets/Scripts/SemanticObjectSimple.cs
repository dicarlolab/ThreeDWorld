using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Representation for a collection of rigidbodies that together 
/// represent a single semantic idea of an object.
/// The simple version of this is just a single rigidbody and it's children colliders.
/// The more complex version includes several other SemanticObject's that are 
/// wholly contained as part of this SemanticObject. (Not implemented yet)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SemanticObjectSimple : SemanticObject
{
#region Fields
    // Cached reference to associated rigidbody component
    private Rigidbody _myRigidbody = null;
    // Keep track of currently active collisions on this object for evaluating various SemanticRelationships
    public List<Collision> activeCollisions = new List<Collision>();
    // Each physics frame, mark the current collision list as dirty
    private bool needClearCollisions = true;
#endregion

#region Properties
    // Public accessor property for associated rigidbody component
    public Rigidbody myRigidbody {
        get {
            if (_myRigidbody == null)
                _myRigidbody = gameObject.GetComponent<Rigidbody>();
            return _myRigidbody;
        }
    }
#endregion

#region Unity Callbacks
    private void FixedUpdate()
    {
        needClearCollisions = true;
    }
    
    private void OnCollisionEnter(Collision other)
    {
        ClearCollisions();
        activeCollisions.Add(other);
    }
    
    private void OnCollisionStay(Collision other)
    {
        ClearCollisions();
        activeCollisions.Add(other);
    }

    private void OnCollisionExit(Collision other)
    {
        ClearCollisions(other);
    }
#endregion

#region Internal Functions
    private void ClearCollisions(Collision toRemove = null)
    {
        if (needClearCollisions)
        {
            activeCollisions.Clear();
        }
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
        return activeCollisions;
    }
#endregion
}
