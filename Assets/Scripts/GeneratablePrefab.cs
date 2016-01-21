using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GeneratablePrefab : MonoBehaviour
{
    #region Fields
    // Represents roughly how complex this object is to process
    public int myComplexity = 0;
    public Bounds myBounds = new Bounds();
    public List<string> generationTags = new List<string>();
    #endregion

    public void Process()
    {
        int newComplexity = GetComponentsInChildren<MeshCollider>().Length + GetComponentsInChildren<Collider>().Length + (3 * GetComponentsInChildren<SemanticObjectSimple>().Length);
        if (newComplexity <= 0)
            newComplexity = 1;
        if (newComplexity != myComplexity)
            myComplexity = newComplexity;
        foreach(Collider col in GetComponentsInChildren<Collider>())
        {
            myBounds.Encapsulate(col.bounds);
        }
    }
}