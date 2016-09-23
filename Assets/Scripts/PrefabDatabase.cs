using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

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
	}

	#region Fields
	public List<PrefabInfo> prefabs = new List<PrefabInfo>();
	private Dictionary<PrefabInfo, float> sceneScale = new Dictionary<PrefabInfo, float> ();
	private static PrefabDatabase _Instance = null;
	#endregion

	#region Properties
	public static PrefabDatabase Instance
	{
		get { return _Instance; }
	}
	#endregion

	#region Unity Callbacks
	private void Awake()
	{
		_Instance = this;
	}

	void Start()
	{
		Init ();
	}
	#endregion

	private void Init()
	{
		foreach (PrefabInfo prefab in prefabs)
		{
			sceneScale.Add (prefab, 1f);
		}
	}

	public float GetSceneScale(PrefabInfo prefab)
	{
		float value;
		if (sceneScale.TryGetValue (prefab, out value))
		{
			return value;
		} else 	{
			Debug.LogErrorFormat ("{0} not in database!", prefab.fileName);
			return 0f;
		}
	}

	public void SetPrefabScaleAll()
	{
		//TODO: Implement this
	}

	public void SetPrefabScaleSingle()
	{
		//TODO: Implement this
	}

	private float GetScaleFromAbsolute(GameObject obj, Vector3 absolute)
	{
		// TODO: Implement this
		return 1f;
	}

	public GameObject Load(string name)
	{
		// TODO: Implement this to load assetbundles
		return null;
	}

	#if UNITY_EDITOR
	// Setup a new prefab object from a model
	[MenuItem ("Prefab Database/Create Prefabs")]
	private static void CreatePrefabFromModel()
	{
		Dictionary<GameObject, string> allSelected = new Dictionary<GameObject, string>();

		foreach (GameObject obj in Selection.gameObjects) {
			allSelected.Add (obj, AssetDatabase.GetAssetPath (obj));
		}

		foreach(UnityEngine.Object obj in Selection.objects)
		{
			if (obj != null)
			{
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

		List<GameObject> toRemove = new List<GameObject>();
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
				System.IO.Directory.CreateDirectory(Application.dataPath + "/Resources/Prefabs/Converted Models/" + subPathWithoutFilename);

				Debug.Log (Application.dataPath + "/Resources/Prefabs/Converted Models/" + subPathWithoutFilename);

				//make meta file
				EditorApplication.Step ();

				//make prefab
				MakeSimplePrefabObj (entry.Key, subPathWithoutFileType);

				//force GC
				EditorApplication.SaveAssets ();
				EditorUtility.UnloadUnusedAssetsImmediate ();
				GC.Collect ();
				EditorApplication.Step ();
			}
		}
		SetupPrefabsFull ();
	}

	private static void MakeSimplePrefabObj(GameObject obj, string subPath)
	{
		Debug.LogFormat("MakeSimplePrefabObj {0}", obj.name);
		// Find vrml text if we have it
		string vrmlText = null;
		string vrmlPath = AssetDatabase.GetAssetPath(obj);
		if (vrmlPath != null)
		{
			vrmlPath = vrmlPath.Replace(".obj", ".wrl");
			string fullPath = System.IO.Path.Combine(Application.dataPath, vrmlPath.Substring(7));
			if (!System.IO.File.Exists(fullPath))
				fullPath = fullPath.Replace(".wrl", "_vhacd.wrl");
			if (System.IO.File.Exists(fullPath))
			{
				using (System.IO.StreamReader reader = new System.IO.StreamReader(fullPath))
				{
					vrmlText = reader.ReadToEnd();
				}
			}
			else
				Debug.LogFormat("Cannot find {0}\n{1}", vrmlPath, fullPath);
		}

		GameObject instance = GameObject.Instantiate(obj) as GameObject;
		instance.name = obj.name;

		// Remove any old colliders.
		Collider[] foundColliders = instance.transform.GetComponentsInChildren<Collider>();
		foreach(Collider col in foundColliders)
			UnityEngine.Object.DestroyImmediate(col, true);

		// Create SemanticObject/Rigidbody
		instance.AddComponent<SemanticObjectSimple>().name = instance.name;

		// Add generatable prefab tags
		instance.AddComponent<GeneratablePrefab>();

		//NormalSolver.RecalculateNormals (instance.GetComponentInChildren<MeshFilter> ().sharedMesh, 60);

		// Save as a prefab
		string prefabAssetPath = string.Format("Assets/Resources/Prefabs/Converted Models/{0}.prefab", subPath);
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
		if (prefab == null)
			prefab = PrefabUtility.CreatePrefab(prefabAssetPath, instance);
		else
			prefab = PrefabUtility.ReplacePrefab(instance, prefab);
		GameObject.DestroyImmediate(instance);


		// Create colliders for the prefab
		ConcaveCollider.FH_CreateColliders(prefab, vrmlText, true);

		// Save out updated metadata settings
		GeneratablePrefab metaData = prefab.GetComponent<GeneratablePrefab>();
		metaData.ProcessPrefab();
		SetupPrefabs(false);

		EditorUtility.SetDirty (prefab);
		try {
			EditorUtility.SetDirty (instance);
		} catch {
			//do nothing
		}
	}

	// Finds all prefabs that we can use and create a lookup table with relevant information
	[MenuItem ("Prefab Database/Setup Prefabs Quick")]
	private static void SetupPrefabsQuick()
	{
		SetupPrefabs(false);
	}

	[MenuItem ("Prefab Database/Setup Prefabs Full")]
	private static void SetupPrefabsFull()
	{
		SetupPrefabs(true);
	}

	public static void SetupPrefabs(bool shouldRecompute)
	{
		PrefabDatabase database =  AssetDatabase.LoadAssetAtPath<PrefabDatabase> ("Assets/ScenePrefabs/PrefabDatabase.prefab");
		if (database != null)
			database.CompileListOfProceduralComponents(shouldRecompute);
	}

	public void SavePrefabInformation(GeneratablePrefab prefab, bool shouldRecomputePrefabInformation, bool replaceOld = true)
	{
		const string resPrefix = "PrefabDatabase/";
		string assetPath = AssetDatabase.GetAssetPath(prefab);
		if (string.IsNullOrEmpty(assetPath))
			return;
		string newFileName = assetPath.Substring(assetPath.LastIndexOf(resPrefix) + resPrefix.Length);
		newFileName = newFileName.Substring(0, newFileName.LastIndexOf("."));
		int replaceIndex = -1;
		if (replaceOld)
		{
			replaceIndex = prefabs.FindIndex( (PrefabInfo testInfo)=>{
				return testInfo.fileName == newFileName;
			});
			if (replaceIndex >= 0)
				prefabs.RemoveAt(replaceIndex);
		}

		if (prefab.shouldUse)
		{
			if (shouldRecomputePrefabInformation)
				prefab.ProcessPrefab();
			PrefabInfo newInfo = new PrefabInfo();
			newInfo.fileName = newFileName;
			newInfo.complexity = prefab.myComplexity;
			newInfo.bounds = prefab.myBounds;
			newInfo.isLight = prefab.isLight;
			newInfo.anchorType = prefab.attachMethod;
			foreach(GeneratablePrefab.StackableInfo stackRegion in prefab.stackableAreas)
				newInfo.stackableAreas.Add(stackRegion);
			if (replaceIndex < 0)
				prefabs.Add(newInfo);
			else
				prefabs.Insert(replaceIndex, newInfo);
		}        
		EditorUtility.SetDirty(this);
	}

	// Save out core information so we can decide whether to place the objects dynamically even if they aren't loaded yet
	private void CompileListOfProceduralComponents(bool shouldRecomputePrefabInformation)
	{
		GeneratablePrefab [] allThings = Resources.LoadAll<GeneratablePrefab>("");
		prefabs.Clear();
		foreach(GeneratablePrefab prefab in allThings)
			SavePrefabInformation(prefab, shouldRecomputePrefabInformation);
		EditorUtility.SetDirty(this);
	}
	#endif
}
