using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Collections.Generic;

[ExecuteInEditMode]
[AddComponentMenu("Ultimate Game Tools/Colliders/Concave Collider")]
public class ConcaveCollider : MonoBehaviour
{
    public enum EAlgorithm
    {
        Normal,
        Fast,
        VHACD,
        Legacy
    }
                       public EAlgorithm       Algorithm                   = EAlgorithm.VHACD;
                       public int              MaxHullVertices             = 64;
                       public int              MaxHulls                    = 128;
                       public float            InternalScale               = 10.0f;
                       public float            Precision                   = 0.8f;
                       public bool             CreateMeshAssets            = true;
                       public bool             CreateHullMesh              = false;
                       public bool             DebugLog                    = true;
                       public int              LegacyDepth                 = 6;
                       public bool             ShowAdvancedOptions         = false;
                       public float            MinHullVolume               = 0.00001f;
                       public float            BackFaceDistanceFactor      = 0.2f;
                       public bool             NormalizeInputMesh          = false;
                       public bool             ForceNoMultithreading       = false;

                       public float            VHACD_Concavity             = 0.001f;
                       public float            VHACD_MinVolumePerCH        = 0.001f;
                       public int              VHACD_NumVoxels             = 80000;
                       public int              VHACD_MaxVerticesPerCH      = 64;
                       public bool             VHACD_NormalizeMesh         = false;

                       public PhysicMaterial   PhysMaterial                = null;
                       public bool             IsTrigger                   = false;

                       public GameObject[]     m_aGoHulls                  = null;

    [SerializeField]   private PhysicMaterial  LastMaterial                = null;
    [SerializeField]   private bool            LastIsTrigger               = false;

    [SerializeField]   private int             LargestHullVertices         = 0;
    [SerializeField]   private int             LargestHullFaces            = 0;

                       private static bool     InBatchSaveMode             = true;
                       public static bool      DelayBatchedCalls           = false;

    public delegate void LogDelegate     ([MarshalAs(UnmanagedType.LPStr)]string message);
    public delegate void ProgressDelegate([MarshalAs(UnmanagedType.LPStr)]string message, float fPercent);

    void OnDestroy()
    {
        // Only destroy created hulls if this was created during Play mode
        if(!(Application.isEditor && Application.isPlaying == false))
            DestroyHulls();
    }

    void Reset()
    {
        DestroyHulls();
    }

    void Update()
    {
        if(PhysMaterial != LastMaterial)
        {
            foreach(GameObject hull in m_aGoHulls)
            {
                if(hull)
                {
                    Collider collider = hull.GetComponent<Collider>();

                    if(collider)
                    {
                        collider.material = PhysMaterial;
                        LastMaterial = PhysMaterial;
                    }
                }
            }
        }

        if(IsTrigger != LastIsTrigger)
        {
            foreach(GameObject hull in m_aGoHulls)
            {
                if(hull)
                {
                    Collider collider = hull.GetComponent<Collider>();

                    if(collider)
                    {
                        collider.isTrigger = IsTrigger;
                        LastIsTrigger = IsTrigger;
                    }
                }
            }
        }
    }

    public void DestroyHulls()
    {
        LargestHullVertices = 0;
        LargestHullFaces    = 0;

        if(m_aGoHulls != null)
        {
            if(Application.isEditor && Application.isPlaying == false)
            {
                foreach(GameObject hull in m_aGoHulls)
                {
                    if(hull) DestroyImmediate(hull);
                }
            }
            else
            {
                foreach(GameObject hull in m_aGoHulls)
                {
                    if(hull) Destroy(hull);
                }
            }

            m_aGoHulls = null;
        }

        Transform hullParent = this.transform.Find("Generated Colliders");
        if(hullParent != null)
        {
            if(Application.isEditor && Application.isPlaying == false)
                DestroyImmediate(hullParent.gameObject);
            else
                Destroy(hullParent.gameObject);
        }
    }

    public void CancelComputation()
    {
        if(Algorithm == EAlgorithm.VHACD)
        {
            ConcaveColliderDll20.CancelConvexDecomposition();
        }
        else
        {
            CancelConvexDecomposition();
        }
    }

#if UNITY_EDITOR

    public bool ComputeHulls(LogDelegate log, ProgressDelegate progress)
    {
        bool hadError = false;
        string strMeshAssetPath = "";

        if(CreateMeshAssets)
        {
            string uniqueID = null;;
            Debug.LogFormat("Obj: {0} of type {1}", gameObject.transform.FullPath(), UnityEditor.PrefabUtility.GetPrefabType(this.gameObject));
            if (UnityEditor.PrefabUtility.GetPrefabParent(this.gameObject) != null)
            {
                string prefabPath = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.PrefabUtility.GetPrefabParent(this.gameObject));
                int startIndex = 1 + prefabPath.LastIndexOf("/");
                prefabPath = prefabPath.Substring(startIndex, prefabPath.LastIndexOf(".") - startIndex);
                uniqueID = string.Format("{0}_{1}", prefabPath, UnityEditor.PrefabUtility.GetPrefabParent(this.gameObject).name);
            }
            else
                uniqueID = string.Format("{0}_{1}", gameObject.name, this.GetInstanceID().ToString());
            strMeshAssetPath = "ColliderMeshes\\" + uniqueID + " _colMesh.asset";
            if (!InBatchSaveMode)
                strMeshAssetPath = UnityEditor.EditorUtility.SaveFilePanelInProject("Save mesh asset", strMeshAssetPath, "asset", "Save collider mesh for " + gameObject.name);
            else
                strMeshAssetPath = "Assets/" + strMeshAssetPath;

            if(strMeshAssetPath.Length == 0)
            {
                return false;
            }
        }

        MeshFilter theMesh = (MeshFilter)gameObject.GetComponent<MeshFilter>();
        
        bool bForceNoMultithreading = ForceNoMultithreading;

        if(Algorithm == EAlgorithm.Legacy)
        {
            // Force no multithreading for the legacy method since sometimes it hangs when merging hulls
            bForceNoMultithreading = true;
        }

        SConvexDecompositionInfoInOut      info      = new SConvexDecompositionInfoInOut();
        SConvexDecompositionInfoInOutVHACD infoVHACD = new SConvexDecompositionInfoInOutVHACD();

        if(Algorithm == EAlgorithm.VHACD)
        {
            ConcaveColliderDll20.DllInit(!bForceNoMultithreading);

            if(log != null)
            {
                ConcaveColliderDll20.SetLogFunctionPointer(Marshal.GetFunctionPointerForDelegate(log));
            }
            else
            {
                ConcaveColliderDll20.SetLogFunctionPointer(IntPtr.Zero);
            }

            if(progress != null)
            {
                ConcaveColliderDll20.SetProgressFunctionPointer(Marshal.GetFunctionPointerForDelegate(progress));
            }
            else
            {
                ConcaveColliderDll20.SetProgressFunctionPointer(IntPtr.Zero);
            }
        }
        else
        {
            DllInit(!bForceNoMultithreading);

            if(log != null)
            {
                SetLogFunctionPointer(Marshal.GetFunctionPointerForDelegate(log));
            }
            else
            {
                SetLogFunctionPointer(IntPtr.Zero);
            }

            if(progress != null)
            {
                SetProgressFunctionPointer(Marshal.GetFunctionPointerForDelegate(progress));
            }
            else
            {
                SetProgressFunctionPointer(IntPtr.Zero);
            }
        }

        int nMeshCount = 0;

        bool bDllCloseVHACD = false;

        if(theMesh)
        {
            if(theMesh.sharedMesh)
            {
                if(Algorithm == EAlgorithm.VHACD)
                {
	                  infoVHACD.fConcavity               = VHACD_Concavity;
	                  infoVHACD.fAlpha                   = 0.05f;
	                  infoVHACD.fBeta                    = 0.05f;
	                  infoVHACD.fGamma                   = 0.05f;
	                  infoVHACD.fDelta                   = 0.0005f;
	                  infoVHACD.fMinVolumePerCH          = VHACD_MinVolumePerCH;
	                  infoVHACD.uResolution              = (uint)VHACD_NumVoxels;
	                  infoVHACD.uMaxNumVerticesPerCH     = (uint)VHACD_MaxVerticesPerCH;
	                  infoVHACD.nDepth                   = 20;
	                  infoVHACD.nPlaneDownsampling       = 4;
	                  infoVHACD.nConvexhullDownsampling  = 4;
	                  infoVHACD.nPca                     = VHACD_NormalizeMesh ? 1 : 0;
	                  infoVHACD.nMode                    = 0;
	                  infoVHACD.nConvexhullApproximation = 1;
	                  infoVHACD.nOclAcceleration         = 1;

                    infoVHACD.uTriangleCount          = (uint)theMesh.sharedMesh.triangles.Length / 3;
                    infoVHACD.uVertexCount            = (uint)theMesh.sharedMesh.vertexCount;

                    if(DebugLog)
                    {
                        Debug.Log(string.Format("Processing mesh: {0} triangles, {1} vertices.", infoVHACD.uTriangleCount, infoVHACD.uVertexCount));
                    }
                }
                else
                {
                    info.uMaxHullVertices        = (uint)(Mathf.Max(3, MaxHullVertices));
                    info.uMaxHulls               = (uint)(Mathf.Max(1, MaxHulls));
                    info.fPrecision              = 1.0f - Mathf.Clamp01(Precision);
                    info.fBackFaceDistanceFactor = BackFaceDistanceFactor;
                    info.uLegacyDepth            = Algorithm == EAlgorithm.Legacy ? (uint)(Mathf.Max(1, LegacyDepth)) : 0;
                    info.uNormalizeInputMesh     = NormalizeInputMesh == true ? (uint)1 : (uint)0;
                    info.uUseFastVersion         = Algorithm == EAlgorithm.Fast ? (uint)1 : (uint)0;

                    info.uTriangleCount          = (uint)theMesh.sharedMesh.triangles.Length / 3;
                    info.uVertexCount            = (uint)theMesh.sharedMesh.vertexCount;

                    if(DebugLog)
                    {
                        Debug.Log(string.Format("Processing mesh: {0} triangles, {1} vertices.", info.uTriangleCount, info.uVertexCount));
                    }
                }


                Vector3[] av3Vertices = theMesh.sharedMesh.vertices;
                float fMeshRescale    = 1.0f;

                if(NormalizeInputMesh == false && InternalScale > 0.0f && Algorithm != EAlgorithm.VHACD)
                {
                    av3Vertices = new Vector3[theMesh.sharedMesh.vertexCount];
                    float fMaxDistSquared = 0.0f;

                    for(int nVertex = 0; nVertex < theMesh.sharedMesh.vertexCount; nVertex++)
                    {
                        float fDistSquared = theMesh.sharedMesh.vertices[nVertex].sqrMagnitude;

                        if(fDistSquared > fMaxDistSquared)
                        {
                            fMaxDistSquared = fDistSquared;
                        }
                    }

                    fMeshRescale = InternalScale / Mathf.Sqrt(fMaxDistSquared);

                    if(DebugLog)
                    {
                        Debug.Log("Max vertex distance = " + Mathf.Sqrt(fMaxDistSquared) + ". Rescaling mesh by a factor of " + fMeshRescale);
                    }

                    for(int nVertex = 0; nVertex < theMesh.sharedMesh.vertexCount; nVertex++)
                    {
                        av3Vertices[nVertex] = theMesh.sharedMesh.vertices[nVertex] * fMeshRescale;
                    }
                }

                bool bConvexDecompositionOK = false;

                int nHullsOut = 0;

                if(Algorithm == EAlgorithm.VHACD)
                {
                    bConvexDecompositionOK = ConcaveColliderDll20.DoConvexDecomposition(ref infoVHACD, av3Vertices, theMesh.sharedMesh.triangles);
                    nHullsOut = infoVHACD.nHullsOut;
                    bDllCloseVHACD = true;
                }
                else
                {
                    bConvexDecompositionOK = DoConvexDecomposition(ref info, av3Vertices, theMesh.sharedMesh.triangles);
                    nHullsOut = info.nHullsOut;
                }

                if(bConvexDecompositionOK)
                {
                    if(nHullsOut > 0)
                    {
                        if(DebugLog)
                        {
                            Debug.Log(string.Format("Created {0} hulls", nHullsOut));
                        }

                        DestroyHulls();

                        foreach(Collider collider in GetComponents<Collider>())
                        {
                            collider.enabled = false;
                        }

                        m_aGoHulls = new GameObject[nHullsOut];
                    }
                    else if(nHullsOut == 0)
                    {
                        hadError = true;
                        if(log != null) log("Error: No hulls were generated");
                    }
                    else
                    {
                        // -1 User cancelled
                        hadError = true;
                    }

                    Transform hullParent = this.transform.Find("Generated Colliders");
                    if (hullParent == null)
                    {
                        hullParent = (new GameObject("Generated Colliders")).transform;
                        hullParent.transform.SetParent(this.transform, false);
                    }

                    for(int nHull = 0; nHull < nHullsOut; nHull++)
                    {
                        SConvexDecompositionHullInfo hullInfo = new SConvexDecompositionHullInfo();

                        if(Algorithm == EAlgorithm.VHACD)
                        {
                            ConcaveColliderDll20.GetHullInfo((uint)nHull, ref hullInfo);
                        }
                        else
                        {
                            GetHullInfo((uint)nHull, ref hullInfo);
                        }

                        if(hullInfo.nTriangleCount > 0)
                        {
                            m_aGoHulls[nHull] = new GameObject("Hull " + nHull);
                            m_aGoHulls[nHull].transform.position = this.transform.position;
                            m_aGoHulls[nHull].transform.rotation = this.transform.rotation;
                            m_aGoHulls[nHull].transform.parent   = hullParent;
                            m_aGoHulls[nHull].layer              = this.gameObject.layer;

                            Vector3[] hullVertices = new Vector3[hullInfo.nVertexCount];
                            int[]     hullIndices  = new int[hullInfo.nTriangleCount * 3];
                            
                            float fHullVolume     = -1.0f;
                            float fInvMeshRescale = 1.0f / fMeshRescale;

                            if(Algorithm == EAlgorithm.VHACD)
                            {
                                ConcaveColliderDll20.FillHullMeshData((uint)nHull, ref fHullVolume, hullIndices, hullVertices);
                            }
                            else
                            {
                                FillHullMeshData((uint)nHull, ref fHullVolume, hullIndices, hullVertices);
                            }

                            if(NormalizeInputMesh == false && InternalScale > 0.0f && Algorithm != EAlgorithm.VHACD)
                            {
                                fInvMeshRescale = 1.0f / fMeshRescale;

                                for(int nVertex = 0; nVertex < hullVertices.Length; nVertex++)
                                {
                                    hullVertices[nVertex] *= fInvMeshRescale;
                                    hullVertices[nVertex]  = Vector3.Scale(hullVertices[nVertex], transform.localScale);
                                }
                            }
                            else
                            {
                                for(int nVertex = 0; nVertex < hullVertices.Length; nVertex++)
                                {
                                    hullVertices[nVertex] = Vector3.Scale(hullVertices[nVertex], transform.localScale);
                                }
                            }

                            Mesh hullMesh = new Mesh();
                            hullMesh.vertices  = hullVertices;
                            hullMesh.triangles = hullIndices;
                            
                            Collider hullCollider = null;

                            fHullVolume *= Mathf.Pow(fInvMeshRescale, 3.0f);
                            
                            if(fHullVolume < MinHullVolume && Algorithm != EAlgorithm.VHACD)
                            {
                                if(DebugLog)
                                {
                                    Debug.Log(string.Format("Hull {0} will be approximated as a box collider (volume is {1:F2})", nHull, fHullVolume));
                                }
                                
                                MeshFilter meshf = m_aGoHulls[nHull].AddComponent<MeshFilter>();
                                meshf.sharedMesh = hullMesh;

                                // Let Unity3D compute the best fitting box (it will use the meshfilter)
                                hullCollider = m_aGoHulls[nHull].AddComponent<BoxCollider>() as BoxCollider;
                                BoxCollider boxCollider = hullCollider as BoxCollider;
                                boxCollider.center = hullMesh.bounds.center;
                                boxCollider.size = hullMesh.bounds.size;

                                if(CreateHullMesh == false)
                                {
                                    if(Application.isEditor && Application.isPlaying == false)
                                    {
                                        DestroyImmediate(meshf);
                                        DestroyImmediate(hullMesh);
                                    }
                                    else
                                    {
                                        Destroy(meshf);
                                        Destroy(hullMesh);
                                    }
                                }
                                else
                                {
                                    meshf.sharedMesh.RecalculateNormals();
                                    meshf.sharedMesh.uv = new Vector2[hullVertices.Length];
                                }
                            }                           
                            else
                            {
                                if(DebugLog)
                                {
                                    Debug.Log(string.Format("Hull {0} collider: {1} vertices and {2} triangles. Volume = {3}", nHull, hullMesh.vertexCount, hullMesh.triangles.Length / 3, fHullVolume));
                                }

                                MeshCollider meshCollider = m_aGoHulls[nHull].AddComponent<MeshCollider>() as MeshCollider;

                                meshCollider.sharedMesh = hullMesh;
                                meshCollider.convex     = true;
                                
                                hullCollider = meshCollider;

                                if(CreateHullMesh)
                                {
                                    MeshFilter meshf = m_aGoHulls[nHull].AddComponent<MeshFilter>();
                                    meshf.sharedMesh = hullMesh;
                                }
                                
                                if(CreateMeshAssets)
                                {
                                    if(nMeshCount == 0)
                                    {
                                        if(progress != null)
                                        {
                                            progress("Creating mesh assets", 0.0f);
                                        }

                                        // Avoid some shader warnings
                                        hullMesh.RecalculateNormals();
                                        hullMesh.uv = new Vector2[hullVertices.Length];

                                        UnityEditor.AssetDatabase.CreateAsset(hullMesh, strMeshAssetPath);
                                    }
                                    else
                                    {
                                        if(progress != null)
                                        {
                                            progress("Creating mesh assets", nHullsOut > 1 ? (nHull / (nHullsOut - 1.0f)) * 100.0f : 100.0f);
                                        }

                                        // Avoid some shader warnings
                                        hullMesh.RecalculateNormals();
                                        hullMesh.uv = new Vector2[hullVertices.Length];

                                        UnityEditor.AssetDatabase.AddObjectToAsset(hullMesh, strMeshAssetPath);
                                        UnityEditor.AssetDatabase.ImportAsset(UnityEditor.AssetDatabase.GetAssetPath(hullMesh));
                                    }
                                }
                                
                                nMeshCount++;
                            }
                            
                            if(hullCollider)
                            {
                                hullCollider.material  = PhysMaterial;
                                hullCollider.isTrigger = IsTrigger;

                                if(hullInfo.nTriangleCount > LargestHullFaces)    LargestHullFaces    = hullInfo.nTriangleCount;
                                if(hullInfo.nVertexCount   > LargestHullVertices) LargestHullVertices = hullInfo.nVertexCount;
                            }
                        }
                    }

                    if(CreateMeshAssets)
                    {
                        UnityEditor.AssetDatabase.Refresh();
                    }
                }
                else
                {
                    hadError = true;
                    if(log != null) log("Error: convex decomposition could not be completed due to errors");
                }
            }
            else
            {
                hadError = true;
                if(log != null) log("Error: " + this.name + " has no mesh");
            }
        }
        else
        {
            hadError = true;
            if(log != null) log("Error: " + this.name + " has no mesh");
        }

        if(Algorithm == EAlgorithm.VHACD)
        {
            if(bDllCloseVHACD)
            {
                ConcaveColliderDll20.DllClose();
            }
        }
        else
        {
            DllClose();
        }
        return !hadError;
    }

    private static ProgressDelegate CreateProgressBar(System.Action onCancel, string title = "Computing hulls")
    {
        return (string message, float fPercent)=>
        {
            if(UnityEditor.EditorUtility.DisplayCancelableProgressBar(title, message, fPercent / 100.0f) && onCancel != null)
                onCancel();
        };
    }

    private void ShowEditorProgressBar(string message, float fPercent)
    {
        if(UnityEditor.EditorUtility.DisplayCancelableProgressBar("Computing hulls", message, fPercent / 100.0f))
        {
            CancelComputation();
        }
    }

    public static List<Mesh> ReadVrml(string contents, ProgressDelegate progress, string debugNameObj = "???")
    {
        if (progress != null)
            progress("Loading Vrml file", 0.0f);
        List<Mesh> ret = new List<Mesh>();
        MatchCollection matches = Regex.Matches(contents, 
            "Group {.*?"
            + "point\\s*\\[(?<verts>[^\\]]+).*?"
            + "coordIndex\\s*\\[(?<indices>[^\\]]+)"
            + "",
            RegexOptions.Multiline | RegexOptions.Singleline);
        int meshCounter = 0;
        foreach(Match m in matches)
        {
            if (progress != null)
                progress("Converting Vrml file to Unity meshes", 100f * (meshCounter + 0.5f) / matches.Count);
            Mesh newMesh = new Mesh();
            MatchCollection vertMatches = Regex.Matches(m.Groups["verts"].Value,
                "^\\s*(?<x>-?\\d*\\.?\\d+)\\s+(?<y>-?\\d*\\.?\\d+)\\s+(?<z>-?\\d*\\.?\\d+),", RegexOptions.Multiline);
            MatchCollection indexMatches = Regex.Matches(m.Groups["indices"].Value,
                "^\\s*(?<x>\\d+),\\s+(?<y>\\d+),\\s+(?<z>\\d+),", RegexOptions.Multiline);
//            Debug.LogFormat("Found match: v:{3}-{0},i:{4}-{1}, all:{2}", m.Groups["verts"].Value, m.Groups["indices"].Value, m.Value, vertMatches.Count, indexMatches.Count * 3);
            Vector3[] verts = new Vector3[vertMatches.Count];
            int[] indices = new int[indexMatches.Count * 3];
            int itrCounter = 0;
            // Have to reflect the x value for some reason
            foreach(Match vm in vertMatches)
                verts[itrCounter++] = new Vector3(-float.Parse(vm.Groups["x"].Value), float.Parse(vm.Groups["y"].Value), float.Parse(vm.Groups["z"].Value));
            itrCounter = 0;
            foreach(Match im in indexMatches)
            {
                indices[itrCounter++] = int.Parse(im.Groups["x"].Value);
                indices[itrCounter++] = int.Parse(im.Groups["y"].Value);
                indices[itrCounter++] = int.Parse(im.Groups["z"].Value);
            }
            newMesh.vertices  = verts;
            newMesh.triangles = indices;
			// This checks the number of triangles in the each mesh and ignores 
			// the larger meshes. Probably for performance issues.
			// newMesh.triangles.Length actually spits out three time the number of triangles!
            if (newMesh.triangles.Length > 765)
                Debug.LogWarningFormat("Too many triangles in vrml mesh for {0}! Found {1}", debugNameObj, newMesh.triangles.Length);
			else if (newMesh.triangles.Length < 3)
				Debug.LogWarningFormat ("Insufficient vertices to form a triangle in vrml mesh for {0}! Found {1}", debugNameObj, newMesh.triangles.Length);
            else
                ret.Add(newMesh);
            meshCounter++;
        }
        return ret;
    }

    [UnityEditor.MenuItem ("GameObject/ConcaveCollider/CreateColliders %&c")]
    private static void CreateCollidersMenuCommand()
    {
        GameObject obj = UnityEditor.Selection.activeGameObject;
        FH_CreateColliders(obj, null, false);
    }

    static GameObject batchTarget = null;
    static bool shouldCancelBatch = false;
    private static void FH_Cancel()
    {
        shouldCancelBatch = true;
        ConcaveCollider current = null;
        if (batchTarget != null)
            current = batchTarget.GetComponent<ConcaveCollider>();
        if (current != null)
            current.CancelComputation();
    }

    public static void FH_CreateColliders(GameObject obj, string vrmlText, bool isBatchMode)
    {
        if (obj == null)
            return;
        if (UnityEditor.PrefabUtility.GetPrefabType(obj) == UnityEditor.PrefabType.ModelPrefab)
        {
            Debug.LogWarningFormat("{0} is a model prefab and can't be edited to have meshes", obj.name);
            return;
        }
        InBatchSaveMode = isBatchMode;
        GameObject prefab = UnityEditor.PrefabUtility.FindPrefabRoot(obj) as GameObject;
        GameObject createdInstance = null;
        if (prefab != null && UnityEditor.PrefabUtility.GetPrefabType(obj) == UnityEditor.PrefabType.Prefab)
        {
            // Since we can't modify prefabs directly, we'll instantiate it first and modify that instead
            createdInstance = UnityEditor.PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Debug.LogFormat("{0} is prefab", obj.name, UnityEditor.PrefabUtility.GetPrefabType(createdInstance));
            obj = createdInstance;
        }
        else
        {
            Debug.LogFormat("{0} is not prefab: {1}", obj.name, UnityEditor.PrefabUtility.GetPrefabType(obj));
        }
        shouldCancelBatch = false;
        ProgressDelegate progressBar = new ConcaveCollider.ProgressDelegate(CreateProgressBar(FH_Cancel));
        if (vrmlText != null)
        {
            FH_CreateHullsFromVrml(obj, vrmlText, progressBar);
            UnityEditor.EditorUtility.ClearProgressBar();
        }
        else
        {
            MeshFilter [] foundMeshFilters = obj.GetComponentsInChildren<MeshFilter>();
            if (foundMeshFilters == null || foundMeshFilters.Length == 0)
                FH_ComputeHulls(obj, progressBar);
            else
            {
                foreach(MeshFilter curMeshFilter in foundMeshFilters)
                    FH_ComputeHulls(curMeshFilter.gameObject, progressBar);
            }
        }
        shouldCancelBatch = false;
        if (createdInstance != null)
        {
            UnityEditor.PrefabUtility.ReplacePrefab(createdInstance, prefab);
            DestroyImmediate(createdInstance);
        }
        InBatchSaveMode = false;
    }

    private static void FH_CreateHullsFromVrml(GameObject obj, string vrml, ProgressDelegate progress)
    {
        Debug.Log("Loading VRML for " + obj.name);
        List<Mesh> meshes = ReadVrml(vrml, progress, obj.name);
        for (int i = 0; i < meshes.Count; ++i)
        {
            if (meshes[i] == null)
            {
                Debug.LogWarningFormat("Found null mesh at #{0} in {1}. Aborting early.\n{2}", i, obj.name, vrml);
                return;
            }
        }
        int nMeshCount = 0;

        progress("Finding collision mesh asset paths", 0.0f);
        string strMeshAssetPath = "";
        bool CreateMeshAssets = true;
        bool DebugLog = true;
        PhysicMaterial PhysMaterial = new PhysicMaterial();
        bool IsTrigger = false;
        bool CreateHullMesh = true;

        if(CreateMeshAssets)
        {
            string uniqueID = null;;
            Debug.LogFormat("Obj: {0} of type {1}", obj.transform.FullPath(), UnityEditor.PrefabUtility.GetPrefabType(obj));
            if (UnityEditor.PrefabUtility.GetPrefabParent(obj) != null)
            {
                string prefabPath = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.PrefabUtility.GetPrefabParent(obj));
                int startIndex = 1 + prefabPath.LastIndexOf("/");
                prefabPath = prefabPath.Substring(startIndex, prefabPath.LastIndexOf(".") - startIndex);
                uniqueID = string.Format("{0}_{1}", prefabPath, UnityEditor.PrefabUtility.GetPrefabParent(obj).name);
            }
            else
                uniqueID = string.Format("{0}_{1}", obj.name, obj.GetInstanceID().ToString());
            strMeshAssetPath = "ColliderMeshes\\" + uniqueID + " _colMesh.asset";
            if (!InBatchSaveMode)
                strMeshAssetPath = UnityEditor.EditorUtility.SaveFilePanelInProject("Save mesh asset", strMeshAssetPath, "asset", "Save collider mesh for " + obj.name);
            else
                strMeshAssetPath = "Assets/" + strMeshAssetPath;

            if(strMeshAssetPath.Length == 0)
            {
                return;
            }
        }

        progress("Clearing old colliders", 0.0f);
        int nHullsOut = meshes.Count;
        if(DebugLog)
            Debug.Log(string.Format("Created {0} hulls from VRML for {1}", nHullsOut, obj.name));

        Transform hullParent = obj.transform.Find("Generated Colliders");
        if(hullParent != null)
        {
            if(Application.isEditor && Application.isPlaying == false)
                DestroyImmediate(hullParent.gameObject);
            else
                Destroy(hullParent.gameObject);
        }
        hullParent = (new GameObject("Generated Colliders")).transform;
        hullParent.transform.SetParent(obj.transform, false);

        foreach(Collider collider in obj.GetComponents<Collider>())
        {
            collider.enabled = false;
        }

        GameObject[] m_aGoHulls = new GameObject[nHullsOut];

        obj.transform.rotation = Quaternion.Euler(new Vector3(0,0f,0));
        Vector3 newScale = obj.transform.localScale;
//        newScale.z = -newScale.z;
        for(int nHull = 0; nHull < nHullsOut && !shouldCancelBatch; nHull++)
        {
            progress("Transforming VRML vertices and saving out mesh data", 100f * (nHull + 0.5f) / nHullsOut);
            Mesh refMesh = meshes[nHull];
            if (refMesh == null)
                Debug.LogWarningFormat("Found null mesh at #{0} in {1}", nHull, obj.name);
            if(refMesh != null && refMesh.vertexCount > 0)
            {
                m_aGoHulls[nHull] = new GameObject("Hull " + nHull);
                m_aGoHulls[nHull].transform.position = obj.transform.position;
                m_aGoHulls[nHull].transform.rotation = obj.transform.rotation;
                m_aGoHulls[nHull].transform.parent   = hullParent;
                m_aGoHulls[nHull].layer              = obj.layer;


                Vector3[] hullVertices = refMesh.vertices;

//                    float fHullVolume     = -1.0f;
//                    float fInvMeshRescale = 1.0f / fMeshRescale;

                for(int nVertex = 0; nVertex < hullVertices.Length; nVertex++)
                    hullVertices[nVertex] = Vector3.Scale(hullVertices[nVertex], newScale);

                Mesh hullMesh = refMesh;
                hullMesh.vertices  = hullVertices;

                Collider hullCollider = null;

//                    fHullVolume *= Mathf.Pow(fInvMeshRescale, 3.0f);

                // Create mesh collider
                MeshCollider meshCollider = m_aGoHulls[nHull].AddComponent<MeshCollider>() as MeshCollider;

                meshCollider.sharedMesh = hullMesh;
                meshCollider.convex     = true;

                hullCollider = meshCollider;

                if(CreateHullMesh)
                {
                    MeshFilter meshf = m_aGoHulls[nHull].AddComponent<MeshFilter>();
                    meshf.sharedMesh = hullMesh;
                }

                if(CreateMeshAssets)
                {
                    if(nMeshCount == 0)
                    {
                        // Avoid some shader warnings
                        hullMesh.RecalculateNormals();
                        hullMesh.uv = new Vector2[hullVertices.Length];

                        UnityEditor.AssetDatabase.CreateAsset(hullMesh, strMeshAssetPath);
                    }
                    else
                    {
                        // Avoid some shader warnings
                        hullMesh.RecalculateNormals();
                        hullMesh.uv = new Vector2[hullVertices.Length];

                        UnityEditor.AssetDatabase.AddObjectToAsset(hullMesh, strMeshAssetPath);
                        UnityEditor.AssetDatabase.ImportAsset(UnityEditor.AssetDatabase.GetAssetPath(hullMesh));
                    }
                }

                nMeshCount++;

                if(hullCollider)
                {
                    hullCollider.material  = PhysMaterial;
                    hullCollider.isTrigger = IsTrigger;
                }
            }
        }

        if(CreateMeshAssets)
        {
            UnityEditor.AssetDatabase.Refresh();
        }
    }

    private static void FH_ComputeHulls(GameObject obj, ProgressDelegate progress = null)
    {
        if (obj == null || shouldCancelBatch)
            return;
        ConcaveCollider current = obj.GetComponent<ConcaveCollider>();
        bool hadComponent = current != null;
        Debug.LogWarningFormat("Processing mesh: {0}", obj.name);
        if (current == null)
            current = obj.AddComponent<ConcaveCollider>();
        try {
            if (current.ComputeHulls(new ConcaveCollider.LogDelegate(Debug.Log), progress))
                Debug.LogFormat("Completed computing hulls for {0}", obj.name);
            else
                Debug.LogWarningFormat("Error computing hulls for {0}", obj.name);
        }
        catch(System.Exception e)
        {
            Debug.LogWarningFormat("Error computing hulls for {0}: {1}\n{2}", obj.name, e.Message, e.StackTrace);
        }
        Debug.LogWarningFormat("Finished processing mesh: {0}", obj.name);
        UnityEditor.EditorUtility.ClearProgressBar();
        if (!hadComponent)
            DestroyImmediate(current, true);
    }

#endif // UNITY_EDITOR

    public int GetLargestHullVertices()
    {
        return LargestHullVertices;
    }

    public int GetLargestHullFaces()
    {
        return LargestHullFaces;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SConvexDecompositionInfoInOut
    {
        // In parameters

        public uint     uMaxHullVertices;
        public uint     uMaxHulls;
        public float    fPrecision;
        public float    fBackFaceDistanceFactor;
        public uint     uLegacyDepth;
        public uint     uNormalizeInputMesh;
        public uint     uUseFastVersion;

        public uint     uTriangleCount;
        public uint     uVertexCount;

        // Out parameters

        public int      nHullsOut;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SConvexDecompositionInfoInOutVHACD
    {
    	  // In parameters

	      public uint  uTriangleCount;
	      public uint  uVertexCount;

	      public float fConcavity;
	      public float fAlpha;
	      public float fBeta;
	      public float fGamma;
	      public float fDelta;
	      public float fMinVolumePerCH;
	      public uint  uResolution;
	      public uint  uMaxNumVerticesPerCH;
	      public int   nDepth;
	      public int   nPlaneDownsampling;
	      public int   nConvexhullDownsampling;
	      public int   nPca;
	      public int   nMode;
	      public int   nConvexhullApproximation;
	      public int   nOclAcceleration;

	      // Out parameters

	      public int   nHullsOut;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SConvexDecompositionHullInfo
    {
        public int      nVertexCount;
        public int      nTriangleCount;
    };

    [DllImport("ConvexDecompositionDll")]
    private static extern void DllInit(bool bUseMultithreading);

    [DllImport("ConvexDecompositionDll")]
    private static extern void DllClose();

    [DllImport("ConvexDecompositionDll")]
    private static extern void SetLogFunctionPointer(IntPtr pfnUnity3DLog);

    [DllImport("ConvexDecompositionDll")]
    private static extern void SetProgressFunctionPointer(IntPtr pfnUnity3DProgress);

    [DllImport("ConvexDecompositionDll")]
    private static extern void CancelConvexDecomposition();

    [DllImport("ConvexDecompositionDll")]
    private static extern bool DoConvexDecomposition(ref SConvexDecompositionInfoInOut infoInOut, Vector3[] pfVertices, int[] puIndices);

    [DllImport("ConvexDecompositionDll")]
    private static extern bool GetHullInfo(uint uHullIndex, ref SConvexDecompositionHullInfo infoOut);

    [DllImport("ConvexDecompositionDll")]
    private static extern bool FillHullMeshData(uint uHullIndex, ref float pfVolumeOut, int[] pnIndicesOut, Vector3[] pfVerticesOut);

    private class ConcaveColliderDll20
    {
        // Version 2.0 with VHACD

        [DllImport("ConvexDecompositionDll20")]
        public static extern void DllInit(bool bUseMultithreading);

        [DllImport("ConvexDecompositionDll20")]
        public static extern void DllClose();

        [DllImport("ConvexDecompositionDll20")]
        public static extern void SetLogFunctionPointer(IntPtr pfnUnity3DLog);

        [DllImport("ConvexDecompositionDll20")]
        public static extern void SetProgressFunctionPointer(IntPtr pfnUnity3DProgress);

        [DllImport("ConvexDecompositionDll20")]
        public static extern void CancelConvexDecomposition();

        [DllImport("ConvexDecompositionDll20")]
        public static extern bool DoConvexDecomposition(ref SConvexDecompositionInfoInOutVHACD infoInOut, Vector3[] pfVertices, int[] puIndices);

        [DllImport("ConvexDecompositionDll20")]
        public static extern bool GetHullInfo(uint uHullIndex, ref SConvexDecompositionHullInfo infoOut);

        [DllImport("ConvexDecompositionDll20")]
        public static extern bool FillHullMeshData(uint uHullIndex, ref float pfVolumeOut, int[] pnIndicesOut, Vector3[] pfVerticesOut);
    }
}