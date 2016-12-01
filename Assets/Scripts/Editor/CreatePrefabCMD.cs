using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.IO;
using System.Text;
using UnityEditor;

public class CreatePrefabCMD
{
        private static string GetArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name && args.Length > i + 1)
                {
                    return args[i + 1];
                }
            }
            return null;
        }



    public static void LoadInfoFromBundle(){
        using (StreamWriter sw = new StreamWriter(GetArg ("-outputFile"))) 
        {
            /*
            // Add some text to the file.
            sw.Write("This is the ");
            sw.WriteLine("header for the file.");
            sw.WriteLine("-------------------");
            // Arbitrary objects can also be written to the file.
            sw.Write("The date is: ");
            sw.WriteLine(DateTime.Now);
            */

            //sw.Write("This is the Test file");
            string tmp_asset_path = GetArg ("-inputDir");
            string[] dir = Directory.GetFiles(tmp_asset_path, "*.bundle");
            foreach (string d_tmp in dir) {
                //sw.Write("This is line for ");
                //sw.WriteLine(d_tmp);
                string data_new_path = d_tmp;
                sw.Write(d_tmp.Remove(0, d_tmp.LastIndexOf ('/')+1));
                sw.Write(",");
                Debug.Log(data_new_path);

                //allSelected.Add (AssetDatabase.LoadMainAssetAtPath(data_new_path) as GameObject, data_new_path);
                AssetBundle loadedAssetBundle = AssetBundle.LoadFromFile (data_new_path);
                if (loadedAssetBundle == null) {
                    Debug.Log ("Failed to load AssetBundle!");
                    continue;
                }
                GameObject gObj = loadedAssetBundle.LoadAsset<GameObject> (loadedAssetBundle.GetAllAssetNames () [0]);
                GeneratablePrefab[] prefab = gObj.GetComponents<GeneratablePrefab> ();
                loadedAssetBundle.Unload (false);
                sw.Write(prefab[0].myComplexity);
                sw.Write(",");
                sw.Write(prefab[0].myBounds);
                sw.Write(",");
                sw.Write(prefab[0].isLight);
                sw.Write(",");
                sw.Write(prefab[0].attachMethod);
                sw.Write("\n");
            }

        }
    }

    public static void CreatePrefabFromModel_script (){
        Dictionary<GameObject, string> allSelected = new Dictionary<GameObject, string> ();
        Dictionary<string, string> path_to_id = new Dictionary<string, string> ();
        // argument -inputFile is for the txt file with each line of "obj-path,_id"
        string tmp_doc_path = GetArg ("-inputFile");;
        StreamReader theReader = new StreamReader (tmp_doc_path, Encoding.Default);
        string line;

        // Get the obj path and add it to the dictionary for further processing
        using (theReader) {
            do {
                line = theReader.ReadLine ();
                if (line != null){
                    Debug.Log(line);
                    string now_obj_path     = line.Substring(0, line.LastIndexOf(','));
                    string now_id           = line.Substring(line.LastIndexOf(',')+1, line.Length -(line.LastIndexOf(',')+1));
                    //Debug.Log(now_obj_path);
                    //Debug.Log(now_id);
                    
                    if (now_obj_path.ToLowerInvariant ().EndsWith (".obj")) {
                        AssetDatabase.ImportAsset (now_obj_path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
                    }

                    allSelected.Add (AssetDatabase.LoadMainAssetAtPath(now_obj_path) as GameObject, now_obj_path);
                    path_to_id.Add (now_obj_path, now_id);
                }
            } while (line != null);
            theReader.Close ();
        }

        // Generate prefabs from the dictionary
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

        string saving_directory     = Application.dataPath + "/PrefabDatabase/GeneratedPrefabs/objs_by_id/";
        System.IO.Directory.CreateDirectory(saving_directory);

        foreach (KeyValuePair<GameObject, string> entry in allSelected) {
            //make meta file
            EditorApplication.Step ();

            //make prefab
            MakeSimplePrefabObj (entry.Key, "objs_by_id/" + path_to_id[entry.Value]);

            //force GC
            EditorApplication.SaveAssets ();
            Resources.UnloadUnusedAssets ();
            EditorUtility.UnloadUnusedAssetsImmediate ();

            GC.Collect ();
            EditorApplication.Step ();

        }

        // Get the available prefabs and generate assetbundles from them
        allSelected = new Dictionary<GameObject, string> ();
        foreach (KeyValuePair<string, string> entry in path_to_id){
            string data_new_path    = "Assets/PrefabDatabase/GeneratedPrefabs/objs_by_id/" + entry.Value + ".prefab";
            allSelected.Add (AssetDatabase.LoadMainAssetAtPath(data_new_path) as GameObject, data_new_path);
        }
        AssetBundleBuild[] buildMap = new AssetBundleBuild[allSelected.Count];

        int loop_counter = 0;
        foreach (KeyValuePair<GameObject, string> entry in allSelected) {
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

        string bundle_prefix    = "Assets/PrefabDatabase/AssetBundles/Separated";
        BuildPipeline.BuildAssetBundles (bundle_prefix, 
            buildMap, 
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneOSXUniversal
        );

        // Write the information into the text file indicated by -outputFile
        string[] all_bundle_path    = new string[allSelected.Count];
        int now_indx                = 0;
        foreach (KeyValuePair<GameObject, string> entry in allSelected) {
            string _bundleName;
            _bundleName = entry.Key.name + ".bundle";
            _bundleName = bundle_prefix + "/" + _bundleName;
            all_bundle_path[now_indx]   = _bundleName;
            now_indx++;
        }

        using (StreamWriter sw = new StreamWriter(GetArg ("-outputFile"))) 
        {
            foreach (string d_tmp in all_bundle_path) {
                //sw.Write("This is line for ");
                //sw.WriteLine(d_tmp);
                string data_new_path = d_tmp;
                sw.Write(d_tmp.Remove(0, d_tmp.LastIndexOf ('/')+1));
                sw.Write(",");
                Debug.Log(data_new_path);

                //allSelected.Add (AssetDatabase.LoadMainAssetAtPath(data_new_path) as GameObject, data_new_path);
                AssetBundle loadedAssetBundle = AssetBundle.LoadFromFile (data_new_path);
                if (loadedAssetBundle == null) {
                    Debug.Log ("Failed to load AssetBundle!");
                    continue;
                }
                GameObject gObj = loadedAssetBundle.LoadAsset<GameObject> (loadedAssetBundle.GetAllAssetNames () [0]);
                GeneratablePrefab[] prefab = gObj.GetComponents<GeneratablePrefab> ();
                loadedAssetBundle.Unload (false);
                sw.Write(prefab[0].myComplexity);
                sw.Write(",");
                sw.Write(prefab[0].myBounds);
                sw.Write(",");
                sw.Write(prefab[0].isLight);
                sw.Write(",");
                sw.Write(prefab[0].attachMethod);
                sw.Write("\n");
            }

        }
    }

	public static void CreatePrefabFromModel ()
	{
		Dictionary<GameObject, string> allSelected = new Dictionary<GameObject, string> ();

                /*
		foreach (GameObject obj in Selection.gameObjects) {

			allSelected.Add (obj, AssetDatabase.GetAssetPath (obj));

                        tmp_asset_path   = AssetDatabase.GetAssetPath (obj);
                        Debug.Log(tmp_asset_path);
		}

		foreach (UnityEngine.Object obj in Selection.objects) {
			if (obj != null) {
				if (obj is GameObject) {
					allSelected.Add (obj as GameObject, AssetDatabase.GetAssetPath (obj));

                                        tmp_asset_path   = AssetDatabase.GetAssetPath (obj);
                                        Debug.Log(tmp_asset_path);

				} else if (obj is DefaultAsset) {
					HashSet<GameObject> children = (obj as DefaultAsset).GetAllChildrenAssets<GameObject> ();
					foreach (GameObject child in children) {
						allSelected.Add (child, AssetDatabase.GetAssetPath (child));

                                                tmp_asset_path   = AssetDatabase.GetAssetPath (child);
                                                Debug.Log(tmp_asset_path);
                                                //Debug.Log(AssetDatabase.LoadMainAssetAtPath(tmp_asset_path)==child);
					}
				}
			}
		}
                */

                //string tmp_asset_path = "Assets/Models/sel_objs/02747177/8acbba54310b76cb6c43a5634bf2724/8acbba54310b76cb6c43a5634bf2724.obj";
                //string tmp_asset_path = "Assets/Models/sel_objs/test_mine/";
                string tmp_asset_path = GetArg ("-outputDir");;
                string[] dir = Directory.GetDirectories(tmp_asset_path);
                foreach (string d_tmp in dir) {
                    //Debug.Log(d_tmp);
                    string data_new_path = d_tmp + d_tmp.Remove(0, d_tmp.LastIndexOf ('/')) + ".obj";
                    Debug.Log(data_new_path);

                    allSelected.Add (AssetDatabase.LoadMainAssetAtPath(data_new_path) as GameObject, data_new_path);
                }
                //allSelected.Add (AssetDatabase.LoadMainAssetAtPath(tmp_asset_path) as GameObject, tmp_asset_path);

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
                                //Debug.Log(subPathWithFileType);
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
                
                allSelected = new Dictionary<GameObject, string> ();

                tmp_asset_path = GetArg ("-outputDir");
                tmp_asset_path = tmp_asset_path.Replace("/Models/", "/PrefabDatabase/GeneratedPrefabs/");
                dir = Directory.GetDirectories(tmp_asset_path);
                foreach (string d_tmp in dir) {
                    //Debug.Log(d_tmp);
                    string data_new_path = d_tmp + d_tmp.Remove(0, d_tmp.LastIndexOf ('/')) + ".prefab";
                    Debug.Log(data_new_path);
                    allSelected.Add (AssetDatabase.LoadMainAssetAtPath(data_new_path) as GameObject, data_new_path);
                }

		AssetBundleBuild[] buildMap = new AssetBundleBuild[allSelected.Count];

		int loop_counter = 0;
		foreach (KeyValuePair<GameObject, string> entry in allSelected) {
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
		
		// Create colliders for the prefab
		ConcaveCollider.FH_CreateColliders (prefab, vrmlText, true);

		// Save out updated metadata settings
		GeneratablePrefab metaData = prefab.GetComponent<GeneratablePrefab> ();
		metaData.ProcessPrefab ();

		EditorUtility.SetDirty (prefab);
		try {
			EditorUtility.SetDirty (instance);
		} catch {
			Debug.LogFormat("Instance could not be collected: {0}", instance);
		}
		GameObject.Destroy (instance);
	}
}

