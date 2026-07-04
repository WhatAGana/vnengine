using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// NotoSansKR-Regular SDF.asset 을 정상 설정으로 다시 굽고, 기존 .asset 의 GUID 를
// 유지한 채 내용만 교체한다. 씬의 텍스트 컴포넌트들이 참조를 잃지 않는다.
public static class FontAssetRebaker
{
    private const string TtfPath    = "Assets/Fonts/NotoSansKR-Regular.ttf";
    private const string AssetPath  = "Assets/Fonts/NotoSansKR-Regular SDF.asset";
    private const string GlyphsPath = "Assets/Fonts/glyphs.txt";

    private const int  SamplingPointSize = 90;
    private const int  AtlasPadding      = 9;
    private const int  AtlasWidth        = 2048;
    private const int  AtlasHeight       = 2048;

    [MenuItem("Tools/VN/Rebake NotoSansKR SDF (90pt, 2048, SDFAA, Dynamic)")]
    public static void Rebake()
    {
        var ttf          = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        var existing     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
        var glyphsAsset  = AssetDatabase.LoadAssetAtPath<TextAsset>(GlyphsPath);

        if (ttf == null)         { Debug.LogError($"[Rebake] ttf 가 없다: {TtfPath}"); return; }
        if (existing == null)    { Debug.LogError($"[Rebake] 기존 SDF asset 이 없다: {AssetPath}"); return; }
        if (glyphsAsset == null) { Debug.LogError($"[Rebake] glyphs.txt 가 없다: {GlyphsPath}. Tools/VN/Extract Glyphs 먼저 실행해라."); return; }

        // 1) 원하는 설정으로 새 폰트 에셋을 메모리에 만든다.
        var rebuilt = TMP_FontAsset.CreateFontAsset(
            font:                 ttf,
            samplingPointSize:    SamplingPointSize,
            atlasPadding:         AtlasPadding,
            renderMode:           GlyphRenderMode.SDFAA,
            atlasWidth:           AtlasWidth,
            atlasHeight:          AtlasHeight,
            atlasPopulationMode:  AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true
        );

        if (rebuilt == null)
        {
            Debug.LogError("[Rebake] CreateFontAsset 가 null 을 반환했다. ttf 가 손상됐을 수 있다.");
            return;
        }

        // 2) glyphs.txt 의 모든 글자를 미리 굽는다. Dynamic 이므로 누락은 런타임에 자동 추가.
        rebuilt.TryAddCharacters(glyphsAsset.text, out string missing);
        if (!string.IsNullOrEmpty(missing))
        {
            Debug.LogWarning($"[Rebake] ttf 에 없는 글자(폴백 폰트가 처리 시도): {missing}");
        }

        // 3) 기존 .asset 안의 옛 sub-asset(아틀라스 텍스처/머티리얼) 제거.
        var existingSubs = AssetDatabase.LoadAllAssetsAtPath(AssetPath);
        foreach (var sub in existingSubs)
        {
            if (sub == null || sub == existing) continue;
            if (sub is Texture2D || sub is Material)
            {
                AssetDatabase.RemoveObjectFromAsset(sub);
                Object.DestroyImmediate(sub, allowDestroyingAssets: true);
            }
        }

        // 4) 메모리에서 만든 데이터를 기존 에셋으로 통째로 복사 (GUID 보존 → 씬 참조 유지).
        EditorUtility.CopySerialized(rebuilt, existing);

        // 5) 새 아틀라스 텍스처와 머티리얼을 기존 에셋의 sub-asset 으로 붙인다.
        if (rebuilt.atlasTextures != null)
        {
            for (int i = 0; i < rebuilt.atlasTextures.Length; i++)
            {
                var tex = rebuilt.atlasTextures[i];
                if (tex == null) continue;
                tex.name = existing.name + (i == 0 ? " Atlas" : $" Atlas {i}");
                tex.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(tex, existing);
            }
        }
        if (rebuilt.material != null)
        {
            rebuilt.material.name = existing.name + " Material";
            rebuilt.material.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(rebuilt.material, existing);
        }

        // 6) 메모리 인스턴스 정리.
        Object.DestroyImmediate(rebuilt);

        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);

        Debug.Log(
            $"[Rebake] 완료. " +
            $"pointSize={existing.faceInfo.pointSize}, " +
            $"atlas={existing.atlasWidth}x{existing.atlasHeight}, " +
            $"renderMode=SDFAA, " +
            $"populationMode={existing.atlasPopulationMode}, " +
            $"glyphs={existing.glyphTable.Count}"
        );
    }
}
