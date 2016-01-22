using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Represents the Semantic Object associated with a single Rigidbody
/// Every Rigidbody in the scene shouold have a SemanticObjectSimple
/// attached to it.
/// The rigidbody can have multiple colliders as part of it, but they
/// will only be described as a single object in the simulation and 
/// cannot be moved or separated from one another.
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
        if (other.rigidbody != null)
            activeCollisions.Add(other);
        else
            Debug.LogWarningFormat("No associated rigidbody found with {0}", other.gameObject.name);
    }
    
    private void OnCollisionStay(Collision other)
    {
        ClearCollisions();
        if (other.rigidbody != null)
            activeCollisions.Add(other);
        else
            Debug.LogWarningFormat("No associated rigidbody found with {0}", other.gameObject.name);
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
