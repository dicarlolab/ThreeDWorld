using UnityEditor;

public class CreateAssetBundles
{
    [MenuItem ("Assets/Build SkyBox Bundle")]
    static void BuildAllAssetBundles ()
    {
        BuildPipeline.BuildAssetBundles ("Assets/Scenes/Lighting", BuildAssetBundleOptions.None, BuildTarget.StandaloneOSXUniversal);
    }
}