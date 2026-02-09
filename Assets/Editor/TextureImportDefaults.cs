using UnityEditor;

namespace TowerDefense.Editor
{
    public class TextureImportDefaults : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            var importer = (TextureImporter)assetImporter;
            if (importer.importSettingsMissing)
            {
                importer.textureType = TextureImporterType.Sprite;
            }
        }
    }
}
