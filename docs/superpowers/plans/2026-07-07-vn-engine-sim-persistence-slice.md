# 시뮬 커널 영속화 슬라이스 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 다회차 캠페인 상태를 디스크에 저장/복원하고, LoopCount를 VN 변수로 읽기전용 투영하며, 그 전제로 RunState 불변성을 방어적 복사로 강화한다.

**Architecture:** Core에 순수 직렬화 모델(`CampaignSaveData`)+캡처/복원(`CampaignSave`)+투영(`MetaProjection`)을 추가하고, Unity 레이어에 디스크 IO(`CampaignSaveSystem`)와 SimController 세이브/로드 버튼을 얹는다. 기존 VN VM 세이브(`SaveData`/`SaveSystem`)와는 독립된 별도 파일(`campaign_N.json`).

**Tech Stack:** C# (Unity 2022.3, .NET Standard 2.1), NUnit EditMode, UnityMCP(refresh/console/run_tests/playmode), JsonUtility.

## Global Constraints

- `Core/**` (`Assets/Scripts/VNEngine/Core/**`) 는 `UnityEngine`·`System.IO` 절대 미참조 (순수 C#). Unity 어댑터(`Assets/Scripts/VNEngine/Unity/**`)만 IO·MonoBehaviour 허용.
- 모든 상태 전이·복원은 **불변**: 새 인스턴스 반환, 입력 변형 금지.
- 런타임 오류는 기존 `VnRuntimeException` 재사용 (새 예외 타입 금지).
- 직렬화 모델은 `JsonUtility` 호환: **딕셔너리 금지, 리스트+원시 타입만**, `[System.Serializable]`.
- Core 클래스 네임스페이스 = `VNEngine`. Unity 어댑터 = `VNEngine.Unity`.
- 테스트: `Assets/Tests/Editor`, ns `VNEngine.Tests`, NUnit EditMode. GameState 생성은 `new GameState(new SeededRandom(1))` 패턴.
- 새 `.cs` 추가/삭제·수정 후: UnityMCP `refresh_unity` scope:`all` → `read_console`(에러0) → `run_tests` assembly `VNEngine.Tests` (scope:`scripts`는 false-green이라 금지).
- 투영은 커널→VN **단방향·읽기전용**. Core는 테마 중립 — 투영 변수명은 주입.
- 비스코프(건들지 말 것): Regress 내용 로직(편지/진실플래그/미갈/계승몹), 캠페인+VN VM 통합 세이브, 세이브 슬롯 관리 UI.

---

### Task 1: RunState 방어적 복사

`RunState`가 넘겨받은 딕셔너리를 참조 보관하던 것을, 생성자에서 내부 새 딕셔너리로 깊은 복사하도록 바꿔 호출자 무관 불변을 보장한다.

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Sim/RunState.cs`
- Test: `Assets/Tests/Editor/RunStateTests.cs` (create)

**Interfaces:**
- Consumes: 기존 `RunState(int day, IReadOnlyDictionary<string,int> resources)`.
- Produces: 동일 시그니처. 단 `Resources`는 이제 생성자 인자와 **참조 공유하지 않는** 내부 복사본.

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/Editor/RunStateTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class RunStateTests
    {
        [Test]
        public void ConstructorCopiesResourcesSoLaterMutationDoesNotLeak()
        {
            var src = new Dictionary<string, int> { { "money", 100 }, { "magic", 50 } };
            var run = new RunState(1, src);

            src["money"] = 999;      // 원본 딕셔너리 수정
            src["extra"] = 7;        // 키 추가

            Assert.AreEqual(100, run.Resources["money"], "원본 수정이 새어들면 안 됨");
            Assert.IsFalse(run.Resources.ContainsKey("extra"), "원본에 추가한 키가 새어들면 안 됨");
            Assert.AreEqual(2, run.Resources.Count);
        }

        [Test]
        public void ConstructorPreservesDayAndValues()
        {
            var run = new RunState(3, new Dictionary<string, int> { { "money", 40 } });
            Assert.AreEqual(3, run.Day);
            Assert.AreEqual(40, run.Resources["money"]);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`(컴파일 에러0) → `run_tests` assembly:`VNEngine.Tests` test_names:`VNEngine.Tests.RunStateTests`.
Expected: `ConstructorCopiesResourcesSoLaterMutationDoesNotLeak` FAIL (원본 수정이 `run.Resources`에 반영돼 money==999 → 단정 실패).

- [ ] **Step 3: 최소 구현 — 생성자 깊은 복사**

`Assets/Scripts/VNEngine/Core/Sim/RunState.cs` 전체:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    public sealed class RunState
    {
        public int Day { get; }
        public IReadOnlyDictionary<string, int> Resources { get; }

        public RunState(int day, IReadOnlyDictionary<string, int> resources)
        {
            Day = day;
            var copy = new Dictionary<string, int>(resources.Count);
            foreach (var kv in resources) copy[kv.Key] = kv.Value; // 방어적 복사
            Resources = copy;
        }
    }
}
```

- [ ] **Step 4: 통과 확인 (+회귀 없음)**

UnityMCP: `refresh_unity` scope:`all` → `read_console`(에러0) → `run_tests` assembly:`VNEngine.Tests`.
Expected: RunStateTests 2/2 PASS, 그리고 기존 스위트 전건 PASS(방어적 복사는 TurnEngine이 이미 새 딕셔너리를 넘기므로 회귀 없음). 총 178/178 이상.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/Scripts/VNEngine/Core/Sim/RunState.cs" "Assets/Tests/Editor/RunStateTests.cs"
git commit -m "feat(sim): RunState defensive-copies resources for true immutability"
```

---

### Task 2: CampaignSave 모델 + Capture/Restore (Core)

`CampaignState`를 JsonUtility 호환 평면 모델로 캡처/복원하는 순수 계층.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/CampaignSaveData.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/CampaignSave.cs`
- Test: `Assets/Tests/Editor/CampaignSaveTests.cs` (create)

**Interfaces:**
- Consumes (Task 1): `RunState(int, IReadOnlyDictionary<string,int>)`(방어적 복사), `MetaState(int)`, `CampaignState(MetaState, RunState)`.
- Produces:
  - `ResEntry { string id; int value; }` `[Serializable]`
  - `CampaignSaveData { const int CampaignSaveVersion=1; int version; int loopCount; int day; List<ResEntry> resources; }` `[Serializable]`
  - `static CampaignSaveData CampaignSave.Capture(CampaignState c)`
  - `static CampaignState CampaignSave.Restore(CampaignSaveData d)` — `version` 불일치 시 `VnRuntimeException`.

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/Editor/CampaignSaveTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine; // JsonUtility

namespace VNEngine.Tests
{
    public class CampaignSaveTests
    {
        private static CampaignState Sample() =>
            new CampaignState(
                new MetaState(3),
                new RunState(5, new Dictionary<string, int> { { "money", 150 }, { "magic", 30 } }));

        [Test]
        public void CaptureThenRestoreRoundTrips()
        {
            var restored = CampaignSave.Restore(CampaignSave.Capture(Sample()));
            Assert.AreEqual(3, restored.Meta.LoopCount);
            Assert.AreEqual(5, restored.Run.Day);
            Assert.AreEqual(150, restored.Run.Resources["money"]);
            Assert.AreEqual(30, restored.Run.Resources["magic"]);
            Assert.AreEqual(2, restored.Run.Resources.Count);
        }

        [Test]
        public void JsonUtilityRoundTripThroughSaveData()
        {
            var data = CampaignSave.Capture(Sample());
            string json = JsonUtility.ToJson(data);
            var back = JsonUtility.FromJson<CampaignSaveData>(json);
            var restored = CampaignSave.Restore(back);
            Assert.AreEqual(3, restored.Meta.LoopCount);
            Assert.AreEqual(5, restored.Run.Day);
            Assert.AreEqual(150, restored.Run.Resources["money"]);
        }

        [Test]
        public void CapturedVersionIsCurrent()
        {
            Assert.AreEqual(CampaignSaveData.CampaignSaveVersion, CampaignSave.Capture(Sample()).version);
        }

        [Test]
        public void RestoreRejectsIncompatibleVersion()
        {
            var data = CampaignSave.Capture(Sample());
            data.version = 999;
            Assert.Throws<VnRuntimeException>(() => CampaignSave.Restore(data));
        }

        [Test]
        public void RestoreDoesNotAliasSaveDataList()
        {
            var data = CampaignSave.Capture(Sample());
            var restored = CampaignSave.Restore(data);
            data.resources[0].value = 999;                 // 복원 후 원본 세이브데이터 수정
            data.resources.Add(new ResEntry { id = "x", value = 1 });
            Assert.AreEqual(150, restored.Run.Resources["money"], "복원 상태가 세이브데이터 리스트를 참조 공유하면 안 됨");
            Assert.AreEqual(2, restored.Run.Resources.Count);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`.
Expected: 컴파일 에러 — `CampaignSave`/`CampaignSaveData`/`ResEntry` 미정의. (테스트 실행 전 컴파일 실패가 곧 RED.)

- [ ] **Step 3: 모델 구현**

`Assets/Scripts/VNEngine/Core/Sim/CampaignSaveData.cs`:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    [System.Serializable]
    public sealed class ResEntry
    {
        public string id;
        public int value;
    }

    // JsonUtility 호환: 딕셔너리 대신 리스트, 원시 타입만.
    [System.Serializable]
    public sealed class CampaignSaveData
    {
        public const int CampaignSaveVersion = 1;

        public int version;
        public int loopCount;                 // Meta.LoopCount
        public int day;                       // Run.Day
        public List<ResEntry> resources = new List<ResEntry>(); // Run.Resources 평면화
    }
}
```

- [ ] **Step 4: Capture/Restore 구현**

`Assets/Scripts/VNEngine/Core/Sim/CampaignSave.cs`:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    // CampaignState <-> CampaignSaveData 순수 캡처/복원. IO 없음(Core).
    public static class CampaignSave
    {
        public static CampaignSaveData Capture(CampaignState c)
        {
            if (c == null) throw new System.ArgumentNullException(nameof(c));
            var data = new CampaignSaveData
            {
                version = CampaignSaveData.CampaignSaveVersion,
                loopCount = c.Meta.LoopCount,
                day = c.Run.Day,
            };
            foreach (var kv in c.Run.Resources)
                data.resources.Add(new ResEntry { id = kv.Key, value = kv.Value });
            return data;
        }

        public static CampaignState Restore(CampaignSaveData data)
        {
            if (data == null) throw new System.ArgumentNullException(nameof(data));
            if (data.version != CampaignSaveData.CampaignSaveVersion)
                throw new VnRuntimeException(
                    $"Incompatible campaign save version: {data.version} (expected {CampaignSaveData.CampaignSaveVersion})");

            var res = new Dictionary<string, int>(data.resources.Count);
            foreach (var e in data.resources)
                res[e.id] = e.value;

            // RunState 생성자가 res를 다시 방어적 복사 → 세이브데이터 리스트와 참조 분리.
            return new CampaignState(new MetaState(data.loopCount), new RunState(data.day, res));
        }
    }
}
```

- [ ] **Step 5: 통과 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`(에러0) → `run_tests` assembly:`VNEngine.Tests` test_names:`VNEngine.Tests.CampaignSaveTests`.
Expected: CampaignSaveTests 5/5 PASS. 전체 스위트도 PASS.

- [ ] **Step 6: 커밋**

```bash
git add "Assets/Scripts/VNEngine/Core/Sim/CampaignSaveData.cs" "Assets/Scripts/VNEngine/Core/Sim/CampaignSave.cs" "Assets/Tests/Editor/CampaignSaveTests.cs"
git commit -m "feat(sim): CampaignSave capture/restore + JsonUtility-friendly CampaignSaveData"
```

---

### Task 3: MetaProjection — LoopCount → VN 변수 (Core)

메타 상태를 VN `GameState` 변수로 읽기전용 투영하는 순수 함수. 변수명은 주입(테마 중립).

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/MetaProjection.cs`
- Test: `Assets/Tests/Editor/MetaProjectionTests.cs` (create)

**Interfaces:**
- Consumes: `MetaState.LoopCount`, `GameState.Set(string, VnValue)`, `GameState.Get(string)`, `VnValue.Int(int)`.
- Produces: `static void MetaProjection.Project(MetaState meta, GameState state, string loopCountVar)`.

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/Editor/MetaProjectionTests.cs`:
```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class MetaProjectionTests
    {
        [Test]
        public void ProjectsLoopCountIntoNamedVariable()
        {
            var state = new GameState(new SeededRandom(1));
            MetaProjection.Project(new MetaState(3), state, "회차");
            Assert.AreEqual(VnValue.Int(3), state.Get("회차"));
        }

        [Test]
        public void ProjectionOverwritesOnRepeat()
        {
            var state = new GameState(new SeededRandom(1));
            MetaProjection.Project(new MetaState(1), state, "loop");
            MetaProjection.Project(new MetaState(2), state, "loop");
            Assert.AreEqual(VnValue.Int(2), state.Get("loop"));
        }

        [Test]
        public void RejectsEmptyVariableName()
        {
            var state = new GameState(new SeededRandom(1));
            Assert.Throws<System.ArgumentException>(() => MetaProjection.Project(new MetaState(1), state, ""));
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`.
Expected: 컴파일 에러 — `MetaProjection` 미정의 (RED).

- [ ] **Step 3: 최소 구현**

`Assets/Scripts/VNEngine/Core/Sim/MetaProjection.cs`:
```csharp
namespace VNEngine
{
    // 커널 → VN 단방향·읽기전용 투영. Core는 테마 중립이라 변수명은 주입받는다.
    public static class MetaProjection
    {
        public static void Project(MetaState meta, GameState state, string loopCountVar)
        {
            if (meta == null) throw new System.ArgumentNullException(nameof(meta));
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (string.IsNullOrEmpty(loopCountVar))
                throw new System.ArgumentException("loopCountVar required", nameof(loopCountVar));

            state.Set(loopCountVar, VnValue.Int(meta.LoopCount));
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`(에러0) → `run_tests` assembly:`VNEngine.Tests` test_names:`VNEngine.Tests.MetaProjectionTests`.
Expected: MetaProjectionTests 3/3 PASS. 전체 스위트 PASS.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/Scripts/VNEngine/Core/Sim/MetaProjection.cs" "Assets/Tests/Editor/MetaProjectionTests.cs"
git commit -m "feat(sim): MetaProjection projects LoopCount into a VN variable (read-only)"
```

---

### Task 4: Unity 배선 — CampaignSaveSystem + SimController 세이브/로드 버튼

디스크 IO(Unity 레이어)와 SimController 세이브/로드 버튼을 붙이고 플레이모드로 왕복 검증한다. (Core 테스트로 검증 못 하는 디스크 IO·씬 배선을 수동 검증하는 태스크.)

**Files:**
- Create: `Assets/Scripts/VNEngine/Unity/Sim/CampaignSaveSystem.cs`
- Modify: `Assets/Scripts/VNEngine/Unity/Sim/SimController.cs`
- (수동) Modify: `Assets/Scenes/SimSlice.unity` — "세이브"·"로드" 버튼 배치·배선

**Interfaces:**
- Consumes (Task 2): `CampaignSave.Capture(CampaignState)`, `CampaignSave.Restore(CampaignSaveData)`, `CampaignSaveData`.
- Produces: `CampaignSaveSystem.Write/Read/Exists/Delete` (Unity 레이어 정적 IO).

- [ ] **Step 1: CampaignSaveSystem 구현**

`Assets/Scripts/VNEngine/Unity/Sim/CampaignSaveSystem.cs`:
```csharp
using System.IO;
using UnityEngine;

namespace VNEngine.Unity
{
    // CampaignSaveData 디스크 영속화. 기존 SaveSystem과 독립 파일(campaign_N.json).
    // 모바일 안전: Application.persistentDataPath 만 사용.
    public static class CampaignSaveSystem
    {
        private static string Dir => Path.Combine(Application.persistentDataPath, "saves");
        public static string SlotPath(int slot) => Path.Combine(Dir, $"campaign_{slot}.json");

        public static void Write(int slot, CampaignSaveData data)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(data, true));
        }

        public static CampaignSaveData Read(int slot)
        {
            string path = SlotPath(slot);
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<CampaignSaveData>(File.ReadAllText(path)); }
            catch (System.Exception e)
            {
                Debug.LogError($"[CampaignSaveSystem] failed to read slot {slot}: {e.Message}");
                return null;
            }
        }

        public static bool Exists(int slot) => File.Exists(SlotPath(slot));

        public static void Delete(int slot)
        {
            string path = SlotPath(slot);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`.
Expected: 컴파일 에러 0.

- [ ] **Step 3: SimController에 세이브/로드 배선**

`Assets/Scripts/VNEngine/Unity/Sim/SimController.cs` — 아래 표시된 3곳만 변경(나머지 유지):

(a) `[Header("UI")]` 필드 블록에 두 버튼 슬롯 추가 (기존 `newLoopButton` 아래):
```csharp
        [SerializeField] private Button newLoopButton; // "새 회차" — 없으면 무시
        [SerializeField] private Button saveButton;    // "세이브" — 없으면 무시
        [SerializeField] private Button loadButton;    // "로드" — 없으면 무시
        [SerializeField] private int saveSlot = 0;
```

(b) `Start()` 의 `newLoopButton` 배선 블록 바로 아래에 추가:
```csharp
            if (saveButton != null)
            {
                saveButton.onClick.RemoveAllListeners();
                saveButton.onClick.AddListener(OnSave);
            }
            if (loadButton != null)
            {
                loadButton.onClick.RemoveAllListeners();
                loadButton.onClick.AddListener(OnLoad);
            }
```

(c) `OnNewLoop()` 메서드 아래에 두 핸들러 추가:
```csharp
        private void OnSave()
        {
            CampaignSaveSystem.Write(saveSlot, CampaignSave.Capture(_campaign));
            Debug.Log($"[SimController] saved slot {saveSlot}: 회차{_campaign.Meta.LoopCount}/일차{_campaign.Run.Day}");
        }

        private void OnLoad()
        {
            var data = CampaignSaveSystem.Read(saveSlot);
            if (data == null) { Debug.Log($"[SimController] no save in slot {saveSlot}"); return; }
            _campaign = CampaignSave.Restore(data);
            Refresh();
        }
```

- [ ] **Step 4: 컴파일 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`.
Expected: 컴파일 에러 0. (`CampaignSaveSystem`·`CampaignSave`가 `VNEngine.Unity`·`VNEngine`에서 보임.)

- [ ] **Step 5: 씬에 "세이브"·"로드" 버튼 배치·배선 (수동)**

`Assets/Scenes/SimSlice.unity` 열기 → "새 회차" 버튼 옆에 UI Button 2개 추가(라벨 "세이브", "로드", CommandButton 프리팹 재사용) → `SimController` 컴포넌트의 `Save Button`·`Load Button` 슬롯에 각각 드래그. 저장.

- [ ] **Step 6: 플레이모드 왕복 검증**

`manage_editor` play 진입 → 시작 `회차1/일차1/재보100/마력50` →
"약탈" 클릭 → `회차1/일차2/재보150/마력30` →
"세이브" 클릭(콘솔에 saved 로그 확인) →
"약탈" 클릭 + "새 회차" 클릭으로 상태 변경(예: `회차2/일차1/재보100/마력50`) →
"로드" 클릭 → **세이브 시점 `회차1/일차2/재보150/마력30` 으로 정확히 복원** 확인 →
`read_console` 에러 0 → play 종료.

(검증 구동은 런타임 버튼 `onClick.Invoke()` 또는 실제 클릭. 세이브 슬롯 파일은 `persistentDataPath/saves/campaign_0.json`.)

- [ ] **Step 7: 커밋**

```bash
git add "Assets/Scripts/VNEngine/Unity/Sim/CampaignSaveSystem.cs" "Assets/Scripts/VNEngine/Unity/Sim/SimController.cs" "Assets/Scenes/SimSlice.unity"
git commit -m "feat(sim): campaign disk save + SimController save/load buttons"
```

---

### Task 5: 문서 갱신 (05, 06)

영속화·투영 구현을 엔진 레퍼런스에 반영.

**Files:**
- Modify: `docs/engine/05-simulation-kernel.md`
- Modify: `docs/engine/06-loop-and-state.md`

- [ ] **Step 1: `05-simulation-kernel.md` 갱신**

- 코어 모델 표에 행 추가: `CampaignSaveData { version, loopCount, day, List<ResEntry> }`, `CampaignSave.Capture/Restore`, `MetaProjection.Project`.
- §5(VN 코어와의 관계)에 "메타→VN 단방향 투영(`MetaProjection`, 읽기전용, LoopCount→변수)이 접합 심으로 추가됨. `.vns`에서 `if 회차 >= 2` 분기 가능" 한 단락 추가.
- "회차 루프 상태" 착수 노트를 갱신: 이제 **디스크 세이브(별도 파일 `campaign_N.json`)·읽기전용 투영**까지 됨. 방어적 복사로 RunState 불변 강화됨. 여전히 미룸: Regress 내용 로직·캠페인+VN 통합 세이브.

- [ ] **Step 2: `06-loop-and-state.md` 상단 착수 노트 갱신**

기존 "착수 상태(2026-07-06)" 블록 아래에 한 줄 추가:
```markdown
> **착수 상태(2026-07-07)**: 여기에 더해 **디스크 영속화**(`CampaignSave` + Unity `CampaignSaveSystem`, 별도 파일 `campaign_N.json`)와
> **메타→VN 읽기전용 투영**(`MetaProjection`: LoopCount→변수)이 구현됨. RunState는 방어적 복사로 불변 강화됨.
> `Regress` 내용 로직(계승·편지·진실플래그·미갈)과 캠페인+VN VM 통합 세이브는 **여전히 미구현**. 스펙:
> `docs/superpowers/specs/2026-07-07-vn-engine-sim-persistence-slice-design.md`.
```

- [ ] **Step 3: 커밋**

```bash
git add docs/engine/05-simulation-kernel.md docs/engine/06-loop-and-state.md
git commit -m "docs(sim): reflect campaign persistence + meta->VN projection in engine refs"
```

---

## Self-Review

**1. Spec coverage:**
- 스펙 §1 방어적 복사 → Task 1. ✅
- §2 CampaignSaveData/CampaignSave/CampaignSaveSystem → Task 2(Core 모델·캡처/복원) + Task 4(디스크 IO). ✅
- §3 MetaProjection → Task 3. ✅
- §4 Unity 배선·플레이모드 → Task 4. ✅
- 문서 갱신 → Task 5. ✅
- 비스코프(Regress 내용·통합 세이브)는 어느 태스크도 건드리지 않음 — 의도적. ✅

**2. Placeholder scan:** 모든 코드 스텝에 실제 전체 코드 포함. "적절히"·TBD 없음. Task 4 씬 배선만 수동(불가피 — UnityMCP 씬 편집은 구현자가 수행). ✅

**3. Type consistency:** `RunState(int, IReadOnlyDictionary<string,int>)`, `MetaState(int)`, `CampaignState(MetaState, RunState)`, `CampaignSaveData{version,loopCount,day,resources}`, `ResEntry{id,value}`, `CampaignSave.Capture/Restore`, `MetaProjection.Project(MetaState,GameState,string)`, `CampaignSaveSystem.Write/Read/Exists/Delete`, `VnValue.Int`, `GameState.Set/Get`, `SeededRandom` — 태스크 간 시그니처 일치. ✅
