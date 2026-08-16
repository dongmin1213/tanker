using UnityEditor;
using UnityEngine;

namespace Tanker
{
    /// Assets/Resources/Art 이하 텍스처에 픽셀아트 임포트 설정을 강제한다 (docs/art-guide.md).
    class TankerArtImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("Assets/Resources/Art/")) return;
            var imp = (TextureImporter)assetImporter;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single; // 프로젝트 기본이 Multiple이라 서브에셋이 안 생김 — 명시 필수
            imp.spritePixelsPerUnit = 128;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
        }
    }
}
