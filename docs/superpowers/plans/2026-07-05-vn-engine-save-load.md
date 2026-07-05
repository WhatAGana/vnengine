# VN 엔진 세이브/로드 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 실행 중인 VN의 완전한 상태(변수·진행위치·화면·난수)를 디스크 슬롯에 저장하고 그대로 복원해 이어서 플레이한다.

**Architecture:** P0의 "컴파일 후 실행" 인터프리터에 스냅샷 캡처/복원 API를 추가한다. Core(순수 C#)가 직렬화 친화적 `SaveData`를 산출하고, Unity 계층 `SaveSystem`이 `JsonUtility`로 JSON화해 `Application.persistentDataPath`에 기록한다. 저장은 입력 대기 지점에서만, 로드 시 화면과 난수까지 결정적으로 복원한다.

**Tech Stack:** C# (Unity 2022.3), NUnit EditMode 테스트, UnityMCP(컴파일·테스트 실행). 설계 스펙: `docs/superpowers/specs/2026-07-05-vn-engine-save-load-design.md`.

## Global Constraints

- **Core는 `noEngineReferences: true`** — `Assets/Scripts/VNEngine/Core/**`의 새 코드는 `UnityEngine.*` / `System.IO`를 절대 참조하지 않는다. `[System.Serializable]`(System 네임스페이스)은 허용.
- **모바일 안전** — 런타임 파일 IO는 Unity 계층에서 `Application.persistentDataPath`로만.
- **난수 계약 유지** — `IRandom.Range(min,max)`는 양끝 포함, `max < min`이면 `min` 반환.
- **테스트 실행** — UnityMCP `run_tests`(mode=EditMode, assembly=`VNEngine.Tests`). 각 구현 후 `refresh_unity`로 컴파일 → `read_console`(error 0 확인) → `run_tests` → `get_test_job`.
- **커밋 메시지 꼬리말** (모든 커밋):
  ```
  Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01G5HoHNcCXiyXs1bjhxFCQh
  ```
- **브랜치**: `save-load` (이미 생성됨).

---

### Task 1: 결정적 직렬화 가능 PRNG

`System.Random`(직렬화 불가)을 상태가 `uint` 하나인 xorshift32로 교체하고, `IRandom`에 세이브/복원용 `State`를 노출한다.

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Runtime/IRandom.cs`
- Test: `Assets/Tests/Editor/RandomTests.cs` (create)

**Interfaces:**
- Produces: `IRandom.State { get; set; }` (uint), `SeededRandom(int seed)` (xorshift32, 계약 동일)

- [ ] **Step 1: 실패 테스트 작성** — `Assets/Tests/Editor/RandomTests.cs`

```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class RandomTests
    {
        [Test]
        public void SameSeedSameSequence()
        {
            var a = new SeededRandom(42);
            var b = new SeededRandom(42);
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(a.Range(0, 1000), b.Range(0, 1000));
        }

        [Test]
        public void StateRestoresSequence()
        {
            var a = new SeededRandom(7);
            for (int i = 0; i < 5; i++) a.Range(0, 1000); // advance
            uint snap = a.State;
            var expected = new int[10];
            for (int i = 0; i < 10; i++) expected[i] = a.Range(0, 1000);

            var b = new SeededRandom(999);   // different seed
            b.State = snap;                  // but restored state
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(expected[i], b.Range(0, 1000));
        }

        [Test]
        public void RangeBoundsInclusive()
        {
            var r = new SeededRandom(3);
            for (int i = 0; i < 500; i++)
            {
                int v = r.Range(1, 6);
                Assert.IsTrue(v >= 1 && v <= 6);
            }
            Assert.AreEqual(5, r.Range(5, 5));   // single value
            Assert.AreEqual(3, r.Range(3, 1));   // max < min => min
        }
    }
}
```

- [ ] **Step 2: 컴파일 실패 확인** — `run_tests`(EditMode) 실행. Expected: 컴파일 실패(`IRandom.State` 미정의) 또는 테스트 FAIL.

- [ ] **Step 3: 구현** — `Assets/Scripts/VNEngine/Core/Runtime/IRandom.cs` 전체를 아래로 교체

```csharp
namespace VNEngine
{
    public interface IRandom
    {
        // Inclusive on both ends.
        int Range(int minInclusive, int maxInclusive);

        // Full internal PRNG state, for save/restore. uint so the whole
        // generator round-trips through a single serialized value.
        uint State { get; set; }
    }

    // Deterministic, serializable xorshift32. Its entire state is one uint,
    // so a save just stores State and a load sets it back — the subsequent
    // sequence is then identical.
    public sealed class SeededRandom : IRandom
    {
        private uint _state;

        public SeededRandom(int seed)
        {
            // xorshift must never sit at 0; map a 0 seed to a fixed nonzero.
            _state = unchecked((uint)seed);
            if (_state == 0) _state = 0x9E3779B9u;
        }

        public uint State { get => _state; set => _state = value == 0 ? 0x9E3779B9u : value; }

        private uint Next()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        public int Range(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive) return minInclusive;
            uint span = (uint)(maxInclusive - minInclusive) + 1u; // span>=1
            return minInclusive + (int)(Next() % span);
        }
    }
}
```

- [ ] **Step 4: 통과 확인** — `run_tests`(EditMode). Expected: `RandomTests` 3개 PASS. 전체 스위트도 그린인지 확인(기존 `ExprEvalTests.RandomWithinBounds`는 경계 검사라 여전히 PASS; 값 고정 테스트는 없음).

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/VNEngine/Core/Runtime/IRandom.cs Assets/Tests/Editor/RandomTests.cs Assets/Tests/Editor/RandomTests.cs.meta
git commit -m "feat(vnengine): deterministic serializable PRNG (xorshift32 + IRandom.State)"
```

---

### Task 2: `StageState` — 화면 논리 상태 (Core 순수 클래스)

배경과 슬롯→캐릭터 매핑을 담고, 뷰의 축출 규칙과 동일하게 갱신하는 순수 클래스.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Runtime/StageState.cs`
- Test: `Assets/Tests/Editor/StageStateTests.cs` (create)

**Interfaces:**
- Produces: `StageState { string Background; Dictionary<string,string> Slots; void SetBackground(string); void Show(string character, string position); void Hide(string character); }`

- [ ] **Step 1: 실패 테스트 작성** — `Assets/Tests/Editor/StageStateTests.cs`

```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class StageStateTests
    {
        [Test]
        public void ShowPlacesCharacterInSlot()
        {
            var s = new StageState();
            s.Show("요르", "left");
            Assert.AreEqual("요르", s.Slots["left"]);
        }

        [Test]
        public void ShowSameCharacterNewSlotMoves()
        {
            var s = new StageState();
            s.Show("요르", "left");
            s.Show("요르", "right");
            Assert.IsFalse(s.Slots.ContainsKey("left"));
            Assert.AreEqual("요르", s.Slots["right"]);
        }

        [Test]
        public void ShowDifferentCharacterSameSlotEvicts()
        {
            var s = new StageState();
            s.Show("요르", "center");
            s.Show("민지", "center");
            Assert.AreEqual("민지", s.Slots["center"]);
            Assert.AreEqual(1, s.Slots.Count);
        }

        [Test]
        public void HideRemoves()
        {
            var s = new StageState();
            s.Show("요르", "left");
            s.Hide("요르");
            Assert.AreEqual(0, s.Slots.Count);
        }

        [Test]
        public void SetBackgroundStores()
        {
            var s = new StageState();
            s.SetBackground("공원");
            Assert.AreEqual("공원", s.Background);
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — `run_tests`. Expected: 컴파일 실패(`StageState` 미정의).

- [ ] **Step 3: 구현** — `Assets/Scripts/VNEngine/Core/Runtime/StageState.cs`

```csharp
using System.Collections.Generic;

namespace VNEngine
{
    // Logical stage state the interpreter maintains alongside firing view
    // calls, so a save can snapshot what is on screen and a load can rebuild it.
    public sealed class StageState
    {
        public string Background;
        // position ("left"/"center"/"right") -> character name
        public readonly Dictionary<string, string> Slots = new Dictionary<string, string>();

        public void SetBackground(string name) => Background = name;

        public void Show(string character, string position)
        {
            // A character occupies at most one slot: remove it from any slot
            // it currently stands in, then place it (evicting whoever is here).
            string current = null;
            foreach (var kv in Slots)
                if (kv.Value == character) { current = kv.Key; break; }
            if (current != null) Slots.Remove(current);
            Slots[position] = character;
        }

        public void Hide(string character)
        {
            string at = null;
            foreach (var kv in Slots)
                if (kv.Value == character) { at = kv.Key; break; }
            if (at != null) Slots.Remove(at);
        }
    }
}
```

- [ ] **Step 4: 통과 확인** — `run_tests`. Expected: `StageStateTests` 5개 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/VNEngine/Core/Runtime/StageState.cs Assets/Scripts/VNEngine/Core/Runtime/StageState.cs.meta Assets/Tests/Editor/StageStateTests.cs Assets/Tests/Editor/StageStateTests.cs.meta
git commit -m "feat(vnengine): StageState core model for save/restore of on-screen state"
```

---

### Task 3: `IStageView.Clear()` 추가 (인터페이스 + 두 구현)

로드 시 화면을 리셋하고 스테이지를 재생하기 위한 `Clear()`.

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Presentation/IStageView.cs`
- Modify: `Assets/Scripts/VNEngine/Unity/Presentation/StageViewUnity.cs`
- Modify: `Assets/Tests/Editor/Fakes/FakeStageView.cs`
- Test: `Assets/Tests/Editor/Fakes/FakeStageView.cs` 자체(로그) — 검증은 아래 스텝의 테스트로.

**Interfaces:**
- Produces: `IStageView.Clear()` — 표시 중 캐릭터 전부 제거 + 배경 초기화. `FakeStageView.Clear()`는 `Log`에 `"clear"` 추가.

- [ ] **Step 1: 실패 테스트 작성** — `Assets/Tests/Editor/StageStateTests.cs`에 아래 테스트 추가(같은 파일 하단)

```csharp
    public class FakeStageViewTests
    {
        [Test]
        public void ClearIsLogged()
        {
            var v = new FakeStageView();
            v.ShowCharacter("요르", "left");
            v.Clear();
            Assert.Contains("clear", v.Log);
        }
    }
```

- [ ] **Step 2: 실패 확인** — `run_tests`. Expected: 컴파일 실패(`FakeStageView.Clear` 미정의 / `IStageView.Clear` 미정의).

- [ ] **Step 3a: 인터페이스에 추가** — `Assets/Scripts/VNEngine/Core/Presentation/IStageView.cs`

```csharp
namespace VNEngine
{
    public interface IStageView
    {
        void SetBackground(string name);
        void ShowCharacter(string name, string position);
        void HideCharacter(string name);
        void Clear();
    }
}
```

- [ ] **Step 3b: `FakeStageView`에 구현** — `Assets/Tests/Editor/Fakes/FakeStageView.cs`

```csharp
using System.Collections.Generic;

namespace VNEngine.Tests
{
    public sealed class FakeStageView : IStageView
    {
        public readonly List<string> Log = new List<string>();
        public void SetBackground(string name) => Log.Add($"bg:{name}");
        public void ShowCharacter(string name, string position) => Log.Add($"show:{name}:{position}");
        public void HideCharacter(string name) => Log.Add($"hide:{name}");
        public void Clear() => Log.Add("clear");
    }
}
```

- [ ] **Step 3c: `StageViewUnity`에 구현** — `Assets/Scripts/VNEngine/Unity/Presentation/StageViewUnity.cs` 수정. 죽은 필드 `_currentBackground`(line 26)를 제거하고, `SetBackground`에서 그 대입(line 30)을 삭제하며, `Clear()`를 추가한다.

`private string _currentBackground;` 줄을 삭제. `SetBackground` 본문에서 `_currentBackground = name;` 줄을 삭제(나머지 스프라이트 스왑은 유지). 클래스 하단(`GetSlot` 앞)에 아래 메서드 추가:

```csharp
        public void Clear()
        {
            foreach (var kv in _active)
                if (kv.Value != null) Destroy(kv.Value);
            _active.Clear();
            if (background != null) background.sprite = null;
        }
```

- [ ] **Step 4: 통과 확인** — `refresh_unity`(compile) → `read_console`(error 0) → `run_tests`. Expected: `FakeStageViewTests.ClearIsLogged` PASS, 전체 그린. StageViewUnity는 컴파일만으로 검증(MonoBehaviour/Destroy는 플레이모드 필요).

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/VNEngine/Core/Presentation/IStageView.cs Assets/Scripts/VNEngine/Unity/Presentation/StageViewUnity.cs Assets/Tests/Editor/Fakes/FakeStageView.cs Assets/Tests/Editor/StageStateTests.cs
git commit -m "feat(vnengine): IStageView.Clear() for load-time stage reset; drop dead _currentBackground"
```

---

### Task 4: `SaveData` 모델 + `Interpreter.CaptureSave` (대기 추적·스테이지 추적)

직렬화 모델을 만들고, 인터프리터가 실행 중 스테이지/마지막 대사를 추적하며, 대기 지점에서 스냅샷을 산출한다.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Runtime/SaveData.cs`
- Modify: `Assets/Scripts/VNEngine/Core/Runtime/Interpreter.cs`
- Test: `Assets/Tests/Editor/SaveCaptureTests.cs` (create)

**Interfaces:**
- Consumes: `StageState`(Task 2), `IRandom.State`(Task 1)
- Produces:
  - `SaveData`(필드는 아래), 상수 `SaveData.SaveFormatVersion = 1`
  - `VarEntry { string name; int kind; int value }`, `StageChar { string position; string character }`
  - `Interpreter.IsWaiting` (bool), `Interpreter.CaptureSave(string programHash)` → `SaveData`

- [ ] **Step 1: 실패 테스트 작성** — `Assets/Tests/Editor/SaveCaptureTests.cs`

```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class SaveCaptureTests
    {
        // Build an interpreter over src (a "label start:" is prepended) and
        // tick until it is waiting for input or finished.
        private static Interpreter Build(string src, out GameState state,
                                         out FakeDialogueView dlg, out FakeStageView stage,
                                         params int[] answers)
        {
            var program = Compiler.Compile(Parser.Parse(LineReader.Read("label start:\n" + src, "t.vns")));
            state = new GameState(new SeededRandom(1));
            dlg = new FakeDialogueView(answers);
            stage = new FakeStageView();
            var interp = new Interpreter(program, state, dlg, stage);
            interp.Start("start");
            return interp;
        }

        private static void TickToWait(Interpreter interp)
        {
            int guard = 0;
            while (!interp.IsWaiting && !interp.IsFinished)
            {
                interp.Tick();
                if (++guard > 100000) Assert.Fail("did not reach a wait point");
            }
        }

        [Test]
        public void CaptureAtLineHoldsRenderedText()
        {
            var interp = Build("$ x = 5\n요르 \"hi [x]\"", out _, out _, out _);
            TickToWait(interp);
            var data = interp.CaptureSave("H");
            Assert.AreEqual(SaveData.SaveFormatVersion, data.version);
            Assert.AreEqual("H", data.programHash);
            Assert.AreEqual(1, data.pending); // Line
            Assert.AreEqual("요르", data.lineSpeaker);
            Assert.AreEqual("hi 5", data.lineText);
        }

        [Test]
        public void CaptureIncludesVars()
        {
            var interp = Build("$ gold = 42\n$ met = true\n요르 \"x\"", out _, out _, out _);
            TickToWait(interp);
            var data = interp.CaptureSave("H");
            int gold = 0, metKind = -1, metVal = -1;
            foreach (var v in data.vars)
            {
                if (v.name == "gold") gold = v.value;
                if (v.name == "met") { metKind = v.kind; metVal = v.value; }
            }
            Assert.AreEqual(42, gold);
            Assert.AreEqual(1, metKind);   // Bool
            Assert.AreEqual(1, metVal);    // true
        }

        [Test]
        public void CaptureIncludesStage()
        {
            var interp = Build("bg 공원\nshow 요르 left\n요르 \"x\"", out _, out _, out _);
            TickToWait(interp);
            var data = interp.CaptureSave("H");
            Assert.AreEqual("공원", data.background);
            Assert.AreEqual(1, data.stage.Count);
            Assert.AreEqual("left", data.stage[0].position);
            Assert.AreEqual("요르", data.stage[0].character);
        }

        [Test]
        public void CaptureAtChoiceHoldsLabelsAndTargets()
        {
            var src =
                "menu:\n" +
                "    \"a\":\n" +
                "        jump end\n" +
                "    \"b\":\n" +
                "        jump end\n" +
                "label end:\n" +
                "요르 \"done\"";
            var interp = Build(src, out _, out _, out _, 0);
            TickToWait(interp);
            var data = interp.CaptureSave("H");
            Assert.AreEqual(2, data.pending); // Choice
            Assert.AreEqual(2, data.choiceLabels.Count);
            Assert.AreEqual("a", data.choiceLabels[0]);
            Assert.AreEqual("b", data.choiceLabels[1]);
            Assert.AreEqual(2, data.choiceTargets.Count);
        }

        [Test]
        public void CaptureWhenNotWaitingThrows()
        {
            var interp = Build("return", out _, out _, out _);
            TickToWait(interp); // finishes immediately, not waiting
            Assert.IsTrue(interp.IsFinished);
            Assert.Throws<VnRuntimeException>(() => interp.CaptureSave("H"));
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — `run_tests`. Expected: 컴파일 실패(`SaveData`/`CaptureSave`/`IsWaiting` 미정의).

- [ ] **Step 3a: `SaveData` 생성** — `Assets/Scripts/VNEngine/Core/Runtime/SaveData.cs`

```csharp
using System.Collections.Generic;

namespace VNEngine
{
    [System.Serializable]
    public sealed class VarEntry
    {
        public string name;
        public int kind;   // 0 = Int, 1 = Bool
        public int value;  // Bool stored as 0/1
    }

    [System.Serializable]
    public sealed class StageChar
    {
        public string position;
        public string character;
    }

    // Plain, JsonUtility-friendly snapshot of a running interpreter.
    // Lists (not dictionaries) and primitives only.
    [System.Serializable]
    public sealed class SaveData
    {
        public const int SaveFormatVersion = 1;

        public int version;
        public string programHash;

        public List<VarEntry> vars = new List<VarEntry>();
        public int rngState;            // bit pattern of the PRNG's uint state

        public int pc;
        public List<int> callStack = new List<int>(); // top-first order

        public int pending;             // 0=None, 1=Line, 2=Choice
        // pending == Line:
        public string lineSpeaker;
        public string lineColor;
        public string lineText;
        // pending == Choice:
        public List<string> choiceLabels = new List<string>();
        public List<int> choiceTargets = new List<int>();

        // stage:
        public string background;
        public List<StageChar> stage = new List<StageChar>();
    }
}
```

- [ ] **Step 3b: `Interpreter` 수정** — `Assets/Scripts/VNEngine/Core/Runtime/Interpreter.cs`

(1) 필드 추가 (`_activeOptions` 선언 아래):

```csharp
        private readonly StageState _stageState = new StageState();
        private string _lastSpeaker, _lastColor, _lastText;
```

(2) `IsWaiting` 노출 (`IsFinished` 프로퍼티 아래):

```csharp
        public bool IsWaiting => _pending != Pending.None;
```

(3) `Say` 케이스에서 표시값을 보관. 기존:
```csharp
                            _dialogue.ShowLine(speaker, color, TextInterpolator.Interpolate(ins.StrB, _state));
```
을 아래로 교체:
```csharp
                            string rendered = TextInterpolator.Interpolate(ins.StrB, _state);
                            _lastSpeaker = speaker; _lastColor = color; _lastText = rendered;
                            _dialogue.ShowLine(speaker, color, rendered);
```

(4) 스테이지 명령에서 `_stageState` 동기화. 기존:
```csharp
                    case Op.Bg: _stage.SetBackground(ins.StrA); _pc++; break;
                    case Op.Show: _stage.ShowCharacter(ins.StrA, ins.StrB); _pc++; break;
                    case Op.Hide: _stage.HideCharacter(ins.StrA); _pc++; break;
```
을 아래로 교체:
```csharp
                    case Op.Bg: _stageState.SetBackground(ins.StrA); _stage.SetBackground(ins.StrA); _pc++; break;
                    case Op.Show: _stageState.Show(ins.StrA, ins.StrB); _stage.ShowCharacter(ins.StrA, ins.StrB); _pc++; break;
                    case Op.Hide: _stageState.Hide(ins.StrA); _stage.HideCharacter(ins.StrA); _pc++; break;
```

(5) `BuildMenu` 메서드 뒤(클래스 끝)에 `CaptureSave` 추가:

```csharp
        public SaveData CaptureSave(string programHash)
        {
            if (!IsWaiting)
                throw new VnRuntimeException("cannot save: interpreter is not waiting for input");

            var data = new SaveData
            {
                version = SaveData.SaveFormatVersion,
                programHash = programHash,
                rngState = unchecked((int)_state.Random.State),
                pc = _pc,
                pending = (int)_pending,
                background = _stageState.Background,
            };

            foreach (var kv in _state.Snapshot)
                data.vars.Add(new VarEntry { name = kv.Key, kind = (int)kv.Value.Kind, value = kv.Value.AsInt });

            foreach (var frame in _callStack) // Stack enumerates top-first
                data.callStack.Add(frame);

            if (_pending == Pending.Line)
            {
                data.lineSpeaker = _lastSpeaker;
                data.lineColor = _lastColor;
                data.lineText = _lastText;
            }
            else if (_pending == Pending.Choice)
            {
                foreach (var opt in _activeOptions)
                {
                    data.choiceLabels.Add(opt.Label);
                    data.choiceTargets.Add(opt.Target);
                }
            }

            foreach (var kv in _stageState.Slots)
                data.stage.Add(new StageChar { position = kv.Key, character = kv.Value });

            return data;
        }
```

- [ ] **Step 4: 통과 확인** — `refresh_unity` → `read_console`(error 0) → `run_tests`. Expected: `SaveCaptureTests` 5개 PASS, 전체 그린.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/VNEngine/Core/Runtime/SaveData.cs Assets/Scripts/VNEngine/Core/Runtime/SaveData.cs.meta Assets/Scripts/VNEngine/Core/Runtime/Interpreter.cs Assets/Tests/Editor/SaveCaptureTests.cs Assets/Tests/Editor/SaveCaptureTests.cs.meta
git commit -m "feat(vnengine): SaveData model + Interpreter.CaptureSave with stage/line tracking"
```

---

### Task 5: `Interpreter.RestoreSave`

스냅샷을 새 인터프리터에 복원하고 화면·난수·대기상태까지 되돌린다.

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Runtime/Interpreter.cs`
- Test: `Assets/Tests/Editor/SaveRestoreTests.cs` (create)

**Interfaces:**
- Consumes: `SaveData`(Task 4), `IStageView.Clear()`(Task 3), `IRandom.State`(Task 1)
- Produces: `Interpreter.RestoreSave(SaveData data)` — pc·콜스택·변수·RNG·스테이지·대기화면 복원

- [ ] **Step 1: 실패 테스트 작성** — `Assets/Tests/Editor/SaveRestoreTests.cs`

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class SaveRestoreTests
    {
        private static Interpreter Build(string src, GameState state,
                                         FakeDialogueView dlg, FakeStageView stage)
        {
            var program = Compiler.Compile(Parser.Parse(LineReader.Read("label start:\n" + src, "t.vns")));
            var interp = new Interpreter(program, state, dlg, stage);
            interp.Start("start");
            return interp;
        }

        private static void TickToWait(Interpreter interp)
        {
            int g = 0;
            while (!interp.IsWaiting && !interp.IsFinished)
                if (++g > 100000) { Assert.Fail("no wait"); } else interp.Tick();
        }

        private static void RunToEnd(Interpreter interp)
        {
            int g = 0;
            while (!interp.IsFinished)
                if (++g > 100000) { Assert.Fail("no finish"); } else interp.Tick();
        }

        private static List<string> Texts(FakeDialogueView d)
        {
            var l = new List<string>();
            foreach (var s in d.Lines) l.Add(s.Text);
            return l;
        }

        [Test]
        public void RestoredInterpreterResumesIdentically()
        {
            const string src =
                "요르 \"one\"\n" +   // wait A here
                "$ gold = 10\n" +
                "요르 \"two\"\n" +
                "요르 \"three\"";

            // A: run to first wait, capture, then finish.
            var sa = new GameState(new SeededRandom(1));
            var da = new FakeDialogueView();
            var interpA = Build(src, sa, da, new FakeStageView());
            TickToWait(interpA);
            var data = interpA.CaptureSave("H");
            RunToEnd(interpA);

            // B: fresh, restore from the capture, then finish.
            var sb = new GameState(new SeededRandom(1));
            var db = new FakeDialogueView();
            var interpB = Build(src, sb, db, new FakeStageView());
            interpB.RestoreSave(data);
            RunToEnd(interpB);

            // A showed one,two,three ; B (restored right after one) re-shows one then two,three.
            Assert.AreEqual(new[] { "one", "two", "three" }, Texts(da).ToArray());
            Assert.AreEqual(new[] { "one", "two", "three" }, Texts(db).ToArray());
            Assert.AreEqual(VnValue.Int(10), sb.Get("gold"));
        }

        [Test]
        public void RandomIsDeterministicAcrossSaveLoad()
        {
            const string src =
                "요르 \"pause\"\n" +
                "$ r = random(1, 1000000)\n" +
                "요르 \"[r]\"";

            var sa = new GameState(new SeededRandom(12345));
            var da = new FakeDialogueView();
            var interpA = Build(src, sa, da, new FakeStageView());
            TickToWait(interpA);                 // at "pause"
            var data = interpA.CaptureSave("H");
            RunToEnd(interpA);
            int ra = sa.Get("r").AsInt;

            var sb = new GameState(new SeededRandom(999)); // different seed
            var db = new FakeDialogueView();
            var interpB = Build(src, sb, db, new FakeStageView());
            interpB.RestoreSave(data);           // restores RNG state
            RunToEnd(interpB);
            int rb = sb.Get("r").AsInt;

            Assert.AreEqual(ra, rb);
        }

        [Test]
        public void StageIsClearedAndReapplied()
        {
            const string src = "bg 공원\nshow 요르 left\n요르 \"hi\"";
            var sa = new GameState(new SeededRandom(1));
            var interpA = Build(src, sa, new FakeDialogueView(), new FakeStageView());
            TickToWait(interpA);
            var data = interpA.CaptureSave("H");

            var stageB = new FakeStageView();
            var interpB = Build(src, new GameState(new SeededRandom(1)), new FakeDialogueView(), stageB);
            interpB.RestoreSave(data);

            Assert.Contains("clear", stageB.Log);
            Assert.Contains("bg:공원", stageB.Log);
            Assert.Contains("show:요르:left", stageB.Log);
        }

        [Test]
        public void NarrationNullSpeakerSurvivesEmptyString()
        {
            // Simulate the JsonUtility null->"" round trip: RestoreSave must
            // treat "" speaker/color/background as null.
            const string src = "\"just narration\"";
            var interpA = Build(src, new GameState(new SeededRandom(1)),
                                new FakeDialogueView(), new FakeStageView());
            TickToWait(interpA);
            var data = interpA.CaptureSave("H");
            data.lineSpeaker = "";   // as JsonUtility would produce
            data.lineColor = "";
            data.background = "";

            var db = new FakeDialogueView();
            var interpB = Build(src, new GameState(new SeededRandom(1)), db, new FakeStageView());
            interpB.RestoreSave(data);

            Assert.IsNull(db.Lines[0].Speaker);
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — `run_tests`. Expected: 컴파일 실패(`RestoreSave` 미정의).

- [ ] **Step 3: 구현** — `Assets/Scripts/VNEngine/Core/Runtime/Interpreter.cs`의 `CaptureSave` 뒤에 추가

```csharp
        public void RestoreSave(SaveData data)
        {
            // Variables.
            foreach (var v in data.vars)
                _state.Set(v.name, v.kind == 1 ? VnValue.Bool(v.value != 0) : VnValue.Int(v.value));

            // RNG.
            _state.Random.State = unchecked((uint)data.rngState);

            // Execution position.
            _pc = data.pc;
            _callStack.Clear();
            for (int i = data.callStack.Count - 1; i >= 0; i--) // stored top-first
                _callStack.Push(data.callStack[i]);

            // Stage: rebuild logical state and replay onto the (cleared) view.
            _stageState.Background = null;
            _stageState.Slots.Clear();
            _stage.Clear();
            string bg = NullIfEmpty(data.background);
            if (bg != null) { _stageState.SetBackground(bg); _stage.SetBackground(bg); }
            foreach (var sc in data.stage)
            {
                _stageState.Show(sc.character, sc.position);
                _stage.ShowCharacter(sc.character, sc.position);
            }

            // Pending input screen.
            _pending = (Pending)data.pending;
            _activeOptions = null;
            if (_pending == Pending.Line)
            {
                _lastSpeaker = NullIfEmpty(data.lineSpeaker);
                _lastColor = NullIfEmpty(data.lineColor);
                _lastText = data.lineText;
                _dialogue.ShowLine(_lastSpeaker, _lastColor, _lastText);
            }
            else if (_pending == Pending.Choice)
            {
                if (data.choiceLabels.Count != data.choiceTargets.Count)
                    throw new VnRuntimeException("corrupt save: choice labels/targets length mismatch");
                _activeOptions = new List<MenuOption>();
                var labels = new List<string>();
                for (int i = 0; i < data.choiceLabels.Count; i++)
                {
                    _activeOptions.Add(new MenuOption { Label = data.choiceLabels[i], Target = data.choiceTargets[i] });
                    labels.Add(data.choiceLabels[i]);
                }
                _dialogue.ShowChoices(labels);
            }

            IsFinished = false;
        }

        // JsonUtility serializes a null string as "", so a round-tripped save
        // brings empty strings where we stored null (narration speaker, etc).
        private static string NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;
```

- [ ] **Step 4: 통과 확인** — `run_tests`. Expected: `SaveRestoreTests` 4개 PASS, 전체 그린.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/VNEngine/Core/Runtime/Interpreter.cs Assets/Tests/Editor/SaveRestoreTests.cs Assets/Tests/Editor/SaveRestoreTests.cs.meta
git commit -m "feat(vnengine): Interpreter.RestoreSave (state/RNG/stage/pending restore)"
```

---

### Task 6: 스크립트 지문 `VnHash` + `VnScriptLoader` 노출

세이브 호환성 가드에 쓸 안정적 스크립트 해시.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Runtime/VnHash.cs`
- Modify: `Assets/Scripts/VNEngine/Unity/VnScriptLoader.cs`
- Test: `Assets/Tests/Editor/VnHashTests.cs` (create)

**Interfaces:**
- Produces:
  - `VnHash.Fnv1a(string)` → `string` (8자리 hex, 결정적)
  - `VnScriptLoader.LoadAndCompile(string subfolder, out string programHash)` (기존 단일 인자 오버로드 유지)

- [ ] **Step 1: 실패 테스트 작성** — `Assets/Tests/Editor/VnHashTests.cs`

```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class VnHashTests
    {
        [Test]
        public void SameInputSameHash()
            => Assert.AreEqual(VnHash.Fnv1a("label start:\n요르 \"hi\""), VnHash.Fnv1a("label start:\n요르 \"hi\""));

        [Test]
        public void DifferentInputDifferentHash()
            => Assert.AreNotEqual(VnHash.Fnv1a("a"), VnHash.Fnv1a("b"));

        [Test]
        public void EmptyIsStable()
            => Assert.AreEqual(VnHash.Fnv1a(""), VnHash.Fnv1a(""));

        [Test]
        public void HashIsEightHexChars()
            => Assert.AreEqual(8, VnHash.Fnv1a("anything").Length);
    }
}
```

- [ ] **Step 2: 실패 확인** — `run_tests`. Expected: 컴파일 실패(`VnHash` 미정의).

- [ ] **Step 3a: `VnHash` 생성** — `Assets/Scripts/VNEngine/Core/Runtime/VnHash.cs`

```csharp
namespace VNEngine
{
    // Deterministic FNV-1a (32-bit) over a string's chars. Used to fingerprint
    // the compiled scenario so a save can detect a changed script.
    public static class VnHash
    {
        public static string Fnv1a(string s)
        {
            uint hash = 2166136261u;
            if (s != null)
                foreach (char c in s)
                    hash = unchecked((hash ^ c) * 16777619u);
            return hash.ToString("x8");
        }
    }
}
```

- [ ] **Step 3b: `VnScriptLoader` 수정** — `Assets/Scripts/VNEngine/Unity/VnScriptLoader.cs` 전체 교체. 정렬된 에셋 텍스트를 `\n`으로 연결해 해시하고, 그 문자열을 파싱한다(해시 대상 == 파싱 대상 == 안정적).

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VNEngine.Unity
{
    public static class VnScriptLoader
    {
        public static VnProgram LoadAndCompile(string resourcesSubfolder)
            => LoadAndCompile(resourcesSubfolder, out _);

        // Also yields a stable fingerprint of the loaded scripts for save compat.
        public static VnProgram LoadAndCompile(string resourcesSubfolder, out string programHash)
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>(resourcesSubfolder);
            if (assets == null || assets.Length == 0)
                throw new VnException($"no .vns TextAssets found under Resources/{resourcesSubfolder}");

            Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));

            var joined = new StringBuilder();
            var parsed = new List<List<Command>>();
            foreach (var ta in assets)
            {
                joined.Append(ta.name).Append('\n').Append(ta.text).Append('\n');
                parsed.Add(Parser.Parse(LineReader.Read(ta.text, ta.name)));
            }

            programHash = VnHash.Fnv1a(joined.ToString());
            return Compiler.Compile(parsed);
        }
    }
}
```

- [ ] **Step 4: 통과 확인** — `refresh_unity` → `read_console`(error 0) → `run_tests`. Expected: `VnHashTests` 4개 PASS, 전체 그린. (기존 `VNRunner`의 단일 인자 호출은 유지된 오버로드로 계속 컴파일됨.)

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/VNEngine/Core/Runtime/VnHash.cs Assets/Scripts/VNEngine/Core/Runtime/VnHash.cs.meta Assets/Scripts/VNEngine/Unity/VnScriptLoader.cs Assets/Tests/Editor/VnHashTests.cs Assets/Tests/Editor/VnHashTests.cs.meta
git commit -m "feat(vnengine): VnHash fingerprint + VnScriptLoader programHash out-param"
```

---

### Task 7: `SaveSystem` — 디스크 IO + JSON + 호환성 가드 (Unity 계층)

`SaveData`를 JSON으로 슬롯 파일에 읽고 쓴다. 이 태스크는 테스트 asmdef가 Unity 계층을 참조하도록 확장한다.

**Files:**
- Create: `Assets/Scripts/VNEngine/Unity/SaveSystem.cs`
- Modify: `Assets/Tests/Editor/VNEngine.Tests.asmdef` (add `VNEngine.Unity` reference)
- Test: `Assets/Tests/Editor/SaveSystemTests.cs` (create)

**Interfaces:**
- Consumes: `SaveData`(Task 4)
- Produces (all static on `VNEngine.Unity.SaveSystem`):
  - `string SlotPath(int slot)`, `void Write(int slot, SaveData data)`, `SaveData Read(int slot)`,
    `bool Exists(int slot)`, `void Delete(int slot)`, `IEnumerable<int> ListSlots()`,
    `bool IsCompatible(SaveData data, string programHash)`

- [ ] **Step 1: 테스트 asmdef에 Unity 참조 추가** — `Assets/Tests/Editor/VNEngine.Tests.asmdef`의 `references`에 `"VNEngine.Unity"` 추가:

```json
    "references": [
        "VNEngine.Core",
        "VNEngine.Unity",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
```

- [ ] **Step 2: 실패 테스트 작성** — `Assets/Tests/Editor/SaveSystemTests.cs`. 실제 `persistentDataPath`의 높은 슬롯 번호(테스트 전용)를 쓰고 정리한다.

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using VNEngine;
using VNEngine.Unity;

namespace VNEngine.Tests
{
    public class SaveSystemTests
    {
        private const int Slot = 987654; // test-only slot, unlikely to collide

        [TearDown] public void Cleanup() => SaveSystem.Delete(Slot);

        private static SaveData Sample()
        {
            var d = new SaveData
            {
                version = SaveData.SaveFormatVersion,
                programHash = "abc123",
                rngState = -42,
                pc = 7,
                pending = 1,
                lineSpeaker = "요르",
                lineText = "hi 5",
                background = "공원",
            };
            d.vars.Add(new VarEntry { name = "gold", kind = 0, value = 100 });
            d.vars.Add(new VarEntry { name = "met", kind = 1, value = 1 });
            d.callStack.Add(3);
            d.callStack.Add(9);
            d.stage.Add(new StageChar { position = "left", character = "요르" });
            return d;
        }

        [Test]
        public void WriteThenReadRoundTrips()
        {
            SaveSystem.Write(Slot, Sample());
            var r = SaveSystem.Read(Slot);
            Assert.IsNotNull(r);
            Assert.AreEqual(7, r.pc);
            Assert.AreEqual(-42, r.rngState);
            Assert.AreEqual(2, r.vars.Count);
            Assert.AreEqual("gold", r.vars[0].name);
            Assert.AreEqual(new List<int> { 3, 9 }, r.callStack);
            Assert.AreEqual("left", r.stage[0].position);
            Assert.AreEqual("공원", r.background);
        }

        [Test]
        public void ExistsAndDelete()
        {
            Assert.IsFalse(SaveSystem.Exists(Slot));
            SaveSystem.Write(Slot, Sample());
            Assert.IsTrue(SaveSystem.Exists(Slot));
            SaveSystem.Delete(Slot);
            Assert.IsFalse(SaveSystem.Exists(Slot));
        }

        [Test]
        public void ReadMissingSlotIsNull()
            => Assert.IsNull(SaveSystem.Read(Slot));

        [Test]
        public void ListSlotsIncludesWritten()
        {
            SaveSystem.Write(Slot, Sample());
            Assert.Contains(Slot, new List<int>(SaveSystem.ListSlots()));
        }

        [Test]
        public void IsCompatibleChecksVersionAndHash()
        {
            var d = Sample();
            Assert.IsTrue(SaveSystem.IsCompatible(d, "abc123"));
            Assert.IsFalse(SaveSystem.IsCompatible(d, "different"));
            d.version = 999;
            Assert.IsFalse(SaveSystem.IsCompatible(d, "abc123"));
            Assert.IsFalse(SaveSystem.IsCompatible(null, "abc123"));
        }
    }
}
```

- [ ] **Step 3: 실패 확인** — `refresh_unity` → `run_tests`. Expected: 컴파일 실패(`SaveSystem` 미정의). asmdef 참조는 추가됨.

- [ ] **Step 4: 구현** — `Assets/Scripts/VNEngine/Unity/SaveSystem.cs`

```csharp
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VNEngine.Unity
{
    // Disk persistence for SaveData. Mobile-safe: only Application.persistentDataPath.
    public static class SaveSystem
    {
        private static string Dir => Path.Combine(Application.persistentDataPath, "saves");
        public static string SlotPath(int slot) => Path.Combine(Dir, $"slot_{slot}.json");

        public static void Write(int slot, SaveData data)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(data, true));
        }

        public static SaveData Read(int slot)
        {
            string path = SlotPath(slot);
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(path)); }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveSystem] failed to read slot {slot}: {e.Message}");
                return null;
            }
        }

        public static bool Exists(int slot) => File.Exists(SlotPath(slot));

        public static void Delete(int slot)
        {
            string path = SlotPath(slot);
            if (File.Exists(path)) File.Delete(path);
        }

        public static IEnumerable<int> ListSlots()
        {
            if (!Directory.Exists(Dir)) yield break;
            foreach (var file in Directory.GetFiles(Dir, "slot_*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(file); // slot_N
                if (int.TryParse(name.Substring("slot_".Length), out int n))
                    yield return n;
            }
        }

        public static bool IsCompatible(SaveData data, string programHash)
            => data != null && data.version == SaveData.SaveFormatVersion && data.programHash == programHash;
    }
}
```

- [ ] **Step 5: 통과 확인** — `refresh_unity` → `read_console`(error 0) → `run_tests`. Expected: `SaveSystemTests` 5개 PASS, 전체 그린.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/VNEngine/Unity/SaveSystem.cs Assets/Scripts/VNEngine/Unity/SaveSystem.cs.meta Assets/Tests/Editor/VNEngine.Tests.asmdef Assets/Tests/Editor/SaveSystemTests.cs Assets/Tests/Editor/SaveSystemTests.cs.meta
git commit -m "feat(vnengine): SaveSystem disk IO + JSON round-trip + compat guard"
```

---

### Task 8: `VNRunner` 통합 — `SaveToSlot` / `LoadFromSlot`

MonoBehaviour에 저장/로드 진입점을 얹는다. Start를 빌드/실행으로 분리하고 프로그램 해시를 보관한다. (P0 Unity 태스크 선례대로 유닛테스트 없음 — 컴파일 검증 + 로직은 Task 4~7·9가 커버.)

**Files:**
- Modify: `Assets/Scripts/VNEngine/Unity/VNRunner.cs`

**Interfaces:**
- Consumes: `VnScriptLoader.LoadAndCompile(subfolder, out hash)`(Task 6), `Interpreter.CaptureSave/RestoreSave/IsWaiting`(Task 4·5), `SaveSystem.*`(Task 7)
- Produces: `VNRunner.SaveToSlot(int)`, `VNRunner.LoadFromSlot(int)`

- [ ] **Step 1: 구현** — `Assets/Scripts/VNEngine/Unity/VNRunner.cs` 전체 교체

```csharp
using System.Collections;
using UnityEngine;

namespace VNEngine.Unity
{
    public class VNRunner : MonoBehaviour
    {
        [Header("References")]
        public DialogueViewUnity dialogueView;
        public StageViewUnity stageView;

        [Header("Script")]
        [Tooltip("Subfolder inside a Resources/ folder that holds the .vns TextAssets")]
        public string scriptsResourcesFolder = "scripts";
        public string entryLabel = "start";

        [Tooltip("Seed for random(); fixed for reproducible runs")]
        public int randomSeed = 12345;

        private Interpreter _interp;
        private GameState _state;
        private VnProgram _program;
        private string _programHash;
        private Coroutine _loop;

        private IEnumerator Start()
        {
            if (dialogueView == null || stageView == null)
            {
                Debug.LogError("[VNRunner] dialogueView and stageView must be assigned");
                yield break;
            }

            if (!BuildInterpreter()) yield break;

            string startError = null;
            try { _interp.Start(entryLabel); }
            catch (VnException e) { startError = e.Message; }
            if (startError != null) { Debug.LogError($"[VNRunner] {startError}"); yield break; }

            _loop = StartCoroutine(RunLoop());
        }

        // Loads+compiles the scripts and creates a fresh interpreter/state.
        private bool BuildInterpreter()
        {
            string loadError = null;
            try { _program = VnScriptLoader.LoadAndCompile(scriptsResourcesFolder, out _programHash); }
            catch (VnException e) { loadError = e.Message; }
            if (loadError != null)
            {
                Debug.LogError($"[VNRunner] script load/compile failed: {loadError}");
                dialogueView.ShowLine(null, null, "[script load failed]");
                return false;
            }
            _state = new GameState(new SeededRandom(randomSeed));
            _interp = new Interpreter(_program, _state, dialogueView, stageView);
            return true;
        }

        private IEnumerator RunLoop()
        {
            while (!_interp.IsFinished)
            {
                string tickError = null;
                try { _interp.Tick(); }
                catch (VnException e) { tickError = e.Message; }
                if (tickError != null)
                {
                    Debug.LogError($"[VNRunner] runtime error: {tickError}");
                    yield break;
                }
                yield return null;
            }
        }

        // Save the current run to a slot. Only valid while waiting for input.
        public bool SaveToSlot(int slot)
        {
            if (_interp == null || !_interp.IsWaiting)
            {
                Debug.LogWarning("[VNRunner] cannot save: not currently waiting for input");
                return false;
            }
            SaveSystem.Write(slot, _interp.CaptureSave(_programHash));
            return true;
        }

        // Restore a slot into a fresh interpreter and resume.
        public bool LoadFromSlot(int slot)
        {
            var data = SaveSystem.Read(slot);
            if (data == null) { Debug.LogWarning($"[VNRunner] no save in slot {slot}"); return false; }
            if (!SaveSystem.IsCompatible(data, _programHash))
            {
                Debug.LogWarning($"[VNRunner] save slot {slot} incompatible: script changed or version mismatch");
                return false;
            }

            if (_loop != null) { StopCoroutine(_loop); _loop = null; }
            if (!BuildInterpreter()) return false;
            _interp.RestoreSave(data);
            _loop = StartCoroutine(RunLoop());
            return true;
        }
    }
}
```

- [ ] **Step 2: 컴파일 검증** — `refresh_unity` → `read_console`. Expected: 에러 0, 새 경고 0.

- [ ] **Step 3: 전체 스위트 재확인** — `run_tests`(EditMode). Expected: 기존 전 테스트 그린(회귀 없음).

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/VNEngine/Unity/VNRunner.cs
git commit -m "feat(vnengine): VNRunner SaveToSlot/LoadFromSlot with build/run split"
```

---

### Task 9: 엔드투엔드 세이브/로드 통합 테스트 (durable)

실제 파이프라인으로 진행 중 저장→JSON 왕복→로드→이어서 진행이 원본과 일치함을 검증(스테이지·변수·난수·선택지 포함).

**Files:**
- Test: `Assets/Tests/Editor/SaveLoadIntegrationTests.cs` (create)

**Interfaces:**
- Consumes: `Compiler/Parser/LineReader`, `Interpreter`(Task 4·5), `VnHash`(Task 6), `SaveData`, `UnityEngine.JsonUtility`

- [ ] **Step 1: 테스트 작성** — `Assets/Tests/Editor/SaveLoadIntegrationTests.cs`

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace VNEngine.Tests
{
    public class SaveLoadIntegrationTests
    {
        private const string Src =
            "bg 공원\n" +
            "show 요르 left\n" +
            "$ affinity = 10\n" +
            "요르 \"주말에 뭐 할래?\"\n" +      // wait point we save at
            "menu:\n" +
            "    \"데이트\":\n" +
            "        $ affinity += 30\n" +
            "        $ luck = random(1, 1000000)\n" +
            "        요르 \"좋아! [affinity] [luck]\"\n" +
            "        jump end\n" +
            "    \"거절\":\n" +
            "        요르 \"그렇구나\"\n" +
            "        jump end\n" +
            "label end:\n" +
            "요르 \"끝\"";

        private static Interpreter Build(GameState s, FakeDialogueView d, FakeStageView g)
        {
            var program = Compiler.Compile(Parser.Parse(LineReader.Read("label start:\n" + Src, "t.vns")));
            var interp = new Interpreter(program, s, d, g);
            interp.Start("start");
            return interp;
        }

        private static void TickToWait(Interpreter i)
        { int g = 0; while (!i.IsWaiting && !i.IsFinished) if (++g > 100000) Assert.Fail(); else i.Tick(); }
        private static void RunToEnd(Interpreter i)
        { int g = 0; while (!i.IsFinished) if (++g > 100000) Assert.Fail(); else i.Tick(); }
        private static string[] Texts(FakeDialogueView d)
        { var l = new List<string>(); foreach (var s in d.Lines) l.Add(s.Text); return l.ToArray(); }

        [Test]
        public void SaveJsonLoadResumesWithParity()
        {
            string hash = VnHash.Fnv1a(Src);

            // Baseline: run straight through, choosing "데이트" (index 0).
            var sBase = new GameState(new SeededRandom(777));
            var dBase = new FakeDialogueView(0);
            var iBase = Build(sBase, dBase, new FakeStageView());
            RunToEnd(iBase);
            int baseAffinity = sBase.Get("affinity").AsInt;
            int baseLuck = sBase.Get("luck").AsInt;

            // Save/load run: save at the first line, JSON round-trip, restore, resume.
            var sA = new GameState(new SeededRandom(777));
            var iA = Build(sA, new FakeDialogueView(0), new FakeStageView());
            TickToWait(iA);
            var data = iA.CaptureSave(hash);

            // Simulate disk via the real JSON serializer.
            string json = JsonUtility.ToJson(data);
            var loaded = JsonUtility.FromJson<SaveData>(json);
            Assert.AreEqual(SaveData.SaveFormatVersion, loaded.version);
            Assert.AreEqual(hash, loaded.programHash);

            var sB = new GameState(new SeededRandom(0)); // seed irrelevant; restored
            var dB = new FakeDialogueView(0);
            var gB = new FakeStageView();
            var iB = Build(sB, dB, gB);
            iB.RestoreSave(loaded);
            RunToEnd(iB);

            // Parity: same final variables (incl. deterministic random) and ending line.
            Assert.AreEqual(baseAffinity, sB.Get("affinity").AsInt);
            Assert.AreEqual(baseLuck, sB.Get("luck").AsInt);
            CollectionAssert.Contains(Texts(dB), "끝");
            // Stage was restored (cleared + re-applied) before resuming.
            Assert.Contains("clear", gB.Log);
            Assert.Contains("show:요르:left", gB.Log);
        }

        [Test]
        public void IncompatibleHashIsRejected()
        {
            var data = new SaveData { version = SaveData.SaveFormatVersion, programHash = "real" };
            Assert.IsFalse(VNEngine.Unity.SaveSystem.IsCompatible(data, "tampered"));
        }
    }
}
```

- [ ] **Step 2: 실행/통과 확인** — `refresh_unity` → `read_console`(error 0) → `run_tests`(EditMode). Expected: `SaveLoadIntegrationTests` 2개 PASS, **전체 스위트 그린**.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Tests/Editor/SaveLoadIntegrationTests.cs Assets/Tests/Editor/SaveLoadIntegrationTests.cs.meta
git commit -m "test(vnengine): end-to-end save/load parity (JSON round-trip, stage/vars/random)"
```

---

## 완료 기준

- 신규 테스트 전부 PASS + 기존 스위트 회귀 없음(EditMode 그린).
- `intro.vns` 유사 시나리오를 진행 중 저장→JSON 왕복→로드하면 변수·진행위치·화면·이후 난수·선택지 결과가 이어진다(Task 9).
- Core는 UnityEngine/System.IO 무참조 유지, 디스크 IO는 `persistentDataPath`만.
- 이후: 최종 whole-branch 리뷰 → superpowers:finishing-a-development-branch로 `save-load` → `main` 머지.
