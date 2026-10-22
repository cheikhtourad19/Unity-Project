using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HeritageXR.Editor
{
    // Apply detail quality to the existing assets during the next build without
    // regenerating the museum scene or changing its manually configured objects.
    public sealed class ViewerBuildPreparation : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var texture = AssetImporter.GetAtPath("Assets/HeritageXR/Models/bm_ea24/fiveHundred.jpg") as TextureImporter;
            if (texture != null && texture.maxTextureSize != 4096)
            {
                texture.maxTextureSize = 4096;
                texture.SaveAndReimport();
            }
            var preview = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/HeritageXR/Settings/ModelPreview.renderTexture");
            if (preview != null && (preview.width != 1536 || preview.height != 1536))
            {
                preview.Release();
                preview.width = 1536;
                preview.height = 1536;
                EditorUtility.SetDirty(preview);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
