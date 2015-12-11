using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class SemanticObject : MonoBehaviour
{
#region Fields
    public string identifier = "";

    private Rigidbody _myRigidbody = null;
    // Keep track of currently active collisions on this object
    public List<Collision> activeCollisions = new List<Collision>();
    // Each physics frame, mark the current collision list as dirty
    private bool needClearCollisions = true;
    private static List<string> usedIdentifiers = new List<string>();
#endregion

#region Properties
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
        // TODO: Find subObjects
    }
#endregion
}
