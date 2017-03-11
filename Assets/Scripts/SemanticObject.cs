using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Representation for a collection of rigidbodies that together 
/// represent a single semantic idea of an object.
/// The simple version of this is just a single rigidbody and it's children colliders.
/// The more complex version includes several other SemanticObject's that are 
/// wholly contained as part of this SemanticObject.
/// </summary>
public abstract class SemanticObject : MonoBehaviour
{
#region Fields
    // A unique identifier for this SemanticObject instance in the entire scene
    // We may want to have a second identifier for this type of object, but allowing
    // for multiple objects of that type identifier in the scene as long as they 
    // share the same source
    public string identifier = "";

    // Identifies the object as static or not
    public bool isStatic = true;

    // Identifies the object as stackable or not
	public bool isStackable = false;

    protected List<SemanticObjectComplex> parentObjects;

//    // Keep track of currently active collisions on this object for evaluating various SemanticRelationships
//    public List<Collision> activeCollisions = new List<Collision>();
    // Static list to ensure we don't reuse any identifiers in the scene
    private static List<string> usedIdentifiers = new List<string>();
#endregion

#region Public Functions
    public abstract List<Collision> GetActiveCollisions();
    public virtual List<SemanticObjectComplex> GetParentObjects()
    {
//        List<SemanticObjectComplex> ret = new List<SemanticObjectComplex>();
//        if (other is SemanticObjectComplex && this is SemanticObjectSimple)
//        {
//            return (other as SemanticObjectComplex).mySubObjects.Contains(this as SemanticObjectSimple);
//        }
        return parentObjects;
    }
    public virtual bool IsChildObjectOf(SemanticObject other)
    {
        if (other is SemanticObjectComplex)
        {
            return (other as SemanticObjectComplex).mySubObjects.Contains(this);
        }
        return false;
    }
#endregion

#region Unity Callbacks
    protected virtual void Awake()
    {
        parentObjects = new List<SemanticObjectComplex>();
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
#endregion
}
