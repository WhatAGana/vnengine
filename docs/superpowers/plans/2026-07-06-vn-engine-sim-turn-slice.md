# VN 엔진 — 시뮬 커널 첫 수직 슬라이스 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 커맨드 버튼을 누르면 자원 수치가 변하고 주차(週)가 +1 되는 최소 턴 루프를, 순수 C# 커널 + ScriptableObject 어댑터 + 클릭 UI 로 end-to-end 구현한다.

**Architecture:** 기존 엔진의 `Core/`(UnityEngine 비의존) ↔ `Unity/`(어댑터) 분리 규율을 미러링. 순수 C# 턴 커널(`Core/Sim/`)이 규칙을 전부 소유하고, ScriptableObject 어댑터(`Unity/Sim/`)가 테마 데이터를 순수 def로 변환해 커널을 구동한다. 값은 int, 커맨드 효과는 평면 델타.

**Tech Stack:** C# (순수, `VNEngine.Core` asmdef), Unity (MonoBehaviour/ScriptableObject, `VNEngine.Unity` asmdef, TextMeshPro + UnityEngine.UI), NUnit EditMode 테스트(`VNEngine.Tests` asmdef).

## Global Constraints

- `Core/Sim/` 코드는 **UnityEngine·System.IO 참조 금지** (`VNEngine.Core.asmdef` 는 `noEngineReferences: true`). 순수 C# 만.
- 순수 C# 코드 네임스페이스: `VNEngine`. Unity 어댑터 네임스페이스: `VNEngine.Unity`.
- 예외는 기존 `VnRuntimeException`(네임스페이스 `VNEngine`, `VnException` 하위) 재사용. 새 예외 타입 만들지 않음.
- 테스트는 `Assets/Tests/Editor/` 아래, 네임스페이스 `VNEngine.Tests`, NUnit `[Test]`.
- 값은 정수(int). 클램프·최소값·파산·수식 효과·VN 접합·save/load 통합은 이 슬라이스 밖 — 넣지 않는다(YAGNI).
- **EditMode 테스트 실행**: UnityMCP `run_tests` (mode `EditMode`, 어셈블리 `VNEngine.Tests`). 새 `.cs` 파일 추가 후에는 UnityMCP `refresh_unity` (scope `all`)로 `.meta` 생성 + 도메인 리로드하고, `read_console` 로 컴파일 에러 0 확인 후 테스트를 돌린다(스크립트만 refresh 하면 false-green 위험).

---

### Task 1: 순수 커널 데이터 타입 + TurnEngine 생성·검증·초기상태

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/ResourceDef.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/ResourceDelta.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/CommandDef.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/SimState.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/TurnEngine.cs`
- Test: `Assets/Tests/Editor/TurnEngineTests.cs`

**Interfaces:**
- Consumes: `VnRuntimeException(string)` — 기존 `Assets/Scripts/VNEngine/Core/Errors/VnException.cs`.
- Produces:
  - `ResourceDef(string id, string displayName, int startValue)` — 프로퍼티 `Id`, `DisplayName`, `StartValue`.
  - `ResourceDelta(string resourceId, int amount)` — readonly struct, 프로퍼티 `ResourceId`, `Amount`.
  - `CommandDef(string id, string displayName, IReadOnlyList<ResourceDelta> effects)` — 프로퍼티 `Id`, `DisplayName`, `Effects`.
  - `SimState(int week, IReadOnlyDictionary<string,int> resources)` — 프로퍼티 `Week`, `Resources`.
  - `TurnEngine(IReadOnlyList<ResourceDef> resources, IReadOnlyList<CommandDef> commands)` — 프로퍼티 `IReadOnlyList<ResourceDef> Resources`, `IReadOnlyList<CommandDef> Commands`; 메서드 `SimState CreateInitialState()`.

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/Tests/Editor/TurnEngineTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class TurnEngineTests
    {
        private static ResourceDef Money(int start = 100) => new ResourceDef("money", "재보", start);
        private static ResourceDef Magic(int start = 50) => new ResourceDef("magic", "마력", start);

        private static CommandDef Raid() => new CommandDef("raid", "약탈", new List<ResourceDelta>
        {
            new ResourceDelta("money", 50),
            new ResourceDelta("magic", -20),
        });

        private static TurnEngine Engine() => new TurnEngine(
            new List<ResourceDef> { Money(), Magic() },
            new List<CommandDef> { Raid() });

        [Test]
        public void CreateInitialStateUsesStartValuesAndWeekOne()
        {
            var state = Engine().CreateInitialState();
            Assert.AreEqual(1, state.Week);
            Assert.AreEqual(100, state.Resources["money"]);
            Assert.AreEqual(50, state.Resources["magic"]);
        }

        [Test]
        public void DuplicateResourceIdThrows()
        {
            Assert.Throws<VnRuntimeException>(() => new TurnEngine(
                new List<ResourceDef> { Money(), Money() },
                new List<CommandDef>()));
        }

        [Test]
        public void DuplicateCommandIdThrows()
        {
            Assert.Throws<VnRuntimeException>(() => new TurnEngine(
                new List<ResourceDef> { Money(), Magic() },
                new List<CommandDef> { Raid(), Raid() }));
        }

        [Test]
        public void CommandReferencingUndefinedResourceThrows()
        {
            var bad = new CommandDef("bad", "나쁨", new List<ResourceDelta>
            {
                new ResourceDelta("gold", 10), // "gold" 는 정의 안 됨
            });
            Assert.Throws<VnRuntimeException>(() => new TurnEngine(
                new List<ResourceDef> { Money() },
                new List<CommandDef> { bad }));
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패(컴파일 에러)하는지 확인**

UnityMCP `refresh_unity` (scope `all`) → `read_console`: `TurnEngine` / `ResourceDef` 등 미정의로 컴파일 실패 예상. (컴파일이 깨진 상태라 `run_tests` 는 아직 못 돌린다 — 다음 스텝에서 타입을 만들면 실행 가능.)

- [ ] **Step 3: 데이터 타입 구현**

`Assets/Scripts/VNEngine/Core/Sim/ResourceDef.cs`:

```csharp
namespace VNEngine
{
    public sealed class ResourceDef
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int StartValue { get; }

        public ResourceDef(string id, string displayName, int startValue)
        {
            Id = id;
            DisplayName = displayName;
            StartValue = startValue;
        }
    }
}
```

`Assets/Scripts/VNEngine/Core/Sim/ResourceDelta.cs`:

```csharp
namespace VNEngine
{
    public readonly struct ResourceDelta
    {
        public string ResourceId { get; }
        public int Amount { get; }

        public ResourceDelta(string resourceId, int amount)
        {
            ResourceId = resourceId;
            Amount = amount;
        }
    }
}
```

`Assets/Scripts/VNEngine/Core/Sim/CommandDef.cs`:

```csharp
using System.Collections.Generic;

namespace VNEngine
{
    public sealed class CommandDef
    {
        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<ResourceDelta> Effects { get; }

        public CommandDef(string id, string displayName, IReadOnlyList<ResourceDelta> effects)
        {
            Id = id;
            DisplayName = displayName;
            Effects = effects ?? new List<ResourceDelta>();
        }
    }
}
```

`Assets/Scripts/VNEngine/Core/Sim/SimState.cs`:

```csharp
using System.Collections.Generic;

namespace VNEngine
{
    public sealed class SimState
    {
        public int Week { get; }
        public IReadOnlyDictionary<string, int> Resources { get; }

        public SimState(int week, IReadOnlyDictionary<string, int> resources)
        {
            Week = week;
            Resources = resources;
        }
    }
}
```

- [ ] **Step 4: TurnEngine 구현 (생성자 검증 + CreateInitialState)**

`Assets/Scripts/VNEngine/Core/Sim/TurnEngine.cs`:

```csharp
using System.Collections.Generic;

namespace VNEngine
{
    public sealed class TurnEngine
    {
        private readonly List<ResourceDef> _resources = new List<ResourceDef>();
        private readonly List<CommandDef> _commandList = new List<CommandDef>();
        private readonly Dictionary<string, CommandDef> _commands = new Dictionary<string, CommandDef>();

        public IReadOnlyList<ResourceDef> Resources => _resources;
        public IReadOnlyList<CommandDef> Commands => _commandList;

        public TurnEngine(IReadOnlyList<ResourceDef> resources, IReadOnlyList<CommandDef> commands)
        {
            if (resources == null) throw new System.ArgumentNullException(nameof(resources));
            if (commands == null) throw new System.ArgumentNullException(nameof(commands));

            var resourceIds = new HashSet<string>();
            foreach (var r in resources)
            {
                if (!resourceIds.Add(r.Id))
                    throw new VnRuntimeException($"Duplicate resource id: {r.Id}");
                _resources.Add(r);
            }

            foreach (var c in commands)
            {
                if (_commands.ContainsKey(c.Id))
                    throw new VnRuntimeException($"Duplicate command id: {c.Id}");
                foreach (var e in c.Effects)
                {
                    if (!resourceIds.Contains(e.ResourceId))
                        throw new VnRuntimeException(
                            $"Command '{c.Id}' references undefined resource: {e.ResourceId}");
                }
                _commands.Add(c.Id, c);
                _commandList.Add(c);
            }
        }

        public SimState CreateInitialState()
        {
            var res = new Dictionary<string, int>();
            foreach (var r in _resources)
                res[r.Id] = r.StartValue;
            return new SimState(1, res);
        }
    }
}
```

- [ ] **Step 5: 테스트 통과 확인**

UnityMCP `refresh_unity` (scope `all`) → `read_console` (컴파일 에러 0) → `run_tests` (EditMode, 어셈블리 `VNEngine.Tests`, 클래스 `TurnEngineTests`).
Expected: 4개 테스트 PASS (`CreateInitialStateUsesStartValuesAndWeekOne`, `DuplicateResourceIdThrows`, `DuplicateCommandIdThrows`, `CommandReferencingUndefinedResourceThrows`).

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/VNEngine/Core/Sim/ Assets/Tests/Editor/TurnEngineTests.cs
git commit -m "feat(sim): TurnEngine data types + constructor validation + initial state"
```

(주의: `.meta` 파일도 함께 스테이징 — `refresh_unity` 가 생성한 `Core/Sim/*.cs.meta`, `Core/Sim.meta`, `TurnEngineTests.cs.meta` 포함.)

---

### Task 2: TurnEngine.ExecuteCommand

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Sim/TurnEngine.cs`
- Test: `Assets/Tests/Editor/TurnEngineTests.cs` (테스트 추가)

**Interfaces:**
- Consumes: Task 1의 `TurnEngine`, `SimState`, `CommandDef`, `ResourceDelta`.
- Produces: `SimState TurnEngine.ExecuteCommand(SimState state, string commandId)` — 델타 적용된 새 `SimState`(Week+1) 반환. 입력 `state` 불변. 미정의 commandId → `VnRuntimeException`.

- [ ] **Step 1: 실패하는 테스트 추가**

`Assets/Tests/Editor/TurnEngineTests.cs` 의 `TurnEngineTests` 클래스 안에 추가:

```csharp
        [Test]
        public void ExecuteCommandAppliesDeltasAndAdvancesWeek()
        {
            var engine = Engine();
            var next = engine.ExecuteCommand(engine.CreateInitialState(), "raid");
            Assert.AreEqual(2, next.Week);
            Assert.AreEqual(150, next.Resources["money"]); // 100 + 50
            Assert.AreEqual(30, next.Resources["magic"]);  // 50 - 20
        }

        [Test]
        public void ExecuteCommandDoesNotMutateInputState()
        {
            var engine = Engine();
            var initial = engine.CreateInitialState();
            engine.ExecuteCommand(initial, "raid");
            Assert.AreEqual(1, initial.Week);
            Assert.AreEqual(100, initial.Resources["money"]);
            Assert.AreEqual(50, initial.Resources["magic"]);
        }

        [Test]
        public void ExecuteCommandLeavesUntouchedResourcesUnchanged()
        {
            // rest 는 magic 만 건드림 → money 는 그대로여야 한다
            var rest = new CommandDef("rest", "휴식", new List<ResourceDelta>
            {
                new ResourceDelta("magic", 30),
            });
            var engine = new TurnEngine(
                new List<ResourceDef> { Money(100), Magic(50) },
                new List<CommandDef> { rest });
            var next = engine.ExecuteCommand(engine.CreateInitialState(), "rest");
            Assert.AreEqual(100, next.Resources["money"]); // 안 건드린 자원 유지
            Assert.AreEqual(80, next.Resources["magic"]);  // 50 + 30
        }

        [Test]
        public void ExecuteCommandAllowsNegativeValuesNoClamp()
        {
            var drain = new CommandDef("drain", "고갈", new List<ResourceDelta>
            {
                new ResourceDelta("magic", -100),
            });
            var engine = new TurnEngine(
                new List<ResourceDef> { Money(), Magic(50) },
                new List<CommandDef> { drain });
            var next = engine.ExecuteCommand(engine.CreateInitialState(), "drain");
            Assert.AreEqual(-50, next.Resources["magic"]); // 클램프 없음
        }

        [Test]
        public void ExecuteCommandUnknownCommandThrows()
        {
            var engine = Engine();
            var initial = engine.CreateInitialState();
            Assert.Throws<VnRuntimeException>(() => engine.ExecuteCommand(initial, "nope"));
        }
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

UnityMCP `refresh_unity` (scope `all`) → `read_console` → `run_tests` (EditMode, 클래스 `TurnEngineTests`).
Expected: 새 5개 테스트가 컴파일 에러(`ExecuteCommand` 미정의)로 FAIL.

- [ ] **Step 3: ExecuteCommand 구현**

`TurnEngine` 클래스 안, `CreateInitialState()` 아래에 추가:

```csharp
        public SimState ExecuteCommand(SimState state, string commandId)
        {
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (!_commands.TryGetValue(commandId, out var cmd))
                throw new VnRuntimeException($"Unknown command: {commandId}");

            var res = new Dictionary<string, int>(state.Resources.Count);
            foreach (var kv in state.Resources)
                res[kv.Key] = kv.Value;

            foreach (var e in cmd.Effects)
                res[e.ResourceId] = (res.TryGetValue(e.ResourceId, out var cur) ? cur : 0) + e.Amount;

            return new SimState(state.Week + 1, res);
        }
```

- [ ] **Step 4: 테스트 통과 확인**

UnityMCP `refresh_unity` (scope `all`) → `read_console` (에러 0) → `run_tests` (EditMode, 클래스 `TurnEngineTests`).
Expected: `TurnEngineTests` 전체 9개 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/VNEngine/Core/Sim/TurnEngine.cs Assets/Tests/Editor/TurnEngineTests.cs
git commit -m "feat(sim): TurnEngine.ExecuteCommand (pure delta apply + week advance)"
```

---

### Task 3: ScriptableObject 어댑터 (ResourceDefinitionSO / CommandDefinitionSO)

**Files:**
- Create: `Assets/Scripts/VNEngine/Unity/Sim/ResourceDefinitionSO.cs`
- Create: `Assets/Scripts/VNEngine/Unity/Sim/CommandDefinitionSO.cs`
- Test: `Assets/Tests/Editor/SimDefinitionSOTests.cs`

**Interfaces:**
- Consumes: Task 1의 `ResourceDef`, `CommandDef`, `ResourceDelta`.
- Produces:
  - `VNEngine.Unity.ResourceDefinitionSO` : `ScriptableObject` — 필드 `string id`, `string displayName`, `int startValue`; 메서드 `ResourceDef ToDef()`.
  - `VNEngine.Unity.CommandDefinitionSO` : `ScriptableObject` — 필드 `string id`, `string displayName`, `List<CommandDefinitionSO.Effect> effects`(각 `Effect`: `string resourceId`, `int amount`); 메서드 `CommandDef ToDef()`.

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/Tests/Editor/SimDefinitionSOTests.cs`:

```csharp
using UnityEngine;
using NUnit.Framework;
using VNEngine.Unity;

namespace VNEngine.Tests
{
    public class SimDefinitionSOTests
    {
        [Test]
        public void ResourceDefinitionToDefMapsFields()
        {
            var so = ScriptableObject.CreateInstance<ResourceDefinitionSO>();
            so.id = "money";
            so.displayName = "재보";
            so.startValue = 100;

            ResourceDef def = so.ToDef();

            Assert.AreEqual("money", def.Id);
            Assert.AreEqual("재보", def.DisplayName);
            Assert.AreEqual(100, def.StartValue);

            Object.DestroyImmediate(so);
        }

        [Test]
        public void CommandDefinitionToDefMapsEffects()
        {
            var so = ScriptableObject.CreateInstance<CommandDefinitionSO>();
            so.id = "raid";
            so.displayName = "약탈";
            so.effects.Add(new CommandDefinitionSO.Effect { resourceId = "money", amount = 50 });
            so.effects.Add(new CommandDefinitionSO.Effect { resourceId = "magic", amount = -20 });

            CommandDef def = so.ToDef();

            Assert.AreEqual("raid", def.Id);
            Assert.AreEqual("약탈", def.DisplayName);
            Assert.AreEqual(2, def.Effects.Count);
            Assert.AreEqual("money", def.Effects[0].ResourceId);
            Assert.AreEqual(50, def.Effects[0].Amount);
            Assert.AreEqual("magic", def.Effects[1].ResourceId);
            Assert.AreEqual(-20, def.Effects[1].Amount);

            Object.DestroyImmediate(so);
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

UnityMCP `refresh_unity` (scope `all`) → `read_console`: `ResourceDefinitionSO` / `CommandDefinitionSO` 미정의로 컴파일 실패 예상.

- [ ] **Step 3: SO 어댑터 구현**

`Assets/Scripts/VNEngine/Unity/Sim/ResourceDefinitionSO.cs`:

```csharp
using UnityEngine;

namespace VNEngine.Unity
{
    [CreateAssetMenu(fileName = "Resource", menuName = "VNEngine/Sim/Resource Definition")]
    public sealed class ResourceDefinitionSO : ScriptableObject
    {
        public string id;
        public string displayName;
        public int startValue;

        public ResourceDef ToDef() => new ResourceDef(id, displayName, startValue);
    }
}
```

`Assets/Scripts/VNEngine/Unity/Sim/CommandDefinitionSO.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace VNEngine.Unity
{
    [CreateAssetMenu(fileName = "Command", menuName = "VNEngine/Sim/Command Definition")]
    public sealed class CommandDefinitionSO : ScriptableObject
    {
        [System.Serializable]
        public struct Effect
        {
            public string resourceId;
            public int amount;
        }

        public string id;
        public string displayName;
        public List<Effect> effects = new List<Effect>();

        public CommandDef ToDef()
        {
            var deltas = new List<ResourceDelta>(effects.Count);
            foreach (var e in effects)
                deltas.Add(new ResourceDelta(e.resourceId, e.amount));
            return new CommandDef(id, displayName, deltas);
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

UnityMCP `refresh_unity` (scope `all`) → `read_console` (에러 0) → `run_tests` (EditMode, 클래스 `SimDefinitionSOTests`).
Expected: 2개 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/VNEngine/Unity/Sim/ Assets/Tests/Editor/SimDefinitionSOTests.cs
git commit -m "feat(sim): ScriptableObject adapters for resource/command definitions"
```

---

### Task 4: SimController + 최소 UI + SO 에셋 + 라이브 검증

**Files:**
- Create: `Assets/Scripts/VNEngine/Unity/Sim/SimController.cs`
- Create: SO 에셋 — `Assets/Sim/Resources/Money.asset`, `Assets/Sim/Resources/Magic.asset`, `Assets/Sim/Commands/Raid.asset`, `Assets/Sim/Commands/Rest.asset`, `Assets/Sim/Commands/Build.asset`
- Create/Modify: 테스트용 씬 `Assets/Scenes/SimSlice.unity` (신규 씬 — 기존 VN 씬과 분리) 또는 `Main.unity` 에 임시 패널. **신규 씬 권장**(기존 VN 배선과 이질감/충돌 방지).

**Interfaces:**
- Consumes: Task 1의 `TurnEngine`; Task 3의 `ResourceDefinitionSO`, `CommandDefinitionSO`.
- Produces: `VNEngine.Unity.SimController` : `MonoBehaviour` — 직렬화 필드 `List<ResourceDefinitionSO> resources`, `List<CommandDefinitionSO> commands`, `TMP_Text statusText`, `Transform buttonContainer`, `Button buttonPrefab`.

- [ ] **Step 1: SimController 구현**

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

        private TurnEngine _engine;
        private SimState _state;

        private void Start()
        {
            var resDefs = new List<ResourceDef>(resources.Count);
            foreach (var r in resources) resDefs.Add(r.ToDef());

            var cmdDefs = new List<CommandDef>(commands.Count);
            foreach (var c in commands) cmdDefs.Add(c.ToDef());

            _engine = new TurnEngine(resDefs, cmdDefs); // 배선 오류면 여기서 VnRuntimeException → 콘솔 에러
            _state = _engine.CreateInitialState();

            BuildButtons();
            Refresh();
        }

        private void BuildButtons()
        {
            if (buttonPrefab == null || buttonContainer == null)
            {
                Debug.LogError("[SimController] buttonPrefab or buttonContainer not assigned");
                return;
            }
            foreach (var c in _engine.Commands)
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
            _state = _engine.ExecuteCommand(_state, commandId);
            Refresh();
        }

        private void Refresh()
        {
            if (statusText == null) return;
            var sb = new StringBuilder();
            sb.Append("주차: ").Append(_state.Week);
            foreach (var r in _engine.Resources)
                sb.Append("    ").Append(r.DisplayName).Append(": ").Append(_state.Resources[r.Id]);
            statusText.text = sb.ToString();
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

UnityMCP `refresh_unity` (scope `all`) → `read_console`: 컴파일 에러 0 확인.

- [ ] **Step 3: SO 에셋 생성**

UnityMCP `manage_asset`(또는 `execute_code` 로 `ScriptableObject.CreateInstance` + `AssetDatabase.CreateAsset`)로 아래 값의 에셋 생성:

- `Assets/Sim/Resources/Money.asset` (`ResourceDefinitionSO`): id `money`, displayName `재보`, startValue `100`
- `Assets/Sim/Resources/Magic.asset` (`ResourceDefinitionSO`): id `magic`, displayName `마력`, startValue `50`
- `Assets/Sim/Commands/Raid.asset` (`CommandDefinitionSO`): id `raid`, displayName `약탈`, effects `[{money,+50},{magic,-20}]`
- `Assets/Sim/Commands/Rest.asset` (`CommandDefinitionSO`): id `rest`, displayName `휴식`, effects `[{magic,+30}]`
- `Assets/Sim/Commands/Build.asset` (`CommandDefinitionSO`): id `build`, displayName `건설`, effects `[{money,-40}]`

- [ ] **Step 4: 씬 + UI 배선**

신규 씬 `Assets/Scenes/SimSlice.unity` 생성 (Camera + Directional Light 포함). 그 안에:
- Canvas (Screen Space - Overlay) + EventSystem
- `statusText`: TMP_Text 하나 (상단, 폰트는 기존 `NotoSansKR-Regular SDF` 재사용)
- `buttonContainer`: Horizontal Layout Group 붙은 빈 Panel
- `buttonPrefab`: Button + 자식 TMP_Text 를 가진 프리팹 (`Assets/Sim/CommandButton.prefab`)
- 빈 GameObject `SimController` 에 `SimController` 컴포넌트 부착 → 인스펙터에 resources[Money,Magic], commands[Raid,Rest,Build], statusText, buttonContainer, buttonPrefab 배선.
씬 저장.

- [ ] **Step 5: 라이브 검증 (playmode)**

UnityMCP `manage_editor` 로 `SimSlice` 씬 로드 → Play 진입 → `read_console` 에러 0 확인. 초기 표시가 `주차: 1    재보: 100    마력: 50` 인지 확인. `약탈` 버튼 onClick 호출(또는 UI 클릭 시뮬) → 표시가 `주차: 2    재보: 150    마력: 30` 로 바뀌는지 확인. `휴식` → `주차: 3 ... 마력: 60`. Play 종료.
Expected: 콘솔 에러 0, 위 수치 전이 정확.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/VNEngine/Unity/Sim/SimController.cs Assets/Sim/ Assets/Scenes/SimSlice.unity
git commit -m "feat(sim): SimController + minimal turn UI wired to SO-driven kernel"
```

(주의: `.meta` 파일 전부 스테이징 — `Assets/Sim/**.meta`, 씬 `.meta`, 스크립트 `.meta`.)

---

## Self-Review

**Spec coverage:**
- 데이터 모델(ResourceDef/CommandDef/ResourceDelta/SimState) → Task 1 ✓
- TurnEngine 생성·CreateInitialState·ExecuteCommand → Task 1, 2 ✓
- 에러 처리(생성자 검증 3종 + 미정의 command) → Task 1(3종), Task 2(command) ✓
- SO 어댑터 ToDef → Task 3 ✓
- SimController + 최소 UI + SO 에셋 + 라이브 검증 → Task 4 ✓
- 테스트 11개(스펙 목록) → TurnEngineTests 9 + SimDefinitionSOTests 2 ✓
- 슬라이스 밖 항목(클램프/파산/수식/VN접합/save통합/디펜스) → 어느 태스크에도 없음 ✓ (의도적)

**Placeholder scan:** 모든 코드 스텝에 실제 구현 코드 포함, "TBD/적절히 처리" 없음 ✓

**Type consistency:** `ResourceDef.Id/DisplayName/StartValue`, `ResourceDelta.ResourceId/Amount`, `CommandDef.Id/DisplayName/Effects`, `SimState.Week/Resources`, `TurnEngine.Resources/Commands/CreateInitialState/ExecuteCommand` — Task 1→2→3→4 전반에서 동일 시그니처 사용 확인 ✓. `CommandDefinitionSO.Effect.resourceId/amount` → `ToDef` 에서 `ResourceDelta(resourceId, amount)` 매핑 일치 ✓.
