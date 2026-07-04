using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Assets/Resources/scripts 의 모든 .vns 스크립트에서 사용된 글자를 추출해
// Assets/Fonts/glyphs.txt 로 저장한다. 그 파일을 TMP Font Asset Creator 의
// "Characters from File" 에 넣어 한 번에 굽는다. (에디터 전용 도구, 런타임 영향 없음)
//
// 원본 텍스트를 그대로 훑으므로 DSL 키워드/문장부호 같은 ASCII 글자도 함께 포함된다
// (폰트 아틀라스 상 무해한 상위집합). 대사 텍스트만 골라내려면 엔진 파서가 필요하다.
public static class GlyphExtractor
{
    private const string ScriptsFolder = "Assets/Resources/scripts";
    private const string OutputAssetPath = "Assets/Fonts/glyphs.txt";

    [MenuItem("Tools/VN/Extract Glyphs from .vns scripts")]
    public static void Extract()
    {
        string scriptsAbsolute = Path.Combine(Application.dataPath, "..", ScriptsFolder);
        if (!Directory.Exists(scriptsAbsolute))
        {
            Debug.LogError($"[GlyphExtractor] 스크립트 폴더를 찾을 수 없다: {ScriptsFolder}");
            return;
        }

        string[] files = Directory.GetFiles(scriptsAbsolute, "*.vns", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            Debug.LogError($"[GlyphExtractor] {ScriptsFolder} 에서 .vns 파일을 찾지 못했다.");
            return;
        }

        var unique = new HashSet<char>();
        foreach (var file in files)
            CollectChars(File.ReadAllText(file), unique);

        // 보너스로 시스템 메시지에서 쓰는 글자도 포함 ("끝", 줄표, 대괄호 등)
        CollectChars("— 끝 —[script load failed]", unique);

        var sorted = new List<char>(unique);
        sorted.Sort();

        var sb = new StringBuilder(sorted.Count);
        foreach (var c in sorted)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c)) continue;
            sb.Append(c);
        }

        string outAbsolute = Path.Combine(Application.dataPath, "..", OutputAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outAbsolute));

        // BOM 없는 UTF-8 로 저장 — Font Asset Creator 가 BOM 있으면 첫 글자를 잘못 읽는 경우가 있다.
        File.WriteAllText(outAbsolute, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();

        Debug.Log($"[GlyphExtractor] {sb.Length} 개 글자를 추출해 {OutputAssetPath} 에 저장했다. (원본 {files.Length} 개 .vns)");

        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(OutputAssetPath);
        if (asset != null) EditorGUIUtility.PingObject(asset);
    }

    private static void CollectChars(string s, HashSet<char> sink)
    {
        if (string.IsNullOrEmpty(s)) return;
        for (int i = 0; i < s.Length; i++) sink.Add(s[i]);
    }
}
