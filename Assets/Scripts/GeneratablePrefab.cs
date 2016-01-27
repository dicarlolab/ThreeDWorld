using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GeneratablePrefab : MonoBehaviour
{
    public enum AttachAnchor
    {
        Ground,
        Wall,
        Ceiling
    }

    #region Fields
    // Quick toggle to enable/disable using this prefab in the list of available prefabs during procedural generation
    public bool shouldUse = true;
    // Represents roughly how complex this object is to process
    public int myComplexity = 0;
    public Bounds myBounds = new Bounds();
    public bool isLight;
    public AttachAnchor attachMethod = AttachAnchor.Ground;
    public List<string> generationTags = new List<string>();
    #endregion

#if UNITY_EDITOR
    public void ProcessPrefab()
    {
        int newComplexity = GetComponentsInChildren<MeshCollider>().Length + GetComponentsInChildren<Collider>().Length + (3 * GetComponentsInChildren<SemanticObjectSimple>().Length);
        if (newComplexity <= 0)
            newComplexity = 1;
        if (newComplexity != myComplexity)
            myComplexity = newComplexity;
        myBounds = new Bounds(transform.position, Vector3.zero);
        BoxCollider c = null;
        bool firstBounds = true;
        if (GetComponent<Renderer>() != null)
        {
            c = gameObject.AddComponent<BoxCollider>();
            myBounds = new Bounds(transform.TransformPoint(c.center), transform.TransformVector(c.size).Abs());
            Debug.LogFormat("Encapsulating bounds for {0} as {1} with {2}", name, myBounds, c.bounds);
            firstBounds = false;
        }
        foreach(Renderer col in GetComponentsInChildren<Renderer>())
        {
            if (firstBounds)
            {
                myBounds = col.bounds;
                firstBounds = false;
            }
            else
            {
                //                Debug.LogFormat("Encapsulating bounds for {0} as part of {1} as {2} with {3}", col.name, name, myBounds, col.bounds);
                myBounds.Encapsulate(col.bounds);
            }
        }
//        foreach(Collider col in GetComponentsInChildren<Collider>())
//        {
//            if (col == c)
//                continue;
//            if (firstBounds)
//            {
//                myBounds = col.bounds;
//                firstBounds = false;
//            }
//            else
//            {
//                //                Debug.LogFormat("Encapsulating bounds for {0} as part of {1} as {2} with {3}", col.name, name, myBounds, col.bounds);
//                myBounds.Encapsulate(col.bounds);
//            }
//        }
        Debug.LogFormat("Final bounds for {0} is {1}", name, myBounds);
        myBounds.center = myBounds.center - transform.localPosition;
        Debug.LogFormat("Real final bounds for {0} is {1}", name, myBounds);
        if (c != null)
        {
            if(Application.isEditor && Application.isPlaying == false)
                DestroyImmediate(c, true);
            else
                Destroy(c);
        }
    }
#endif
}