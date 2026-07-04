# VN Engine P0 — Core Rearchitecture + DSL Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the monolithic `DialogueManager` with a modular, testable VN engine driven by a Ren'Py-style `.vns` DSL, reproducing the current sample scenario exactly.

**Architecture:** Pure-C# core (Parsing → Compiler → Interpreter + Expression engine) with zero Unity dependency, driven through `IDialogueView`/`IStageView` interfaces. Unity-side presentation classes implement those interfaces; `VNRunner` (a coroutine host) loads `.vns` files, compiles them to a flat instruction array, and ticks the interpreter each frame.

**Tech Stack:** Unity 2022.3.27f1, C# (.NET Standard 2.1), Unity Test Framework 1.1.33 (NUnit EditMode), TextMeshPro, uGUI.

## Global Constraints

- Unity version: **2022.3.27f1**. Do not upgrade.
- Core assembly `VNEngine.Core` MUST have **no engine references** (`"noEngineReferences": true`, `"autoReferenced": true`). It may not use any `UnityEngine.*` type. Randomness comes from the injected `IRandom`, never `UnityEngine.Random`. Errors are thrown as `VnException`, never `Debug.Log`.
- Variables are two types only in P0: **int** and **bool**. No float, no string variables. Integer division truncates toward zero (`7 / 2 == 3`).
- Undefined variables read as `VnValue.Int(0)`.
- Scripts are authored as `.vns` files (UTF-8, space indentation only — tabs in indentation are a parse error) and placed in `Assets/Resources/scripts/`. A custom `ScriptedImporter` imports `.vns` as `TextAsset`; the runtime loads them with `Resources.LoadAll<TextAsset>("scripts")`. **Do NOT use `System.IO.File`/`Directory.GetFiles` on `StreamingAssets` for script loading** — that path fails on Android/WebGL. This is the mobile-support requirement (target platforms: Android, iOS, Windows/Mac desktop).
- Touch input works via `Input.GetMouseButtonDown(0)` (Unity maps the first touch to mouse button 0); no separate touch code is needed for P0.
- Default entry label is `start`, overridable via a `VNRunner` inspector field.
- After any script edit that adds/removes C# types, verify compilation via the UnityMCP `read_console` tool before using new types. Poll `editor_state.isCompiling` until false.
- Test runner: run EditMode tests via UnityMCP `run_tests` (mode `EditMode`). A task's tests must be green before its commit.
- Namespaces: core = `VNEngine`; Unity layer = `VNEngine.Unity`; tests = `VNEngine.Tests`.
- Commit after every task with the exact message given. Do not squash tasks.

---

## File Structure

**Core** — `Assets/Scripts/VNEngine/Core/` (asmdef `VNEngine.Core`, no engine refs):
- `VNEngine.Core.asmdef` — assembly definition.
- `Values/VnValue.cs` — value struct (Int/Bool), truthiness, ToString.
- `Runtime/IRandom.cs` — random source interface + `SeededRandom`.
- `Runtime/GameState.cs` — variable store.
- `Errors/VnException.cs` — `VnException`, `VnParseException`, `VnRuntimeException`.
- `Expressions/Expr.cs` — expression AST nodes.
- `Expressions/ExprParser.cs` — string → `Expr` (recursive descent).
- `Expressions/ExprEval.cs` — evaluate `Expr` against `GameState`.
- `Parsing/LogicalLine.cs` — logical-line struct.
- `Parsing/LineReader.cs` — source text → `List<LogicalLine>`.
- `Parsing/Command.cs` — command AST nodes.
- `Parsing/Parser.cs` — `List<LogicalLine>` → `List<Command>`.
- `Runtime/Instruction.cs` — flat instruction + `MenuOption` + `Op` enum + `CharacterDef` + `VnProgram`.
- `Runtime/Compiler.cs` — commands (multi-file) → `VnProgram`.
- `Presentation/IDialogueView.cs`, `Presentation/IStageView.cs` — host interfaces.
- `Runtime/Interpreter.cs` — executes `VnProgram`.
- `Runtime/TextInterpolator.cs` — `[var]` substitution.

**Unity layer** — `Assets/Scripts/VNEngine/Unity/` (asmdef `VNEngine.Unity`, refs `VNEngine.Core`, `Unity.TextMeshPro`, `UnityEngine.UI`):
- `VNEngine.Unity.asmdef`.
- `Presentation/StageViewUnity.cs` — `IStageView` over sprite slots + background.
- `Presentation/DialogueViewUnity.cs` — `IDialogueView` over TMP typewriter + choice buttons.
- `VnScriptLoader.cs` — loads `.vns` `TextAsset`s from `Resources/scripts` and compiles them (mobile-safe, no `File.IO`).
- `VNRunner.cs` — coroutine host; loads scripts, compiles, ticks interpreter, routes click/tap input.

**Editor layer** — `Assets/Scripts/VNEngine/Editor/` (asmdef `VNEngine.Editor`, Editor-only):
- `VNEngine.Editor.asmdef`.
- `VnsImporter.cs` — `ScriptedImporter` that imports `.vns` files as `TextAsset` (so they load via `Resources.LoadAll<TextAsset>` on every platform).

**Tests** — `Assets/Tests/Editor/` (asmdef `VNEngine.Tests`, refs `VNEngine.Core` + test framework):
- `VNEngine.Tests.asmdef`.
- `Fakes/FakeDialogueView.cs`, `Fakes/FakeStageView.cs`.
- `VnValueTests.cs`, `GameStateTests.cs`, `ExprParserTests.cs`, `ExprEvalTests.cs`, `LineReaderTests.cs`, `ParserTests.cs`, `CompilerTests.cs`, `InterpreterTests.cs`, `MenuInterpreterTests.cs`.

**Content / migration:**
- `Assets/Resources/scripts/intro.vns` — DSL port of `dialogues.json` (imported as `TextAsset` by `VnsImporter`).
- Removed at end: `Assets/Scripts/DialogueManager.cs`, `Assets/StreamingAssets/dialogues.json`.

---

## Task 1: Assembly scaffolding + smoke test

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/VNEngine.Core.asmdef`
- Create: `Assets/Scripts/VNEngine/Unity/VNEngine.Unity.asmdef`
- Create: `Assets/Tests/Editor/VNEngine.Tests.asmdef`
- Create: `Assets/Scripts/VNEngine/Core/VnEngineInfo.cs` (permanent public marker type)
- Create: `Assets/Tests/Editor/SmokeTest.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: three compiling assemblies; `VNEngine` namespace resolvable from tests.

- [ ] **Step 1: Create the core asmdef**

`Assets/Scripts/VNEngine/Core/VNEngine.Core.asmdef`:
```json
{
    "name": "VNEngine.Core",
    "rootNamespace": "VNEngine",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

- [ ] **Step 2: Create the Unity-layer asmdef**

`Assets/Scripts/VNEngine/Unity/VNEngine.Unity.asmdef`:
```json
{
    "name": "VNEngine.Unity",
    "rootNamespace": "VNEngine.Unity",
    "references": [
        "VNEngine.Core",
        "Unity.TextMeshPro",
        "UnityEngine.UI"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "autoReferenced": true,
    "noEngineReferences": false
}
```

- [ ] **Step 3: Create the test asmdef**

`Assets/Tests/Editor/VNEngine.Tests.asmdef`:
```json
{
    "name": "VNEngine.Tests",
    "rootNamespace": "VNEngine.Tests",
    "references": [
        "VNEngine.Core",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ]
}
```

- [ ] **Step 4: Create a permanent public marker type so the core assembly is non-empty and cross-assembly referencing is verified**

`Assets/Scripts/VNEngine/Core/VnEngineInfo.cs`:
```csharp
namespace VNEngine
{
    // Permanent, referenced by the smoke test to prove the test assembly can
    // see public Core types across the assembly boundary.
    public static class VnEngineInfo
    {
        public const string Name = "VNEngine.Core";
    }
}
```

- [ ] **Step 5: Write the smoke test**

`Assets/Tests/Editor/SmokeTest.cs`:
```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class SmokeTest
    {
        [Test]
        public void CoreAssemblyIsReferencable()
        {
            Assert.AreEqual("VNEngine.Core", VNEngine.VnEngineInfo.Name);
        }
    }
}
```

- [ ] **Step 6: Compile and run**

Refresh Unity (UnityMCP `refresh_unity`), poll `editor_state.isCompiling` until false, then `read_console` (types: error) — expect zero errors. Run tests: UnityMCP `run_tests` mode `EditMode`, filter `VNEngine.Tests.SmokeTest`.
Expected: 1 passed, 0 failed.

- [ ] **Step 7: Commit**
```bash
git add Assets/Scripts/VNEngine Assets/Tests
git commit -m "feat(vnengine): scaffold Core/Unity/Test assemblies + smoke test"
```

---

## Task 2: VnValue

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Values/VnValue.cs`
- Create: `Assets/Tests/Editor/VnValueTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum VNEngine.VnKind { Int, Bool }`
  - `readonly struct VNEngine.VnValue` with:
    - `static VnValue Int(int n)`, `static VnValue Bool(bool b)`
    - `VnKind Kind { get; }`, `int AsInt { get; }`, `bool AsBool { get; }`, `bool Truthy { get; }`
    - `override string ToString()` → `"true"`/`"false"` for Bool, decimal for Int
    - value equality via `Equals`/`GetHashCode`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/VnValueTests.cs`:
```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class VnValueTests
    {
        [Test] public void IntStoresValue() => Assert.AreEqual(42, VnValue.Int(42).AsInt);
        [Test] public void IntKind() => Assert.AreEqual(VnKind.Int, VnValue.Int(1).Kind);
        [Test] public void BoolKind() => Assert.AreEqual(VnKind.Bool, VnValue.Bool(true).Kind);
        [Test] public void BoolTrueAsBool() => Assert.IsTrue(VnValue.Bool(true).AsBool);
        [Test] public void BoolFalseAsBool() => Assert.IsFalse(VnValue.Bool(false).AsBool);

        [Test] public void ZeroIntIsFalsy() => Assert.IsFalse(VnValue.Int(0).Truthy);
        [Test] public void NonZeroIntIsTruthy() => Assert.IsTrue(VnValue.Int(3).Truthy);
        [Test] public void NegativeIntIsTruthy() => Assert.IsTrue(VnValue.Int(-1).Truthy);
        [Test] public void BoolTrueTruthy() => Assert.IsTrue(VnValue.Bool(true).Truthy);

        [Test] public void IntToString() => Assert.AreEqual("7", VnValue.Int(7).ToString());
        [Test] public void BoolTrueToString() => Assert.AreEqual("true", VnValue.Bool(true).ToString());
        [Test] public void BoolFalseToString() => Assert.AreEqual("false", VnValue.Bool(false).ToString());

        [Test] public void EqualityByKindAndValue()
        {
            Assert.AreEqual(VnValue.Int(5), VnValue.Int(5));
            Assert.AreNotEqual(VnValue.Int(1), VnValue.Bool(true));
            Assert.AreNotEqual(VnValue.Int(0), VnValue.Bool(false));
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run tests filter `VNEngine.Tests.VnValueTests`. Expected: compile error / fail (`VnValue` shape not final).

- [ ] **Step 3: Implement VnValue**

Create `Assets/Scripts/VNEngine/Core/Values/VnValue.cs`:
```csharp
namespace VNEngine
{
    public enum VnKind { Int, Bool }

    public readonly struct VnValue : System.IEquatable<VnValue>
    {
        public VnKind Kind { get; }
        private readonly int _i; // Int payload, or Bool as 0/1

        private VnValue(VnKind kind, int i) { Kind = kind; _i = i; }

        public static VnValue Int(int n) => new VnValue(VnKind.Int, n);
        public static VnValue Bool(bool b) => new VnValue(VnKind.Bool, b ? 1 : 0);

        public int AsInt => _i;
        public bool AsBool => _i != 0;
        public bool Truthy => _i != 0;

        public override string ToString() =>
            Kind == VnKind.Bool ? (_i != 0 ? "true" : "false") : _i.ToString();

        public bool Equals(VnValue other) => other.Kind == Kind && other._i == _i;
        public override bool Equals(object obj) => obj is VnValue v && Equals(v);
        public override int GetHashCode() => (_i * 397) ^ (int)Kind;
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run tests filter `VNEngine.Tests.VnValueTests`. Expected: 12 passed.

- [ ] **Step 5: Commit**
```bash
git add Assets/Scripts/VNEngine/Core/Values/VnValue.cs Assets/Tests/Editor/VnValueTests.cs
git commit -m "feat(vnengine): VnValue int/bool value type"
```

---

## Task 3: IRandom + GameState

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Runtime/IRandom.cs`
- Create: `Assets/Scripts/VNEngine/Core/Runtime/GameState.cs`
- Create: `Assets/Tests/Editor/GameStateTests.cs`

**Interfaces:**
- Consumes: `VnValue`.
- Produces:
  - `interface VNEngine.IRandom { int Range(int minInclusive, int maxInclusive); }`
  - `sealed class VNEngine.SeededRandom : IRandom` — ctor `SeededRandom(int seed)`.
  - `sealed class VNEngine.GameState` — ctor `GameState(IRandom random)`; `IRandom Random { get; }`; `VnValue Get(string name)` (default `Int(0)`); `void Set(string name, VnValue value)`; `bool Has(string name)`; `IReadOnlyDictionary<string,VnValue> Snapshot { get; }`.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/GameStateTests.cs`:
```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class GameStateTests
    {
        private GameState New() => new GameState(new SeededRandom(1));

        [Test] public void UndefinedVarIsIntZero()
        {
            var s = New();
            Assert.AreEqual(VnValue.Int(0), s.Get("gold"));
            Assert.IsFalse(s.Has("gold"));
        }

        [Test] public void SetThenGet()
        {
            var s = New();
            s.Set("gold", VnValue.Int(50));
            Assert.AreEqual(VnValue.Int(50), s.Get("gold"));
            Assert.IsTrue(s.Has("gold"));
        }

        [Test] public void SetOverwrites()
        {
            var s = New();
            s.Set("x", VnValue.Int(1));
            s.Set("x", VnValue.Bool(true));
            Assert.AreEqual(VnValue.Bool(true), s.Get("x"));
        }

        [Test] public void SeededRandomIsDeterministic()
        {
            var a = new SeededRandom(123);
            var b = new SeededRandom(123);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(a.Range(1, 6), b.Range(1, 6));
        }

        [Test] public void RandomInRangeInclusive()
        {
            var r = new SeededRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                int v = r.Range(1, 3);
                Assert.IsTrue(v >= 1 && v <= 3, $"out of range: {v}");
            }
        }

        [Test] public void RandomDegenerateRangeReturnsLow()
        {
            var r = new SeededRandom(7);
            Assert.AreEqual(5, r.Range(5, 5));
            Assert.AreEqual(9, r.Range(9, 2)); // hi < lo → lo
        }
    }
}
```

- [ ] **Step 2: Run to verify failure** — filter `VNEngine.Tests.GameStateTests`. Expected: compile error (types missing).

- [ ] **Step 3: Implement IRandom**

`Assets/Scripts/VNEngine/Core/Runtime/IRandom.cs`:
```csharp
namespace VNEngine
{
    public interface IRandom
    {
        // Inclusive on both ends.
        int Range(int minInclusive, int maxInclusive);
    }

    public sealed class SeededRandom : IRandom
    {
        private readonly System.Random _r;
        public SeededRandom(int seed) { _r = new System.Random(seed); }
        public int Range(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive) return minInclusive;
            return _r.Next(minInclusive, maxInclusive + 1);
        }
    }
}
```

- [ ] **Step 4: Implement GameState**

`Assets/Scripts/VNEngine/Core/Runtime/GameState.cs`:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    public sealed class GameState
    {
        private readonly Dictionary<string, VnValue> _vars = new Dictionary<string, VnValue>();

        public IRandom Random { get; }

        public GameState(IRandom random)
        {
            Random = random ?? throw new System.ArgumentNullException(nameof(random));
        }

        public VnValue Get(string name) =>
            _vars.TryGetValue(name, out var v) ? v : VnValue.Int(0);

        public void Set(string name, VnValue value) => _vars[name] = value;

        public bool Has(string name) => _vars.ContainsKey(name);

        public IReadOnlyDictionary<string, VnValue> Snapshot => _vars;
    }
}
```

- [ ] **Step 5: Run to verify pass** — filter `VNEngine.Tests.GameStateTests`. Expected: 6 passed.

- [ ] **Step 6: Commit**
```bash
git add Assets/Scripts/VNEngine/Core/Runtime/IRandom.cs Assets/Scripts/VNEngine/Core/Runtime/GameState.cs Assets/Tests/Editor/GameStateTests.cs
git commit -m "feat(vnengine): IRandom + GameState variable store"
```

---

## Task 4: Errors + Expression AST + Expression parser

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Errors/VnException.cs`
- Create: `Assets/Scripts/VNEngine/Core/Expressions/Expr.cs`
- Create: `Assets/Scripts/VNEngine/Core/Expressions/ExprParser.cs`
- Create: `Assets/Tests/Editor/ExprParserTests.cs`

**Interfaces:**
- Consumes: `VnValue`.
- Produces:
  - `class VNEngine.VnException : System.Exception` (ctor `(string)`); `VnParseException : VnException`; `VnRuntimeException : VnException`.
  - Expr nodes: `abstract class Expr`; `LitExpr { VnValue Value }`; `VarExpr { string Name }`; `UnaryExpr { string Op; Expr Operand }` (Op ∈ `"-"`,`"not"`); `BinaryExpr { string Op; Expr Left; Expr Right }` (Op ∈ `+ - * / % >= <= > < == != and or`); `RandomExpr { Expr Lo; Expr Hi }`.
  - `static class VNEngine.ExprParser { static Expr Parse(string src) }` — throws `VnParseException` on malformed input.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/ExprParserTests.cs`:
```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class ExprParserTests
    {
        [Test] public void IntLiteral()
        {
            var e = (LitExpr)ExprParser.Parse("42");
            Assert.AreEqual(VnValue.Int(42), e.Value);
        }

        [Test] public void TrueFalseLiterals()
        {
            Assert.AreEqual(VnValue.Bool(true), ((LitExpr)ExprParser.Parse("true")).Value);
            Assert.AreEqual(VnValue.Bool(false), ((LitExpr)ExprParser.Parse("false")).Value);
        }

        [Test] public void Variable()
        {
            var e = (VarExpr)ExprParser.Parse("gold");
            Assert.AreEqual("gold", e.Name);
        }

        [Test] public void UnicodeVariable()
        {
            var e = (VarExpr)ExprParser.Parse("요르");
            Assert.AreEqual("요르", e.Name);
        }

        [Test] public void AdditionParsesAsBinary()
        {
            var e = (BinaryExpr)ExprParser.Parse("a + 2");
            Assert.AreEqual("+", e.Op);
            Assert.AreEqual("a", ((VarExpr)e.Left).Name);
            Assert.AreEqual(VnValue.Int(2), ((LitExpr)e.Right).Value);
        }

        [Test] public void PrecedenceMulOverAdd()
        {
            // a + b * 2  ->  (+ a (* b 2))
            var e = (BinaryExpr)ExprParser.Parse("a + b * 2");
            Assert.AreEqual("+", e.Op);
            var right = (BinaryExpr)e.Right;
            Assert.AreEqual("*", right.Op);
        }

        [Test] public void ParenOverridesPrecedence()
        {
            // (a + b) * 2 -> (* (+ a b) 2)
            var e = (BinaryExpr)ExprParser.Parse("(a + b) * 2");
            Assert.AreEqual("*", e.Op);
            Assert.AreEqual("+", ((BinaryExpr)e.Left).Op);
        }

        [Test] public void ComparisonBelowArithmetic()
        {
            // a + 1 >= 5 -> (>= (+ a 1) 5)
            var e = (BinaryExpr)ExprParser.Parse("a + 1 >= 5");
            Assert.AreEqual(">=", e.Op);
            Assert.AreEqual("+", ((BinaryExpr)e.Left).Op);
        }

        [Test] public void AndOrPrecedence()
        {
            // a or b and c -> (or a (and b c))
            var e = (BinaryExpr)ExprParser.Parse("a or b and c");
            Assert.AreEqual("or", e.Op);
            Assert.AreEqual("and", ((BinaryExpr)e.Right).Op);
        }

        [Test] public void NotUnary()
        {
            var e = (UnaryExpr)ExprParser.Parse("not met");
            Assert.AreEqual("not", e.Op);
            Assert.AreEqual("met", ((VarExpr)e.Operand).Name);
        }

        [Test] public void NegativeNumber()
        {
            var e = (UnaryExpr)ExprParser.Parse("-3");
            Assert.AreEqual("-", e.Op);
            Assert.AreEqual(VnValue.Int(3), ((LitExpr)e.Operand).Value);
        }

        [Test] public void RandomCall()
        {
            var e = (RandomExpr)ExprParser.Parse("random(1, 6)");
            Assert.AreEqual(VnValue.Int(1), ((LitExpr)e.Lo).Value);
            Assert.AreEqual(VnValue.Int(6), ((LitExpr)e.Hi).Value);
        }

        [Test] public void NotEqualOperator()
        {
            var e = (BinaryExpr)ExprParser.Parse("a != 2");
            Assert.AreEqual("!=", e.Op);
        }

        [Test] public void UnbalancedParenThrows()
        {
            Assert.Throws<VnParseException>(() => ExprParser.Parse("(a + 2"));
        }

        [Test] public void TrailingGarbageThrows()
        {
            Assert.Throws<VnParseException>(() => ExprParser.Parse("a 2"));
        }

        [Test] public void EmptyThrows()
        {
            Assert.Throws<VnParseException>(() => ExprParser.Parse("   "));
        }
    }
}
```

- [ ] **Step 2: Run to verify failure** — filter `VNEngine.Tests.ExprParserTests`. Expected: compile error.

- [ ] **Step 3: Implement errors**

`Assets/Scripts/VNEngine/Core/Errors/VnException.cs`:
```csharp
namespace VNEngine
{
    public class VnException : System.Exception
    {
        public VnException(string message) : base(message) { }
    }
    public sealed class VnParseException : VnException
    {
        public VnParseException(string message) : base(message) { }
    }
    public sealed class VnRuntimeException : VnException
    {
        public VnRuntimeException(string message) : base(message) { }
    }
}
```

- [ ] **Step 4: Implement Expr nodes**

`Assets/Scripts/VNEngine/Core/Expressions/Expr.cs`:
```csharp
namespace VNEngine
{
    public abstract class Expr { }

    public sealed class LitExpr : Expr { public VnValue Value; }
    public sealed class VarExpr : Expr { public string Name; }
    public sealed class UnaryExpr : Expr { public string Op; public Expr Operand; }
    public sealed class BinaryExpr : Expr { public string Op; public Expr Left; public Expr Right; }
    public sealed class RandomExpr : Expr { public Expr Lo; public Expr Hi; }
}
```

- [ ] **Step 5: Implement ExprParser**

`Assets/Scripts/VNEngine/Core/Expressions/ExprParser.cs`:
```csharp
using System.Collections.Generic;
using System.Globalization;

namespace VNEngine
{
    // Recursive-descent parser. Grammar (low → high precedence):
    //   or   := and ("or" and)*
    //   and  := cmp ("and" cmp)*
    //   cmp  := add (("=="|"!="|">="|"<="|">"|"<") add)*
    //   add  := mul (("+"|"-") mul)*
    //   mul  := unary (("*"|"/"|"%") unary)*
    //   unary:= ("-"|"not") unary | primary
    //   primary := INT | "true" | "false" | "random" "(" or "," or ")" | IDENT | "(" or ")"
    public static class ExprParser
    {
        public static Expr Parse(string src)
        {
            var tokens = Tokenize(src);
            int pos = 0;
            var expr = ParseOr(tokens, ref pos);
            if (pos != tokens.Count)
                throw new VnParseException($"Unexpected token '{tokens[pos].Text}' in expression: {src}");
            return expr;
        }

        private enum TT { Int, Ident, Op, LParen, RParen, Comma }
        private struct Tok { public TT Type; public string Text; }

        private static List<Tok> Tokenize(string s)
        {
            var toks = new List<Tok>();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (char.IsDigit(c))
                {
                    int start = i;
                    while (i < s.Length && char.IsDigit(s[i])) i++;
                    toks.Add(new Tok { Type = TT.Int, Text = s.Substring(start, i - start) });
                    continue;
                }
                if (c == '(') { toks.Add(new Tok { Type = TT.LParen, Text = "(" }); i++; continue; }
                if (c == ')') { toks.Add(new Tok { Type = TT.RParen, Text = ")" }); i++; continue; }
                if (c == ',') { toks.Add(new Tok { Type = TT.Comma, Text = "," }); i++; continue; }
                // two-char operators
                if (i + 1 < s.Length)
                {
                    string two = s.Substring(i, 2);
                    if (two == ">=" || two == "<=" || two == "==" || two == "!=")
                    { toks.Add(new Tok { Type = TT.Op, Text = two }); i += 2; continue; }
                }
                if (c == '>' || c == '<' || c == '+' || c == '-' || c == '*' || c == '/' || c == '%')
                { toks.Add(new Tok { Type = TT.Op, Text = c.ToString() }); i++; continue; }
                // identifier: letters/digits/underscore/unicode, not starting with digit (handled above)
                if (IsIdentChar(c))
                {
                    int start = i;
                    while (i < s.Length && IsIdentChar(s[i])) i++;
                    string word = s.Substring(start, i - start);
                    if (word == "and" || word == "or" || word == "not")
                        toks.Add(new Tok { Type = TT.Op, Text = word });
                    else
                        toks.Add(new Tok { Type = TT.Ident, Text = word });
                    continue;
                }
                throw new VnParseException($"Unexpected character '{c}' in expression: {s}");
            }
            return toks;
        }

        private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        private static Tok? Peek(List<Tok> t, int pos) => pos < t.Count ? t[pos] : (Tok?)null;

        private static Expr ParseOr(List<Tok> t, ref int pos)
        {
            var left = ParseAnd(t, ref pos);
            while (Peek(t, pos) is Tok tk && tk.Type == TT.Op && tk.Text == "or")
            { pos++; var right = ParseAnd(t, ref pos); left = new BinaryExpr { Op = "or", Left = left, Right = right }; }
            return left;
        }

        private static Expr ParseAnd(List<Tok> t, ref int pos)
        {
            var left = ParseCmp(t, ref pos);
            while (Peek(t, pos) is Tok tk && tk.Type == TT.Op && tk.Text == "and")
            { pos++; var right = ParseCmp(t, ref pos); left = new BinaryExpr { Op = "and", Left = left, Right = right }; }
            return left;
        }

        private static Expr ParseCmp(List<Tok> t, ref int pos)
        {
            var left = ParseAdd(t, ref pos);
            while (Peek(t, pos) is Tok tk && tk.Type == TT.Op && IsCmp(tk.Text))
            { pos++; var right = ParseAdd(t, ref pos); left = new BinaryExpr { Op = tk.Text, Left = left, Right = right }; }
            return left;
        }

        private static bool IsCmp(string o) => o == "==" || o == "!=" || o == ">=" || o == "<=" || o == ">" || o == "<";

        private static Expr ParseAdd(List<Tok> t, ref int pos)
        {
            var left = ParseMul(t, ref pos);
            while (Peek(t, pos) is Tok tk && tk.Type == TT.Op && (tk.Text == "+" || tk.Text == "-"))
            { pos++; var right = ParseMul(t, ref pos); left = new BinaryExpr { Op = tk.Text, Left = left, Right = right }; }
            return left;
        }

        private static Expr ParseMul(List<Tok> t, ref int pos)
        {
            var left = ParseUnary(t, ref pos);
            while (Peek(t, pos) is Tok tk && tk.Type == TT.Op && (tk.Text == "*" || tk.Text == "/" || tk.Text == "%"))
            { pos++; var right = ParseUnary(t, ref pos); left = new BinaryExpr { Op = tk.Text, Left = left, Right = right }; }
            return left;
        }

        private static Expr ParseUnary(List<Tok> t, ref int pos)
        {
            if (Peek(t, pos) is Tok tk && tk.Type == TT.Op && (tk.Text == "-" || tk.Text == "not"))
            { pos++; var operand = ParseUnary(t, ref pos); return new UnaryExpr { Op = tk.Text, Operand = operand }; }
            return ParsePrimary(t, ref pos);
        }

        private static Expr ParsePrimary(List<Tok> t, ref int pos)
        {
            if (!(Peek(t, pos) is Tok tk))
                throw new VnParseException("Unexpected end of expression");

            if (tk.Type == TT.Int)
            {
                pos++;
                return new LitExpr { Value = VnValue.Int(int.Parse(tk.Text, CultureInfo.InvariantCulture)) };
            }
            if (tk.Type == TT.Ident)
            {
                if (tk.Text == "true") { pos++; return new LitExpr { Value = VnValue.Bool(true) }; }
                if (tk.Text == "false") { pos++; return new LitExpr { Value = VnValue.Bool(false) }; }
                if (tk.Text == "random")
                {
                    pos++;
                    Expect(t, ref pos, TT.LParen, "(");
                    var lo = ParseOr(t, ref pos);
                    ExpectComma(t, ref pos);
                    var hi = ParseOr(t, ref pos);
                    Expect(t, ref pos, TT.RParen, ")");
                    return new RandomExpr { Lo = lo, Hi = hi };
                }
                pos++;
                return new VarExpr { Name = tk.Text };
            }
            if (tk.Type == TT.LParen)
            {
                pos++;
                var inner = ParseOr(t, ref pos);
                Expect(t, ref pos, TT.RParen, ")");
                return inner;
            }
            throw new VnParseException($"Unexpected token '{tk.Text}' in expression");
        }

        private static void Expect(List<Tok> t, ref int pos, TT type, string what)
        {
            if (!(Peek(t, pos) is Tok tk) || tk.Type != type)
                throw new VnParseException($"Expected '{what}' in expression");
            pos++;
        }

        private static void ExpectComma(List<Tok> t, ref int pos)
        {
            if (!(Peek(t, pos) is Tok tk) || tk.Type != TT.Comma)
                throw new VnParseException("Expected ',' in random(...)");
            pos++;
        }
    }
}
```

- [ ] **Step 6: Run to verify pass** — filter `VNEngine.Tests.ExprParserTests`. Expected: 16 passed.

- [ ] **Step 7: Commit**
```bash
git add Assets/Scripts/VNEngine/Core/Errors Assets/Scripts/VNEngine/Core/Expressions Assets/Tests/Editor/ExprParserTests.cs
git commit -m "feat(vnengine): expression AST + recursive-descent parser"
```

---

## Task 5: Expression evaluator

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Expressions/ExprEval.cs`
- Create: `Assets/Tests/Editor/ExprEvalTests.cs`

**Interfaces:**
- Consumes: `Expr` nodes, `GameState`, `VnValue`, `VnRuntimeException`.
- Produces: `static class VNEngine.ExprEval { static VnValue Eval(Expr e, GameState state) }`.
- Semantics: arithmetic (`+ - * / %`) requires both operands `Int`, yields `Int`; division/modulo by zero → `VnRuntimeException`; integer division truncates toward zero (C# default). Comparisons (`> < >= <= == !=`) yield `Bool`; `==`/`!=` compare by kind+value; ordering operators require `Int` operands. Logical (`and or not`) operate on `Truthy`, yield `Bool`. Unary `-` requires `Int`. `random(a,b)` requires `Int` bounds, yields `Int` via `state.Random.Range`.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/ExprEvalTests.cs`:
```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class ExprEvalTests
    {
        private GameState _s;

        [SetUp] public void Setup() => _s = new GameState(new SeededRandom(42));

        private VnValue Eval(string src) => ExprEval.Eval(ExprParser.Parse(src), _s);

        [Test] public void IntLiteral() => Assert.AreEqual(VnValue.Int(5), Eval("5"));
        [Test] public void UndefinedVarIsZero() => Assert.AreEqual(VnValue.Int(0), Eval("gold"));

        [Test] public void Addition() => Assert.AreEqual(VnValue.Int(7), Eval("3 + 4"));
        [Test] public void Subtraction() => Assert.AreEqual(VnValue.Int(-1), Eval("3 - 4"));
        [Test] public void Multiplication() => Assert.AreEqual(VnValue.Int(12), Eval("3 * 4"));
        [Test] public void IntegerDivisionTruncates() => Assert.AreEqual(VnValue.Int(3), Eval("7 / 2"));
        [Test] public void Modulo() => Assert.AreEqual(VnValue.Int(1), Eval("7 % 2"));
        [Test] public void Precedence() => Assert.AreEqual(VnValue.Int(11), Eval("3 + 4 * 2"));
        [Test] public void Parens() => Assert.AreEqual(VnValue.Int(14), Eval("(3 + 4) * 2"));
        [Test] public void NegativeUnary() => Assert.AreEqual(VnValue.Int(-3), Eval("-3"));

        [Test] public void VariableArithmetic()
        {
            _s.Set("gold", VnValue.Int(10));
            _s.Set("yield", VnValue.Int(5));
            Assert.AreEqual(VnValue.Int(20), Eval("gold + yield * 2"));
        }

        [Test] public void GreaterEqualTrue() => Assert.AreEqual(VnValue.Bool(true), Eval("5 >= 5"));
        [Test] public void LessThanFalse() => Assert.AreEqual(VnValue.Bool(false), Eval("5 < 3"));
        [Test] public void EqualityInt() => Assert.AreEqual(VnValue.Bool(true), Eval("2 == 2"));
        [Test] public void NotEqualInt() => Assert.AreEqual(VnValue.Bool(true), Eval("2 != 3"));

        [Test] public void VarVsVarComparison()
        {
            _s.Set("a", VnValue.Int(7));
            _s.Set("b", VnValue.Int(3));
            Assert.AreEqual(VnValue.Bool(true), Eval("a > b"));
        }

        [Test] public void AndOrNot()
        {
            _s.Set("met", VnValue.Bool(true));
            _s.Set("gold", VnValue.Int(100));
            Assert.AreEqual(VnValue.Bool(true), Eval("gold >= 50 and met"));
            Assert.AreEqual(VnValue.Bool(false), Eval("gold >= 50 and not met"));
            Assert.AreEqual(VnValue.Bool(true), Eval("gold < 0 or met"));
        }

        [Test] public void BoolTruthyFlag()
        {
            _s.Set("flag", VnValue.Int(1)); // int used as flag
            Assert.AreEqual(VnValue.Bool(true), Eval("flag and true"));
        }

        [Test] public void RandomWithinBounds()
        {
            for (int i = 0; i < 200; i++)
            {
                var v = Eval("random(1, 6)");
                Assert.AreEqual(VnKind.Int, v.Kind);
                Assert.IsTrue(v.AsInt >= 1 && v.AsInt <= 6);
            }
        }

        [Test] public void DivideByZeroThrows()
            => Assert.Throws<VnRuntimeException>(() => Eval("1 / 0"));

        [Test] public void ModuloByZeroThrows()
            => Assert.Throws<VnRuntimeException>(() => Eval("1 % 0"));

        [Test] public void ArithmeticOnBoolThrows()
            => Assert.Throws<VnRuntimeException>(() => Eval("true + 1"));

        [Test] public void OrderingOnBoolThrows()
            => Assert.Throws<VnRuntimeException>(() => Eval("true > false"));
    }
}
```

- [ ] **Step 2: Run to verify failure** — filter `VNEngine.Tests.ExprEvalTests`. Expected: compile error (`ExprEval` missing).

- [ ] **Step 3: Implement ExprEval**

`Assets/Scripts/VNEngine/Core/Expressions/ExprEval.cs`:
```csharp
namespace VNEngine
{
    public static class ExprEval
    {
        public static VnValue Eval(Expr e, GameState state)
        {
            switch (e)
            {
                case LitExpr lit: return lit.Value;
                case VarExpr v: return state.Get(v.Name);
                case UnaryExpr u: return EvalUnary(u, state);
                case BinaryExpr b: return EvalBinary(b, state);
                case RandomExpr r: return EvalRandom(r, state);
                default: throw new VnRuntimeException($"Unknown expression node: {e?.GetType().Name}");
            }
        }

        private static VnValue EvalUnary(UnaryExpr u, GameState s)
        {
            var v = Eval(u.Operand, s);
            if (u.Op == "-")
            {
                if (v.Kind != VnKind.Int) throw new VnRuntimeException("Unary '-' requires an integer");
                return VnValue.Int(-v.AsInt);
            }
            if (u.Op == "not") return VnValue.Bool(!v.Truthy);
            throw new VnRuntimeException($"Unknown unary operator '{u.Op}'");
        }

        private static VnValue EvalRandom(RandomExpr r, GameState s)
        {
            var lo = Eval(r.Lo, s);
            var hi = Eval(r.Hi, s);
            if (lo.Kind != VnKind.Int || hi.Kind != VnKind.Int)
                throw new VnRuntimeException("random(a, b) requires integer bounds");
            return VnValue.Int(s.Random.Range(lo.AsInt, hi.AsInt));
        }

        private static VnValue EvalBinary(BinaryExpr b, GameState s)
        {
            // Short-circuit logical operators.
            if (b.Op == "and")
            {
                var l = Eval(b.Left, s);
                if (!l.Truthy) return VnValue.Bool(false);
                return VnValue.Bool(Eval(b.Right, s).Truthy);
            }
            if (b.Op == "or")
            {
                var l = Eval(b.Left, s);
                if (l.Truthy) return VnValue.Bool(true);
                return VnValue.Bool(Eval(b.Right, s).Truthy);
            }

            var left = Eval(b.Left, s);
            var right = Eval(b.Right, s);

            switch (b.Op)
            {
                case "+": return VnValue.Int(Int(left, "+") + Int(right, "+"));
                case "-": return VnValue.Int(Int(left, "-") - Int(right, "-"));
                case "*": return VnValue.Int(Int(left, "*") * Int(right, "*"));
                case "/":
                    { int d = Int(right, "/"); if (d == 0) throw new VnRuntimeException("Division by zero"); return VnValue.Int(Int(left, "/") / d); }
                case "%":
                    { int d = Int(right, "%"); if (d == 0) throw new VnRuntimeException("Modulo by zero"); return VnValue.Int(Int(left, "%") % d); }
                case "==": return VnValue.Bool(left.Equals(right));
                case "!=": return VnValue.Bool(!left.Equals(right));
                case ">": return VnValue.Bool(Int(left, ">") > Int(right, ">"));
                case "<": return VnValue.Bool(Int(left, "<") < Int(right, "<"));
                case ">=": return VnValue.Bool(Int(left, ">=") >= Int(right, ">="));
                case "<=": return VnValue.Bool(Int(left, "<=") <= Int(right, "<="));
                default: throw new VnRuntimeException($"Unknown operator '{b.Op}'");
            }
        }

        private static int Int(VnValue v, string op)
        {
            if (v.Kind != VnKind.Int)
                throw new VnRuntimeException($"Operator '{op}' requires integer operands");
            return v.AsInt;
        }
    }
}
```

- [ ] **Step 4: Run to verify pass** — filter `VNEngine.Tests.ExprEvalTests`. Expected: 24 passed.

- [ ] **Step 5: Commit**
```bash
git add Assets/Scripts/VNEngine/Core/Expressions/ExprEval.cs Assets/Tests/Editor/ExprEvalTests.cs
git commit -m "feat(vnengine): expression evaluator (int/bool, integer division)"
```

---

## Task 6: LineReader (logical lines + indentation)

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Parsing/LogicalLine.cs`
- Create: `Assets/Scripts/VNEngine/Core/Parsing/LineReader.cs`
- Create: `Assets/Tests/Editor/LineReaderTests.cs`

**Interfaces:**
- Consumes: `VnParseException`.
- Produces:
  - `readonly struct VNEngine.LogicalLine { int Indent; string Text; int LineNumber; string File; }` with ctor `(int indent, string text, int lineNumber, string file)`.
  - `static class VNEngine.LineReader { static System.Collections.Generic.List<LogicalLine> Read(string source, string file) }`.
- Rules: split on `\n` (strip trailing `\r`); drop blank lines and full-line comments (`#...`); strip inline comments that begin with ` #` (space-hash) unless inside a quoted string; `Indent` = count of leading spaces; a tab anywhere in leading whitespace → `VnParseException`; `Text` is the trimmed remainder; `LineNumber` is 1-based line in `source`.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/LineReaderTests.cs`:
```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class LineReaderTests
    {
        [Test] public void SkipsBlankAndCommentLines()
        {
            var src = "\n# a comment\n요르 \"hi\"\n\n";
            var lines = LineReader.Read(src, "f.vns");
            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual("요르 \"hi\"", lines[0].Text);
            Assert.AreEqual(0, lines[0].Indent);
            Assert.AreEqual(3, lines[0].LineNumber);
        }

        [Test] public void CountsIndentSpaces()
        {
            var src = "menu:\n    \"a\":\n        jump x";
            var lines = LineReader.Read(src, "f.vns");
            Assert.AreEqual(0, lines[0].Indent);
            Assert.AreEqual(4, lines[1].Indent);
            Assert.AreEqual(8, lines[2].Indent);
        }

        [Test] public void StripsTrailingCarriageReturn()
        {
            var lines = LineReader.Read("label a:\r\n    return\r\n", "f.vns");
            Assert.AreEqual("label a:", lines[0].Text);
            Assert.AreEqual("return", lines[1].Text);
        }

        [Test] public void InlineCommentStripped()
        {
            var lines = LineReader.Read("jump x  # go", "f.vns");
            Assert.AreEqual("jump x", lines[0].Text);
        }

        [Test] public void HashInsideQuotesKept()
        {
            var lines = LineReader.Read("나 \"# 1등이야\"", "f.vns");
            Assert.AreEqual("나 \"# 1등이야\"", lines[0].Text);
        }

        [Test] public void TabIndentThrows()
        {
            Assert.Throws<VnParseException>(() => LineReader.Read("\t요르 \"x\"", "f.vns"));
        }

        [Test] public void CarriesFileName()
        {
            var lines = LineReader.Read("return", "chap1.vns");
            Assert.AreEqual("chap1.vns", lines[0].File);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure** — filter `VNEngine.Tests.LineReaderTests`. Expected: compile error.

- [ ] **Step 3: Implement LogicalLine**

`Assets/Scripts/VNEngine/Core/Parsing/LogicalLine.cs`:
```csharp
namespace VNEngine
{
    public readonly struct LogicalLine
    {
        public readonly int Indent;
        public readonly string Text;
        public readonly int LineNumber;
        public readonly string File;

        public LogicalLine(int indent, string text, int lineNumber, string file)
        {
            Indent = indent; Text = text; LineNumber = lineNumber; File = file;
        }
    }
}
```

- [ ] **Step 4: Implement LineReader**

`Assets/Scripts/VNEngine/Core/Parsing/LineReader.cs`:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    public static class LineReader
    {
        public static List<LogicalLine> Read(string source, string file)
        {
            var result = new List<LogicalLine>();
            if (source == null) return result;

            string[] raw = source.Split('\n');
            for (int i = 0; i < raw.Length; i++)
            {
                string line = raw[i];
                if (line.EndsWith("\r")) line = line.Substring(0, line.Length - 1);

                // measure indentation, rejecting tabs
                int indent = 0;
                while (indent < line.Length && line[indent] == ' ') indent++;
                for (int k = 0; k < indent + 1 && k < line.Length; k++)
                {
                    if (line[k] == '\t')
                        throw new VnParseException($"{file}:{i + 1}: tab used in indentation (use spaces)");
                }

                string body = StripInlineComment(line.Substring(indent));
                body = body.TrimEnd();

                if (body.Length == 0) continue;              // blank
                if (body[0] == '#') continue;                // full-line comment

                result.Add(new LogicalLine(indent, body, i + 1, file));
            }
            return result;
        }

        // Removes a trailing " # comment" that is not inside double quotes.
        private static string StripInlineComment(string s)
        {
            bool inQuote = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"') inQuote = !inQuote;
                else if (c == '#' && !inQuote && i > 0 && s[i - 1] == ' ')
                    return s.Substring(0, i);
            }
            return s;
        }
    }
}
```

- [ ] **Step 5: Run to verify pass** — filter `VNEngine.Tests.LineReaderTests`. Expected: 7 passed.

- [ ] **Step 6: Commit**
```bash
git add Assets/Scripts/VNEngine/Core/Parsing/LogicalLine.cs Assets/Scripts/VNEngine/Core/Parsing/LineReader.cs Assets/Tests/Editor/LineReaderTests.cs
git commit -m "feat(vnengine): LineReader (logical lines + indentation)"
```

---

## Task 7: Command AST + Parser

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Parsing/Command.cs`
- Create: `Assets/Scripts/VNEngine/Core/Parsing/Parser.cs`
- Create: `Assets/Tests/Editor/ParserTests.cs`

**Interfaces:**
- Consumes: `LogicalLine`, `Expr`, `ExprParser`, `VnParseException`.
- Produces:
  - Command nodes (all in `VNEngine`): `abstract class Command { int Line; string File; }` and:
    - `CharacterDefCommand { string Id; string DisplayName; string Color; }`
    - `LabelCommand { string Name; }`
    - `SayCommand { string SpeakerRef; string Text; }` (`SpeakerRef == null` → narration)
    - `BgCommand { string Name; }`
    - `ShowCommand { string Character; string Position; }` (Position default `"center"`)
    - `HideCommand { string Character; }`
    - `SetCommand { string Var; Expr Value; }`
    - `JumpCommand { string Label; }`
    - `CallCommand { string Label; }`
    - `ReturnCommand { }`
    - `IfCommand { List<IfBranch> Branches; }` with `class IfBranch { Expr Condition; /*null=else*/ List<Command> Body; }`
    - `WhileCommand { Expr Condition; List<Command> Body; }`
    - `MenuCommand { List<MenuChoiceNode> Choices; }` with `class MenuChoiceNode { string Label; Expr Condition; /*nullable*/ List<Command> Body; }`
  - `static class VNEngine.Parser { static List<Command> Parse(List<LogicalLine> lines) }`.
- Rules:
  - A line whose first whitespace-delimited word is a keyword (`character`, `label`, `bg`, `show`, `hide`, `menu`, `if`, `elif`, `else`, `while`, `jump`, `call`, `return`) is that statement; `$` prefix is `SetCommand`; a line starting with `"` is narration; otherwise it is `speaker "text"`.
  - Block headers end with `:` (`label`, `menu`, `if`/`elif`/`else`, `while`, and menu choice lines). A block body is the run of following lines with strictly greater indent.
  - `elif`/`else` must immediately follow an `if`/`elif` block at the same indent; otherwise `VnParseException`.
  - `show <name> [left|center|right]` — missing position defaults to `center`; unknown position → `VnParseException`.
  - `$ <var> <op> <expr>` where op ∈ `= += -= *= /=`; compound ops compile to `Value = Binary(Var(var), rhs)` (e.g. `+=` → `var + (rhs)`).
  - Menu choice header: `"<label>" [if <expr>] :`.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/ParserTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class ParserTests
    {
        private static List<Command> Parse(string src) =>
            Parser.Parse(LineReader.Read(src, "t.vns"));

        [Test] public void Narration()
        {
            var c = (SayCommand)Parse("\"…정적이 흘렀다.\"")[0];
            Assert.IsNull(c.SpeakerRef);
            Assert.AreEqual("…정적이 흘렀다.", c.Text);
        }

        [Test] public void SpeakerLine()
        {
            var c = (SayCommand)Parse("요르 \"주말에 뭐 할래?\"")[0];
            Assert.AreEqual("요르", c.SpeakerRef);
            Assert.AreEqual("주말에 뭐 할래?", c.Text);
        }

        [Test] public void CharacterDef()
        {
            var c = (CharacterDefCommand)Parse("character 요르 name:\"요르 (숲의 요정)\" color:\"#8fd3ff\"")[0];
            Assert.AreEqual("요르", c.Id);
            Assert.AreEqual("요르 (숲의 요정)", c.DisplayName);
            Assert.AreEqual("#8fd3ff", c.Color);
        }

        [Test] public void CharacterDefNoColor()
        {
            var c = (CharacterDefCommand)Parse("character 나 name:\"나\"")[0];
            Assert.AreEqual("나", c.Id);
            Assert.AreEqual("나", c.DisplayName);
            Assert.IsNull(c.Color);
        }

        [Test] public void LabelStripsColon()
        {
            var c = (LabelCommand)Parse("label 데이트:")[0];
            Assert.AreEqual("데이트", c.Name);
        }

        [Test] public void ShowWithPosition()
        {
            var c = (ShowCommand)Parse("show 요르 left")[0];
            Assert.AreEqual("요르", c.Character);
            Assert.AreEqual("left", c.Position);
        }

        [Test] public void ShowDefaultsToCenter()
        {
            var c = (ShowCommand)Parse("show 민지")[0];
            Assert.AreEqual("center", c.Position);
        }

        [Test] public void ShowBadPositionThrows()
            => Assert.Throws<VnParseException>(() => Parse("show 요르 up"));

        [Test] public void HideAndBg()
        {
            Assert.AreEqual("민지", ((HideCommand)Parse("hide 민지")[0]).Character);
            Assert.AreEqual("공원", ((BgCommand)Parse("bg 공원")[0]).Name);
        }

        [Test] public void JumpCallReturn()
        {
            Assert.AreEqual("데이트", ((JumpCommand)Parse("jump 데이트")[0]).Label);
            Assert.AreEqual("인트로", ((CallCommand)Parse("call 인트로")[0]).Label);
            Assert.IsInstanceOf<ReturnCommand>(Parse("return")[0]);
        }

        [Test] public void SimpleAssignment()
        {
            var c = (SetCommand)Parse("$ 요르 = 10")[0];
            Assert.AreEqual("요르", c.Var);
            Assert.AreEqual(VnValue.Int(10), ExprEval.Eval(c.Value, new GameState(new SeededRandom(1))));
        }

        [Test] public void CompoundAssignmentExpands()
        {
            // $ gold += 5  →  gold + 5
            var s = new GameState(new SeededRandom(1));
            s.Set("gold", VnValue.Int(20));
            var c = (SetCommand)Parse("$ gold += 5")[0];
            Assert.AreEqual(VnValue.Int(25), ExprEval.Eval(c.Value, s));
        }

        [Test] public void IfElifElse()
        {
            var src =
                "if 요르 >= 50:\n" +
                "    요르 \"사귈래?\"\n" +
                "elif 요르 >= 0:\n" +
                "    요르 \"친구로.\"\n" +
                "else:\n" +
                "    요르 \"안 맞아.\"\n";
            var c = (IfCommand)Parse(src)[0];
            Assert.AreEqual(3, c.Branches.Count);
            Assert.IsNotNull(c.Branches[0].Condition);
            Assert.IsNotNull(c.Branches[1].Condition);
            Assert.IsNull(c.Branches[2].Condition); // else
            Assert.AreEqual("사귈래?", ((SayCommand)c.Branches[0].Body[0]).Text);
        }

        [Test] public void WhileBlock()
        {
            var src =
                "while 남은턴 > 0:\n" +
                "    $ 남은턴 -= 1\n";
            var c = (WhileCommand)Parse(src)[0];
            Assert.IsNotNull(c.Condition);
            Assert.AreEqual(1, c.Body.Count);
            Assert.IsInstanceOf<SetCommand>(c.Body[0]);
        }

        [Test] public void MenuWithConditionalChoice()
        {
            var src =
                "menu:\n" +
                "    \"같이 영화 보자\":\n" +
                "        jump 데이트\n" +
                "    \"금화를 준다\" if 골드 >= 10:\n" +
                "        $ 골드 -= 10\n" +
                "        jump 뇌물\n";
            var c = (MenuCommand)Parse(src)[0];
            Assert.AreEqual(2, c.Choices.Count);
            Assert.AreEqual("같이 영화 보자", c.Choices[0].Label);
            Assert.IsNull(c.Choices[0].Condition);
            Assert.AreEqual("금화를 준다", c.Choices[1].Label);
            Assert.IsNotNull(c.Choices[1].Condition);
            Assert.AreEqual(2, c.Choices[1].Body.Count);
        }

        [Test] public void ElseWithoutIfThrows()
            => Assert.Throws<VnParseException>(() => Parse("else:\n    return"));

        [Test] public void PreservesLineNumbers()
        {
            var c = Parse("\n\n요르 \"hi\"");
            Assert.AreEqual(3, c[0].Line);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure** — filter `VNEngine.Tests.ParserTests`. Expected: compile error.

- [ ] **Step 3: Implement Command nodes**

`Assets/Scripts/VNEngine/Core/Parsing/Command.cs`:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    public abstract class Command { public int Line; public string File; }

    public sealed class CharacterDefCommand : Command { public string Id; public string DisplayName; public string Color; }
    public sealed class LabelCommand : Command { public string Name; }
    public sealed class SayCommand : Command { public string SpeakerRef; public string Text; }
    public sealed class BgCommand : Command { public string Name; }
    public sealed class ShowCommand : Command { public string Character; public string Position; }
    public sealed class HideCommand : Command { public string Character; }
    public sealed class SetCommand : Command { public string Var; public Expr Value; }
    public sealed class JumpCommand : Command { public string Label; }
    public sealed class CallCommand : Command { public string Label; }
    public sealed class ReturnCommand : Command { }

    public sealed class IfBranch { public Expr Condition; public List<Command> Body; }
    public sealed class IfCommand : Command { public List<IfBranch> Branches; }

    public sealed class WhileCommand : Command { public Expr Condition; public List<Command> Body; }

    public sealed class MenuChoiceNode { public string Label; public Expr Condition; public List<Command> Body; }
    public sealed class MenuCommand : Command { public List<MenuChoiceNode> Choices; }
}
```

- [ ] **Step 4: Implement Parser**

`Assets/Scripts/VNEngine/Core/Parsing/Parser.cs`:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    public static class Parser
    {
        public static List<Command> Parse(List<LogicalLine> lines)
        {
            int pos = 0;
            return ParseBlock(lines, ref pos, 0);
        }

        // Parses all lines whose indent >= minIndent, stopping when indent drops below minIndent.
        private static List<Command> ParseBlock(List<LogicalLine> lines, ref int pos, int minIndent)
        {
            var result = new List<Command>();
            while (pos < lines.Count && lines[pos].Indent >= minIndent)
            {
                var line = lines[pos];
                // 'elif'/'else' are consumed by ParseIf; encountering them here is an error.
                string first = FirstWord(line.Text);
                if (first == "elif" || first == "else")
                    throw Err(line, $"'{first}' without matching 'if'");

                result.Add(ParseStatement(lines, ref pos));
            }
            return result;
        }

        private static Command ParseStatement(List<LogicalLine> lines, ref int pos)
        {
            var line = lines[pos];
            string text = line.Text;
            string first = FirstWord(text);

            switch (first)
            {
                case "character": pos++; return ParseCharacter(line);
                case "label": pos++; return Tag(new LabelCommand { Name = RequireColonName(line, "label") }, line);
                case "bg": pos++; return Tag(new BgCommand { Name = Rest(text, "bg") }, line);
                case "show": pos++; return ParseShow(line);
                case "hide": pos++; return Tag(new HideCommand { Character = Rest(text, "hide") }, line);
                case "jump": pos++; return Tag(new JumpCommand { Label = Rest(text, "jump") }, line);
                case "call": pos++; return Tag(new CallCommand { Label = Rest(text, "call") }, line);
                case "return": pos++; return Tag(new ReturnCommand(), line);
                case "menu": return ParseMenu(lines, ref pos);
                case "if": return ParseIf(lines, ref pos);
                case "while": return ParseWhile(lines, ref pos);
            }

            if (text.StartsWith("$")) { pos++; return ParseSet(line); }
            if (text.StartsWith("\"")) { pos++; return ParseSay(line, null, text); }

            // speaker "text"
            int q = text.IndexOf('"');
            if (q < 0) throw Err(line, $"cannot parse statement: {text}");
            string speaker = text.Substring(0, q).Trim();
            if (speaker.Length == 0) throw Err(line, "missing speaker before quote");
            pos++;
            return ParseSay(line, speaker, text);
        }

        private static Command ParseCharacter(LogicalLine line)
        {
            // character <id> name:"..." [color:"#.."]
            string rest = Rest(line.Text, "character");
            int sp = rest.IndexOf(' ');
            if (sp < 0) throw Err(line, "character requires an id and name:\"...\"");
            string id = rest.Substring(0, sp).Trim();
            string tail = rest.Substring(sp + 1);
            string name = ExtractKeyed(tail, "name:");
            if (name == null) throw Err(line, "character requires name:\"...\"");
            string color = ExtractKeyed(tail, "color:");
            return Tag(new CharacterDefCommand { Id = id, DisplayName = name, Color = color }, line);
        }

        // Finds key + quoted value, e.g. name:"요르"  → 요르 ; returns null if key absent.
        private static string ExtractKeyed(string s, string key)
        {
            int k = s.IndexOf(key, System.StringComparison.Ordinal);
            if (k < 0) return null;
            int q1 = s.IndexOf('"', k + key.Length);
            if (q1 < 0) return null;
            int q2 = s.IndexOf('"', q1 + 1);
            if (q2 < 0) return null;
            return s.Substring(q1 + 1, q2 - q1 - 1);
        }

        private static Command ParseShow(LogicalLine line)
        {
            string rest = Rest(line.Text, "show");
            string[] parts = rest.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) throw Err(line, "show requires a character name");
            string ch = parts[0];
            string pos = parts.Length >= 2 ? parts[1] : "center";
            if (pos != "left" && pos != "center" && pos != "right")
                throw Err(line, $"unknown position '{pos}' (use left/center/right)");
            return Tag(new ShowCommand { Character = ch, Position = pos }, line);
        }

        private static Command ParseSay(LogicalLine line, string speaker, string text)
        {
            int q1 = text.IndexOf('"');
            int q2 = text.LastIndexOf('"');
            if (q1 < 0 || q2 <= q1) throw Err(line, $"unterminated string: {text}");
            string body = text.Substring(q1 + 1, q2 - q1 - 1);
            return Tag(new SayCommand { SpeakerRef = speaker, Text = body }, line);
        }

        private static Command ParseSet(LogicalLine line)
        {
            // $ <var> <op> <expr>
            string rest = line.Text.Substring(1).Trim();
            string op = null; int opIdx = -1;
            foreach (var cand in new[] { "+=", "-=", "*=", "/=", "=" })
            {
                int idx = rest.IndexOf(cand, System.StringComparison.Ordinal);
                if (idx >= 0) { op = cand; opIdx = idx; break; }
            }
            if (op == null) throw Err(line, $"assignment needs an operator: {line.Text}");
            string var = rest.Substring(0, opIdx).Trim();
            if (var.Length == 0) throw Err(line, "assignment missing variable name");
            string rhs = rest.Substring(opIdx + op.Length).Trim();
            Expr rhsExpr = ExprParser.Parse(rhs);
            Expr value = op == "="
                ? rhsExpr
                : new BinaryExpr { Op = op.Substring(0, 1), Left = new VarExpr { Name = var }, Right = rhsExpr };
            return Tag(new SetCommand { Var = var, Value = value }, line);
        }

        private static Command ParseMenu(List<LogicalLine> lines, ref int pos)
        {
            var header = lines[pos];
            if (header.Text.TrimEnd() != "menu:") throw Err(header, "menu header must be 'menu:'");
            int baseIndent = header.Indent;
            pos++;
            var choices = new List<MenuChoiceNode>();
            while (pos < lines.Count && lines[pos].Indent > baseIndent)
            {
                var cl = lines[pos];
                // choice header: "label" [if <cond>] :
                if (!cl.Text.StartsWith("\"")) throw Err(cl, "menu choice must start with a quoted label");
                if (!cl.Text.EndsWith(":")) throw Err(cl, "menu choice header must end with ':'");
                int q2 = cl.Text.IndexOf('"', 1);
                if (q2 < 0) throw Err(cl, "unterminated choice label");
                string label = cl.Text.Substring(1, q2 - 1);
                string between = cl.Text.Substring(q2 + 1, cl.Text.Length - (q2 + 1) - 1).Trim(); // drop trailing ':'
                Expr cond = null;
                if (between.StartsWith("if "))
                    cond = ExprParser.Parse(between.Substring(3).Trim());
                else if (between.Length != 0)
                    throw Err(cl, $"unexpected text in choice header: {between}");
                int choiceIndent = cl.Indent;
                pos++;
                var body = ParseBlock(lines, ref pos, choiceIndent + 1);
                choices.Add(new MenuChoiceNode { Label = label, Condition = cond, Body = body });
            }
            if (choices.Count == 0) throw Err(header, "menu has no choices");
            return Tag(new MenuCommand { Choices = choices }, header);
        }

        private static Command ParseIf(List<LogicalLine> lines, ref int pos)
        {
            var header = lines[pos];
            int baseIndent = header.Indent;
            var branches = new List<IfBranch>();

            // first 'if'
            branches.Add(ParseCondBranch(lines, ref pos, "if", baseIndent));
            // subsequent elif/else at same indent
            while (pos < lines.Count && lines[pos].Indent == baseIndent)
            {
                string fw = FirstWord(lines[pos].Text);
                if (fw == "elif") branches.Add(ParseCondBranch(lines, ref pos, "elif", baseIndent));
                else if (fw == "else") { branches.Add(ParseElseBranch(lines, ref pos, baseIndent)); break; }
                else break;
            }
            return Tag(new IfCommand { Branches = branches }, header);
        }

        private static IfBranch ParseCondBranch(List<LogicalLine> lines, ref int pos, string kw, int baseIndent)
        {
            var line = lines[pos];
            string cond = RequireColonName(line, kw);
            pos++;
            var body = ParseBlock(lines, ref pos, baseIndent + 1);
            return new IfBranch { Condition = ExprParser.Parse(cond), Body = body };
        }

        private static IfBranch ParseElseBranch(List<LogicalLine> lines, ref int pos, int baseIndent)
        {
            var line = lines[pos];
            if (line.Text.TrimEnd() != "else:") throw Err(line, "else header must be 'else:'");
            pos++;
            var body = ParseBlock(lines, ref pos, baseIndent + 1);
            return new IfBranch { Condition = null, Body = body };
        }

        private static Command ParseWhile(List<LogicalLine> lines, ref int pos)
        {
            var header = lines[pos];
            int baseIndent = header.Indent;
            string cond = RequireColonName(header, "while");
            pos++;
            var body = ParseBlock(lines, ref pos, baseIndent + 1);
            return Tag(new WhileCommand { Condition = ExprParser.Parse(cond), Body = body }, header);
        }

        // ---- helpers ----

        private static Command Tag(Command c, LogicalLine line) { c.Line = line.LineNumber; c.File = line.File; return c; }

        private static string FirstWord(string s)
        {
            int sp = s.IndexOf(' ');
            string w = sp < 0 ? s : s.Substring(0, sp);
            if (w.EndsWith(":")) w = w.Substring(0, w.Length - 1);
            return w;
        }

        private static string Rest(string text, string keyword)
        {
            string r = text.Substring(keyword.Length).Trim();
            return r;
        }

        // For 'label x:', 'if cond:', etc. — returns the text between keyword and trailing ':'.
        private static string RequireColonName(LogicalLine line, string keyword)
        {
            string t = line.Text.TrimEnd();
            if (!t.EndsWith(":")) throw Err(line, $"'{keyword}' header must end with ':'");
            string inner = t.Substring(keyword.Length, t.Length - keyword.Length - 1).Trim();
            if (inner.Length == 0) throw Err(line, $"'{keyword}' requires a value before ':'");
            return inner;
        }

        private static VnParseException Err(LogicalLine line, string msg)
            => new VnParseException($"{line.File}:{line.LineNumber}: {msg}");
    }
}
```

- [ ] **Step 5: Run to verify pass** — filter `VNEngine.Tests.ParserTests`. Expected: 18 passed.

- [ ] **Step 6: Commit**
```bash
git add Assets/Scripts/VNEngine/Core/Parsing/Command.cs Assets/Scripts/VNEngine/Core/Parsing/Parser.cs Assets/Tests/Editor/ParserTests.cs
git commit -m "feat(vnengine): command AST + indentation-based parser"
```

---

## Task 8: Compiler + Linker → VnProgram

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Runtime/Instruction.cs`
- Create: `Assets/Scripts/VNEngine/Core/Runtime/Compiler.cs`
- Create: `Assets/Tests/Editor/CompilerTests.cs`

**Interfaces:**
- Consumes: all `Command` nodes, `Expr`, `VnException`.
- Produces:
  - `enum VNEngine.Op { Say, Bg, Show, Hide, Set, Jump, JumpIfFalse, Menu, Call, Return, Halt }`
  - `class VNEngine.MenuOption { string Label; Expr Condition; string TargetLabel; int Target; }`
  - `class VNEngine.Instruction { Op Op; string StrA; string StrB; Expr ExprA; string TargetLabel; int Target; List<MenuOption> Menu; int Line; string File; }`
    - Say: `StrA`=speakerRef (null=narration), `StrB`=raw text. Bg: `StrA`=name. Show: `StrA`=char, `StrB`=position. Hide: `StrA`=char. Set: `StrA`=var, `ExprA`=value. Jump/Call: `TargetLabel`/`Target`. JumpIfFalse: `ExprA`=condition, `TargetLabel`/`Target`. Menu: `Menu`.
  - `class VNEngine.CharacterDef { string Id; string DisplayName; string Color; }`
  - `class VNEngine.VnProgram { Instruction[] Code; Dictionary<string,int> Labels; Dictionary<string,CharacterDef> Characters; }`
  - `static class VNEngine.Compiler` with `VnProgram Compile(List<Command> commands)` and `VnProgram Compile(IReadOnlyList<List<Command>> files)`.
- Rules: user `label` names go into the global `Labels` map (duplicate → `VnParseException`). `character` defs go into `Characters` (duplicate id → `VnParseException`). `if`/`while`/`menu` are lowered to `Jump`/`JumpIfFalse` with compiler-generated unique labels (prefix `@L`). Program always ends with a `Halt`. All `TargetLabel`s are resolved to indices after emitting every file; unknown label → `VnParseException`.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/CompilerTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class CompilerTests
    {
        private static VnProgram Compile(string src) =>
            Compiler.Compile(Parser.Parse(LineReader.Read(src, "t.vns")));

        [Test] public void SayThenHalt()
        {
            var p = Compile("요르 \"hi\"");
            Assert.AreEqual(Op.Say, p.Code[0].Op);
            Assert.AreEqual("요르", p.Code[0].StrA);
            Assert.AreEqual("hi", p.Code[0].StrB);
            Assert.AreEqual(Op.Halt, p.Code[p.Code.Length - 1].Op);
        }

        [Test] public void NarrationHasNullSpeaker()
        {
            var p = Compile("\"…\"");
            Assert.IsNull(p.Code[0].StrA);
        }

        [Test] public void LabelResolvesToNextInstruction()
        {
            var p = Compile("label a:\n요르 \"x\"\njump a");
            Assert.IsTrue(p.Labels.ContainsKey("a"));
            int aIndex = p.Labels["a"];
            Assert.AreEqual(Op.Say, p.Code[aIndex].Op); // label points at the say
            var jump = FindOp(p, Op.Jump);
            Assert.AreEqual(aIndex, jump.Target);
        }

        [Test] public void DuplicateLabelThrows()
            => Assert.Throws<VnParseException>(() => Compile("label a:\nlabel a:"));

        [Test] public void UnknownJumpLabelThrows()
            => Assert.Throws<VnParseException>(() => Compile("jump nowhere"));

        [Test] public void IfLowersToJumpIfFalse()
        {
            var p = Compile("if x >= 1:\n    요르 \"y\"\n요르 \"z\"");
            var jif = FindOp(p, Op.JumpIfFalse);
            Assert.IsNotNull(jif.ExprA);
            // false-target must be a valid instruction index
            Assert.IsTrue(jif.Target >= 0 && jif.Target < p.Code.Length);
        }

        [Test] public void WhileHasBackwardJump()
        {
            var p = Compile("while n > 0:\n    $ n -= 1");
            // there must be a Jump whose target is <= its own index (loop back)
            bool foundBackward = false;
            for (int i = 0; i < p.Code.Length; i++)
                if (p.Code[i].Op == Op.Jump && p.Code[i].Target <= i) foundBackward = true;
            Assert.IsTrue(foundBackward, "expected a backward jump for while loop");
        }

        [Test] public void MenuOptionsTargetBodies()
        {
            var src =
                "menu:\n" +
                "    \"a\":\n" +
                "        jump end\n" +
                "    \"b\" if g >= 1:\n" +
                "        jump end\n" +
                "label end:\n" +
                "요르 \"done\"";
            var p = Compile(src);
            var menu = FindOp(p, Op.Menu);
            Assert.AreEqual(2, menu.Menu.Count);
            Assert.AreEqual("a", menu.Menu[0].Label);
            Assert.IsNull(menu.Menu[0].Condition);
            Assert.IsNotNull(menu.Menu[1].Condition);
            foreach (var opt in menu.Menu)
                Assert.IsTrue(opt.Target >= 0 && opt.Target < p.Code.Length);
        }

        [Test] public void CharacterDefsCollected()
        {
            var p = Compile("character 요르 name:\"요르\" color:\"#fff\"\n요르 \"hi\"");
            Assert.IsTrue(p.Characters.ContainsKey("요르"));
            Assert.AreEqual("요르", p.Characters["요르"].DisplayName);
            Assert.AreEqual("#fff", p.Characters["요르"].Color);
            // character def emits no instruction; first code is the say
            Assert.AreEqual(Op.Say, p.Code[0].Op);
        }

        [Test] public void DuplicateCharacterThrows()
            => Assert.Throws<VnParseException>(() =>
                Compile("character a name:\"A\"\ncharacter a name:\"B\""));

        [Test] public void MultiFileGlobalLabels()
        {
            var f1 = Parser.Parse(LineReader.Read("jump other", "a.vns"));
            var f2 = Parser.Parse(LineReader.Read("label other:\n요르 \"hi\"", "b.vns"));
            var p = Compiler.Compile(new List<List<Command>> { f1, f2 });
            Assert.IsTrue(p.Labels.ContainsKey("other"));
            var jump = FindOp(p, Op.Jump);
            Assert.AreEqual(p.Labels["other"], jump.Target);
        }

        private static Instruction FindOp(VnProgram p, Op op)
        {
            foreach (var ins in p.Code) if (ins.Op == op) return ins;
            Assert.Fail($"no instruction with op {op}");
            return null;
        }
    }
}
```

- [ ] **Step 2: Run to verify failure** — filter `VNEngine.Tests.CompilerTests`. Expected: compile error.

- [ ] **Step 3: Implement Instruction/VnProgram types**

`Assets/Scripts/VNEngine/Core/Runtime/Instruction.cs`:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    public enum Op { Say, Bg, Show, Hide, Set, Jump, JumpIfFalse, Menu, Call, Return, Halt }

    public sealed class MenuOption
    {
        public string Label;
        public Expr Condition;       // null = always shown
        public string TargetLabel;
        public int Target = -1;
    }

    public sealed class Instruction
    {
        public Op Op;
        public string StrA;
        public string StrB;
        public Expr ExprA;
        public string TargetLabel;   // for Jump/JumpIfFalse/Call
        public int Target = -1;
        public List<MenuOption> Menu;
        public int Line;
        public string File;
    }

    public sealed class CharacterDef
    {
        public string Id;
        public string DisplayName;
        public string Color;
    }

    public sealed class VnProgram
    {
        public Instruction[] Code;
        public Dictionary<string, int> Labels;
        public Dictionary<string, CharacterDef> Characters;
    }
}
```

- [ ] **Step 4: Implement Compiler**

`Assets/Scripts/VNEngine/Core/Runtime/Compiler.cs`:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    public static class Compiler
    {
        public static VnProgram Compile(List<Command> commands)
            => Compile(new List<List<Command>> { commands });

        public static VnProgram Compile(IReadOnlyList<List<Command>> files)
        {
            var code = new List<Instruction>();
            var labels = new Dictionary<string, int>();
            var characters = new Dictionary<string, CharacterDef>();
            int synth = 0;

            string NewLabel() => "@L" + (synth++);

            void Define(string name, Instruction origin)
            {
                if (labels.ContainsKey(name))
                    throw new VnParseException($"{Where(origin)}: duplicate label '{name}'");
                labels[name] = code.Count;
            }

            // A pending synthetic label to attach to the next-emitted instruction index.
            var pendingLabels = new List<string>();
            void DefineHere(string name) { labels[name] = code.Count; }

            void Emit(Instruction ins) => code.Add(ins);

            void EmitBlock(List<Command> cmds)
            {
                foreach (var c in cmds) EmitCommand(c);
            }

            void EmitCommand(Command c)
            {
                switch (c)
                {
                    case CharacterDefCommand cd:
                        if (characters.ContainsKey(cd.Id))
                            throw new VnParseException($"{c.File}:{c.Line}: duplicate character '{cd.Id}'");
                        characters[cd.Id] = new CharacterDef { Id = cd.Id, DisplayName = cd.DisplayName, Color = cd.Color };
                        break;

                    case LabelCommand lc:
                        if (labels.ContainsKey(lc.Name))
                            throw new VnParseException($"{c.File}:{c.Line}: duplicate label '{lc.Name}'");
                        labels[lc.Name] = code.Count;
                        break;

                    case SayCommand say:
                        Emit(new Instruction { Op = Op.Say, StrA = say.SpeakerRef, StrB = say.Text, Line = c.Line, File = c.File });
                        break;

                    case BgCommand bg:
                        Emit(new Instruction { Op = Op.Bg, StrA = bg.Name, Line = c.Line, File = c.File });
                        break;

                    case ShowCommand sh:
                        Emit(new Instruction { Op = Op.Show, StrA = sh.Character, StrB = sh.Position, Line = c.Line, File = c.File });
                        break;

                    case HideCommand hd:
                        Emit(new Instruction { Op = Op.Hide, StrA = hd.Character, Line = c.Line, File = c.File });
                        break;

                    case SetCommand set:
                        Emit(new Instruction { Op = Op.Set, StrA = set.Var, ExprA = set.Value, Line = c.Line, File = c.File });
                        break;

                    case JumpCommand jp:
                        Emit(new Instruction { Op = Op.Jump, TargetLabel = jp.Label, Line = c.Line, File = c.File });
                        break;

                    case CallCommand cl:
                        Emit(new Instruction { Op = Op.Call, TargetLabel = cl.Label, Line = c.Line, File = c.File });
                        break;

                    case ReturnCommand rc:
                        Emit(new Instruction { Op = Op.Return, Line = c.Line, File = c.File });
                        break;

                    case IfCommand ifc:
                        EmitIf(ifc);
                        break;

                    case WhileCommand wc:
                        EmitWhile(wc);
                        break;

                    case MenuCommand mc:
                        EmitMenu(mc);
                        break;

                    default:
                        throw new VnParseException($"{c.File}:{c.Line}: cannot compile {c.GetType().Name}");
                }
            }

            void EmitIf(IfCommand ifc)
            {
                string endLabel = NewLabel();
                foreach (var branch in ifc.Branches)
                {
                    if (branch.Condition != null)
                    {
                        string nextLabel = NewLabel();
                        Emit(new Instruction { Op = Op.JumpIfFalse, ExprA = branch.Condition, TargetLabel = nextLabel, Line = ifc.Line, File = ifc.File });
                        EmitBlock(branch.Body);
                        Emit(new Instruction { Op = Op.Jump, TargetLabel = endLabel, Line = ifc.Line, File = ifc.File });
                        DefineHere(nextLabel);
                    }
                    else
                    {
                        EmitBlock(branch.Body); // else
                    }
                }
                DefineHere(endLabel);
            }

            void EmitWhile(WhileCommand wc)
            {
                string startLabel = NewLabel();
                string endLabel = NewLabel();
                DefineHere(startLabel);
                Emit(new Instruction { Op = Op.JumpIfFalse, ExprA = wc.Condition, TargetLabel = endLabel, Line = wc.Line, File = wc.File });
                EmitBlock(wc.Body);
                Emit(new Instruction { Op = Op.Jump, TargetLabel = startLabel, Line = wc.Line, File = wc.File });
                DefineHere(endLabel);
            }

            void EmitMenu(MenuCommand mc)
            {
                string afterLabel = NewLabel();
                var options = new List<MenuOption>();
                var bodyLabels = new List<string>();
                foreach (var choice in mc.Choices)
                {
                    string bodyLabel = NewLabel();
                    bodyLabels.Add(bodyLabel);
                    options.Add(new MenuOption { Label = choice.Label, Condition = choice.Condition, TargetLabel = bodyLabel });
                }
                Emit(new Instruction { Op = Op.Menu, Menu = options, Line = mc.Line, File = mc.File });
                for (int i = 0; i < mc.Choices.Count; i++)
                {
                    DefineHere(bodyLabels[i]);
                    EmitBlock(mc.Choices[i].Body);
                    Emit(new Instruction { Op = Op.Jump, TargetLabel = afterLabel, Line = mc.Line, File = mc.File });
                }
                DefineHere(afterLabel);
            }

            // Emit every file sequentially into the shared code list.
            foreach (var file in files)
                EmitBlock(file);

            Emit(new Instruction { Op = Op.Halt });

            // Resolve all symbolic targets.
            foreach (var ins in code)
            {
                if (ins.TargetLabel != null)
                    ins.Target = ResolveLabel(labels, ins.TargetLabel, ins);
                if (ins.Menu != null)
                    foreach (var opt in ins.Menu)
                        opt.Target = ResolveLabel(labels, opt.TargetLabel, ins);
            }

            return new VnProgram { Code = code.ToArray(), Labels = labels, Characters = characters };
        }

        private static int ResolveLabel(Dictionary<string, int> labels, string name, Instruction origin)
        {
            if (!labels.TryGetValue(name, out int idx))
                throw new VnParseException($"{Where(origin)}: unknown label '{name}'");
            return idx;
        }

        private static string Where(Instruction ins)
            => ins?.File != null ? $"{ins.File}:{ins.Line}" : "<compiled>";
    }
}
```

- [ ] **Step 5: Run to verify pass** — filter `VNEngine.Tests.CompilerTests`. Expected: 11 passed.

- [ ] **Step 6: Commit**
```bash
git add Assets/Scripts/VNEngine/Core/Runtime/Instruction.cs Assets/Scripts/VNEngine/Core/Runtime/Compiler.cs Assets/Tests/Editor/CompilerTests.cs
git commit -m "feat(vnengine): compiler lowering if/while/menu to flat instructions"
```

---

## Task 9: Host interfaces + Fakes + Interpreter

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Presentation/IDialogueView.cs`
- Create: `Assets/Scripts/VNEngine/Core/Presentation/IStageView.cs`
- Create: `Assets/Scripts/VNEngine/Core/Runtime/Interpreter.cs`
- Create: `Assets/Tests/Editor/Fakes/FakeDialogueView.cs`
- Create: `Assets/Tests/Editor/Fakes/FakeStageView.cs`
- Create: `Assets/Tests/Editor/InterpreterTests.cs`

**Interfaces:**
- Consumes: `VnProgram`, `Instruction`, `Op`, `MenuOption`, `GameState`, `ExprEval`, `VnRuntimeException`.
- Produces:
  - `interface VNEngine.IDialogueView { void ShowLine(string speakerName, string colorHex, string text); bool IsLineComplete { get; } void ShowChoices(IReadOnlyList<string> labels); bool HasChoice { get; } int ChosenIndex { get; } void ClearChoices(); }`
  - `interface VNEngine.IStageView { void SetBackground(string name); void ShowCharacter(string name, string position); void HideCharacter(string name); }`
  - `sealed class VNEngine.Interpreter` — ctor `(VnProgram program, GameState state, IDialogueView dialogue, IStageView stage)`; `bool IsFinished { get; }`; `int MaxStepsPerTick` (default 100000); `void Start(string entryLabel)`; `void Tick()`.
- Behavior: `Tick()` advances instructions until it emits a line (then waits for `IsLineComplete`), shows a menu (then waits for `HasChoice`), or the program halts / returns with an empty call stack. Say passes `StrA` (raw speaker ref, `null`=narration) and raw text; display-name/color resolution and `[var]` interpolation are added in Task 10. Menu filters options by condition before showing; an empty eligible set → `VnRuntimeException`. Exceeding `MaxStepsPerTick` within one `Tick` → `VnRuntimeException` (infinite-loop guard).

- [ ] **Step 1: Write the Fakes**

`Assets/Tests/Editor/Fakes/FakeDialogueView.cs`:
```csharp
using System.Collections.Generic;

namespace VNEngine.Tests
{
    public sealed class FakeDialogueView : IDialogueView
    {
        public sealed class Shown { public string Speaker; public string Color; public string Text; }

        public readonly List<Shown> Lines = new List<Shown>();
        public readonly List<List<string>> ChoiceSets = new List<List<string>>();

        private readonly Queue<int> _answers;
        private bool _hasChoice;
        private int _chosen;

        public FakeDialogueView(params int[] answers) { _answers = new Queue<int>(answers); }

        public void ShowLine(string speakerName, string colorHex, string text)
            => Lines.Add(new Shown { Speaker = speakerName, Color = colorHex, Text = text });

        public bool IsLineComplete => true;

        public void ShowChoices(IReadOnlyList<string> labels)
        {
            ChoiceSets.Add(new List<string>(labels));
            _chosen = _answers.Count > 0 ? _answers.Dequeue() : 0;
            _hasChoice = true;
        }

        public bool HasChoice => _hasChoice;
        public int ChosenIndex => _chosen;
        public void ClearChoices() => _hasChoice = false;
    }
}
```

`Assets/Tests/Editor/Fakes/FakeStageView.cs`:
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
    }
}
```

- [ ] **Step 2: Write the failing interpreter tests**

`Assets/Tests/Editor/InterpreterTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class InterpreterTests
    {
        private FakeDialogueView _dlg;
        private FakeStageView _stage;
        private GameState _state;

        private void Run(string src, params int[] answers)
        {
            var program = Compiler.Compile(Parser.Parse(LineReader.Read("label start:\n" + src, "t.vns")));
            _state = new GameState(new SeededRandom(1));
            _dlg = new FakeDialogueView(answers);
            _stage = new FakeStageView();
            var interp = new Interpreter(program, _state, _dlg, _stage);
            interp.Start("start");
            int guard = 0;
            while (!interp.IsFinished)
            {
                interp.Tick();
                if (++guard > 100000) Assert.Fail("interpreter did not finish");
            }
        }

        private List<string> Texts()
        {
            var list = new List<string>();
            foreach (var s in _dlg.Lines) list.Add(s.Text);
            return list;
        }

        [Test] public void LinearOrder()
        {
            Run("요르 \"a\"\n요르 \"b\"");
            Assert.AreEqual(new[] { "a", "b" }, Texts().ToArray());
            Assert.AreEqual("요르", _dlg.Lines[0].Speaker);
        }

        [Test] public void NarrationHasNullSpeaker()
        {
            Run("\"…\"");
            Assert.IsNull(_dlg.Lines[0].Speaker);
        }

        [Test] public void SetAndBranchTrue()
        {
            Run("$ x = 100\nif x >= 50:\n    요르 \"high\"\nelse:\n    요르 \"low\"");
            Assert.AreEqual(new[] { "high" }, Texts().ToArray());
        }

        [Test] public void BranchFalseTakesElse()
        {
            Run("$ x = 10\nif x >= 50:\n    요르 \"high\"\nelse:\n    요르 \"low\"");
            Assert.AreEqual(new[] { "low" }, Texts().ToArray());
        }

        [Test] public void ElifChain()
        {
            Run("$ x = 20\nif x >= 50:\n    요르 \"a\"\nelif x >= 10:\n    요르 \"b\"\nelse:\n    요르 \"c\"");
            Assert.AreEqual(new[] { "b" }, Texts().ToArray());
        }

        [Test] public void WhileAccumulates()
        {
            Run("$ n = 3\n$ gold = 0\nwhile n > 0:\n    $ gold = gold + 10\n    $ n -= 1");
            Assert.AreEqual(VnValue.Int(30), _state.Get("gold"));
            Assert.AreEqual(VnValue.Int(0), _state.Get("n"));
        }

        [Test] public void JumpSkips()
        {
            Run("jump skip\n요르 \"skipped\"\nlabel skip:\n요르 \"here\"");
            Assert.AreEqual(new[] { "here" }, Texts().ToArray());
        }

        [Test] public void CallReturns()
        {
            Run("call sub\n요르 \"after\"\nreturn\nlabel sub:\n요르 \"in-sub\"\nreturn");
            Assert.AreEqual(new[] { "in-sub", "after" }, Texts().ToArray());
        }

        [Test] public void StageCommandsLogged()
        {
            Run("bg 공원\nshow 요르 left\nhide 요르");
            Assert.AreEqual(new[] { "bg:공원", "show:요르:left", "hide:요르" }, _stage.Log.ToArray());
        }

        [Test] public void MenuSelectsSecond()
        {
            var src =
                "menu:\n" +
                "    \"a\":\n" +
                "        요르 \"picked-a\"\n" +
                "        jump end\n" +
                "    \"b\":\n" +
                "        요르 \"picked-b\"\n" +
                "        jump end\n" +
                "label end:\n" +
                "요르 \"done\"";
            Run(src, 1); // choose index 1 => "b"
            Assert.AreEqual(new[] { "picked-b", "done" }, Texts().ToArray());
        }

        [Test] public void MenuEffectAppliesThenJumps()
        {
            var src =
                "menu:\n" +
                "    \"love\":\n" +
                "        $ affinity += 30\n" +
                "        jump end\n" +
                "label end:\n" +
                "요르 \"ok\"";
            Run(src, 0);
            Assert.AreEqual(VnValue.Int(30), _state.Get("affinity"));
        }

        [Test] public void ConditionalChoiceHiddenWhenFalse()
        {
            var src =
                "$ gold = 0\n" +
                "menu:\n" +
                "    \"always\":\n" +
                "        요르 \"a\"\n" +
                "        jump end\n" +
                "    \"bribe\" if gold >= 10:\n" +
                "        요르 \"b\"\n" +
                "        jump end\n" +
                "label end:\n" +
                "요르 \"done\"";
            Run(src, 0);
            // only one choice was eligible
            Assert.AreEqual(1, _dlg.ChoiceSets[0].Count);
            Assert.AreEqual("always", _dlg.ChoiceSets[0][0]);
            Assert.AreEqual(new[] { "a", "done" }, Texts().ToArray());
        }

        [Test] public void ConditionalChoiceShownWhenTrue()
        {
            var src =
                "$ gold = 50\n" +
                "menu:\n" +
                "    \"always\":\n" +
                "        jump end\n" +
                "    \"bribe\" if gold >= 10:\n" +
                "        요르 \"b\"\n" +
                "        jump end\n" +
                "label end:\n" +
                "요르 \"done\"";
            Run(src, 1); // pick the (now visible) bribe option
            Assert.AreEqual(2, _dlg.ChoiceSets[0].Count);
            Assert.AreEqual(new[] { "b", "done" }, Texts().ToArray());
        }

        [Test] public void InfiniteLoopGuardThrows()
        {
            var program = Compiler.Compile(Parser.Parse(LineReader.Read(
                "label start:\nwhile true:\n    $ x = 1", "t.vns")));
            var interp = new Interpreter(program, new GameState(new SeededRandom(1)),
                new FakeDialogueView(), new FakeStageView()) { MaxStepsPerTick = 5000 };
            interp.Start("start");
            Assert.Throws<VnRuntimeException>(() => interp.Tick());
        }

        [Test] public void UnknownEntryLabelThrows()
        {
            var program = Compiler.Compile(Parser.Parse(LineReader.Read("label start:\nreturn", "t.vns")));
            var interp = new Interpreter(program, new GameState(new SeededRandom(1)),
                new FakeDialogueView(), new FakeStageView());
            Assert.Throws<VnRuntimeException>(() => interp.Start("nope"));
        }
    }
}
```

- [ ] **Step 3: Run to verify failure** — filter `VNEngine.Tests.InterpreterTests`. Expected: compile error (interfaces + `Interpreter` missing).

- [ ] **Step 4: Implement the interfaces**

`Assets/Scripts/VNEngine/Core/Presentation/IDialogueView.cs`:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    public interface IDialogueView
    {
        void ShowLine(string speakerName, string colorHex, string text);
        bool IsLineComplete { get; }
        void ShowChoices(IReadOnlyList<string> labels);
        bool HasChoice { get; }
        int ChosenIndex { get; }
        void ClearChoices();
    }
}
```

`Assets/Scripts/VNEngine/Core/Presentation/IStageView.cs`:
```csharp
namespace VNEngine
{
    public interface IStageView
    {
        void SetBackground(string name);
        void ShowCharacter(string name, string position);
        void HideCharacter(string name);
    }
}
```

- [ ] **Step 5: Implement the Interpreter**

`Assets/Scripts/VNEngine/Core/Runtime/Interpreter.cs`:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    public sealed class Interpreter
    {
        private enum Pending { None, Line, Choice }

        private readonly VnProgram _program;
        private readonly GameState _state;
        private readonly IDialogueView _dialogue;
        private readonly IStageView _stage;
        private readonly Stack<int> _callStack = new Stack<int>();

        private int _pc;
        private Pending _pending = Pending.None;
        private List<MenuOption> _activeOptions;

        public bool IsFinished { get; private set; }
        public int MaxStepsPerTick = 100000;

        public Interpreter(VnProgram program, GameState state, IDialogueView dialogue, IStageView stage)
        {
            _program = program ?? throw new System.ArgumentNullException(nameof(program));
            _state = state ?? throw new System.ArgumentNullException(nameof(state));
            _dialogue = dialogue ?? throw new System.ArgumentNullException(nameof(dialogue));
            _stage = stage ?? throw new System.ArgumentNullException(nameof(stage));
        }

        public void Start(string entryLabel)
        {
            if (!_program.Labels.TryGetValue(entryLabel, out _pc))
                throw new VnRuntimeException($"entry label '{entryLabel}' not found");
            _callStack.Clear();
            _pending = Pending.None;
            _activeOptions = null;
            IsFinished = false;
        }

        public void Tick()
        {
            if (IsFinished) return;

            // Resolve any outstanding wait first.
            if (_pending == Pending.Line)
            {
                if (_dialogue.IsLineComplete) _pending = Pending.None;
                else return;
            }
            if (_pending == Pending.Choice)
            {
                if (!_dialogue.HasChoice) return;
                int idx = _dialogue.ChosenIndex;
                _dialogue.ClearChoices();
                if (_activeOptions == null || idx < 0 || idx >= _activeOptions.Count)
                    throw new VnRuntimeException($"choice index {idx} out of range");
                _pc = _activeOptions[idx].Target;
                _pending = Pending.None;
                _activeOptions = null;
            }

            int steps = 0;
            while (true)
            {
                if (++steps > MaxStepsPerTick)
                    throw new VnRuntimeException("step limit exceeded within one Tick (infinite loop?)");

                if (_pc < 0 || _pc >= _program.Code.Length) { IsFinished = true; return; }

                var ins = _program.Code[_pc];
                switch (ins.Op)
                {
                    case Op.Say:
                        _dialogue.ShowLine(ins.StrA, null, ins.StrB);
                        _pc++;
                        _pending = Pending.Line;
                        return;

                    case Op.Bg: _stage.SetBackground(ins.StrA); _pc++; break;
                    case Op.Show: _stage.ShowCharacter(ins.StrA, ins.StrB); _pc++; break;
                    case Op.Hide: _stage.HideCharacter(ins.StrA); _pc++; break;

                    case Op.Set:
                        _state.Set(ins.StrA, ExprEval.Eval(ins.ExprA, _state));
                        _pc++;
                        break;

                    case Op.Jump:
                        _pc = ins.Target;
                        break;

                    case Op.JumpIfFalse:
                        if (!ExprEval.Eval(ins.ExprA, _state).Truthy) _pc = ins.Target;
                        else _pc++;
                        break;

                    case Op.Call:
                        _callStack.Push(_pc + 1);
                        _pc = ins.Target;
                        break;

                    case Op.Return:
                        if (_callStack.Count == 0) { IsFinished = true; return; }
                        _pc = _callStack.Pop();
                        break;

                    case Op.Menu:
                        BuildMenu(ins);
                        _pending = Pending.Choice;
                        return;

                    case Op.Halt:
                        IsFinished = true;
                        return;

                    default:
                        throw new VnRuntimeException($"unknown opcode {ins.Op}");
                }
            }
        }

        private void BuildMenu(Instruction ins)
        {
            _activeOptions = new List<MenuOption>();
            var labels = new List<string>();
            foreach (var opt in ins.Menu)
            {
                if (opt.Condition == null || ExprEval.Eval(opt.Condition, _state).Truthy)
                {
                    _activeOptions.Add(opt);
                    labels.Add(opt.Label);
                }
            }
            if (labels.Count == 0)
                throw new VnRuntimeException($"{ins.File}:{ins.Line}: menu has no available choices");
            _dialogue.ShowChoices(labels);
        }
    }
}
```

- [ ] **Step 6: Run to verify pass** — filter `VNEngine.Tests.InterpreterTests`. Expected: 16 passed.

- [ ] **Step 7: Commit**
```bash
git add Assets/Scripts/VNEngine/Core/Presentation Assets/Scripts/VNEngine/Core/Runtime/Interpreter.cs Assets/Tests/Editor/Fakes Assets/Tests/Editor/InterpreterTests.cs
git commit -m "feat(vnengine): interpreter (flow control, menu, call/return, loop guard)"
```

---

## Task 10: Text interpolation + speaker resolution

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Runtime/TextInterpolator.cs`
- Modify: `Assets/Scripts/VNEngine/Core/Runtime/Interpreter.cs` (the `case Op.Say:` block only)
- Create: `Assets/Tests/Editor/DisplayTests.cs`

**Interfaces:**
- Consumes: `GameState`, `VnProgram.Characters`, `CharacterDef`.
- Produces: `static class VNEngine.TextInterpolator { static string Interpolate(string text, GameState state) }`.
- Behavior: `[name]` → `state.Get(name).ToString()`; `[[` → literal `[`; an unmatched `[` (no `]`) is emitted literally. Interpreter's Say now resolves the speaker: `null` ref → narration (name `null`, color `null`); a ref matching a `character` def → that def's `DisplayName`/`Color`; otherwise the raw ref is used as the name with `null` color. Text is interpolated before display.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/DisplayTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class DisplayTests
    {
        // ---- TextInterpolator ----

        private static string Interp(string text, System.Action<GameState> setup = null)
        {
            var s = new GameState(new SeededRandom(1));
            setup?.Invoke(s);
            return TextInterpolator.Interpolate(text, s);
        }

        [Test] public void NoBracketsUnchanged() => Assert.AreEqual("hello", Interp("hello"));

        [Test] public void SubstitutesIntVar()
            => Assert.AreEqual("gold 5", Interp("gold [gold]", s => s.Set("gold", VnValue.Int(5))));

        [Test] public void UndefinedVarBecomesZero()
            => Assert.AreEqual("x=0", Interp("x=[x]"));

        [Test] public void SubstitutesBool()
            => Assert.AreEqual("met? true", Interp("met? [met]", s => s.Set("met", VnValue.Bool(true))));

        [Test] public void EscapedBracket() => Assert.AreEqual("a[b", Interp("a[[b"));

        [Test] public void UnmatchedBracketLiteral() => Assert.AreEqual("a[b", Interp("a[b"));

        [Test] public void MultipleVars()
            => Assert.AreEqual("1 and 2", Interp("[a] and [b]", s => { s.Set("a", VnValue.Int(1)); s.Set("b", VnValue.Int(2)); }));

        // ---- speaker resolution in the interpreter ----

        private FakeDialogueView Run(string src)
        {
            var program = Compiler.Compile(Parser.Parse(LineReader.Read("label start:\n" + src, "t.vns")));
            var dlg = new FakeDialogueView();
            var interp = new Interpreter(program, new GameState(new SeededRandom(1)), dlg, new FakeStageView());
            interp.Start("start");
            int guard = 0;
            while (!interp.IsFinished) { interp.Tick(); if (++guard > 100000) Assert.Fail("stuck"); }
            return dlg;
        }

        [Test] public void CharacterDefResolvesNameAndColor()
        {
            var dlg = Run("character 요르 name:\"요르 (숲의 요정)\" color:\"#8fd3ff\"\n요르 \"hi\"");
            Assert.AreEqual("요르 (숲의 요정)", dlg.Lines[0].Speaker);
            Assert.AreEqual("#8fd3ff", dlg.Lines[0].Color);
        }

        [Test] public void UndefinedSpeakerFallsBackToLiteral()
        {
            var dlg = Run("민지 \"hi\"");
            Assert.AreEqual("민지", dlg.Lines[0].Speaker);
            Assert.IsNull(dlg.Lines[0].Color);
        }

        [Test] public void NarrationStaysNull()
        {
            var dlg = Run("\"…\"");
            Assert.IsNull(dlg.Lines[0].Speaker);
            Assert.IsNull(dlg.Lines[0].Color);
        }

        [Test] public void SayTextIsInterpolated()
        {
            var dlg = Run("$ gold = 42\n요르 \"남은 골드는 [gold]개야.\"");
            Assert.AreEqual("남은 골드는 42개야.", dlg.Lines[0].Text);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure** — filter `VNEngine.Tests.DisplayTests`. Expected: compile error (`TextInterpolator` missing) + speaker tests fail.

- [ ] **Step 3: Implement TextInterpolator**

`Assets/Scripts/VNEngine/Core/Runtime/TextInterpolator.cs`:
```csharp
using System.Text;

namespace VNEngine
{
    public static class TextInterpolator
    {
        public static string Interpolate(string text, GameState state)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('[') < 0) return text ?? "";

            var sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '[')
                {
                    if (i + 1 < text.Length && text[i + 1] == '[') { sb.Append('['); i += 2; continue; }
                    int close = text.IndexOf(']', i + 1);
                    if (close < 0) { sb.Append(text.Substring(i)); break; }
                    string name = text.Substring(i + 1, close - i - 1).Trim();
                    sb.Append(state.Get(name).ToString());
                    i = close + 1;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }
    }
}
```

- [ ] **Step 4: Modify the Interpreter's Say case**

In `Assets/Scripts/VNEngine/Core/Runtime/Interpreter.cs`, replace the existing `case Op.Say:` block:
```csharp
                    case Op.Say:
                        _dialogue.ShowLine(ins.StrA, null, ins.StrB);
                        _pc++;
                        _pending = Pending.Line;
                        return;
```
with:
```csharp
                    case Op.Say:
                        {
                            string speaker, color;
                            if (ins.StrA == null) { speaker = null; color = null; }
                            else if (_program.Characters.TryGetValue(ins.StrA, out var def))
                            { speaker = def.DisplayName; color = def.Color; }
                            else { speaker = ins.StrA; color = null; }
                            _dialogue.ShowLine(speaker, color, TextInterpolator.Interpolate(ins.StrB, _state));
                        }
                        _pc++;
                        _pending = Pending.Line;
                        return;
```

- [ ] **Step 5: Run to verify pass** — filter `VNEngine.Tests.DisplayTests`; then run the full `VNEngine.Tests` suite to confirm no regressions. Expected: DisplayTests 11 passed, full suite green.

- [ ] **Step 6: Commit**
```bash
git add Assets/Scripts/VNEngine/Core/Runtime/TextInterpolator.cs Assets/Scripts/VNEngine/Core/Runtime/Interpreter.cs Assets/Tests/Editor/DisplayTests.cs
git commit -m "feat(vnengine): text interpolation + speaker display-name/color resolution"
```

---

## Task 11: StageViewUnity (IStageView implementation)

**Files:**
- Create: `Assets/Scripts/VNEngine/Unity/Presentation/StageViewUnity.cs`

**Interfaces:**
- Consumes: `IStageView`.
- Produces: `class VNEngine.Unity.StageViewUnity : MonoBehaviour, IStageView` — inspector fields `Transform leftSlot/centerSlot/rightSlot`, `SpriteRenderer background`, `List<CharacterEntry> characters`, `List<BackgroundEntry> backgrounds`, `int characterSortingOrder` (default 5), `float characterScale` (default 0.35f). Nested `[Serializable] class CharacterEntry { string name; Sprite sprite; }`, `[Serializable] class BackgroundEntry { string name; Sprite sprite; }`.
- Behavior: ports `ShowCharacter`/`HideCharacter`/slot logic from the old `DialogueManager`. `SetBackground(name)` swaps `background.sprite` if a matching `BackgroundEntry` exists, otherwise records the name and no-ops (full transitions are P1). Unknown character/position → `Debug.LogWarning` (non-fatal), matching old behavior.

- [ ] **Step 1: Implement StageViewUnity**

`Assets/Scripts/VNEngine/Unity/Presentation/StageViewUnity.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace VNEngine.Unity
{
    public class StageViewUnity : MonoBehaviour, IStageView
    {
        [System.Serializable] public class CharacterEntry { public string name; public Sprite sprite; }
        [System.Serializable] public class BackgroundEntry { public string name; public Sprite sprite; }

        [Header("Slots (world-space empty GameObjects)")]
        public Transform leftSlot;
        public Transform centerSlot;
        public Transform rightSlot;

        [Header("Background")]
        public SpriteRenderer background;
        public List<BackgroundEntry> backgrounds = new List<BackgroundEntry>();

        [Header("Characters")]
        public List<CharacterEntry> characters = new List<CharacterEntry>();
        public int characterSortingOrder = 5;
        public float characterScale = 0.35f;

        private readonly Dictionary<string, GameObject> _active = new Dictionary<string, GameObject>();
        private string _currentBackground;

        public void SetBackground(string name)
        {
            _currentBackground = name;
            var entry = backgrounds.Find(b => b != null && b.name == name);
            if (entry != null && entry.sprite != null && background != null)
                background.sprite = entry.sprite; // optional swap; full transitions are P1
        }

        public void ShowCharacter(string name, string position)
        {
            var entry = characters.Find(c => c != null && c.name == name);
            if (entry == null) { Debug.LogWarning($"[StageView] character '{name}' not registered"); return; }
            if (entry.sprite == null) { Debug.LogWarning($"[StageView] character '{name}' has no sprite"); return; }

            Transform slot = GetSlot(position);
            if (slot == null) { Debug.LogWarning($"[StageView] slot for '{position}' not assigned"); return; }

            // Evict any other character currently standing in this slot.
            var toRemove = new List<string>();
            foreach (var kv in _active)
                if (kv.Key != name && kv.Value != null && kv.Value.transform.parent == slot)
                    toRemove.Add(kv.Key);
            foreach (var n in toRemove) { if (_active[n] != null) Destroy(_active[n]); _active.Remove(n); }

            if (_active.TryGetValue(name, out var go) && go != null)
            {
                go.transform.SetParent(slot, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one * characterScale;
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) { sr.sprite = entry.sprite; sr.sortingOrder = characterSortingOrder; }
            }
            else
            {
                var newGo = new GameObject($"Char_{name}");
                newGo.transform.SetParent(slot, false);
                newGo.transform.localPosition = Vector3.zero;
                newGo.transform.localScale = Vector3.one * characterScale;
                var sr = newGo.AddComponent<SpriteRenderer>();
                sr.sprite = entry.sprite;
                sr.sortingOrder = characterSortingOrder;
                _active[name] = newGo;
            }
        }

        public void HideCharacter(string name)
        {
            if (_active.TryGetValue(name, out var go))
            {
                if (go != null) Destroy(go);
                _active.Remove(name);
            }
        }

        private Transform GetSlot(string position)
        {
            switch ((position ?? "").ToLowerInvariant())
            {
                case "left": return leftSlot;
                case "center": return centerSlot;
                case "right": return rightSlot;
                default: Debug.LogWarning($"[StageView] unknown position '{position}'"); return null;
            }
        }
    }
}
```

- [ ] **Step 2: Compile check**

Refresh Unity, poll `editor_state.isCompiling` until false, `read_console` (types: error, warning). Expected: zero errors, zero new warnings for this file.

- [ ] **Step 3: Commit**
```bash
git add Assets/Scripts/VNEngine/Unity/Presentation/StageViewUnity.cs
git commit -m "feat(vnengine): StageViewUnity (IStageView over sprite slots)"
```

---

## Task 12: DialogueViewUnity (IDialogueView implementation)

**Files:**
- Create: `Assets/Scripts/VNEngine/Unity/Presentation/DialogueViewUnity.cs`

**Interfaces:**
- Consumes: `IDialogueView`.
- Produces: `class VNEngine.Unity.DialogueViewUnity : MonoBehaviour, IDialogueView` — inspector fields `GameObject dialoguePanel`, `TMP_Text speakerText`, `TMP_Text dialogueText`, `Button choiceButtonPrefab`, `Transform choicesContainer`, `[Range] float typingSpeed` (default 0.04). Implements the full `IDialogueView` contract with a typewriter coroutine and click-driven advance.
- Behavior: `ShowLine` starts the typewriter and marks the line incomplete; a click completes the typewriter if still typing, else marks the line complete (`IsLineComplete` true). While choices are shown, advance-clicks are ignored (buttons drive selection). `ShowChoices` instantiates one button per label; a button click records `ChosenIndex` and sets `HasChoice`. Ports the typewriter/choice logic from the old `DialogueManager`.

- [ ] **Step 1: Implement DialogueViewUnity**

`Assets/Scripts/VNEngine/Unity/Presentation/DialogueViewUnity.cs`:
```csharp
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VNEngine.Unity
{
    public class DialogueViewUnity : MonoBehaviour, IDialogueView
    {
        [Header("UI References")]
        public GameObject dialoguePanel;
        public TMP_Text speakerText;
        public TMP_Text dialogueText;

        [Header("Choices")]
        public Button choiceButtonPrefab;
        public Transform choicesContainer;

        [Header("Typing")]
        [Range(0.005f, 0.2f)] public float typingSpeed = 0.04f;

        private Coroutine _typing;
        private bool _isTyping;
        private string _fullText = "";
        private bool _lineComplete;

        private readonly List<GameObject> _choiceButtons = new List<GameObject>();
        private bool _hasChoice;
        private int _chosen = -1;
        private bool _choicesShowing;

        private void Awake()
        {
            if (choicesContainer != null) choicesContainer.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_choicesShowing) return; // buttons handle input while choosing
            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
                OnAdvanceClick();
            }
        }

        private void OnAdvanceClick()
        {
            if (_isTyping)
            {
                if (_typing != null) StopCoroutine(_typing);
                dialogueText.text = _fullText;
                _isTyping = false;
                return; // first click finishes typing; a second click advances
            }
            _lineComplete = true;
        }

        // ---- IDialogueView ----

        public void ShowLine(string speakerName, string colorHex, string text)
        {
            _lineComplete = false;
            if (dialoguePanel != null) dialoguePanel.SetActive(true);

            if (speakerText != null)
            {
                speakerText.text = speakerName ?? "";
                if (!string.IsNullOrEmpty(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out var col))
                    speakerText.color = col;
                else
                    speakerText.color = Color.white;
            }

            _fullText = text ?? "";
            if (_typing != null) StopCoroutine(_typing);
            _typing = StartCoroutine(TypeLine(_fullText));
        }

        public bool IsLineComplete => _lineComplete;

        private IEnumerator TypeLine(string full)
        {
            _isTyping = true;
            dialogueText.text = "";
            for (int i = 0; i < full.Length; i++)
            {
                dialogueText.text += full[i];
                yield return new WaitForSeconds(typingSpeed);
            }
            _isTyping = false;
        }

        public void ShowChoices(IReadOnlyList<string> labels)
        {
            if (choiceButtonPrefab == null || choicesContainer == null)
            {
                Debug.LogError("[DialogueView] choiceButtonPrefab or choicesContainer not assigned");
                return;
            }
            ClearChoices();
            choicesContainer.gameObject.SetActive(true);
            _choicesShowing = true;

            for (int i = 0; i < labels.Count; i++)
            {
                int index = i; // capture
                Button btn = Instantiate(choiceButtonPrefab, choicesContainer);
                btn.name = $"ChoiceButton_{i}";
                var tmp = btn.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null) tmp.text = labels[i];
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => { _chosen = index; _hasChoice = true; });
                _choiceButtons.Add(btn.gameObject);
            }
        }

        public bool HasChoice => _hasChoice;
        public int ChosenIndex => _chosen;

        public void ClearChoices()
        {
            for (int i = 0; i < _choiceButtons.Count; i++)
                if (_choiceButtons[i] != null) Destroy(_choiceButtons[i]);
            _choiceButtons.Clear();
            if (choicesContainer != null) choicesContainer.gameObject.SetActive(false);
            _hasChoice = false;
            _chosen = -1;
            _choicesShowing = false;
        }
    }
}
```

- [ ] **Step 2: Compile check**

Refresh Unity, poll until compiled, `read_console` (types: error). Expected: zero errors.

- [ ] **Step 3: Commit**
```bash
git add Assets/Scripts/VNEngine/Unity/Presentation/DialogueViewUnity.cs
git commit -m "feat(vnengine): DialogueViewUnity (TMP typewriter + choice buttons)"
```

---

## Task 13: `.vns` importer + VnScriptLoader + VNRunner (coroutine host)

**Files:**
- Create: `Assets/Scripts/VNEngine/Editor/VNEngine.Editor.asmdef`
- Create: `Assets/Scripts/VNEngine/Editor/VnsImporter.cs`
- Create: `Assets/Scripts/VNEngine/Unity/VnScriptLoader.cs`
- Create: `Assets/Scripts/VNEngine/Unity/VNRunner.cs`

**Interfaces:**
- Consumes: `LineReader`, `Parser`, `Compiler`, `VnProgram`, `Interpreter`, `GameState`, `SeededRandom`, `VnException`, `DialogueViewUnity`, `StageViewUnity`.
- Produces:
  - `VnsImporter : ScriptedImporter` — registered for extension `vns`; imports each `.vns` file as a `TextAsset` whose `.text` is the file contents. This lets `.vns` files under `Assets/Resources/scripts/` be loaded on every platform (editor/desktop/Android/iOS/WebGL).
  - `static class VNEngine.Unity.VnScriptLoader { VnProgram LoadAndCompile(string resourcesSubfolder) }` — loads every `TextAsset` under `Resources/<resourcesSubfolder>` via `Resources.LoadAll<TextAsset>`, sorts by ordinal `name`, parses each (using `name` as the file label), and compiles them together. Throws `VnException` when no `.vns` TextAssets are found or on parse/compile error. **No `System.IO`/`Directory` — mobile-safe.**
  - `class VNEngine.Unity.VNRunner : MonoBehaviour` — inspector fields `DialogueViewUnity dialogueView`, `StageViewUnity stageView`, `string scriptsResourcesFolder` (default `"scripts"`), `string entryLabel` (default `"start"`), `int randomSeed` (default 12345). On `Start()` it loads+compiles `Resources/<scriptsResourcesFolder>`, builds a `GameState`+`Interpreter`, and ticks once per frame in a coroutine until finished. All `VnException`s are caught and logged via `Debug.LogError` (never crash the play session).

- [ ] **Step 1: Create the Editor assembly for the importer**

`Assets/Scripts/VNEngine/Editor/VNEngine.Editor.asmdef`:
```json
{
    "name": "VNEngine.Editor",
    "rootNamespace": "VNEngine.Editor",
    "references": [],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "autoReferenced": true,
    "noEngineReferences": false
}
```

- [ ] **Step 2: Implement the `.vns` ScriptedImporter**

`Assets/Scripts/VNEngine/Editor/VnsImporter.cs`:
```csharp
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace VNEngine.Editor
{
    // Imports *.vns files as TextAsset so they can be placed under
    // Assets/Resources/scripts and loaded with Resources.LoadAll<TextAsset>
    // on every platform (Android/iOS included).
    [ScriptedImporter(version: 1, ext: "vns")]
    public class VnsImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string text = File.ReadAllText(ctx.assetPath);
            var asset = new TextAsset(text);
            ctx.AddObjectToAsset("main", asset);
            ctx.SetMainObject(asset);
        }
    }
}
```

- [ ] **Step 3: Compile check (importer)**

Refresh Unity, poll `mcpforunity://editor/state` until `isCompiling` false, `read_console` (types: error). Expected: zero errors. Any existing `.vns` files now show a TextAsset icon in the Project window.

- [ ] **Step 4: Implement VnScriptLoader**

`Assets/Scripts/VNEngine/Unity/VnScriptLoader.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace VNEngine.Unity
{
    public static class VnScriptLoader
    {
        // resourcesSubfolder is relative to any Resources/ folder, e.g. "scripts"
        // loads Assets/Resources/scripts/*.vns (imported as TextAssets).
        public static VnProgram LoadAndCompile(string resourcesSubfolder)
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>(resourcesSubfolder);
            if (assets == null || assets.Length == 0)
                throw new VnException($"no .vns TextAssets found under Resources/{resourcesSubfolder}");

            Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));

            var parsed = new List<List<Command>>();
            foreach (var ta in assets)
                parsed.Add(Parser.Parse(LineReader.Read(ta.text, ta.name)));

            return Compiler.Compile(parsed);
        }
    }
}
```

- [ ] **Step 5: Implement VNRunner**

`Assets/Scripts/VNEngine/Unity/VNRunner.cs`:
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

        private IEnumerator Start()
        {
            if (dialogueView == null || stageView == null)
            {
                Debug.LogError("[VNRunner] dialogueView and stageView must be assigned");
                yield break;
            }

            VnProgram program = null;
            string loadError = null;
            try
            {
                program = VnScriptLoader.LoadAndCompile(scriptsResourcesFolder);
            }
            catch (VnException e) { loadError = e.Message; }

            if (loadError != null)
            {
                Debug.LogError($"[VNRunner] script load/compile failed: {loadError}");
                dialogueView.ShowLine(null, null, "[script load failed]");
                yield break;
            }

            var state = new GameState(new SeededRandom(randomSeed));
            _interp = new Interpreter(program, state, dialogueView, stageView);

            string startError = null;
            try { _interp.Start(entryLabel); }
            catch (VnException e) { startError = e.Message; }
            if (startError != null)
            {
                Debug.LogError($"[VNRunner] {startError}");
                yield break;
            }

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
    }
}
```

- [ ] **Step 6: Compile check**

Refresh Unity, poll `mcpforunity://editor/state` until `isCompiling` false, `read_console` (types: error). Expected: zero errors. (No play test yet — the scene is wired in Task 14.)

- [ ] **Step 7: Commit**
```bash
git add Assets/Scripts/VNEngine/Editor Assets/Scripts/VNEngine/Unity/VnScriptLoader.cs Assets/Scripts/VNEngine/Unity/VNRunner.cs
git commit -m "feat(vnengine): .vns importer + Resources-based loader + VNRunner (mobile-safe)"
```

---

## Task 14: Migration — intro.vns, scene wiring, parity verification, cleanup

**Files:**
- Create: `Assets/Resources/scripts/intro.vns`
- Modify (scene, via UnityMCP): `Assets/Scenes/Main.unity`
- Delete: `Assets/Scripts/DialogueManager.cs`, `Assets/Scripts/DialogueManager.cs.meta`, `Assets/StreamingAssets/dialogues.json`, `Assets/StreamingAssets/dialogues.json.meta`

**Interfaces:**
- Consumes: `VNRunner`, `DialogueViewUnity`, `StageViewUnity`.
- Produces: a playable scene where `intro.vns` reproduces the original `dialogues.json` scenario exactly.

- [ ] **Step 1: Write intro.vns**

`Assets/Resources/scripts/intro.vns` (the `VnsImporter` from Task 13 imports it as a `TextAsset`):
```
# 요르 데이트 시나리오 — dialogues.json 의 DSL 이식본
character 요르 name:"요르"
character 나레이션 name:"나레이션"

label start:
    show 요르 left
    요르 "주말에 뭐 할래?"
    menu:
        "같이 영화 보자":
            $ 요르 += 30
            jump after
        "귀찮은데 그냥 집에":
            $ 요르 -= 10
            jump after

label after:
    나레이션 "...그렇게 시간이 흘렀다."
    if 요르 >= 30:
        요르 "너랑 있으면 즐거워. 우리 사귈래?"
    elif 요르 >= 0:
        요르 "그냥 친구로 지내자."
    else:
        요르 "...우리 안 맞는 것 같아."
    return
```

- [ ] **Step 2: Inspect the old wiring**

Before changing the scene, read the serialized references on the existing `DialogueManager` component so the same objects can be transferred. Use UnityMCP: `find_gameobjects` (by_component `DialogueManager`) → get its instanceID → read `mcpforunity://scene/gameobject/{id}/components`. Record the object references for: `dialoguePanel`, `speakerText`, `dialogueText`, `choiceButtonPrefab`, `choicesContainer`, `leftSlot`, `centerSlot`, `rightSlot`, and each `characters[]` entry (name → Sprite). Also note the `Background` SpriteRenderer object.

- [ ] **Step 3: Add the new components and remove the old one**

On the `DialogueManager` GameObject (via UnityMCP `manage_components`):
1. Add component `VNEngine.Unity.StageViewUnity`.
2. Add component `VNEngine.Unity.DialogueViewUnity`.
3. Add component `VNEngine.Unity.VNRunner`.
4. Remove component `DialogueManager`.

Wire `DialogueViewUnity` with the references recorded in Step 2: `dialoguePanel`, `speakerText`, `dialogueText`, `choiceButtonPrefab`, `choicesContainer`.
Wire `StageViewUnity`: `leftSlot`, `centerSlot`, `rightSlot`, `background` (the `Background` object's SpriteRenderer), and add one `characters[]` entry `{ name: "요르", sprite: <the 요르 sprite from the old list> }`.
Wire `VNRunner`: `dialogueView` → the DialogueViewUnity component, `stageView` → the StageViewUnity component, `scriptsResourcesFolder` = `scripts`, `entryLabel` = `start`, `randomSeed` = `12345`.

Then configure the `Canvas` for mobile (responsive UI): on its `CanvasScaler`, set `uiScaleMode` = `ScaleWithScreenSize`, `referenceResolution` = `(1920, 1080)`, `screenMatchMode` = `MatchWidthOrHeight`, `matchWidthOrHeight` = `0.5`. (Notch/safe-area handling is deferred to P1.)

Save the scene (UnityMCP `manage_scene` action `save`).

- [ ] **Step 4: Parity verification — path A (affinity up)**

Enter play mode (UnityMCP `manage_editor` action `play`). Observe:
1. 요르 appears in the left slot; speaker `요르`; text types out `주말에 뭐 할래?`.
2. Click to finish typing; after the line, two choices appear: `같이 영화 보자`, `귀찮은데 그냥 집에`.
3. Click `같이 영화 보자`.
4. Narration line `...그렇게 시간이 흘렀다.` (speaker `나레이션`).
5. Click to advance → `너랑 있으면 즐거워. 우리 사귈래?`.
6. Click to advance → `— 끝 —` is NOT required (P0 ends by halting; the last line simply remains). No further lines.

`read_console` (types: error) → expect zero errors. Stop play mode.

- [ ] **Step 5: Parity verification — path B (affinity down)**

Enter play mode again. This time choose `귀찮은데 그냥 집에`. Expect final line `...우리 안 맞는 것 같아.` `read_console` (types: error) → zero errors. Stop play mode.

If either path diverges from the original behavior, fix the script/wiring before proceeding. Do not delete the old files until both paths pass.

- [ ] **Step 6: Remove the legacy implementation**

```bash
git rm Assets/Scripts/DialogueManager.cs Assets/Scripts/DialogueManager.cs.meta \
       Assets/StreamingAssets/dialogues.json Assets/StreamingAssets/dialogues.json.meta
```
Refresh Unity, poll until compiled, `read_console` (types: error) → expect zero errors (nothing should reference `DialogueManager` anymore).

- [ ] **Step 7: Final full-suite run**

UnityMCP `run_tests` mode `EditMode`, no filter. Expected: entire `VNEngine.Tests` suite green.

- [ ] **Step 8: Commit**
```bash
git add Assets/StreamingAssets/scripts Assets/Scenes/Main.unity
git commit -m "feat(vnengine): migrate sample scenario to intro.vns; remove legacy DialogueManager"
```

- [ ] **Step 9: Push**
```bash
git push
```

---

## Self-Review

**Spec coverage** (each spec §2–§5 requirement → task):
- DSL commands `character/label/say/narration/bg/show/hide/menu/if-elif-else/while/jump/call/return` → Tasks 7 (parse), 8 (compile), 9–10 (execute). ✓
- Conditional menu choices `"..." if cond:` → Tasks 7, 8, 9. ✓
- Expression engine (int/bool, `+ - * / %`, integer division, comparisons incl. var-vs-var, `and/or/not`, parens, `random`) → Tasks 4–5. ✓
- Text interpolation `[var]`, `[[` escape → Task 10. ✓
- Multi-file scripts + global labels → Tasks 8 (linker), 13 (loader). ✓
- "Compile to flat instructions; PC + call stack" → Task 8 (compile), 9 (interpreter). ✓
- "VM knows nothing about the screen" (interfaces) → Task 9 (`IDialogueView`/`IStageView`), core asmdef `noEngineReferences` → Task 1. ✓
- EditMode unit tests with fakes → Tasks 2–10. ✓
- Migration parity + remove legacy → Task 14. ✓
- Entry label default `start`, `.vns` in `Resources/scripts/`, coroutine host → Task 13. ✓
- Mobile support (target Android/iOS/desktop): mobile-safe script loading via `.vns` ScriptedImporter + `Resources.LoadAll<TextAsset>` (no `File.IO`/StreamingAssets enumeration) → Task 13; touch works via mouse0 → Task 12; responsive `CanvasScaler` → Task 14. Safe-area/notch deferred to P1. ✓
- Deferred (P1+): fade/dissolve, expressions of `bg` render, audio, save/load, float, string vars — correctly absent. ✓

**Placeholder scan:** No TBD/TODO; every code step contains complete code; every test step contains real assertions. ✓

**Type consistency:** `VnValue`, `GameState.Get/Set`, `Expr` nodes, `Op`, `Instruction` fields (`StrA/StrB/ExprA/Target/Menu`), `MenuOption.Target`, `Interpreter(program,state,dialogue,stage)/Start/Tick/IsFinished`, `IDialogueView`/`IStageView` signatures, `Compiler.Compile` overloads, `VnScriptLoader.LoadAndCompile`, `VNRunner` fields — names match across Tasks 2–14. ✓

**Known scope notes:**
- Task 14's scene wiring is editor/MCP work (not scriptable as bash); parity is verified by play-mode observation, which is the correct test cycle for MonoBehaviour glue.
- The core asmdef `noEngineReferences: true` guarantees the interpreter cannot accidentally take a UnityEngine dependency — enforced by compilation, not just discipline.

