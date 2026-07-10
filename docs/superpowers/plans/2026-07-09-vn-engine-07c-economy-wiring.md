# 07-C Economy Wiring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the existing pure sim pieces (combat result, capture, inn income, stat upgrade) into a real resource economy (gold / mana-stone / karma bank) with consumption paths, closing the 07-B/07-A1 defer items, so the game loop actually turns.

**Architecture:** Add the missing state fields (karma bank on Meta, gacha pull counter on Run), add three new pure economy rules (`LootRule`, `DungeonLevelRule`, `GachaRule`), build a single-pass turn orchestrator (`CampaignWaveRule`) that runs *validate-placement → combat → loot→gold → captive accumulate → inn income → inn decay → karma bank*, then wire the consumption side (stat upgrade, level-up, gacha). Every rule stays a pure function over immutable state; all coefficients live as named constants (data-driven). No `UnityEngine` / `System.IO` in `Core/`.

**Tech Stack:** C# (Unity 2022+ editor assembly `VNEngine`), NUnit EditMode tests under `Assets/Tests/Editor/`. Integer-only arithmetic (no `float`/`double` in `Core/Sim`). `IntMath.Isqrt` for square roots.

## Global Constraints

- **Core purity:** no file under `Assets/Scripts/VNEngine/Core/Sim/**` may reference `UnityEngine`, `System.IO`, `float`, `double`, `System.Random`, `DateTime`, or any theme string. Randomness only via the existing `IRandom` abstraction.
- **Immutability:** all state types (`RunState`, `MetaState`, `CampaignState`, `InnState`, `HeroStats`) return NEW instances; never mutate inputs. Constructors defensive-copy collections.
- **Data-driven:** every coefficient is a named `public const` (or a data table), never an inline magic number at a call site. Reuse existing constants; do not duplicate.
- **Determinism:** same inputs (incl. same `IRandom` seed and same call order) → same output. Integer arithmetic only.
- **Layer discipline (06 §8.1):** Run layer = gold, mana-stone, captives, **gacha pull counter** (all loop-reset). Meta layer = **karma bank**, hero stats, inn (all carried across loops via `StartNewLoop`).
- **Save round-trip:** any new persisted field is ADDITIVE — do NOT bump `CampaignSaveVersion`; old saves deserialize the missing field to a safe default (0). `CampaignSave.Capture`/`Restore` must round-trip every new field, with defensive copies.
- **Inn ordering (07-D carry-over):** per tick the income gate (`InnIncomeRule.Compute`, which reads `Decor>0`) MUST be evaluated BEFORE `InnUpkeepRule.Decay` is applied. Honor the comment in `InnUpkeepRule.cs`.
- **MetaProjection scope:** 07-C only ADDS projection *targets* (resource/karma variables). It does NOT wire per-turn projection calls into any controller (that stays for the VN-narrative slice; 06 policy).
- **Tuning values are provisional:** new coefficients (capture karma, release karma, level-up table, gacha base) are initial estimates — mark them as such in a comment, matching the 07-D convention.
- **Test convention:** one `<Type>Tests.cs` per production type in `Assets/Tests/Editor/`, `namespace VNEngine.Tests`, NUnit `[Test]`. Every pure-rule suite includes (a) an input-immutability/non-mutation test and (b) a null-arg `Assert.Throws` test, plus the behavioral cases.

## Design Decisions (locked; provisional tuning)

- **Loot:** `LootGold(threatBase, isCapture)` = `max(1, GoldBase + Isqrt(threatBase)*GoldThreatK)`, halved (integer `/2`) when capture. `GoldBase=5`, `GoldThreatK=3` (from `sim_economy4.py`). Deterministic — the sim's `±3` deviation is dropped for engine determinism.
- **Capture vs release karma (two events):** capture credits immediate `CaptureKarma(threatBase) = Isqrt(threatBase)` to the karma bank; releasing a captive from prison credits a flat `ReleaseKarmaPerCaptive = 3` per captive. Both target the same Meta karma bank. This satisfies both verification bullets ("포획→골드50%+인과율" and "방면→인과율 bank").
- **Level-up cost (integer table):** `LevelUpCost(dungeonLevel)` realizes `120 * dl^1.32` (base=120, exp=1.32) as a precomputed integer table for dl 1..20; dl>20 clamps to the dl=20 value (soft cap). Requires gold + karma to spend.
- **Gacha:** `GachaCost(pullsThisLoop)` = `GachaBaseCost + pullsThisLoop/3` (integer div). `GachaBaseCost=2` (mana-stone). Counter lives on Run, resets each loop.
- **Resources (data, in `RunState.Resources`):** `gold`, `manaStone`. Ids are passed in by the caller (core stays theme-neutral); tests use the literal ids `"gold"`/`"manaStone"`.
- **Karma bank:** new `int KarmaBank` field on `MetaState` (meta-owned, carried in `StartNewLoop`).
- **Gacha pull counter:** new `int PullsThisLoop` field on `RunState` (run-owned; fresh `CreateInitialState` → 0 gives loop-reset for free; `ExecuteCommand` carries it forward).

---

### Task 1: Meta karma bank + Run gacha-pull counter (state fields + save)

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Sim/MetaState.cs`
- Modify: `Assets/Scripts/VNEngine/Core/Sim/RunState.cs`
- Modify: `Assets/Scripts/VNEngine/Core/Sim/TurnEngine.cs` (`ExecuteCommand` must carry `Captives` + `PullsThisLoop` forward)
- Modify: `Assets/Scripts/VNEngine/Core/Sim/LoopEngine.cs` (`StartNewLoop` carries `KarmaBank` forward)
- Modify: the campaign save data + capture/restore (locate via grep: `CampaignSaveData`, `CampaignSave`)
- Test: `Assets/Tests/Editor/MetaStateTests.cs` (create or extend), `RunStateTests.cs` (create or extend), extend `LoopEngineTests.cs`, extend `TurnEngineTests.cs`, extend `CampaignSaveTests.cs`

**Interfaces:**
- Produces: `MetaState(int loopCount, HeroStats heroes, InnState inn, int karmaBank)` with `int KarmaBank { get; }`; all existing shorter ctors default `karmaBank` to 0. `RunState(int day, IReadOnlyDictionary<string,int> resources, IReadOnlyList<Captive> captives, int pullsThisLoop)` with `int PullsThisLoop { get; }`; existing shorter ctors default it to 0.
- Consumes: nothing new.

- [ ] **Step 1: Write failing tests**

`MetaStateTests` — add:
```csharp
[Test] public void KarmaBank_DefaultsToZero() {
    var m = new MetaState(1);
    Assert.AreEqual(0, m.KarmaBank);
}
[Test] public void KarmaBank_RoundTripsThroughFullCtor() {
    var m = new MetaState(2, HeroStats.Empty, InnState.Empty, 42);
    Assert.AreEqual(42, m.KarmaBank);
}
```
`RunStateTests` — add:
```csharp
[Test] public void PullsThisLoop_DefaultsToZero() {
    var r = new RunState(1, new Dictionary<string,int>());
    Assert.AreEqual(0, r.PullsThisLoop);
}
[Test] public void PullsThisLoop_RoundTripsThroughFullCtor() {
    var r = new RunState(1, new Dictionary<string,int>(), System.Array.Empty<Captive>(), 7);
    Assert.AreEqual(7, r.PullsThisLoop);
}
```
`LoopEngineTests` — add: after `StartNewLoop`, `KarmaBank` from the prior Meta is preserved (build a campaign whose Meta has `KarmaBank=10`, start new loop, assert new Meta `KarmaBank==10` and `LoopCount` incremented).

`TurnEngineTests` — add: `ExecuteCommand` preserves `PullsThisLoop` and `Captives` (construct a `RunState` with `PullsThisLoop=5` and one `Captive`, run any command, assert both survive on the returned state).

`CampaignSaveTests` — add: capture→restore round-trips `KarmaBank` and `PullsThisLoop`; AND an old-save back-compat test (construct save data with the new fields absent/zero → restore yields `KarmaBank=0`, `PullsThisLoop=0`, no exception). Do NOT change `CampaignSaveVersion`.

- [ ] **Step 2: Run tests, verify they fail** (compile errors / missing members expected).

- [ ] **Step 3: Implement**
- `MetaState`: add `int KarmaBank { get; }`; add 4-arg ctor; chain all shorter ctors to it with `karmaBank: 0`.
- `RunState`: add `int PullsThisLoop { get; }`; add ctor overload taking `pullsThisLoop`; existing 2-arg and 3-arg ctors pass `0`.
- `TurnEngine.ExecuteCommand`: return `new RunState(state.Day + 1, res, state.Captives, state.PullsThisLoop)` (was dropping captives — fix now).
- `LoopEngine.StartNewLoop`: `new MetaState(loopCount+1, Heroes, Inn, campaign.Meta.KarmaBank)`.
- Save: add `karmaBank` and `pullsThisLoop` fields to the save DTO; `Capture` writes them, `Restore` reads them (missing → 0). Additive; version unchanged.

- [ ] **Step 4: Run tests, verify all pass.** Then run the full EditMode suite to confirm no regression.

- [ ] **Step 5: Commit** — `feat(sim): karma bank (meta) + gacha pull counter (run) with save round-trip (07-C task1)`

---

### Task 2: LootRule (sqrt loot gold + capture karma)

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Economy/LootRule.cs`
- Test: `Assets/Tests/Editor/LootRuleTests.cs`

**Interfaces:**
- Consumes: `IntMath.Isqrt(int)`.
- Produces: `LootRule.LootGold(int threatBase, bool isCapture) -> int`; `LootRule.CaptureKarma(int threatBase) -> int`; consts `GoldBase=5`, `GoldThreatK=3`.

- [ ] **Step 1: Write failing tests** `LootRuleTests`:
```csharp
using NUnit.Framework;
namespace VNEngine.Tests {
  public class LootRuleTests {
    [Test] public void Kill_IsFullLoot() {
      // threat 100: 5 + Isqrt(100)*3 = 5 + 10*3 = 35
      Assert.AreEqual(35, LootRule.LootGold(100, false));
    }
    [Test] public void Capture_IsHalfLoot_IntegerDiv() {
      Assert.AreEqual(35 / 2, LootRule.LootGold(100, true)); // 17
    }
    [Test] public void SqrtFlattening_Threat4x_GoldRoughly2x() {
      int g100 = LootRule.LootGold(100, false); // 35
      int g400 = LootRule.LootGold(400, false); // 5 + 20*3 = 65
      Assert.AreEqual(35, g100);
      Assert.AreEqual(65, g400);
      Assert.Less(g400, g100 * 2 + 1); // ~2x not 4x (65 < 71)
    }
    [Test] public void LootGold_FloorsAtOne() {
      Assert.AreEqual(1, LootRule.LootGold(0, false)); // max(1, 5+0) = 5 -> actually 5
    }
    [Test] public void CaptureKarma_IsIsqrtOfThreat() {
      Assert.AreEqual(10, LootRule.CaptureKarma(100));
      Assert.AreEqual(20, LootRule.CaptureKarma(400));
    }
  }
}
```
Note: `LootGold_FloorsAtOne` — with `threatBase=0`, `5 + Isqrt(0)*3 = 5`, so expected is `5`, not `1`. Fix the assertion to `Assert.AreEqual(5, LootRule.LootGold(0, false));` and keep a separate floor case only if a negative-ish path can produce <1 (it cannot here). Drop the misleading floor test; instead assert `LootRule.LootGold(0, true) == 2` (`5/2`).

- [ ] **Step 2: Run tests, verify fail** (type missing).

- [ ] **Step 3: Implement**
```csharp
namespace VNEngine {
  // 약탈 골드/포획 인과율 — 07 §6.2 제곱근 완화. 계수는 초기 추정(sim_economy4.py).
  public static class LootRule {
    public const int GoldBase = 5;
    public const int GoldThreatK = 3;
    public static int LootGold(int threatBase, bool isCapture) {
      int raw = System.Math.Max(1, GoldBase + IntMath.Isqrt(threatBase) * GoldThreatK);
      return isCapture ? raw / 2 : raw;
    }
    // 포획 즉시 인과율(성장 재료) — loot와 병렬. 방면 인과율은 PrisonRule.
    public static int CaptureKarma(int threatBase) => IntMath.Isqrt(threatBase);
  }
}
```

- [ ] **Step 4: Run tests, verify pass.**
- [ ] **Step 5: Commit** — `feat(sim): LootRule sqrt loot gold + capture karma (07-C task2)`

---

### Task 3: DungeonLevelRule (exponential level-up cost, integer table)

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Economy/DungeonLevelRule.cs`
- Test: `Assets/Tests/Editor/DungeonLevelRuleTests.cs`

**Interfaces:**
- Produces: `DungeonLevelRule.LevelUpCost(int dungeonLevel) -> int` (gold cost). Const `MaxTabulatedLevel = 20`.
- Note: room cap already lives in `Economy/DungeonRoomRule.cs` (`RoomsCap`); do NOT duplicate it here. This file is level-up cost only.

- [ ] **Step 1: Write failing tests** `DungeonLevelRuleTests`:
```csharp
[Test] public void LevelUpCost_MatchesTable() {
    Assert.AreEqual(120, DungeonLevelRule.LevelUpCost(1));
    Assert.AreEqual(299, DungeonLevelRule.LevelUpCost(2));
    Assert.AreEqual(511, DungeonLevelRule.LevelUpCost(3));
    Assert.AreEqual(1004, DungeonLevelRule.LevelUpCost(5));
    Assert.AreEqual(2507, DungeonLevelRule.LevelUpCost(10));
}
[Test] public void LevelUpCost_Monotonic() {
    for (int dl = 1; dl < 20; dl++)
        Assert.Less(DungeonLevelRule.LevelUpCost(dl), DungeonLevelRule.LevelUpCost(dl + 1));
}
[Test] public void LevelUpCost_ClampsAboveTable() {
    Assert.AreEqual(DungeonLevelRule.LevelUpCost(20), DungeonLevelRule.LevelUpCost(25));
}
[Test] public void LevelUpCost_RejectsNonPositive() {
    Assert.Throws<VnRuntimeException>(() => DungeonLevelRule.LevelUpCost(0));
}
```

- [ ] **Step 2: Run tests, verify fail.**

- [ ] **Step 3: Implement** — integer table realizing `int(120 * dl^1.32)`:
```csharp
namespace VNEngine {
  // 던전 레벨업 비용 — 07 §5.2 지수곡선 base=120, exp=1.32.
  // 코어 정수전용이라 float pow 대신 공식을 사전계산한 정수 테이블(데이터). 초기 추정 튜닝값.
  public static class DungeonLevelRule {
    public const int MaxTabulatedLevel = 20;
    // index = dungeonLevel, [0] unused. 120 * dl^1.32, floored.
    private static readonly int[] _cost = {
      0, 120, 299, 511, 748, 1004, 1277, 1565, 1867, 2181, 2507,
      2844, 3191, 3548, 3914, 4289, 4672, 5063, 5462, 5868, 6282
    };
    public static int LevelUpCost(int dungeonLevel) {
      if (dungeonLevel < 1)
        throw new VnRuntimeException($"dungeonLevel must be >= 1: {dungeonLevel}");
      int i = dungeonLevel > MaxTabulatedLevel ? MaxTabulatedLevel : dungeonLevel;
      return _cost[i];
    }
  }
}
```
Implementer note: verify each table entry equals `floor(120 * dl^1.32)` before committing (values above were precomputed; recompute to confirm dl 11..20 if any drifts). If a value is off by ≤1 due to rounding, prefer the recomputed value and update the corresponding test literal.

- [ ] **Step 4: Run tests, verify pass.**
- [ ] **Step 5: Commit** — `feat(sim): DungeonLevelRule exponential level-up cost table (07-C task3)`

---

### Task 4: GachaRule (loop-reset pull cost)

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Economy/GachaRule.cs`
- Test: `Assets/Tests/Editor/GachaRuleTests.cs`

**Interfaces:**
- Produces: `GachaRule.GachaCost(int pullsThisLoop) -> int`; const `GachaBaseCost = 2`.

- [ ] **Step 1: Write failing tests** `GachaRuleTests`:
```csharp
[Test] public void FirstPull_IsBaseCost() => Assert.AreEqual(2, GachaRule.GachaCost(0));
[Test] public void CostRisesEveryThreePulls() {
    Assert.AreEqual(2, GachaRule.GachaCost(2));  // 2 + 2/3 = 2
    Assert.AreEqual(3, GachaRule.GachaCost(3));  // 2 + 1
    Assert.AreEqual(4, GachaRule.GachaCost(6));  // 2 + 2
}
[Test] public void RejectsNegativePulls() =>
    Assert.Throws<VnRuntimeException>(() => GachaRule.GachaCost(-1));
```

- [ ] **Step 2: Run tests, verify fail.**
- [ ] **Step 3: Implement**
```csharp
namespace VNEngine {
  // 가챠 비용 — 07 §6.3. pullsThisLoop는 RunState(런) 소속 → 회차 리셋. 초기 추정.
  public static class GachaRule {
    public const int GachaBaseCost = 2;
    public static int GachaCost(int pullsThisLoop) {
      if (pullsThisLoop < 0)
        throw new VnRuntimeException($"pullsThisLoop must be >= 0: {pullsThisLoop}");
      return GachaBaseCost + pullsThisLoop / 3;
    }
  }
}
```
- [ ] **Step 4: Run tests, verify pass.**
- [ ] **Step 5: Commit** — `feat(sim): GachaRule loop-reset pull cost (07-C task4)`

---

### Task 5: PrisonRule (captive release → karma bank)

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/PrisonRule.cs`
- Test: `Assets/Tests/Editor/PrisonRuleTests.cs`

**Interfaces:**
- Consumes: `RunState.Captives`, `MetaState.KarmaBank` (Task 1).
- Produces: `PrisonRule.ReleaseAll(RunState run, MetaState meta) -> PrisonReleaseResult` where `PrisonReleaseResult` is a `readonly struct { RunState Run; MetaState Meta; int Released; int KarmaGained; }`. Releases every captive from the run (empties `Captives`), credits `ReleaseKarmaPerCaptive * count` to the meta karma bank, returns new Run (no captives) + new Meta (bank += karma). Const `ReleaseKarmaPerCaptive = 3`.

- [ ] **Step 1: Write failing tests** `PrisonRuleTests`:
```csharp
[Test] public void ReleaseAll_CreditsKarmaAndEmptiesPrison() {
    var run = new RunState(1, new Dictionary<string,int>(),
        new[]{ new Captive(new UnitClassId("a"), false, ResetPolicy.Unspecified),
               new Captive(new UnitClassId("b"), true, ResetPolicy.Unspecified) }, 0);
    var meta = new MetaState(1, HeroStats.Empty, InnState.Empty, 5);
    var res = PrisonRule.ReleaseAll(run, meta);
    Assert.AreEqual(2, res.Released);
    Assert.AreEqual(6, res.KarmaGained);          // 2 * 3
    Assert.AreEqual(11, res.Meta.KarmaBank);      // 5 + 6
    Assert.AreEqual(0, res.Run.Captives.Count);
}
[Test] public void ReleaseAll_EmptyPrison_NoOp() {
    var run = new RunState(1, new Dictionary<string,int>());
    var meta = new MetaState(1, HeroStats.Empty, InnState.Empty, 4);
    var res = PrisonRule.ReleaseAll(run, meta);
    Assert.AreEqual(0, res.Released);
    Assert.AreEqual(4, res.Meta.KarmaBank);
}
[Test] public void ReleaseAll_DoesNotMutateInputs() { /* original run.Captives.Count unchanged, original meta.KarmaBank unchanged */ }
[Test] public void ReleaseAll_NullArgs_Throw() { /* Assert.Throws<ArgumentNullException> for null run and null meta */ }
```
(Confirm the exact `UnitClassId`/`Captive` ctor from `Core/Sim/Combat/Captive.cs` before writing — adjust literals if the ctor differs.)

- [ ] **Step 2: Run tests, verify fail.**
- [ ] **Step 3: Implement** `PrisonRule.ReleaseAll` + `PrisonReleaseResult` struct. Build a new `RunState` with the same `Day`, `Resources`, `PullsThisLoop` but empty captives; new `MetaState` with `KarmaBank + ReleaseKarmaPerCaptive * count`. Null-check both args.
- [ ] **Step 4: Run tests, verify pass.**
- [ ] **Step 5: Commit** — `feat(sim): PrisonRule captive release -> karma bank (07-C task5, closes B defer)`

---

### Task 6: Placement gate + StatUpgrade live seeding + StatId validation (07-B / 07-A1 defers)

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Sim/LoopEngine.cs` (`CreateInitialCampaign` live-seeds hero stats)
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Stats/StatId.cs` OR the seeding boundary — reject null/empty `StatId.Value`
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Combat/PlacementBuilder.cs` — add a validate-then-build entry point (or a guard) so callers cannot build an unvalidated plan
- Test: extend `LoopEngineTests.cs`, `HeroStatsTests.cs` / `StatUpgradeTests.cs`, `PlacementBuilderTests.cs` (or `PlacementValidatorTests.cs`)

**Interfaces:**
- Consumes: `StatCatalog.Default()`, `HeroStats.FromDefs(...)`, `PlacementValidator.Validate(plan, graph, catalog)`, `PlacementBuilder.Apply(plan, graph, catalog)`.
- Produces: `LoopEngine.CreateInitialCampaign()` now returns a campaign whose `Meta.Heroes` has the 8 default stats live. A validated-build helper `PlacementBuilder.ValidateAndApply(plan, graph, catalog) -> RoomGraph` that throws `VnRuntimeException` (carrying the `PlacementError`) if `Validate` fails, else returns `Apply(...)`.

- [ ] **Step 1: Write failing tests**
- `LoopEngineTests`: `CreateInitialCampaign_SeedsEightHeroStats` — after create, `campaign.Meta.Heroes.Has(StatIds.Str)` etc. for all 8, values equal `StatCatalog.Default()` start values.
- StatId/seed boundary: `CreateInitialCampaign` (or `HeroStats.FromDefs`) rejects a `StatDef` whose `StatId.Value` is null/empty with `VnRuntimeException`. Write a test feeding a catalog with an empty-id def (via a small local catalog) → throws. (If validation belongs in `FromDefs`, put it there; otherwise guard at the seeding call.)
- **Seed-and-grow (A1 rec 2):** `StatUpgradeTests.SeedAndGrow_AbsentStat_StartsAtStartValueAndGrows` — take a `HeroStats` MISSING some stat, call `StatUpgrade.Upgrade` for that stat with enough karma, assert result stat value climbs from `def.StartValue` upward and `PointsGained > 0`.
- **Placement gate:** `ValidateAndApply_InvalidPlan_Throws` (e.g. hero not at core-front room → `HeroRoomNotCoreFront`) and `ValidateAndApply_ValidPlan_ReturnsGraphWithDefenders`.

- [ ] **Step 2: Run tests, verify fail.**
- [ ] **Step 3: Implement**
- `LoopEngine.CreateInitialCampaign`: `new MetaState(1, HeroStats.FromDefs(StatCatalog.Default()), InnState.Empty, 0)`.
- Reject null/empty `StatId.Value` at the seeding boundary (prefer `HeroStats.FromDefs` guarding each def's id; else guard in `CreateInitialCampaign`). Throw `VnRuntimeException`.
- `PlacementBuilder.ValidateAndApply`: call `PlacementValidator.Validate`; if `!result.IsValid` throw `VnRuntimeException($"Invalid placement: {result.Error}")`; else return `Apply(...)`.

- [ ] **Step 4: Run tests, verify pass.** Run full suite.
- [ ] **Step 5: Commit** — `feat(sim): live hero-stat seeding + StatId validation + placement validate-gate (07-C task6, closes A1/B defers)`

---

### Task 7: CampaignWaveRule — combat + loot→gold + captive accumulate (turn orchestrator, part 1)

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/CampaignWaveRule.cs`
- Test: `Assets/Tests/Editor/CampaignWaveRuleTests.cs`

**Interfaces:**
- Consumes: `PlacementBuilder.ValidateAndApply` (Task 6), `CombatResolver.ResolveWave(...)`, `LootRule` (Task 2), `CaptiveLedger.Accumulate`, `CampaignState`, and the wave threat computation used by the resolver (reuse `ThreatFormula` — read `CombatResolver.cs`/`ThreatFormula.cs` for the exact call; the loot threat MUST equal the threat combat used for that wave).
- Produces:
```csharp
public readonly struct WaveOutcome {
    public CampaignState Campaign { get; }
    public CombatResult Combat { get; }
    public int GoldGained { get; }
    public int CaptureKarmaGained { get; }
}
public static WaveOutcome ResolveWave(
    CampaignState campaign, PlacementPlan plan, WaveDef wave,
    RoomGraph baseGraph, IReadOnlyList<MonsterDef> monsterCatalog,
    HeroStats hero, StatCombatWeights statWeights, ThreatWeights threatWeights,
    IReadOnlyList<UnitClassDef> classCatalog, ClassMatchup matchup,
    CaptureRule captureRule, int dungeonLevel, string goldResourceId, IRandom rng)
```
Behavior, in order: (1) `graph = PlacementBuilder.ValidateAndApply(plan, baseGraph, monsterCatalog)` — throws on invalid placement (this is the hero-placement guard). (2) `combat = CombatResolver.ResolveWave(campaign.Run, wave, graph, hero, statWeights, threatWeights, classCatalog, matchup, captureRule, dungeonLevel, campaign.Meta.LoopCount, rng)`. (3) compute `threatBase` for the wave (same formula the resolver uses). (4) `gold = Killed.Count * LootRule.LootGold(threatBase,false) + Captured.Count * LootRule.LootGold(threatBase,true)`; `captureKarma = Captured.Count * LootRule.CaptureKarma(threatBase)`. (5) new Run = `CaptiveLedger.Accumulate(campaign.Run, combat)` then add `gold` to `Resources[goldResourceId]` (preserving `PullsThisLoop`). (6) new Meta = `campaign.Meta` with `KarmaBank += captureKarma`. (7) return `WaveOutcome`.

- [ ] **Step 1: Write failing tests** `CampaignWaveRuleTests` — build a minimal deterministic scenario (reuse fixture patterns from `CombatResolverTests`):
- `ResolveWave_KillsCreditFullLoot_CapturesCreditHalfLootPlusKarma`: with a known wave/threat, assert `GoldGained == kills*full + captures*half` and `CaptureKarmaGained == captures*Isqrt(threat)`, and that `Campaign.Meta.KarmaBank` increased by exactly that.
- `ResolveWave_AccumulatesCaptivesIntoRun`: `Campaign.Run.Captives.Count == combat.Captured.Count`.
- `ResolveWave_AddsGoldToResource`: `Campaign.Run.Resources["gold"]` increased by `GoldGained`.
- `ResolveWave_InvalidPlacement_Throws`: a plan with hero not at core-front → `VnRuntimeException` (guard proven).
- `ResolveWave_Deterministic`: same seed → identical `GoldGained`/`Captives`.
- `ResolveWave_DoesNotMutateInputCampaign`.

- [ ] **Step 2: Run tests, verify fail.**
- [ ] **Step 3: Implement** `CampaignWaveRule.ResolveWave` per the behavior spec. Keep it a pure static; no per-wave mutation of inputs. Preserve `PullsThisLoop` and `Day` when rebuilding `RunState`.
- [ ] **Step 4: Run tests, verify pass.** Run full suite.
- [ ] **Step 5: Commit** — `feat(sim): CampaignWaveRule combat->loot->gold + captive accumulate (07-C task7)`

---

### Task 8: CampaignWaveRule — inn income wiring (gate before decay) (turn orchestrator, part 2)

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Sim/CampaignWaveRule.cs` (add inn step)
- Test: extend `Assets/Tests/Editor/CampaignWaveRuleTests.cs`

**Interfaces:**
- Consumes: `InnIncomeRule.Compute(InnState)`, `InnUpkeepRule.Decay(InnState)`, `MetaState.Inn`.
- Produces: `WaveOutcome` gains `int InnGoldGained` and `int InnKarmaGained`; `ResolveWave` gains a step, AFTER combat/loot and BEFORE returning: (a) `income = InnIncomeRule.Compute(campaign.Meta.Inn)` — **gate evaluated here, on pre-decay Decor**; (b) add `income.Gold` to gold resource, add `income.Karma` to Meta `KarmaBank`; (c) `decayedInn = InnUpkeepRule.Decay(campaign.Meta.Inn)`; (d) new Meta carries `decayedInn` and the updated `KarmaBank`. Order (a)→(c) is mandatory (07-D carry-over).

- [ ] **Step 1: Write failing tests** (extend suite):
- `ResolveWave_InnIncomeAddsGoldAndKarma`: Meta.Inn with `Decor=5,Staff=3,MenuLevel=2` → `income = InnIncomeRule.Compute(inn)`; assert gold resource increased by `combatGold + income.Gold` and `KarmaBank` increased by `captureKarma + income.Karma`; assert `Campaign.Meta.Inn.Decor == 4` (decayed by `DecorDecayPerTick=1`).
- `ResolveWave_InnGateBeforeDecay_DecorOne_StillEarnsThenDecaysToZero`: Meta.Inn with `Decor=1` → income is computed on `Decor=1` (gate open, earns), and resulting `Inn.Decor==0`. This proves gate-before-decay: had decay run first, `Decor` would be 0 at gate time and income would be zero.
- `ResolveWave_InnDecorZero_NoInnIncome`: `Decor=0` → `InnGoldGained==0`, `InnKarmaGained==0`, `Inn.Decor==0`.

- [ ] **Step 2: Run tests, verify fail.**
- [ ] **Step 3: Implement** the inn step in the documented order. Extend `WaveOutcome` with the two inn fields.
- [ ] **Step 4: Run tests, verify pass.** Run full suite.
- [ ] **Step 5: Commit** — `feat(sim): CampaignWaveRule inn income wiring, gate-before-decay (07-C task8)`

---

### Task 9: Consumption — stat upgrade (spends karma bank) + dungeon level-up (gold + karma)

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Economy/EconomySpend.cs` (or extend `LoopEngine`) — thin wiring functions
- Test: `Assets/Tests/Editor/EconomySpendTests.cs`

**Interfaces:**
- Consumes: `StatUpgrade.Upgrade(HeroStats, StatDef, StatCostCurve, int karmaAvailable)`, `DungeonLevelRule.LevelUpCost(int)`, `MetaState.KarmaBank`, gold resource.
- Produces:
  - `EconomySpend.UpgradeHeroStat(MetaState meta, StatDef def, StatCostCurve curve) -> MetaState` — spends from `meta.KarmaBank`: calls `StatUpgrade.Upgrade(meta.Heroes, def, curve, meta.KarmaBank)`, returns new Meta with upgraded `Heroes` and `KarmaBank - result.KarmaSpent`.
  - `EconomySpend.LevelUpDungeon(CampaignState campaign, int currentDungeonLevel, string goldResourceId, int karmaCost) -> LevelUpResult` where `LevelUpResult { CampaignState Campaign; bool Leveled; }`. Requires `gold >= LevelUpCost(currentDungeonLevel)` AND `KarmaBank >= karmaCost`; on success deduct both (gold from run resource, karma from meta bank) and report `Leveled=true`; else no-op `Leveled=false`. (Dungeon level itself is tracked by the caller/meta; this task only gates+charges. If dungeon level is not yet a Meta field, store it as a Meta field now — add `int DungeonLevel` to `MetaState`, defaulting to 1, additive save like Task 1; increment on success.)

  Decision: add `int DungeonLevel` to `MetaState` (default 1, carried in `StartNewLoop`, saved additively) so level-up has a home. Fold this into Task 9.

- [ ] **Step 1: Write failing tests** `EconomySpendTests`:
- `UpgradeHeroStat_SpendsKarmaAndRaisesStat`: Meta with `KarmaBank=100`, upgrade one stat → `Heroes` stat raised, `KarmaBank == 100 - result.KarmaSpent`, `KarmaBank` never negative.
- `UpgradeHeroStat_ZeroBank_NoChange`: `KarmaBank=0` → stat unchanged, bank stays 0.
- `LevelUpDungeon_EnoughGoldAndKarma_ChargesBothAndIncrementsLevel`: gold=2000, KarmaBank=50, dl=1 (cost 120 gold, karmaCost say 10) → gold -=120, KarmaBank-=10, `Meta.DungeonLevel==2`, `Leveled==true`.
- `LevelUpDungeon_InsufficientGold_NoOp` and `_InsufficientKarma_NoOp`: `Leveled==false`, nothing charged.
- `MetaStateTests`: `DungeonLevel` defaults to 1, round-trips through ctor, carried by `StartNewLoop`, saved/restored (extend `CampaignSaveTests`).

- [ ] **Step 2: Run tests, verify fail.**
- [ ] **Step 3: Implement** `EconomySpend`, add `int DungeonLevel` to `MetaState` (default 1) with save round-trip + `StartNewLoop` carry-forward.
- [ ] **Step 4: Run tests, verify pass.** Full suite.
- [ ] **Step 5: Commit** — `feat(sim): consumption wiring - stat upgrade (karma) + dungeon level-up (gold+karma) (07-C task9)`

---

### Task 10: Consumption — gacha pull (mana-stone → pull, counter increments, loop-reset) + spend priority

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Economy/EconomySpend.cs` (add gacha)
- Create: `Assets/Scripts/VNEngine/Core/Sim/Economy/SpendPriority.cs`
- Test: extend `EconomySpendTests.cs`; create `SpendPriorityTests.cs`

**Interfaces:**
- Consumes: `GachaRule.GachaCost(int)`, `RunState.PullsThisLoop`, mana-stone resource.
- Produces:
  - `EconomySpend.GachaPull(RunState run, string manaResourceId) -> GachaPullResult { RunState Run; bool Pulled; int Cost; }` — cost = `GachaRule.GachaCost(run.PullsThisLoop)`; if `mana >= cost` deduct mana and return new Run with `PullsThisLoop + 1`; else no-op `Pulled=false`.
  - `SpendPriority.Order` — an ordered enumeration/array encoding §6.4: `DurabilityRepair > LevelUp > InnInvest > MobUpgrade > Gacha`. Provide `public enum SpendCategory { DurabilityRepair, LevelUp, InnInvest, MobUpgrade, Gacha }` and `public static readonly IReadOnlyList<SpendCategory> Order = {...}`. (No auto-spender required this slice — expose the order as the safety-net default; actual selection is user choice per brief.)

- [ ] **Step 1: Write failing tests**
- `GachaPull_EnoughMana_DeductsAndIncrementsCounter`: mana=10, pulls=0 → cost 2, mana==8, `PullsThisLoop==1`, `Pulled==true`.
- `GachaPull_CostRisesWithPulls`: from `PullsThisLoop=3` → cost 3.
- `GachaPull_InsufficientMana_NoOp`.
- `GachaPull_CounterResetsOnNewLoop`: build campaign, do pulls (run counter >0), `LoopEngine.StartNewLoop`, assert `Run.PullsThisLoop==0`. (This proves loop-reset — the headline verification item.)
- `SpendPriorityTests.Order_MatchesDoc`: `Order[0]==DurabilityRepair ... Order[4]==Gacha`.

- [ ] **Step 2: Run tests, verify fail.**
- [ ] **Step 3: Implement** `GachaPull` and `SpendPriority`.
- [ ] **Step 4: Run tests, verify pass.** Full suite.
- [ ] **Step 5: Commit** — `feat(sim): gacha pull (loop-reset counter) + spend priority order (07-C task10)`

---

### Task 11: MetaProjection — add resource/karma projection targets (targets only)

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Sim/MetaProjection.cs`
- Test: extend `Assets/Tests/Editor/MetaProjectionTests.cs`

**Interfaces:**
- Produces: new projection methods that write resource + karma values into the injected `GameState` variable store — e.g. `ProjectKarmaBank(MetaState meta, GameState state, string karmaVar)` and `ProjectResources(RunState run, GameState state, IReadOnlyDictionary<string,string> resourceVars)` (map resource id → variable name; absent resource → 0). Follow the existing one-way, theme-neutral, injected-name pattern. Do NOT add any per-turn call site (06 policy — narrative slice owns wiring).

- [ ] **Step 1: Write failing tests** `MetaProjectionTests`:
- `ProjectKarmaBank_WritesValue`: Meta `KarmaBank=17` → `state[karmaVar]==17`.
- `ProjectResources_WritesEach_AbsentIsZero`: run with `gold=50` and mapping `{gold->"varGold", manaStone->"varMana"}` → `varGold==50`, `varMana==0`.
- Null-arg throws; existing projection tests still pass.

- [ ] **Step 2: Run tests, verify fail.**
- [ ] **Step 3: Implement** the two projection methods.
- [ ] **Step 4: Run tests, verify pass.**
- [ ] **Step 5: Commit** — `feat(sim): MetaProjection resource + karma bank targets (07-C task11)`

---

### Task 12: Documentation (07 §5·6·7 reflect wiring)

**Files:**
- Modify: `docs/engine/07-defense-combat.md` (§5.1 level-up paths, §6.2/§6.3/§6.4, §7.2 inn wiring, §9 checklist — mark now-implemented items)
- Modify: `docs/engine/06-loop-and-state.md` (착수/구현 상태 callout for karma bank meta + pull counter run, if such callouts exist there — match the 07-D precedent)

**Interfaces:** docs only, no code, no tests.

- [ ] **Step 1: Update `07-defense-combat.md`** — add "구현(07-C, 2026-07-09)" callouts mirroring the 07-D callout style: LootRule (§6.2), DungeonLevelRule table (§5.2), GachaRule loop-reset (§6.3), SpendPriority (§6.4), CampaignWaveRule orchestration + inn gate-before-decay (§7.2), PrisonRule release→karma (§5.1 path 2), live hero-stat seeding + placement gate. State the actual constants used and mark them provisional tuning values. Mark the §9 checklist items now done.
- [ ] **Step 2: Update `06-loop-and-state.md`** run/meta layer note for KarmaBank (meta) + PullsThisLoop (run) + DungeonLevel (meta), if the file carries such status callouts.
- [ ] **Step 3: Commit** — `docs(sim): reflect 07-C economy wiring (§5·6·7)`

---

## Verification (maps to brief's EditMode checklist)

- Kill → gold 100%, capture → gold 50% + karma (Task 2, Task 7)
- Prison release → karma bank accrues (Task 5, Task 9)
- Inn innGold/innKarma accrue into resources through the turn loop (Task 8)
- Karma-bank-funded hero stat upgrade, bank deducted (Task 9)
- Gold + karma dungeon level-up (Task 9)
- Gacha pull counter loop-reset — 0 after regress (Task 10)
- hero-placement: build without validate is blocked (guard), valid after validate (Task 6, Task 7)
- seed-and-grow: absent stat 0→grow path (Task 6)
- live seeding: CreateInitialCampaign has 8 live hero stats + StatId null/empty rejected (Task 6)
- inn gate evaluated before decay (Task 8)
- integer determinism, immutability, Core purity, save round-trip (defensive copies) — every task

## Final whole-branch review

After Task 12: dispatch the whole-branch code review (most capable model) over `merge-base main HEAD .. HEAD`, then `superpowers:finishing-a-development-branch` → merge to `main` with `--no-ff`.
