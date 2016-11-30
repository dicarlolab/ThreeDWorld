using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

public class MaterialProcessor : AssetPostprocessor
{
	private static bool USE_REGRESSION = true;
	private static string PYTHONPATH = "/usr/local/opt/python/bin/python2.7";

	// path of the mtl file
	private string mtlPath;

    public void OnPreprocessModel()
    {
		mtlPath = assetPath.Replace(".obj", ".mtl");
        // Make sure that we only use local files, not files found elsewhere!
        ModelImporter importer = assetImporter as ModelImporter;
        if (importer != null && assetPath.ToLowerInvariant().EndsWith(".obj"))
            importer.materialSearch = ModelImporterMaterialSearch.Local;

		// Unity struggles with the import of mtl and obj files with spaces in their names
    	// Thus we need to replace all spaces in the file names 
    	// with _ before the actual import
    	if (assetPath.Contains(" ")) {
			// replace spaces in the file name with _
			string newAssetPath = Path.Combine(Path.GetDirectoryName(assetPath),
						Path.GetFileName(assetPath).Replace(" ", "_"));
			string newMtlPath = Path.Combine(Path.GetDirectoryName(mtlPath),
						Path.GetFileName(mtlPath).Replace(" ", "_"));

			// inside the obj file the mtl name also has to be changed 
			// to the new mtl name 
			string fullAssetPath = Path.Combine(Application.dataPath, assetPath.Substring(7));
			string [] lines = File.ReadAllLines(fullAssetPath);
        	string mtlLib = "mtllib";
        	for(int i = 0; i < lines.Length; i++)
        	{
        		if(lines[i].Contains(mtlLib))
        		{
					int startOfName = lines[i].IndexOf(mtlLib, 0) + mtlLib.Length + 1;
					string mtlName = lines[i].Substring(
						startOfName,
						lines[i].Length - startOfName);
					lines[i] = mtlLib + " " + mtlName.Replace(" ", "_");
				}
        	}
        	File.WriteAllLines(fullAssetPath, lines); 

			AssetDatabase.RenameAsset(mtlPath, Path.GetFileNameWithoutExtension(newMtlPath));

			this.assetPath = newAssetPath;
			this.mtlPath = newMtlPath;
    	}
    }

	static void OnPostprocessAllAssets (string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) 
    {
    	for(int i = 0; i < importedAssets.Length; i++)
    	{
			string filename = Path.GetFileNameWithoutExtension(importedAssets[i]);
			string extension = Path.GetExtension(importedAssets[i]);
			if(filename.Contains(" ") && extension.Contains("obj"))
    		{
    			// Renaming the obj files has to be done later / at this stage 
    			// as it is not possible to rename a file that is currently imported
    			AssetDatabase.RenameAsset(importedAssets[i], filename.Replace(" ", "_"));
    		}
    	}
    }

    public void OnPostprocessModel(GameObject obj)
    {
        if (!assetPath.ToLowerInvariant().EndsWith(".obj"))
            return;	

        HashSet<Material> mats = new HashSet<Material>();
        MeshRenderer[] mshRnds = obj.GetComponentsInChildren<MeshRenderer>();
        foreach(MeshRenderer rnd in mshRnds)
        {
        	foreach(Material mat in rnd.sharedMaterials)
        	{
        		mats.Add(mat);
        	}
        }

        Dictionary<Material, Material> toReplace = new Dictionary<Material, Material>();
        foreach(Material mat in mats)
        {
            // Rename if necessary
			if (!mat.name.StartsWith(obj.name.Replace(" ", "_") + "_MAT_"))
            {
				string newName = obj.name.Replace(" ", "_") + "_MAT_" + mat.name;
                // Rename asset, and switch to old asset if it already exists.
                Material prevAsset = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GetAssetPath(mat).Replace(mat.name, newName));
                if (prevAsset != null)
                    toReplace.Add(mat, prevAsset);
                else
                    AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(mat), newName);
            }
        }

        for(int r = 0; r < mshRnds.Length; r++)
        {
        	// Arrays returned in Unity are copies, and hence have to be stored,
        	// modified and reassigned as a whole later
        	Material[] sharedMaterials = mshRnds[r].sharedMaterials;
			for(int m = 0; m < sharedMaterials.Length; m++) {
				if (toReplace.ContainsKey(sharedMaterials[m]))
            	{
					sharedMaterials[m] = toReplace[mshRnds[r].sharedMaterials[m]];
                }
            }
            mshRnds[r].sharedMaterials = sharedMaterials;
        }

        foreach(Material mat in toReplace.Keys)
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(mat));
  
        ProcessMtlFile(mtlPath);
    }

    public void ProcessMtlFile(string fileLocation)
    {
        string matDirectory = fileLocation.Insert(fileLocation.LastIndexOf('/'), "/Materials").Replace(".mtl", "_MAT_");
        string fullPath = Path.Combine(Application.dataPath, fileLocation.Substring(7));
        System.IO.StreamReader reader = new System.IO.StreamReader(fullPath);
        string contents = reader.ReadToEnd();
        Match m = Regex.Match(contents, "newmtl\\s+(?<matname>\\S+).*?((?=\\s*newmtl)|\\s*\\z)", RegexOptions.Multiline | RegexOptions.ExplicitCapture | RegexOptions.Singleline);
        while (m.Success)
        {
            HandleMaterial(matDirectory + m.Groups["matname"].Value + ".mat", m.Value);
            m = m.NextMatch();
        }
    }

    public void HandleMaterial(string materialLocation, string mtlFileContents)
    {
        string texDirectory = materialLocation.Remove(materialLocation.LastIndexOf("Materials/"));
        string fullPath = Path.Combine(Application.dataPath, materialLocation.Substring(7));
        Material m = AssetDatabase.LoadAssetAtPath<Material>(materialLocation);

        if(m == null)
        	return;

        string testString = null;
        float testVal = 0.0f;
        int testInt = 0;
        // First replace the shader to the specular one, if necessary
        testString = LookUpMtlPrefix("illum", mtlFileContents);
        bool isSpecularSetup = (testString != null && int.TryParse(testString, out testInt) && testInt == 2);
		m.shader = Shader.Find(isSpecularSetup ? "Standard (Specular setup)" : "Standard");
        AssetDatabase.SaveAssets();

        // Set the mode to transparency if necessary
        testString = LookUpMtlPrefix("d", mtlFileContents);
        if (testString != null && float.TryParse(testString, out testVal) && testVal < 1f)
        {
            SetShaderTagToTransparent(fullPath);
            AssetDatabase.ImportAsset(materialLocation);
            m = AssetDatabase.LoadAssetAtPath<Material>(materialLocation);
        }

        // Set all the texture maps and colors as needed
        TrySetTexMap("map_Kd", "_MainTex", m, texDirectory, mtlFileContents);
        TrySetTexMap("map_Ks", "_SpecGlossMap", m, texDirectory, mtlFileContents);
        TrySetTexMap("map_bump", "_ParallaxMap", m, texDirectory, mtlFileContents);
        TrySetTexMap("bump", "_ParallaxMap", m, texDirectory, mtlFileContents);
        TrySetColor("Kd", "d", "_Color", m, mtlFileContents);

		if(USE_REGRESSION)
		{
			TryRegressProperties(m, mtlFileContents);
		}
		else 
		{
			TrySetColor("Ks", null, "_SpecColor", m, mtlFileContents);
			testString = LookUpMtlPrefix("Ns", mtlFileContents);
			if (testString != null && float.TryParse(testString, out testVal))
				m.SetFloat("_Glossiness", testVal * 0.001f);
        }

        AssetDatabase.SaveAssets();
    }

    private void TrySetTexMap(string mtlKey, string shaderKey, Material mat, string texDirectory, string mtlContents)
    {
        string texName = LookUpMtlPrefix(mtlKey, mtlContents);
        if (texName != null)
        {
            Texture asset = AssetDatabase.LoadAssetAtPath<Texture>(texDirectory + texName);
            if (asset != null)
                mat.SetTexture(shaderKey, asset);
        }
    }

    private void TrySetColor(string mtlKey, string mtlAlphaKey, string shaderKey, Material mat, string mtlContents)
    {
        Match m = Regex.Match(mtlContents, string.Format("{0}\\s+(?<r>\\d*\\.?\\d+)\\s+(?<g>\\d*\\.?\\d+)\\s+(?<b>\\d*\\.?\\d+)\\b", mtlKey));
        if (!m.Success)
            return;
        
        Color newColor = new Color(float.Parse(m.Groups["r"].Value), float.Parse(m.Groups["g"].Value), float.Parse(m.Groups["b"].Value), 1f);
        if (mtlAlphaKey != null)
        {
            string alphaString = LookUpMtlPrefix(mtlAlphaKey, mtlContents);
            if (alphaString != null)
                float.TryParse(alphaString, out newColor.a);
        }
        mat.SetColor(shaderKey, newColor);
    }

	private void TryRegressProperties(Material mat, string mtlContents)
	{
		float[] p = new float[14];
		MaterialPropertyExtractor.ExtractAllProperties(mtlContents, ref p);

		ProcessStartInfo start = new ProcessStartInfo();
		start.FileName = PYTHONPATH;
		string script =  "Scripts/Editor/MaterialProcessing/predict_material.py";
		string reglib = "Scripts/Editor/MaterialProcessing/matregression.pkl";
		start.Arguments = string.Format("{0} {1} {2} {3} {4} {5} " +
							  "{6} {7} {8} {9} {10} {11} {12} {13} {14} {15}",
							  Path.Combine(Application.dataPath, script),
							  Path.Combine(Application.dataPath, reglib),
							  p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7],
							  p[8], p[9], p[10], p[11], p[12], p[13]);
		start.UseShellExecute = false;
		start.RedirectStandardOutput = true;

		using(Process process = Process.Start(start))
     	{
        	using(StreamReader reader = process.StandardOutput)
        	{
        		Color specColor = new Color();
        		specColor.r = float.Parse(reader.ReadLine());
				specColor.g = float.Parse(reader.ReadLine());
				specColor.b = float.Parse(reader.ReadLine());
				specColor.a = float.Parse(reader.ReadLine());

				float glossiness = float.Parse(reader.ReadLine());
				float metallicness = float.Parse(reader.ReadLine());

				mat.SetColor("_SpecColor", specColor);
				mat.SetFloat("_Glossiness", glossiness);
				mat.SetFloat("_Metallic", metallicness);
			}
		}
	}

    private string LookUpMtlPrefix(string mtlKey, string mtlContents)
    {
        Match m = Regex.Match(mtlContents, string.Format("\\b{0}\\b\\s+(?<val>\\S+)\\b", mtlKey));
        return m.Success ? m.Groups["val"].Value : null;
    }

    private void SetShaderTagToTransparent(string fullMaterialPath)
    {
        System.IO.StreamReader reader = new System.IO.StreamReader(fullMaterialPath);
        string contents = reader.ReadToEnd();
        reader.Close();
        Regex.Match(contents, "stringTagMap: \\{\\}");
        contents = Regex.Replace(contents, "m_ShaderKeywords:(?!\\s*_ALPHAPREMULTIPLY_ON)", "m_ShaderKeywords: _ALPHAPREMULTIPLY_ON");
        contents = Regex.Replace(contents, "stringTagMap: \\{\\}", "stringTagMap:\n    RenderType: Transparent");
        contents = Regex.Replace(contents, "m_CustomRenderQueue:\\s*[-\\d]+\\w", "m_CustomRenderQueue: 3000");
        contents = Regex.Replace(contents, "name:\\s*_DstBlend\\s+second:\\s*0", "name: _DstBlend\n        second: 10");
        contents = Regex.Replace(contents, "name:\\s*_ZWrite\\s+second:\\s*1", "name: _ZWrite\n        second: 0");
        contents = Regex.Replace(contents, "name:\\s*_Mode\\s+second:\\s*0", "name: _Mode\n        second: 3");
        System.IO.StreamWriter writer = new System.IO.StreamWriter(fullMaterialPath, false);
        writer.Write(contents);
        writer.Close();
    }
}