# 회차 루프: 상태 분리 슬라이스 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 평면 `SimState`를 Run/Meta 두 층으로 분리하고, 최소 회차 전이(`StartNewLoop`)로 그 분리가 실제로 작동함을 실증한다.

**Architecture:** `TurnEngine`은 "한 회차 안의 규칙"만 유지(`SimState→RunState`, `Week→Day` 개명). 새 `LoopEngine`이 캠페인 층 파사드로 `TurnEngine`을 소유하고, 모든 전이가 불변 `CampaignState{ Meta, Run }`를 반환한다. `SimController`는 `LoopEngine` + `CampaignState`만 구동한다.

**Tech Stack:** Unity 2022.3 / C# / NUnit(EditMode, `VNEngine.Tests`) / UnityMCP(컴파일·테스트·플레이모드).

## Global Constraints

- `Core/**`는 `UnityEngine`·`System.IO` 참조 금지 — 순수 C#만.
- 모든 전이 함수는 **입력을 변형하지 않고 새 인스턴스를 반환**(불변).
- 검증 실패는 기존 `VnRuntimeException` 재사용(새 예외 타입 만들지 않음).
- 새 `.cs` 추가/삭제 후에는 `refresh_unity` **scope:"all"** 로 도메인 리로드(scope:"scripts"는 .meta 미생성으로 false-green).
- 테스트는 UnityMCP `run_tests` **EditMode**, 어셈블리 `VNEngine.Tests`.
- 스펙: `docs/superpowers/specs/2026-07-06-vn-engine-sim-loop-state-slice-design.md`.

---

## 파일 구조

| 파일 | 책임 | 상태 |
|---|---|---|
| `Assets/Scripts/VNEngine/Core/Sim/RunState.cs` | 매 회차 리셋되는 상태(Day + Resources) | 신규(SimState 대체) |
| `Assets/Scripts/VNEngine/Core/Sim/SimState.cs` | (구) 평면 상태 | 삭제 |
| `Assets/Scripts/VNEngine/Core/Sim/MetaState.cs` | 회차 넘어 유지(LoopCount) | 신규 |
| `Assets/Scripts/VNEngine/Core/Sim/CampaignState.cs` | { Meta, Run } 최상위 상태 | 신규 |
| `Assets/Scripts/VNEngine/Core/Sim/LoopEngine.cs` | 캠페인 층 파사드(TurnEngine 소유) | 신규 |
| `Assets/Scripts/VNEngine/Core/Sim/TurnEngine.cs` | 회차 내 규칙(RunState/Day 반영) | 수정 |
| `Assets/Tests/Editor/TurnEngineTests.cs` | 기존 9 테스트(Week→Day) | 수정 |
| `Assets/Tests/Editor/LoopEngineTests.cs` | LoopEngine 7 테스트 | 신규 |
| `Assets/Scripts/VNEngine/Unity/Sim/SimController.cs` | LoopEngine 구동 + "새 회차" 버튼 | 수정 |
| `docs/engine/05-simulation-kernel.md` | Week→Day, Run/Meta 반영 | 수정 |
| `docs/engine/06-loop-and-state.md` | "얇은 버전 착수" 상태 반영 | 수정 |

---

### Task 1: `SimState → RunState` 개명 (Week → Day)

`TurnEngine`의 규칙은 그대로 두고, 상태 타입과 시간 축 용어만 바꾼다. 순수 리팩터 — 동작 불변.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/RunState.cs`
- Delete: `Assets/Scripts/VNEngine/Core/Sim/SimState.cs` (+ `.meta`)
- Modify: `Assets/Scripts/VNEngine/Core/Sim/TurnEngine.cs`
- Test: `Assets/Tests/Editor/TurnEngineTests.cs`

**Interfaces:**
- Produces:
  - `RunState(int day, IReadOnlyDictionary<string,int> resources)` — 프로퍼티 `int Day`, `IReadOnlyDictionary<string,int> Resources`.
  - `TurnEngine.CreateInitialState() → RunState` (Day=1, 자원 StartValue).
  - `TurnEngine.ExecuteCommand(RunState state, string commandId) → RunState` (델타 적용, Day+1).
  - `TurnEngine.Resources`, `TurnEngine.Commands` (변경 없음).

- [ ] **Step 1: `RunState.cs` 생성**

`Assets/Scripts/VNEngine/Core/Sim/RunState.cs`:
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
            Resources = resources;
        }
    }
}
```

- [ ] **Step 2: `SimState.cs` 삭제**

`Assets/Scripts/VNEngine/Core/Sim/SimState.cs` 와 `SimState.cs.meta` 를 삭제.

- [ ] **Step 3: `TurnEngine.cs` 를 RunState/Day 로 수정**

`Assets/Scripts/VNEngine/Core/Sim/TurnEngine.cs` 의 `CreateInitialState`·`ExecuteCommand` 두 메서드를 아래로 교체(나머지 — 생성자·검증·필드 — 는 그대로):
```csharp
        public RunState CreateInitialState()
        {
            var res = new Dictionary<string, int>();
            foreach (var r in _resources)
                res[r.Id] = r.StartValue;
            return new RunState(1, res);
        }

        public RunState ExecuteCommand(RunState state, string commandId)
        {
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (!_commands.TryGetValue(commandId, out var cmd))
                throw new VnRuntimeException($"Unknown command: {commandId}");

            var res = new Dictionary<string, int>(state.Resources.Count);
            foreach (var kv in state.Resources)
                res[kv.Key] = kv.Value;

            foreach (var e in cmd.Effects)
                res[e.ResourceId] = (res.TryGetValue(e.ResourceId, out var cur) ? cur : 0) + e.Amount;

            return new RunState(state.Day + 1, res);
        }
```

- [ ] **Step 4: `TurnEngineTests.cs` 의 Week 단정을 Day 로 교체**

`Assets/Tests/Editor/TurnEngineTests.cs` 에서 세 곳을 수정:
- 테스트 `CreateInitialStateUsesStartValuesAndWeekOne` → 이름 `CreateInitialStateUsesStartValuesAndDayOne`, 본문 `Assert.AreEqual(1, state.Week);` → `Assert.AreEqual(1, state.Day);`
- 테스트 `ExecuteCommandAppliesDeltasAndAdvancesWeek` → 이름 `ExecuteCommandAppliesDeltasAndAdvancesDay`, 본문 `Assert.AreEqual(2, next.Week);` → `Assert.AreEqual(2, next.Day);`
- 테스트 `ExecuteCommandDoesNotMutateInputState` → 본문 `Assert.AreEqual(1, initial.Week);` → `Assert.AreEqual(1, initial.Day);`

- [ ] **Step 5: 도메인 리로드 + 테스트**

UnityMCP: `refresh_unity` scope:"all" → `read_console`(에러 0 확인) → `run_tests` EditMode 어셈블리 `VNEngine.Tests`.
Expected: `TurnEngineTests` 9개 PASS, 컴파일 에러 0. (SimState 참조가 남아 있으면 컴파일 에러로 드러남 → 마저 교체.)

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/VNEngine/Core/Sim/ Assets/Tests/Editor/TurnEngineTests.cs
git commit -m "refactor(sim): rename SimState->RunState, Week->Day"
```

---

### Task 2: `MetaState` · `CampaignState` · `LoopEngine` (TDD)

두 층 컨테이너와 캠페인 파사드를 테스트 우선으로 만든다.

**Files:**
- Test: `Assets/Tests/Editor/LoopEngineTests.cs` (신규)
- Create: `Assets/Scripts/VNEngine/Core/Sim/MetaState.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/CampaignState.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/LoopEngine.cs`

**Interfaces:**
- Consumes (Task 1): `RunState.Day`, `RunState.Resources`, `TurnEngine.CreateInitialState()`, `TurnEngine.ExecuteCommand(RunState,string)`.
- Produces:
  - `MetaState(int loopCount)` — 프로퍼티 `int LoopCount`.
  - `CampaignState(MetaState meta, RunState run)` — 프로퍼티 `MetaState Meta`, `RunState Run`.
  - `LoopEngine(TurnEngine turnEngine)` with:
    - `CampaignState CreateInitialCampaign()`
    - `CampaignState ExecuteCommand(CampaignState campaign, string commandId)`
    - `CampaignState StartNewLoop(CampaignState campaign)`

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/Tests/Editor/LoopEngineTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class LoopEngineTests
    {
        private static ResourceDef Money(int start = 100) => new ResourceDef("money", "재보", start);
        private static ResourceDef Magic(int start = 50) => new ResourceDef("magic", "마력", start);

        private static CommandDef Raid() => new CommandDef("raid", "약탈", new List<ResourceDelta>
        {
            new ResourceDelta("money", 50),
            new ResourceDelta("magic", -20),
        });

        private static LoopEngine Engine() => new LoopEngine(new TurnEngine(
            new List<ResourceDef> { Money(), Magic() },
            new List<CommandDef> { Raid() }));

        [Test]
        public void CreateInitialCampaignStartsAtLoopOneDayOne()
        {
            var c = Engine().CreateInitialCampaign();
            Assert.AreEqual(1, c.Meta.LoopCount);
            Assert.AreEqual(1, c.Run.Day);
            Assert.AreEqual(100, c.Run.Resources["money"]);
            Assert.AreEqual(50, c.Run.Resources["magic"]);
        }

        [Test]
        public void ExecuteCommandAdvancesRunAndLeavesMetaUntouched()
        {
            var engine = Engine();
            var next = engine.ExecuteCommand(engine.CreateInitialCampaign(), "raid");
            Assert.AreEqual(2, next.Run.Day);
            Assert.AreEqual(150, next.Run.Resources["money"]);
            Assert.AreEqual(30, next.Run.Resources["magic"]);
            Assert.AreEqual(1, next.Meta.LoopCount); // Meta 불변
        }

        [Test]
        public void ExecuteCommandDoesNotMutateInput()
        {
            var engine = Engine();
            var initial = engine.CreateInitialCampaign();
            engine.ExecuteCommand(initial, "raid");
            Assert.AreEqual(1, initial.Run.Day);
            Assert.AreEqual(100, initial.Run.Resources["money"]);
            Assert.AreEqual(1, initial.Meta.LoopCount);
        }

        [Test]
        public void StartNewLoopIncrementsLoopAndResetsRun()
        {
            var engine = Engine();
            var c = engine.CreateInitialCampaign();
            c = engine.ExecuteCommand(c, "raid"); // Day=2, money=150
            var looped = engine.StartNewLoop(c);
            Assert.AreEqual(2, looped.Meta.LoopCount);   // +1
            Assert.AreEqual(1, looped.Run.Day);          // 리셋
            Assert.AreEqual(100, looped.Run.Resources["money"]); // 초기값
            Assert.AreEqual(50, looped.Run.Resources["magic"]);
        }

        [Test]
        public void StartNewLoopDoesNotMutateInput()
        {
            var engine = Engine();
            var c = engine.ExecuteCommand(engine.CreateInitialCampaign(), "raid");
            engine.StartNewLoop(c);
            Assert.AreEqual(1, c.Meta.LoopCount); // 입력 불변
            Assert.AreEqual(2, c.Run.Day);
        }

        [Test]
        public void RoundTripKeepsMetaResetsRun()
        {
            var engine = Engine();
            var c = engine.CreateInitialCampaign();
            c = engine.ExecuteCommand(c, "raid");
            c = engine.ExecuteCommand(c, "raid"); // Day=3, money=200
            c = engine.StartNewLoop(c);           // Loop=2, Run 리셋
            c = engine.ExecuteCommand(c, "raid"); // Day=2, money=150
            Assert.AreEqual(2, c.Meta.LoopCount); // 회차는 유지·증가
            Assert.AreEqual(2, c.Run.Day);        // 새 회차 기준
            Assert.AreEqual(150, c.Run.Resources["money"]);
        }

        [Test]
        public void ExecuteCommandUnknownCommandThrows()
        {
            var engine = Engine();
            var c = engine.CreateInitialCampaign();
            Assert.Throws<VnRuntimeException>(() => engine.ExecuteCommand(c, "nope"));
        }
    }
}
```

- [ ] **Step 2: 리로드 후 실패 확인**

UnityMCP: `refresh_unity` scope:"all" → `read_console`.
Expected: 컴파일 에러(`LoopEngine`/`MetaState`/`CampaignState` 미정의). = 테스트가 아직 못 도는 RED 상태.

- [ ] **Step 3: `MetaState.cs` 생성**

`Assets/Scripts/VNEngine/Core/Sim/MetaState.cs`:
```csharp
namespace VNEngine
{
    public sealed class MetaState
    {
        public int LoopCount { get; }

        public MetaState(int loopCount)
        {
            LoopCount = loopCount;
        }
    }
}
```

- [ ] **Step 4: `CampaignState.cs` 생성**

`Assets/Scripts/VNEngine/Core/Sim/CampaignState.cs`:
```csharp
namespace VNEngine
{
    public sealed class CampaignState
    {
        public MetaState Meta { get; }
        public RunState Run { get; }

        public CampaignState(MetaState meta, RunState run)
        {
            Meta = meta;
            Run = run;
        }
    }
}
```

- [ ] **Step 5: `LoopEngine.cs` 생성**

`Assets/Scripts/VNEngine/Core/Sim/LoopEngine.cs`:
```csharp
namespace VNEngine
{
    // 캠페인 층 파사드. TurnEngine(회차 내 규칙)을 소유하고
    // 모든 전이는 새 CampaignState 를 반환한다(불변).
    public sealed class LoopEngine
    {
        private readonly TurnEngine _turnEngine;

        public LoopEngine(TurnEngine turnEngine)
        {
            _turnEngine = turnEngine ?? throw new System.ArgumentNullException(nameof(turnEngine));
        }

        // 캠페인 시작: LoopCount=1, Run=초기 Run(Day=1, 자원 StartValue).
        public CampaignState CreateInitialCampaign()
        {
            return new CampaignState(new MetaState(1), _turnEngine.CreateInitialState());
        }

        // 회차 내 커맨드: Run 만 진행(Day+1, 델타 적용), Meta 는 그대로 통과.
        public CampaignState ExecuteCommand(CampaignState campaign, string commandId)
        {
            if (campaign == null) throw new System.ArgumentNullException(nameof(campaign));
            var newRun = _turnEngine.ExecuteCommand(campaign.Run, commandId);
            return new CampaignState(campaign.Meta, newRun);
        }

        // 회차 전이(최소): LoopCount+1, Run 은 새 초기 Run 으로 리셋.
        // 계승·편지·진실플래그 등 '내용' 갱신은 이후 슬라이스에서 이 함수를 확장(Regress).
        public CampaignState StartNewLoop(CampaignState campaign)
        {
            if (campaign == null) throw new System.ArgumentNullException(nameof(campaign));
            var newMeta = new MetaState(campaign.Meta.LoopCount + 1);
            return new CampaignState(newMeta, _turnEngine.CreateInitialState());
        }
    }
}
```

- [ ] **Step 6: 리로드 + 테스트 GREEN**

UnityMCP: `refresh_unity` scope:"all" → `read_console`(에러 0) → `run_tests` EditMode `VNEngine.Tests`.
Expected: `LoopEngineTests` 7개 PASS + `TurnEngineTests` 9개 PASS(총 회귀 유지).

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/VNEngine/Core/Sim/ Assets/Tests/Editor/LoopEngineTests.cs
git commit -m "feat(sim): CampaignState/MetaState + LoopEngine facade (run/meta split)"
```

---

### Task 3: `SimController` 를 LoopEngine 구동으로 전환 + "새 회차" 버튼

Unity 어댑터를 캠페인 상태로 전환하고, 분리를 눈으로 볼 수 있게 회차 전이 버튼을 추가한다. (커널 테스트는 Task 2가 커버 — 이 태스크는 플레이모드 수동 검증.)

**Files:**
- Modify: `Assets/Scripts/VNEngine/Unity/Sim/SimController.cs`
- (수동) 씬 `Assets/Scenes/SimSlice.unity` 에 "새 회차" 버튼 배치·배선

**Interfaces:**
- Consumes (Task 2): `LoopEngine`, `CampaignState`, `MetaState.LoopCount`, `RunState.Day`, `RunState.Resources`, `TurnEngine.Resources`, `TurnEngine.Commands`.

- [ ] **Step 1: `SimController.cs` 를 아래 전체 내용으로 교체**

`Assets/Scripts/VNEngine/Unity/Sim/SimController.cs`:
```csharp
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VNEngine.Unity
{
    public sealed class SimController : MonoBehaviour
    {
        [Header("Definitions (ScriptableObjects)")]
        [SerializeField] private List<ResourceDefinitionSO> resources = new List<ResourceDefinitionSO>();
        [SerializeField] private List<CommandDefinitionSO> commands = new List<CommandDefinitionSO>();

        [Header("UI")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private Button buttonPrefab;
        [SerializeField] private Button newLoopButton; // "새 회차" — 없으면 무시

        private TurnEngine _turnEngine;
        private LoopEngine _loop;
        private CampaignState _campaign;

        private void Start()
        {
            var resDefs = new List<ResourceDef>(resources.Count);
            foreach (var r in resources) resDefs.Add(r.ToDef());

            var cmdDefs = new List<CommandDef>(commands.Count);
            foreach (var c in commands) cmdDefs.Add(c.ToDef());

            _turnEngine = new TurnEngine(resDefs, cmdDefs); // 배선 오류면 여기서 VnRuntimeException → 콘솔 에러
            _loop = new LoopEngine(_turnEngine);
            _campaign = _loop.CreateInitialCampaign();

            BuildButtons();

            if (newLoopButton != null)
            {
                newLoopButton.onClick.RemoveAllListeners();
                newLoopButton.onClick.AddListener(OnNewLoop);
            }

            Refresh();
        }

        private void BuildButtons()
        {
            if (buttonPrefab == null || buttonContainer == null)
            {
                Debug.LogError("[SimController] buttonPrefab or buttonContainer not assigned");
                return;
            }
            foreach (var c in _turnEngine.Commands)
            {
                string commandId = c.Id; // capture
                Button btn = Instantiate(buttonPrefab, buttonContainer);
                btn.name = $"CommandButton_{c.Id}";
                var tmp = btn.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null) tmp.text = c.DisplayName;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnCommand(commandId));
            }
        }

        private void OnCommand(string commandId)
        {
            _campaign = _loop.ExecuteCommand(_campaign, commandId);
            Refresh();
        }

        private void OnNewLoop()
        {
            _campaign = _loop.StartNewLoop(_campaign);
            Refresh();
        }

        private void Refresh()
        {
            if (statusText == null) return;
            var sb = new StringBuilder();
            sb.Append("회차: ").Append(_campaign.Meta.LoopCount);
            sb.Append("    일차: ").Append(_campaign.Run.Day);
            foreach (var r in _turnEngine.Resources)
                sb.Append("    ").Append(r.DisplayName).Append(": ").Append(_campaign.Run.Resources[r.Id]);
            statusText.text = sb.ToString();
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

UnityMCP: `refresh_unity` scope:"all" → `read_console`.
Expected: 컴파일 에러 0. (테스트는 Task 2가 이미 커버 — 여기선 컴파일만.)

- [ ] **Step 3: 씬에 "새 회차" 버튼 배치·배선 (수동)**

`Assets/Scenes/SimSlice.unity` 열기 → 기존 커맨드 버튼 영역 근처에 UI Button 하나 추가(라벨 "새 회차") → `SimController` 컴포넌트의 `New Loop Button` 슬롯에 드래그. 저장.

- [ ] **Step 4: 플레이모드 수동 검증**

`manage_editor` play 진입 → 상태 라벨이 `회차: 1    일차: 1    재보: 100    마력: 50` 로 시작하는지 확인 →
"약탈" 클릭 → `회차: 1    일차: 2    재보: 150    마력: 30` → "새 회차" 클릭 →
`회차: 2    일차: 1    재보: 100    마력: 50`(자원·일차 리셋, 회차 +1) 확인 → `read_console` 에러 0 → play 종료.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/VNEngine/Unity/Sim/SimController.cs Assets/Scenes/SimSlice.unity
git commit -m "feat(sim): drive SimController via LoopEngine + 새 회차 button"
```

---

### Task 4: 문서 갱신 (05, 06)

구현 사실에 맞춰 엔진 레퍼런스를 갱신한다.

**Files:**
- Modify: `docs/engine/05-simulation-kernel.md`
- Modify: `docs/engine/06-loop-and-state.md`

- [ ] **Step 1: `05-simulation-kernel.md` 갱신**

다음을 반영:
- 코어 모델 표에서 `SimState { Week, Resources }` → `RunState { Day, Resources }` 로 교체하고, `MetaState { LoopCount }` · `CampaignState { Meta, Run }` · `LoopEngine` 행 추가.
- `TurnEngine` 설명의 `CreateInitialState`(Week=1) → (Day=1), `ExecuteCommand`(Week+1) → (Day+1) 로 수정.
- 예제 라이브 검증 문구의 "주차1/주차2/주차3" → "일차" 로, "회차" 개념 한 줄 추가.
- §5(VN 코어와의 관계)·§6(미구현) 앞에 "회차 루프: Run/Meta 상태 분리는 착수됨(얇은 버전) — `StartNewLoop`까지. 디스크 세이브·회귀 내용 로직·디펜스 필드는 미룸" 한 단락 추가.

- [ ] **Step 2: `06-loop-and-state.md` 상단에 상태 노트 추가**

문서 최상단 인용구 아래에 한 줄 추가:
```markdown
> **착수 상태(2026-07-06)**: 이 문서의 **얇은 버전**이 구현됨 — RunState/MetaState/CampaignState 2층 컨테이너 +
> `LoopEngine.StartNewLoop`(LoopCount+1, Run 리셋)까지. `Regress`의 계승·편지·진실플래그·VN 투영, 디스크 세이브,
> RunState 디펜스 필드는 **여전히 미구현**(이 문서의 나머지 = 다음 슬라이스들). 구현 스펙:
> `docs/superpowers/specs/2026-07-06-vn-engine-sim-loop-state-slice-design.md`.
```

- [ ] **Step 3: 커밋**

```bash
git add docs/engine/05-simulation-kernel.md docs/engine/06-loop-and-state.md
git commit -m "docs(sim): reflect run/meta split (Week->Day, LoopEngine) in engine refs"
```

---

## Self-Review 결과

- **스펙 커버리지**: 스펙 §2(파일)→Task1-3, §3(데이터모델)→Task1-2, §4(LoopEngine 전이)→Task2, §5(Unity/UI)→Task3, §6(테스트 7+9)→Task1·2, §8(문서)→Task4. 모든 섹션에 대응 태스크 있음. §7(범위 밖)은 의도적 무구현 — 각 항목이 미룸으로 문서·주석에 명시됨(LoopEngine `StartNewLoop` 주석, Task4).
- **플레이스홀더 스캔**: 코드 스텝은 전부 실제 전체 코드 포함. TBD/‘적절히 처리’ 없음.
- **타입 일관성**: `RunState.Day`/`Resources`, `MetaState.LoopCount`, `CampaignState.Meta/Run`, `LoopEngine.{CreateInitialCampaign,ExecuteCommand,StartNewLoop}`, `TurnEngine.{CreateInitialState,ExecuteCommand,Resources,Commands}` — Task 간 시그니처 일치 확인. `CreateInitialState`는 개명하지 않고 반환형만 RunState로 유지(Task1·2·3 일관).
