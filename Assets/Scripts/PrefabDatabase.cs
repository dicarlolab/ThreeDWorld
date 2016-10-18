using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.IO;
using System.Text;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PrefabDatabase : MonoBehaviour
{

	[System.Serializable]
	public class PrefabInfo
	{
		public string fileName;
		public int complexity;
		public bool isLight;
		public GeneratablePrefab.AttachAnchor anchorType;
		public Bounds bounds;
		public List<GeneratablePrefab.StackableInfo> stackableAreas = new List<GeneratablePrefab.StackableInfo>();
                public string option_scale="NULL"; // for option of how scale should happen
                public float dynamic_scale=1f; // for the dynamic scale come with option
	}

	#region Fields

	public List<PrefabInfo> prefabs = new List<PrefabInfo> ();
	private Dictionary<PrefabInfo, float> sceneScale = new Dictionary<PrefabInfo, float> ();
	private static PrefabDatabase _Instance = null;
	const string BUNDLES_SUBPATH = "Assets/PrefabDatabase/AssetBundles/Separated/";
	const string PREFABS_SUBPATH = "Assets/PrefabDatabase/GeneratedPrefabs/";

	#endregion

	#region Properties

	public static PrefabDatabase Instance {
		get { return _Instance; }
	}

	#endregion

	#region Unity Callbacks

	private void Awake ()
	{
		_Instance = this;
	}

	void Start ()
	{
		Init ();
	}

	#endregion

	private void Init ()
	{
		foreach (PrefabInfo prefab in prefabs) {
			sceneScale.Add (prefab, 1f);
		}
	}

	public float GetSceneScale (PrefabInfo prefab)
	{
		float value;
		if (sceneScale.TryGetValue (prefab, out value)) {
			return value;
		} else {
			Debug.LogErrorFormat ("{0} not in database!", prefab.fileName);
			return 0f;
		}
	}

	public void SetPrefabScaleAll ()
	{
		//TODO: Implement this
	}

	public void SetPrefabScaleSingle ()
	{
		//TODO: Implement this
	}

	private float GetScaleFromAbsolute (GameObject obj, Vector3 absolute)
	{
		// TODO: Implement this
		return 1f;
	}

	public static GameObject LoadAssetFromBundle (string fileName)
	{
		AssetBundle loadedAssetBundle = AssetBundle.LoadFromFile (fileName);
		if (loadedAssetBundle == null) {
			Debug.Log ("Failed to load AssetBundle!");
			return null;
		}
		GameObject prefab = loadedAssetBundle.LoadAsset<GameObject> (loadedAssetBundle.GetAllAssetNames () [0]);
		loadedAssetBundle.Unload (false);
		return prefab;
	}

	public static GameObject LoadAssetFromBundleWWW (string fileName)
	{
                var www = WWW.LoadFromCacheOrDownload (fileName, 0);

                if (!String.IsNullOrEmpty(www.error))
                {
                        Debug.Log (www.error);
                        return null;
                }                 
                var loadedAssetBundle = www.assetBundle;
                GameObject prefab = loadedAssetBundle.LoadAsset<GameObject> (loadedAssetBundle.GetAllAssetNames () [0]);
                loadedAssetBundle.Unload (false);

                return prefab;
	}

	#if UNITY_EDITOR
	// Setup a new prefab object from a model
	[MenuItem ("Prefab Database/1. Create Prefabs", false, 80)]
	private static void CreatePrefabFromModel ()
	{
		Dictionary<GameObject, string> allSelected = new Dictionary<GameObject, string> ();

		foreach (GameObject obj in Selection.gameObjects) {
			allSelected.Add (obj, AssetDatabase.GetAssetPath (obj));
		}

		foreach (UnityEngine.Object obj in Selection.objects) {
			if (obj != null) {
				if (obj is GameObject) {
					allSelected.Add (obj as GameObject, AssetDatabase.GetAssetPath (obj));
				} else if (obj is DefaultAsset) {
					HashSet<GameObject> children = (obj as DefaultAsset).GetAllChildrenAssets<GameObject> ();
					foreach (GameObject child in children) {
						allSelected.Add (child, AssetDatabase.GetAssetPath (child));
					}
				}
			}
		}
		if (allSelected == null || allSelected.Count == 0)
			return;

		List<GameObject> toRemove = new List<GameObject> ();
		foreach (KeyValuePair<GameObject, string> entry in allSelected) {
			if (entry.Key == null || PrefabUtility.GetPrefabType (entry.Key) != PrefabType.ModelPrefab) {
				toRemove.Add (entry.Key);
			}
		}

		foreach (GameObject entry in toRemove) {
			allSelected.Remove (entry);
		}

		foreach (KeyValuePair<GameObject, string> entry in allSelected) {
			string loadingDirectory = "Assets/Models/";

			if (!entry.Value.StartsWith (loadingDirectory)) {
				Debug.LogError ("Selected Assets are not contained within " + loadingDirectory + " directory. Please move or copy assets to this directory, select them from there and try again.");
			} else {
				//get rid of location in hierarchy
				string subPathWithFileType = entry.Value.Remove (0, loadingDirectory.Length);
				//get rid of file type
				string subPathWithoutFileType = subPathWithFileType.Remove (subPathWithFileType.LastIndexOf ('.'));
				//get rid of file name
				string subPathWithoutFilename = subPathWithoutFileType.Remove (subPathWithoutFileType.LastIndexOf ('/'));

				//if directory does not already exist, make one
//				System.IO.Directory.CreateDirectory(Application.dataPath + "/Resources/Prefabs/Converted Models/" + subPathWithoutFilename);
				System.IO.Directory.CreateDirectory (Application.dataPath + "/PrefabDatabase/GeneratedPrefabs/" + subPathWithoutFilename);

				Debug.Log (Application.dataPath + "/PrefabDatabase/GeneratedPrefabs/" + subPathWithoutFilename);

				//make meta file
				EditorApplication.Step ();

				//make prefab
				MakeSimplePrefabObj (entry.Key, subPathWithoutFileType);

				//force GC
				EditorApplication.SaveAssets ();
				Resources.UnloadUnusedAssets ();
				EditorUtility.UnloadUnusedAssetsImmediate ();
				GC.Collect ();
				EditorApplication.Step ();

			}
		}
		SetupPrefabsFull ();
	}

	private static void MakeSimplePrefabObj (GameObject obj, string subPath)
	{
		Debug.LogFormat ("MakeSimplePrefabObj {0}", obj.name);
		// Find vrml text if we have it
		string vrmlText = null;
		string vrmlPath = AssetDatabase.GetAssetPath (obj);
		if (vrmlPath != null) {
			vrmlPath = vrmlPath.Replace (".obj", ".wrl");
			string fullPath = System.IO.Path.Combine (Application.dataPath, vrmlPath.Substring (7));
			if (!System.IO.File.Exists (fullPath))
				fullPath = fullPath.Replace (".wrl", "_vhacd.wrl");
			if (System.IO.File.Exists (fullPath)) {
				using (System.IO.StreamReader reader = new System.IO.StreamReader (fullPath)) {
					vrmlText = reader.ReadToEnd ();
				}
			} else
				Debug.LogFormat ("Cannot find {0}\n{1}", vrmlPath, fullPath);
		}

		GameObject instance = GameObject.Instantiate (obj) as GameObject;
		instance.name = obj.name;

		// Remove any old colliders.
		Collider[] foundColliders = instance.transform.GetComponentsInChildren<Collider> ();
		foreach (Collider col in foundColliders)
			UnityEngine.Object.DestroyImmediate (col, true);

		// Create SemanticObject/Rigidbody
		instance.AddComponent<SemanticObjectSimple> ().name = instance.name;

		// Add generatable prefab tags
		instance.AddComponent<GeneratablePrefab> ();

		//NormalSolver.RecalculateNormals (instance.GetComponentInChildren<MeshFilter> ().sharedMesh, 60);

		// Save as a prefab
//		string prefabAssetPath = string.Format("Assets/Resources/Prefabs/Converted Models/{0}.prefab", subPath);
		string prefabAssetPath = string.Format ("Assets/PrefabDatabase/GeneratedPrefabs/{0}.prefab", subPath);
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject> (prefabAssetPath);
		if (prefab == null)
			prefab = PrefabUtility.CreatePrefab (prefabAssetPath, instance);
		else
			prefab = PrefabUtility.ReplacePrefab (instance, prefab);
		GameObject.DestroyImmediate (instance);


		// Create colliders for the prefab
		ConcaveCollider.FH_CreateColliders (prefab, vrmlText, true);

		// Save out updated metadata settings
		GeneratablePrefab metaData = prefab.GetComponent<GeneratablePrefab> ();
		metaData.ProcessPrefab ();
		SetupPrefabs (false);

		EditorUtility.SetDirty (prefab);
		try {
			EditorUtility.SetDirty (instance);
		} catch {
			//do nothing
		}
	}

	[MenuItem ("Prefab Database/2. Build AssetBundles/Merged", false, 90)]

	static void BuildAllAssetBundlesMerged ()
	{
		/* 
		 * Build a single asset bundle from selected folder of prefabs.
		 */
		string defaultName = "default_bundle.bundle";
		// Build all bundles existing in the project
		//		BuildPipeline.BuildAssetBundles ("Assets/AssetBundles/", BuildAssetBundleOptions.None, BuildTarget.StandaloneOSXUniversal);
		// For building bundles selectively
		Dictionary<GameObject, string> selectedPrefabs = ListSelectedPrefabs ();
		AssetBundleBuild[] buildMap = new AssetBundleBuild[1];
		string[] assetPath = new string[selectedPrefabs.Count];

		int loop_counter = 0;
		foreach (KeyValuePair<GameObject, string> entry in selectedPrefabs) {
			assetPath [loop_counter] = entry.Value;
			loop_counter++;
		}
		buildMap [0].assetBundleName = defaultName;
		buildMap [0].assetNames = assetPath;
		BuildPipeline.BuildAssetBundles ("Assets/PrefabDatabase/AssetBundles/Merged", 
			buildMap, 
			BuildAssetBundleOptions.None,
			BuildTarget.StandaloneOSXUniversal
		);
	}

	[MenuItem ("Prefab Database/2. Build AssetBundles/Separate", false, 90)]

	static void BuildAllAssetBundlesSeparate ()
	{
		/* 
		 * Build an asset bundle for each prefab inside the selected folder of prefabs.
		 */
		// Build all bundles existing in the project
		//		BuildPipeline.BuildAssetBundles ("Assets/AssetBundles/", BuildAssetBundleOptions.None, BuildTarget.StandaloneOSXUniversal);
		// For building bundles selectively
		Dictionary<GameObject, string> selectedPrefabs = ListSelectedPrefabs ();
		AssetBundleBuild[] buildMap = new AssetBundleBuild[selectedPrefabs.Count];

		int loop_counter = 0;
		foreach (KeyValuePair<GameObject, string> entry in selectedPrefabs) {
			string _bundleName;
			string[] _assetPath = new string[1];
			_bundleName = entry.Key.name + ".bundle";
			// In case prefabs had some kind of file extension replace the file extension with .bundle
			//			_bundleName = _bundleName.Remove(_bundleName.LastIndexOf('.')) + ".bundle";

			buildMap [loop_counter].assetBundleName = _bundleName;
			_assetPath [0] = entry.Value;
			buildMap [loop_counter].assetNames = _assetPath;
			loop_counter++;
		}

		BuildPipeline.BuildAssetBundles ("Assets/PrefabDatabase/AssetBundles/Separated", 
			buildMap, 
			BuildAssetBundleOptions.None,
			BuildTarget.StandaloneOSXUniversal
		);
	}

	[MenuItem ("Prefab Database/3. Setup Bundles", false, 100)]
	private static void SetupBundlesMenu ()
	{
		SetupBundles ();
	}

	// Finds all prefabs that we can use and create a lookup table with relevant information
	[MenuItem ("Prefab Database/Setup Bundles Quick", false, 113)]
	private static void SetupPrefabsQuick ()
	{
		SetupPrefabs (false);
	}

	[MenuItem ("Prefab Database/Setup Prefabs Full", false, 113)]
	private static void SetupPrefabsFull ()
	{
		SetupPrefabs (true);
	}



	public static void SetupBundles ()
	{
		/* 
		 * Loads the PrefabDatabase prefab and stores the bundles information 
		 */
                string path_database = "Assets/ScenePrefabs/PrefabDatabase.prefab";
		PrefabDatabase database = AssetDatabase.LoadAssetAtPath<PrefabDatabase> (path_database);
		if (database != null)
			database.CompileAssetBundles ();
                //database = PrefabUtility.ReplacePrefab (database, database);
                
		//PrefabDatabase database_ = AssetDatabase.LoadAssetAtPath<PrefabDatabase> (path_database);
		//PrefabUtility.ReplacePrefab(database, database_);
                EditorApplication.SaveAssets ();
	}

	public static void SetupPrefabs (bool shouldRecompute)
	{
		/* 
		 * Loads the PrefabDatabase prefab and stores the prefabs asset information information. 
		 */
		PrefabDatabase database = AssetDatabase.LoadAssetAtPath<PrefabDatabase> ("Assets/ScenePrefabs/PrefabDatabase.prefab");
		if (database != null)
			database.CompileListOfProceduralComponents (shouldRecompute);
	}

	public void SavePrefabInformation (GeneratablePrefab prefab, string assetPath, bool shouldRecomputePrefabInformation, bool replaceOld = true)
	{
		/*
		 * Saves the prefab information in "prefabs" property of PrefabDatabase prefab. 
		 */
		string newFileName = "";
		if (assetPath == null) {
			const string resPrefix = "Resources/";
			assetPath = AssetDatabase.GetAssetPath(prefab);
			if (string.IsNullOrEmpty (assetPath))
				return;
			newFileName = assetPath.Substring(assetPath.LastIndexOf(resPrefix) + resPrefix.Length);
			newFileName = newFileName.Substring(0, newFileName.LastIndexOf("."));
		}
		else {
                    if (!(assetPath.ToLowerInvariant().Contains("http://"))) {
			const string resPrefix = BUNDLES_SUBPATH;
			string currentPath = Directory.GetCurrentDirectory ();
			newFileName = Path.Combine (currentPath, assetPath);
                    } else {
                        newFileName = assetPath;
                    }
		}
		int replaceIndex = -1;
		if (replaceOld) {
			replaceIndex = prefabs.FindIndex ((PrefabInfo testInfo) => {
				return testInfo.fileName == newFileName;
			});
			if (replaceIndex >= 0)
				prefabs.RemoveAt (replaceIndex);
		}

		if (prefab.shouldUse) {

			if (shouldRecomputePrefabInformation)
				prefab.ProcessPrefab ();
			PrefabInfo newInfo = new PrefabInfo ();
			newInfo.fileName = newFileName;
			newInfo.complexity = prefab.myComplexity;
			newInfo.bounds = prefab.myBounds;
			newInfo.isLight = prefab.isLight;
			newInfo.anchorType = prefab.attachMethod;
			foreach (GeneratablePrefab.StackableInfo stackRegion in prefab.stackableAreas)
				newInfo.stackableAreas.Add (stackRegion);
			if (replaceIndex < 0)
				prefabs.Add (newInfo);
			else
				prefabs.Insert (replaceIndex, newInfo);
		}        
		EditorUtility.SetDirty (this);
	}

	// Save out core information so we can decide whether to place the objects dynamically even if they aren't loaded yet
	private void CompileListOfProceduralComponents (bool shouldRecomputePrefabInformation)
	{
		GeneratablePrefab[] allThings = Resources.LoadAll<GeneratablePrefab> ("");
		prefabs.Clear ();
		foreach (GeneratablePrefab prefab in allThings)
			SavePrefabInformation(prefab, null, shouldRecomputePrefabInformation);
		EditorUtility.SetDirty (this);
	}

	private void CompileAssetBundles ()
	{ 
		string currentPath = Directory.GetCurrentDirectory ();
		string AssetBundlesPath = Path.Combine (currentPath, "Assets/PrefabDatabase/AssetBundles/");
		string[] assetBundleList = null;		// List of assetbundles in the project
		assetBundleList = Directory.GetFiles (Path.Combine (AssetBundlesPath, "Separated"), "*.bundle");

		// Load all assets from all bundles
		prefabs.Clear ();
		if (assetBundleList == null) {
			return;
		}

		foreach (string f in assetBundleList) {
			var loadedAssetBundle = AssetBundle.LoadFromFile (f);

			if (loadedAssetBundle == null) {
				Debug.LogFormat ("Failed to load AssetBundle {0}", f);
//				loadedAssetBundle.Unload(false);
				continue;
			} else {
				string[] assetList = loadedAssetBundle.GetAllAssetNames ();
				foreach (string asset in assetList) { 
					GameObject gObj = loadedAssetBundle.LoadAsset<GameObject> (asset);
					GeneratablePrefab[] prefab = gObj.GetComponents<GeneratablePrefab> ();
					if (prefab.GetLength (0) == 0) {
						Debug.LogFormat ("Cannot load GeneratablePrefab component on {0}", gObj);
						continue;
					}
					SavePrefabInformation (prefab [0], f, false, false);
				}
			}
			loadedAssetBundle.Unload (false);
		}

                // Test code for Loadfromcacheordownload


                string list_aws_filename = "Assets/PrefabDatabase/list_aws.txt";
                StreamReader theReader = new StreamReader(list_aws_filename, Encoding.Default);
                string line;
                using (theReader)
                {
                    // While there's lines left in the text file, do this:
                    do
                    {
                        line = theReader.ReadLine();
                            
                        if (line != null)
                        {
                            Debug.Log (line);

                            var www = WWW.LoadFromCacheOrDownload (line, 0);

                            if (!String.IsNullOrEmpty(www.error))
                            {
                                    Debug.Log (www.error);
                                    return;
                            } else {
                                var loadedAssetBundle = www.assetBundle;

                                string[] assetList = loadedAssetBundle.GetAllAssetNames ();
                                foreach (string asset in assetList) { 

                                        GameObject gObj = loadedAssetBundle.LoadAsset<GameObject> (asset);
                                        GeneratablePrefab[] prefab = gObj.GetComponents<GeneratablePrefab> ();
                                        if (prefab.GetLength (0) == 0) {
                                                Debug.LogFormat ("Cannot load GeneratablePrefab component on {0}", gObj);
                                                continue;
                                        }
                                        SavePrefabInformation (prefab [0], line, false, false);
                                }
                                loadedAssetBundle.Unload (false);
                            }

                        }
                    }
                    while (line != null);
                    // Done reading, close the reader and return true to broadcast success    
                    theReader.Close();
                }
                Debug.Log (prefabs.Count);
		EditorUtility.SetDirty (this);
	}

	void Update ()
	{
		EditorUtility.UnloadUnusedAssetsImmediate ();
		GC.Collect ();
	}


	public static Dictionary<GameObject, string> ListSelectedPrefabs ()
	{
		/*
		 * This function walks through all the objects selected in the editor and returns 
		 * the list of selected prefabs.
		 */
		Dictionary<GameObject, string> allSelected = new Dictionary<GameObject, string> ();

		foreach (UnityEngine.Object obj in Selection.objects) {
			if (obj != null) {
				if (obj is GameObject) {
					allSelected.Add (obj as GameObject, AssetDatabase.GetAssetPath (obj));
				} else if (obj is DefaultAsset) {
					HashSet<GameObject> children = (obj as DefaultAsset).GetAllChildrenAssets<GameObject> ();
					foreach (GameObject child in children) {
						allSelected.Add (child, AssetDatabase.GetAssetPath (child));
					}
				}
			}
		}

		if (allSelected == null || allSelected.Count == 0)
			return null;

		List<GameObject> toRemove = new List<GameObject> ();
		foreach (KeyValuePair<GameObject, string> entry in allSelected) {
			//			Debug.Log (PrefabUtility.GetPrefabType (entry.Key).ToString());
			if (entry.Key == null || PrefabUtility.GetPrefabType (entry.Key) != PrefabType.Prefab) {
				toRemove.Add (entry.Key);
			}
		}

		foreach (GameObject entry in toRemove) {
			allSelected.Remove (entry);
		}
		return allSelected;
	}
		
	#endif
}
