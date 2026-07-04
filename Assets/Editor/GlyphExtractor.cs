using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// dialogues.json 에서 사용된 모든 글자를 추출해 Assets/Fonts/glyphs.txt 로 저장한다.
// 그 파일을 TMP Font Asset Creator 의 "Characters from File" 에 넣어 한 번에 굽는다.
public static class GlyphExtractor
{
    private const string JsonRelativePath = "dialogues.json";
    private const string OutputAssetPath = "Assets/Fonts/glyphs.txt";

    [MenuItem("Tools/VN/Extract Glyphs from dialogues.json")]
    public static void Extract()
    {
        string jsonPath = Path.Combine(Application.streamingAssetsPath, JsonRelativePath);
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"[GlyphExtractor] JSON 파일을 찾을 수 없다: {jsonPath}");
            return;
        }

        string json = File.ReadAllText(jsonPath);
        DialogueData data = JsonUtility.FromJson<DialogueData>(json);
        if (data == null || data.lines == null || data.lines.Length == 0)
        {
            Debug.LogError("[GlyphExtractor] JSON 파싱 실패 또는 lines 비어 있음.");
            return;
        }

        var unique = new HashSet<char>();
        foreach (var line in data.lines)
        {
            if (line == null) continue;
            CollectChars(line.speaker, unique);
            CollectChars(line.text, unique);
            CollectChars(line.show, unique);
            CollectChars(line.hide, unique);
        }

        // 보너스로 시스템 메시지에서 쓰는 글자도 포함 ("끝", 줄표, 대괄호 등)
        CollectChars("— 끝 —[대사 JSON 로드 실패]", unique);

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

        Debug.Log($"[GlyphExtractor] {sb.Length} 개 글자를 추출해 {OutputAssetPath} 에 저장했다.");

        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(OutputAssetPath);
        if (asset != null) EditorGUIUtility.PingObject(asset);
    }

    private static void CollectChars(string s, HashSet<char> sink)
    {
        if (string.IsNullOrEmpty(s)) return;
        for (int i = 0; i < s.Length; i++) sink.Add(s[i]);
    }
}
