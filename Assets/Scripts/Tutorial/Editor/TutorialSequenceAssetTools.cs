using UnityEditor;
using UnityEngine;

namespace Riftborn.Tutorial.EditorTools
{
    // One-click creation of a fully-populated tutorial sequence asset from the
    // built-in code sequence (TutorialSequence.Build), written to
    // Assets/Resources so the director auto-loads it with no wiring. Safe to
    // re-run: it never overwrites an existing asset — it makes a uniquely-named
    // one and selects it.
    internal static class TutorialSequenceAssetTools
    {
        private const string ResourcesDir = "Assets/Resources";
        private const string AssetPath = ResourcesDir + "/TutorialSequence.asset";

        [MenuItem("Riftborn/Tutorial/Create Sequence Asset From Code")]
        private static void CreateFromCode()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesDir))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var asset = ScriptableObject.CreateInstance<TutorialSequenceAsset>();
            asset.Steps.AddRange(TutorialSequence.Build());

            string path = AssetDatabase.GenerateUniqueAssetPath(AssetPath);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[Tutorial] Created sequence asset with {asset.Steps.Count} steps at {path}. " +
                      "It lives in Resources, so the TutorialDirector loads it automatically — " +
                      "or drag it onto the director's Sequence Asset field.");
        }
    }
}
