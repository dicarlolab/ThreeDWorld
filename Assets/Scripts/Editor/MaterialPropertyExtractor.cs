using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text.RegularExpressions;

/* 
 * This class extracts the smoothness and specular color out of the material file 
 * and the Ns and Ks value out of the corresponding material in the .mtl file 
 * from which it was imported and stores them in a .txt file.
 */
public class MaterialPropertyExtractor {
	[MenuItem("Assets/Extract Material Properties")]
	static void ExtractMaterialProperties() {
		// Open output file
		string output_path = Path.Combine(Application.dataPath, "matparam.txt");
		StreamWriter output = new StreamWriter(output_path);

		// Find all materials with label "ExMat"
		string[] materials = AssetDatabase.FindAssets ("l:ExMat t:Material");

		// Extract specular color and smoothness for out of each material
		foreach (string material in materials) {
			
			string path = AssetDatabase.GUIDToAssetPath(material);

			Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
			if(!mat) {
				Debug.LogError("Material not found: " + path);
				return;
			}

			// Name of the imported .mtl file
			string mtlfile = mat.name.Split(new string[] {"_MAT_"}, StringSplitOptions.None)[0];
			mtlfile = path.Replace("Materials/" + mat.name + ".mat", "") + mtlfile + ".mtl";

			// Name of the imported material
			string name = mat.name.Split(new string[] {"_MAT_"}, StringSplitOptions.None)[1];

			// Albedo Color
			float[] Color = new float[4];
			Color[0] = mat.GetColor("_Color").r;
			Color[1] = mat.GetColor("_Color").g;
			Color[2] = mat.GetColor("_Color").b;
			Color[3] = mat.GetColor("_Color").a;

			// Specular Color
			mat.shader = Shader.Find("Standard (Specular setup)");
			float[] specColor = new float[4];
			specColor[0] = mat.GetColor("_SpecColor").r;
			specColor[1] = mat.GetColor("_SpecColor").g;
			specColor[2] = mat.GetColor("_SpecColor").b;
			specColor[3] = mat.GetColor("_SpecColor").a;
			mat.shader = Shader.Find("Standard");

			// Glossiness / Smoothness Value
			float glossiness = mat.GetFloat("_Glossiness");		

			// Metallicness
			float metallicness = mat.GetFloat("_Metallic");	


			// Look up Ka, Kd, Ks, map_Kd, map_Ks, map_bump, d, and Ns in the corresponding .mtl file 
			StreamReader reader = new StreamReader(mtlfile);
			string contents = reader.ReadToEnd();
			Match m = Regex.Match(contents, 
				"newmtl\\s+(?<matname>\\S+).*?((?=\\s*newmtl)|\\s*\\z)", 
				RegexOptions.Multiline | RegexOptions.ExplicitCapture | 
				RegexOptions.Singleline);

			// Find the corresponding material
			while(m.Success && m.Groups["matname"].Value != name)
			{
				m = m.NextMatch();
			}
			string mtlContents = m.Value;

			// Determine Ns
			float Ns = 0.0f;
			ParseMtlScalar("Ns", ref Ns, mtlContents);

			// Determine d
			float d = 0.0f;
			ParseMtlScalar("d", ref d, mtlContents);

			// Look up map_Kd
			bool hasMapKd = LookUpMtlPrefix("map_Kd", mtlContents);

			// Look up map_Ks
			bool hasMapKs = LookUpMtlPrefix("map_Ks", mtlContents);

			// Look up map_bump
			bool hasMapBump = LookUpMtlPrefix("map_bump", mtlContents) || LookUpMtlPrefix("bump", mtlContents);

			// Determine Ka
			float[] Ka = new float[3];
			ParseMtlVector3D("Ka", ref Ka, mtlContents);

			// Determine Ks
			float[] Kd = new float[3];
			ParseMtlVector3D("Kd", ref Kd, mtlContents);

			// Determine Ks
			float[] Ks = new float[3];
			ParseMtlVector3D("Ks", ref Ks, mtlContents);


			// Write Ks, Ns, specular Color and smoothness into file
			output.WriteLine(
				string.Format("{0}, {1}, {2}, {3}, {4}, {5}, " +
							  "{6}, {7}, {8}, {9}, {10}, {11}, " +
							  "{12}, {13}, {14}, {15}, {16}, {17}, " +
							  "{18}, {19}, {20}, {21}, {22}, {23}" ,
					Ka[0], Ka[1], Ka[2],
					Kd[0], Kd[1], Kd[2],
					Ks[0], Ks[1], Ks[2],
					Ns, d, 
					hasMapBump ? 1 : 0, 
					hasMapKd ? 1 : 0, 
					hasMapKs ? 1 : 0,
					Color[0], Color[1], Color[2], Color[3],
					specColor[0], specColor[1], specColor[2], specColor[3],
					glossiness, metallicness));
		}
		output.Close();
		Debug.Log("Output written to: " + output_path);
	}

	private static bool ParseMtlScalar(string mtlKey, ref float val, string mtlContents)
    {
    	val = -1.0f;
        Match m = Regex.Match(mtlContents, string.Format("{0}\\s+(?<val>\\S+)\\b", mtlKey));
		string valString = m.Success ? m.Groups["val"].Value : null;

		if(valString != null) 
		{
			float.TryParse(valString, out val);
			return true; 
		}
		else
		{
			Debug.LogError("No " + mtlKey + " parameter found!");
			return false;
		}
    }

	private static bool ParseMtlVector3D(string mtlKey, ref float[] val, string mtlContents)
    {
		val[0] = -1.0f;
		val[1] = -1.0f;
		val[2] = -1.0f;

		Match m = Regex.Match(mtlContents, string.Format(
				"{0}\\s+(?<r>\\d*\\.?\\d+)\\s+(?<g>\\d*\\.?\\d+)\\s+(?<b>\\d*\\.?\\d+)\\b", 
				mtlKey));
        if (!m.Success) 
        {
			Debug.LogError("No " + mtlKey + " parameter found!");
            return false;
        }
		val[0] = float.Parse(m.Groups["r"].Value);
		val[1] = float.Parse(m.Groups["g"].Value);
		val[2] = float.Parse(m.Groups["b"].Value);
		return true;
    }

    private static bool LookUpMtlPrefix(string mtlKey, string mtlContents)
    {
		Match m = Regex.Match(mtlContents, string.Format("{0}\\s+(?<val>\\S+)\\b", mtlKey));
		return m.Success;
	}
}