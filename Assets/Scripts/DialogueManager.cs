using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[System.Serializable]
public class DialogueChoice
{
    public string label;
    // 'goto' 는 C# 예약어 → @ 로 이스케이프. JSON 키로는 그대로 "goto" 가 된다.
    public int @goto;
    // 선택 시 적용할 변수 변경. 예: "요르+10" / "민지-3". 없으면 변화 없음.
    public string effect;
}

[System.Serializable]
public class DialogueBranch
{
    // 비교식. 예: "요르>=50". 연산자는 >=, <=, >, <, == 지원.
    // condition 이 없거나 빈 문자열이면 "항상 참" 인 기본 분기다 (맨 아래에 둔다).
    public string condition;
    // 이 분기가 참일 때 점프할 lines 인덱스(0부터).
    public int @goto;
}

[System.Serializable]
public class DialogueLine
{
    public string speaker;
    public string text;

    // 선택 필드: 없어도 기본값(null/빈문자열/빈배열)이 되어 에러 나지 않는다.
    public string show;
    public string position;
    public string hide;
    public DialogueChoice[] choices;

    // 선택: 호감도 등 변수 값에 따라 분기하는 조건 점프.
    //   이 줄의 대사를 보여 준 뒤 클릭하면, 위에서부터 condition 을 검사해
    //   처음으로 참인 분기의 goto 로 점프한다. 있으면 줄 단위 end/goto 보다 우선.
    public DialogueBranch[] branches;

    // 줄 단위 흐름 제어 (모두 선택):
    //   end   : true 면 이 줄을 보여 준 뒤 대화를 종료한다.
    //   @goto : 이 줄 진행 후 점프할 lines 인덱스(0부터). 없으면 -1 = 자연 진행.
    //           JsonUtility 가 누락 시 0 으로 채우는 문제 때문에, LoadDialogue 에서
    //           원본 JSON 을 스캔해 "goto" 키가 없는 줄은 강제로 -1 로 되돌린다.
    public bool end;
    public int @goto;
    // 줄 진입 시 적용할 변수 변경. 예: "요르+10" / "민지-3". 없으면 변화 없음.
    public string effect;
}

[System.Serializable]
public class DialogueData
{
    public DialogueLine[] lines;
}

[System.Serializable]
public class CharacterEntry
{
    public string name;
    public Sprite sprite;
}

public class DialogueManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject dialoguePanel;
    public TMP_Text speakerText;
    public TMP_Text dialogueText;

    [Header("Character Slots (월드 좌표의 빈 GameObject)")]
    public Transform leftSlot;
    public Transform centerSlot;
    public Transform rightSlot;

    [Header("Characters")]
    [Tooltip("JSON의 show/hide 에서 사용할 '이름 ↔ 스프라이트' 매핑")]
    public List<CharacterEntry> characters = new List<CharacterEntry>();

    [Tooltip("캐릭터 SpriteRenderer 의 Sorting Order. 배경보다 크게, Canvas(Screen Space - Overlay) 는 자동으로 최상단이다.")]
    public int characterSortingOrder = 5;

    [Tooltip("등장하는 모든 캐릭터의 localScale 값. 원본 스프라이트가 너무 크면 0.35 정도부터 시작.")]
    public float characterScale = 0.35f;

    [Header("Choices")]
    [Tooltip("선택지 버튼으로 사용할 프리팹. 자식에 TMP_Text 가 하나 있어야 한다.")]
    public Button choiceButtonPrefab;

    [Tooltip("선택지 버튼이 자식으로 들어갈 컨테이너. VerticalLayoutGroup 권장.")]
    public Transform choicesContainer;

    [Header("Debug")]
    [Tooltip("켜면 변수 변화를 Console 에 로그로 찍고, Debug Variables Text 가 연결돼 있으면 화면에도 표시한다.")]
    public bool debugShowVariables = false;

    [Tooltip("화면에 변수 상태를 표시할 TMP 텍스트 (선택). 비워 두면 Console 로그만.")]
    public TMP_Text debugVariablesText;

    [Header("Settings")]
    [Tooltip("StreamingAssets 폴더 안에 있는 JSON 파일 이름")]
    public string jsonFileName = "dialogues.json";

    [Tooltip("한 글자가 출력되는 간격(초). 작을수록 빠르다.")]
    [Range(0.005f, 0.2f)]
    public float typingSpeed = 0.04f;

    [Tooltip("마지막 대사 후 패널을 자동으로 숨길지 여부")]
    public bool hidePanelOnEnd = false;

    private DialogueData data;
    private int currentIndex = -1;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isFinished;
    private bool isWaitingForChoice;
    private string currentFullText = "";

    private readonly Dictionary<string, GameObject> activeCharacters = new Dictionary<string, GameObject>();
    private readonly List<GameObject> spawnedChoiceButtons = new List<GameObject>();

    // 캐릭터별 호감도 등 정수 변수. 없는 키는 TryGetValue 가 0 을 돌려주므로 "시작 시 0" 보장됨.
    private readonly Dictionary<string, int> variables = new Dictionary<string, int>();

    private void Start()
    {
        if (dialogueText == null)
        {
            Debug.LogError("[DialogueManager] dialogueText 가 비어 있다. 인스펙터에서 TMP 텍스트를 연결해라.");
            enabled = false;
            return;
        }

        if (choicesContainer != null) choicesContainer.gameObject.SetActive(false);

        if (debugVariablesText != null)
        {
            debugVariablesText.gameObject.SetActive(debugShowVariables);
        }
        if (debugShowVariables) UpdateDebugVariablesText();

        if (!LoadDialogue())
        {
            dialogueText.text = "[대사 JSON 로드 실패]";
            enabled = false;
            return;
        }

        ShowNextLine();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // UI(선택지 버튼 등) 위에서 클릭한 경우는 그 UI 가 자체적으로 처리한다.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            OnClick();
        }
    }

    private bool LoadDialogue()
    {
        string path = Path.Combine(Application.streamingAssetsPath, jsonFileName);

        if (!File.Exists(path))
        {
            Debug.LogError($"[DialogueManager] JSON 파일을 찾을 수 없다: {path}");
            return false;
        }

        string json = File.ReadAllText(path);
        data = JsonUtility.FromJson<DialogueData>(json);

        if (data == null || data.lines == null || data.lines.Length == 0)
        {
            Debug.LogError("[DialogueManager] JSON 파싱 결과가 비어 있다. 형식을 확인해라.");
            return false;
        }

        // 원본 JSON 을 스캔해서 줄 단위 "goto" 키가 없는 줄은 @goto = -1 로 표시.
        MarkMissingLineGotos(json, data);
        return true;
    }

    private static void MarkMissingLineGotos(string json, DialogueData data)
    {
        var hasGoto = new bool[data.lines.Length];
        ScanLineLevelKey(json, "goto", hasGoto);
        for (int i = 0; i < data.lines.Length; i++)
        {
            if (data.lines[i] == null) continue;
            if (!hasGoto[i]) data.lines[i].@goto = -1;
        }
    }

    private static void ScanLineLevelKey(string json, string key, bool[] result)
    {
        int p = json.IndexOf("\"lines\"");
        if (p < 0) return;
        p = json.IndexOf('[', p);
        if (p < 0) return;
        p++; // past '['

        int lineIdx = 0;
        while (p < json.Length && lineIdx < result.Length)
        {
            while (p < json.Length && (json[p] == ',' || char.IsWhiteSpace(json[p]))) p++;
            if (p >= json.Length || json[p] == ']') break;
            if (json[p] != '{') { p++; continue; }

            int lineStart = p;
            int lineEnd = FindMatchingBrace(json, p);
            if (lineEnd < 0) break;

            if (ContainsTopLevelKey(json, lineStart, lineEnd, key))
                result[lineIdx] = true;

            p = lineEnd + 1;
            lineIdx++;
        }
    }

    private static int FindMatchingBrace(string json, int openBracePos)
    {
        int depth = 0;
        int p = openBracePos;
        while (p < json.Length)
        {
            char c = json[p];
            if (c == '"')
            {
                int e = FindStringEnd(json, p);
                if (e < 0) return -1;
                p = e + 1;
                continue;
            }
            if (c == '{') depth++;
            else if (c == '}') { depth--; if (depth == 0) return p; }
            p++;
        }
        return -1;
    }

    private static int FindStringEnd(string json, int openQuotePos)
    {
        int p = openQuotePos + 1;
        while (p < json.Length)
        {
            char c = json[p];
            if (c == '\\') { p += 2; continue; }
            if (c == '"') return p;
            p++;
        }
        return -1;
    }

    private static bool ContainsTopLevelKey(string json, int objStart, int objEnd, string key)
    {
        // 객체의 첫 '{' 다음부터 매칭 '}' 까지 훑으면서, 내부 깊이가 0 일 때 등장하는
        // 문자열 + ':' 패턴만 "최상위 키" 로 본다. 중첩 객체 / 배열 안의 동명 키는 무시.
        int depth = 0;
        int p = objStart + 1;
        while (p < objEnd)
        {
            char c = json[p];
            if (c == '"')
            {
                int e = FindStringEnd(json, p);
                if (e < 0) return false;
                if (depth == 0)
                {
                    int len = e - p - 1;
                    bool match = (len == key.Length);
                    if (match)
                    {
                        for (int k = 0; k < len; k++)
                        {
                            if (json[p + 1 + k] != key[k]) { match = false; break; }
                        }
                    }
                    int q = e + 1;
                    while (q < objEnd && char.IsWhiteSpace(json[q])) q++;
                    if (match && q < objEnd && json[q] == ':') return true;
                }
                p = e + 1;
                continue;
            }
            if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']') depth--;
            p++;
        }
        return false;
    }

    private void OnClick()
    {
        if (data == null || isFinished) return;
        if (isWaitingForChoice) return; // 선택지 대기 중엔 화면 클릭으로 진행 안 됨 — 버튼만.

        if (isTyping)
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            dialogueText.text = currentFullText;
            isTyping = false;
            TryShowChoicesForCurrent();
        }
        else
        {
            AdvanceFromCurrentLine();
        }
    }

    // 현재 줄을 다 읽고 클릭한 시점에서, 그 줄의 end / 줄 단위 goto / 자연 진행 중 무엇을 할지 결정.
    // (choices 가 있는 줄은 OnClick 단계에서 isWaitingForChoice 가 true 라 여기 안 들어온다.)
    private void AdvanceFromCurrentLine()
    {
        if (data == null) return;
        if (currentIndex < 0 || currentIndex >= data.lines.Length)
        {
            ShowNextLine();
            return;
        }

        var line = data.lines[currentIndex];

        // branches 가 있으면 조건 검사로 점프한다 (end / 줄 단위 goto 보다 우선).
        if (line.branches != null && line.branches.Length > 0)
        {
            EvaluateBranches(line.branches);
            return;
        }

        if (line.end)
        {
            EndDialogue();
            return;
        }

        if (line.@goto >= 0)
        {
            if (line.@goto >= data.lines.Length)
            {
                Debug.LogWarning($"[DialogueManager] 줄 {currentIndex} 의 goto={line.@goto} 가 lines 범위 밖이다. 종료한다.");
                EndDialogue();
                return;
            }
            currentIndex = line.@goto - 1; // ShowNextLine 이 ++ 하므로 -1 로 맞춰 둠
            ShowNextLine();
            return;
        }

        ShowNextLine();
    }

    // branches 를 위에서부터 검사해 처음으로 참인 분기의 goto 로 점프한다.
    private void EvaluateBranches(DialogueBranch[] branches)
    {
        for (int i = 0; i < branches.Length; i++)
        {
            var b = branches[i];
            if (b == null) continue;

            if (EvaluateCondition(b.condition))
            {
                JumpTo(b.@goto, $"줄 {currentIndex} branches[{i}]");
                return;
            }
        }

        // 참인 분기가 하나도 없으면(기본 분기조차 없으면) 그냥 다음 줄로 진행한다.
        Debug.LogWarning($"[DialogueManager] 줄 {currentIndex} 의 branches 에 참인 분기가 없다. " +
                         "condition 없는 기본 분기를 맨 아래에 두는 걸 권장. 자연 진행한다.");
        ShowNextLine();
    }

    // 공통 점프 처리: 범위를 벗어나면 경고 후 종료, 정상이면 해당 줄로 이동.
    private void JumpTo(int target, string source)
    {
        if (data == null) return;
        if (target < 0 || target >= data.lines.Length)
        {
            Debug.LogWarning($"[DialogueManager] {source} 의 goto={target} 가 lines 범위 밖이다. 종료한다.");
            EndDialogue();
            return;
        }

        currentIndex = target - 1; // ShowNextLine 이 ++ 하므로 -1 로 맞춰 둠
        ShowNextLine();
    }

    private void ShowNextLine()
    {
        ClearChoices(); // 안전망: 이전 줄의 버튼이 남아 있으면 정리
        currentIndex++;
        if (currentIndex >= data.lines.Length)
        {
            EndDialogue();
            return;
        }

        DialogueLine line = data.lines[currentIndex];

        // hide 먼저 처리 → 그 다음 show. 같은 줄에서 둘 다 있으면 교체에 가깝게 동작.
        if (!string.IsNullOrEmpty(line.hide))
        {
            HideCharacter(line.hide);
        }
        if (!string.IsNullOrEmpty(line.show))
        {
            string pos = string.IsNullOrEmpty(line.position) ? "center" : line.position;
            ShowCharacter(line.show, pos);
        }

        // 줄 단위 effect 는 타자 시작 전에 적용 → 디버그 표시가 그 줄과 함께 갱신된다.
        if (!string.IsNullOrEmpty(line.effect))
        {
            ApplyEffect(line.effect, $"줄 {currentIndex}");
        }

        if (speakerText != null)
        {
            speakerText.text = string.IsNullOrEmpty(line.speaker) ? "" : line.speaker;
        }

        currentFullText = line.text ?? "";

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeLine(currentFullText));
    }

    private IEnumerator TypeLine(string fullText)
    {
        isTyping = true;
        dialogueText.text = "";

        for (int i = 0; i < fullText.Length; i++)
        {
            dialogueText.text += fullText[i];
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
        TryShowChoicesForCurrent();
    }

    private void TryShowChoicesForCurrent()
    {
        if (data == null) return;
        if (currentIndex < 0 || currentIndex >= data.lines.Length) return;

        var line = data.lines[currentIndex];
        if (line.choices != null && line.choices.Length > 0)
        {
            ShowChoices(line.choices);
        }
    }

    private void ShowChoices(DialogueChoice[] choices)
    {
        if (choiceButtonPrefab == null || choicesContainer == null)
        {
            Debug.LogError("[DialogueManager] Choice Button Prefab 또는 Choices Container 가 인스펙터에 연결돼 있지 않다.");
            return;
        }

        ClearChoices();
        choicesContainer.gameObject.SetActive(true);

        for (int i = 0; i < choices.Length; i++)
        {
            var c = choices[i];
            if (c == null) continue;

            Button btn = Instantiate(choiceButtonPrefab, choicesContainer);
            btn.name = $"ChoiceButton_{i}";

            // 자식 어딘가의 TMP_Text 에 라벨 적용 (활성/비활성 모두 검색).
            var tmp = btn.GetComponentInChildren<TMP_Text>(includeInactive: true);
            if (tmp != null) tmp.text = c.label ?? "";
            else Debug.LogWarning("[DialogueManager] 버튼 프리팹 자식에 TMP_Text 가 없다. 라벨을 표시할 수 없다.");

            int target = c.@goto;        // 클로저 캡처용 로컬
            string effect = c.effect;    // 마찬가지로 로컬에 복사
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnChoiceSelected(target, effect));

            spawnedChoiceButtons.Add(btn.gameObject);
        }

        isWaitingForChoice = true;
    }

    private void OnChoiceSelected(int targetIndex, string effect)
    {
        // 변수 변경은 점프 전에 적용 → 다음 줄이 이미 갱신된 상태를 본다.
        if (!string.IsNullOrEmpty(effect))
        {
            ApplyEffect(effect, "선택지");
        }

        ClearChoices();

        if (data == null || targetIndex < 0 || targetIndex >= data.lines.Length)
        {
            Debug.LogWarning($"[DialogueManager] goto={targetIndex} 가 lines 범위 밖이다. 대화를 종료한다.");
            EndDialogue();
            return;
        }

        currentIndex = targetIndex - 1; // ShowNextLine 이 ++ 하므로 -1 로 맞춰 둠
        ShowNextLine();
    }

    private void ClearChoices()
    {
        for (int i = 0; i < spawnedChoiceButtons.Count; i++)
        {
            if (spawnedChoiceButtons[i] != null) Destroy(spawnedChoiceButtons[i]);
        }
        spawnedChoiceButtons.Clear();
        if (choicesContainer != null) choicesContainer.gameObject.SetActive(false);
        isWaitingForChoice = false;
    }

    private void EndDialogue()
    {
        isFinished = true;

        if (hidePanelOnEnd && dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
            return;
        }

        if (speakerText != null) speakerText.text = "";
        if (dialogueText != null) dialogueText.text = "— 끝 —";
    }

    private Transform GetSlot(string position)
    {
        switch ((position ?? "").ToLowerInvariant())
        {
            case "left": return leftSlot;
            case "center": return centerSlot;
            case "right": return rightSlot;
            default:
                Debug.LogWarning($"[DialogueManager] 알 수 없는 position '{position}'. left / center / right 중 하나여야 한다.");
                return null;
        }
    }

    private CharacterEntry FindCharacterEntry(string charName)
    {
        for (int i = 0; i < characters.Count; i++)
        {
            var c = characters[i];
            if (c != null && c.name == charName) return c;
        }
        return null;
    }

    private void ShowCharacter(string charName, string position)
    {
        CharacterEntry entry = FindCharacterEntry(charName);
        if (entry == null)
        {
            Debug.LogWarning($"[DialogueManager] '{charName}' 캐릭터가 등록돼 있지 않다. Characters 리스트에 추가해라.");
            return;
        }
        if (entry.sprite == null)
        {
            Debug.LogWarning($"[DialogueManager] '{charName}' 의 Sprite 가 비어 있다.");
            return;
        }

        Transform slot = GetSlot(position);
        if (slot == null)
        {
            Debug.LogWarning($"[DialogueManager] '{position}' Slot Transform 이 인스펙터에 연결돼 있지 않다.");
            return;
        }

        // 이 슬롯에 다른 캐릭터가 이미 서 있으면 치우고 교체한다.
        List<string> occupantsToRemove = null;
        foreach (var kv in activeCharacters)
        {
            if (kv.Key == charName) continue;
            if (kv.Value != null && kv.Value.transform.parent == slot)
            {
                if (occupantsToRemove == null) occupantsToRemove = new List<string>();
                occupantsToRemove.Add(kv.Key);
            }
        }
        if (occupantsToRemove != null)
        {
            foreach (var n in occupantsToRemove)
            {
                if (activeCharacters[n] != null) Destroy(activeCharacters[n]);
                activeCharacters.Remove(n);
            }
        }

        // 이미 다른 슬롯에 떠 있으면 옮기고, 없으면 새로 만든다.
        if (activeCharacters.TryGetValue(charName, out GameObject go) && go != null)
        {
            go.transform.SetParent(slot, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * characterScale;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = entry.sprite;
                sr.sortingOrder = characterSortingOrder;
            }
        }
        else
        {
            var newGo = new GameObject($"Char_{charName}");
            newGo.transform.SetParent(slot, worldPositionStays: false);
            newGo.transform.localPosition = Vector3.zero;
            newGo.transform.localScale = Vector3.one * characterScale;
            var sr = newGo.AddComponent<SpriteRenderer>();
            sr.sprite = entry.sprite;
            sr.sortingOrder = characterSortingOrder;
            activeCharacters[charName] = newGo;
        }
    }

    private void HideCharacter(string charName)
    {
        if (activeCharacters.TryGetValue(charName, out GameObject go))
        {
            if (go != null) Destroy(go);
            activeCharacters.Remove(charName);
        }
    }

    // ───────── 변수 / effect ─────────

    public int GetVariable(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        return variables.TryGetValue(name, out int v) ? v : 0;
    }

    private void ApplyEffect(string effect, string source)
    {
        if (!TryParseEffect(effect, out string name, out int delta))
        {
            Debug.LogWarning($"[DialogueManager] effect 파싱 실패: '{effect}' (출처: {source}). 형식은 '이름+숫자' 또는 '이름-숫자'.");
            return;
        }

        int oldVal = GetVariable(name);
        int newVal = oldVal + delta;
        variables[name] = newVal;

        if (debugShowVariables)
        {
            string sign = (delta >= 0) ? "+" : "";
            Debug.Log($"[DialogueManager] ({source}) {name}: {oldVal} → {newVal} ({sign}{delta})");
            UpdateDebugVariablesText();
        }
    }

    // "요르+10" → name="요르", delta=+10
    // "민지-3"  → name="민지", delta=-3
    // 뒤에서부터 숫자를 잘라 내고, 그 직전 한 문자가 '+' 또는 '-' 면 연산자, 그 앞이 이름.
    private static bool TryParseEffect(string effect, out string name, out int delta)
    {
        name = null;
        delta = 0;
        if (string.IsNullOrWhiteSpace(effect)) return false;

        string s = effect.Trim();
        int i = s.Length - 1;
        while (i >= 0 && char.IsDigit(s[i])) i--;
        if (i < 0 || i == s.Length - 1) return false; // 숫자가 없거나 연산자가 없다

        char op = s[i];
        if (op != '+' && op != '-') return false;

        if (!int.TryParse(s.Substring(i + 1), out int magnitude)) return false;

        name = s.Substring(0, i).Trim();
        if (string.IsNullOrEmpty(name)) return false;

        delta = (op == '+') ? magnitude : -magnitude;
        return true;
    }

    // ───────── 조건식 (branches) ─────────

    // "요르>=50" 형태의 비교식을 평가한다. 비어 있으면 항상 참(기본 분기).
    // 좌변은 변수 이름(없으면 0), 우변은 정수. 연산자: >=, <=, >, <, ==.
    private bool EvaluateCondition(string condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true; // 기본 분기

        if (!TryParseCondition(condition, out string name, out string op, out int rhs))
        {
            Debug.LogWarning($"[DialogueManager] 조건 파싱 실패: '{condition}'. " +
                             "형식은 '이름>=숫자' 등 (연산자: >=, <=, >, <, ==).");
            return false;
        }

        int lhs = GetVariable(name);
        switch (op)
        {
            case ">=": return lhs >= rhs;
            case "<=": return lhs <= rhs;
            case ">":  return lhs > rhs;
            case "<":  return lhs < rhs;
            case "==": return lhs == rhs;
            default:   return false;
        }
    }

    // "요르>=50" → name="요르", op=">=", value=50
    // 두 글자 연산자(>=, <=, ==) 를 먼저 찾고, 없으면 한 글자(>, <) 를 찾는다.
    private static bool TryParseCondition(string condition, out string name, out string op, out int value)
    {
        name = null;
        op = null;
        value = 0;
        if (string.IsNullOrWhiteSpace(condition)) return false;

        string s = condition.Trim();

        int idx = -1;
        string found = null;

        foreach (string two in new[] { ">=", "<=", "==" })
        {
            int p = s.IndexOf(two, System.StringComparison.Ordinal);
            if (p >= 0) { idx = p; found = two; break; }
        }
        if (found == null)
        {
            foreach (string one in new[] { ">", "<" })
            {
                int p = s.IndexOf(one, System.StringComparison.Ordinal);
                if (p >= 0) { idx = p; found = one; break; }
            }
        }

        if (found == null || idx <= 0) return false; // 연산자가 없거나 이름이 비었다

        name = s.Substring(0, idx).Trim();
        if (string.IsNullOrEmpty(name)) return false;

        string rhsStr = s.Substring(idx + found.Length).Trim();
        if (!int.TryParse(rhsStr, out value)) return false;

        op = found;
        return true;
    }

    private void UpdateDebugVariablesText()
    {
        if (debugVariablesText == null) return;

        if (variables.Count == 0)
        {
            debugVariablesText.text = "(변수 없음)";
            return;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var kv in variables)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(kv.Key).Append(": ").Append(kv.Value);
        }
        debugVariablesText.text = sb.ToString();
    }
}
