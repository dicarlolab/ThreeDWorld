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

			// Specular Color
			float[] specColor = new float[4];
			specColor[0] = mat.GetColor("_SpecColor").r;
			specColor[1] = mat.GetColor("_SpecColor").g;
			specColor[2] = mat.GetColor("_SpecColor").b;
			specColor[3] = mat.GetColor("_SpecColor").a;
			// Glossiness / Smoothness Value
			float glossiness = mat.GetFloat("_Glossiness");			
			
			// Look up Ns and Ks in the corresponding .mtl file 
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
			string NsString = LookUpMtlPrefix("Ns", mtlContents);
			if(NsString != null)
				float.TryParse(NsString, out Ns);
			else
				Debug.LogError("No Ns parameter found!");

			// Determine Ks
			float[] Ks = new float[3];
			Match m_Ks = Regex.Match(mtlContents, string.Format(
				"{0}\\s+(?<r>\\d*\\.?\\d+)\\s+(?<g>\\d*\\.?\\d+)\\s+(?<b>\\d*\\.?\\d+)\\b", 
				"Ks"));
        	if (!m_Ks.Success) {
				Debug.LogError("No Ks parameter found!");
            	return;
            }
			Ks[0] = float.Parse(m_Ks.Groups["r"].Value);
			Ks[1] = float.Parse(m_Ks.Groups["g"].Value);
			Ks[2] = float.Parse(m_Ks.Groups["b"].Value);

			// Write Ks, Ns, specular Color and smoothness into file
			output.WriteLine(
				string.Format("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}",
					Ks[0], Ks[1], Ks[2],
					Ns,
					specColor[0], specColor[1], specColor[2],
					glossiness));
		}
		output.Close();
		Debug.Log("Output written to: " + output_path);
	}

	private static string LookUpMtlPrefix(string mtlKey, string mtlContents)
    {
        Match m = Regex.Match(mtlContents, string.Format("{0}\\s+(?<val>\\S+)\\b", mtlKey));
        return m.Success ? m.Groups["val"].Value : null;
    }
}