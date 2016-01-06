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
public class SemanticObject : MonoBehaviour
{
#region Fields
    // A unique identifier for this SemanticObject instance in the entire scene
    // We may want to have a second identifier for this type of object, but allowing
    // for multiple objects of that type identifier in the scene as long as they 
    // share the same source
    public string identifier = "";

    // Cached reference to associated rigidbody component
    private Rigidbody _myRigidbody = null;
    // Keep track of currently active collisions on this object for evaluating various SemanticRelationships
    public List<Collision> activeCollisions = new List<Collision>();
    // Each physics frame, mark the current collision list as dirty
    private bool needClearCollisions = true;
    // Static list to ensure we don't reuse any identifiers in the scene
    private static List<string> usedIdentifiers = new List<string>();
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
    private void Awake()
    {
        // Ensure that each identifier is unique
        if (string.IsNullOrEmpty(identifier))
            identifier = name;
        // Reinitialize if necessary(only if doing a live edit in editor)
        if (usedIdentifiers == null)
            usedIdentifiers = new List<string>();
        // Quick and dirty method to ensure a unique identifier for every item
        while (usedIdentifiers.Contains(identifier))
            identifier = identifier + "+";
        usedIdentifiers.Add(identifier);
    }

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
    public void GetDefaultLayout()
    {
        // TODO: Find all SemanticObject's in this object based on the object hierarchy
    }
#endregion
}
