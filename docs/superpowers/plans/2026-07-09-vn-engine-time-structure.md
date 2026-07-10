# 1회차 시간구조 (90일 루프 + 모듈형 진행) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 06/07까지 완성된 게임 루프 조각(전투·배치·포획·가챠·여관·회귀)을 90일(9주기×10일) 시간구조로 묶어, "하루 전이" 순수 코어 위에 속도·스킵을 표시 계층으로 분리한 모듈형 진행 시스템을 만든다.

**Architecture:** 코어는 순수함수 `CampaignDayRule.AdvanceDay(campaign, ctx, rng) → AdvanceResult` 하나뿐 — 하루 Day+1 후 그날 페이즈에 따라 웨이브날은 기존 `CampaignWaveRule.ResolveWave`, 정비날은 신규 `MaintenanceRule.ApplyInnTick`(게이트=Decay前)을 실행하고, Day>90이면 처리 없이 회귀신호만 반환한다. 순수 질의 `TimeQuery`(페이즈/세이브일/다음웨이브)와, `AdvanceDay`를 리듬으로 조합하는 모듈 `TimeController`(Step/SkipToNextWave/SkipToDay)를 얹는다. 속도(FastForward)는 코어를 모르는 Unity 표시 계층(SimController)에만 존재한다.

**Tech Stack:** C# (Unity 2022+; 코어 어셈블리 `VNEngine.Core`, `noEngineReferences:true`). NUnit EditMode 테스트 `Assets/Tests/Editor/` (asmdef `VNEngine.Tests`, namespace `VNEngine.Tests`). 정수 전용 산술, RNG는 `IRandom` 추상화만.

## Global Constraints

- **코어 순수성:** `Assets/Scripts/VNEngine/Core/Sim/Time/**`의 어떤 파일도 `UnityEngine`·`UnityEditor`·`System.IO`·`float`·`double`·`System.Random`·`DateTime`를 참조하지 않는다. 속도/틱/코루틴 등 시간표시 코드는 오직 `VNEngine.Unity`(SimController)에만 둔다.
- **불변 상태:** 모든 상태 타입(`RunState`/`MetaState`/`CampaignState`/`InnState`)은 새 인스턴스를 반환한다. 입력을 절대 변형하지 않는다. 컬렉션은 방어적 복사.
- **데이터 주도:** 모든 계수(MaxDay=90, WaveInterval=10, Cycles=9, SaveDayInCycle=9)는 명명된 `public const`. 호출부 매직넘버 금지.
- **결정론:** 같은 입력(같은 `IRandom` 시드·같은 호출 순서) → 같은 출력. 정수 산술만.
- **레이어 규율(06 §8.1):** Day는 **Run 소속(회차 리셋)**. 시간구조는 Meta에 아무 필드도 추가하지 않는다.
- **여관 순서(07-D 이월 불변식):** 하루 정산에서 `InnIncomeRule.Compute`(게이트=`Decor>0`)는 반드시 `InnUpkeepRule.Decay` **前**에 평가. 정비일 경로와 웨이브일 경로 양쪽에서 지켜져야 한다.
- **테스트 관례:** 프로덕션 타입당 `<Type>Tests.cs` 하나, `namespace VNEngine.Tests`, NUnit `[Test]`. 각 순수-규칙 스위트는 (a) 입력 비변형(non-mutation) 테스트와 (b) null-arg `Assert.Throws` 테스트 + 행동 케이스를 포함한다.
- **세이브 무변경:** 시간구조는 새 영속 상태를 추가하지 않는다(`Day`는 이미 `RunState`에 있고 세이브 라운드트립됨, `DayContext`는 런타임 config로 저장 안 함). `CampaignSaveVersion` 건드리지 않는다.

## Design Decisions (locked; rationale)

- **[결정 1] `TimeState{int Day}` 신설하지 않고 기존 `RunState.Day` 재사용.** `RunState.Day`는 이미 런 귀속이고 `LoopEngine.StartNewLoop`→`TurnEngine.CreateInitialState()`→`new RunState(1, …)`로 회차마다 1로 리셋되며 세이브도 됨. 별도 Day 필드 추가는 경쟁하는 두 개의 일자 카운터 = 상태 중복(리뷰 결함). 프롬프트의 "TimeState{int Day}는 06 RunState에 편입(회차 리셋)"은 "일자는 RunState에 있다"의 충실한 실현이다. Time 폴더는 `TimeQuery`/`AdvanceDay`/`TimeController`만 담고, 일자 자체는 `RunState.Day`를 읽고 쓴다.
- **[결정 2] 스킵은 웨이브를 절대 자동 해소하지 않는다 — 웨이브 전날(eve)에서 정지.** `AdvanceDay`는 진입한 그 날을 즉시 정산한다(하루=1 정산). 따라서 프롬프트 원칙 "스킵은 정비 구간만, 웨이브 만나면 멈춤"을 지키려면 스킵은 다음 웨이브의 **전날**까지만 `AdvanceDay`를 반복하고 멈춘다(예: Day3에서 `SkipToNextWave`→Day9). 웨이브는 오직 `Step`으로 그 날에 진입할 때만 `ResolveWave`로 해소된다. 이는 검증 항목 "SkipToNextWave 웨이브전날 정지" 및 "스킵 정산=하루씩과 동일결과"(스킵 구간이 정비날뿐이라 RNG 미소비, 결정론적으로 동일)와 정합한다. `SkipToDay(n)`도 사이에 웨이브가 있으면 그 웨이브 전날에서 멈춘다(웨이브를 넘지 않음).
- **[결정 3] 하루 1회 여관 정산.** 웨이브날은 `ResolveWave` 내부 여관 스텝이, 정비날은 `MaintenanceRule.ApplyInnTick`이 각각 하루치 여관 수급+Decay를 수행한다(하루당 정확히 1틱). 두 경로 모두 게이트-전-Decay 불변식을 지킨다. `MaintenanceRule`은 신규 canonical 정비 틱이며, `CampaignWaveRule.ResolveWave`의 기존 인라인 여관 스텝은 전투 경로와 얽혀 있어 이번 슬라이스에서 리팩터링하지 않는다(전투 경로 무변경 원칙). 두 구현의 통합은 후속으로 명시적으로 미룬다.
- **[결정 4] 범위 밖(YAGNI, 훅만 문서화):** (a) 정비일 "모험가 웨이브 확률 난입" — story의 가능성 서술이지 AdvanceDay 스펙이 아님. 정비일은 여관 정산만. (b) "코어붕괴 조건" 조기 회귀 — 이번 슬라이스는 `Day>90` 트리거만 구현하고 코어붕괴는 문서 훅으로 남긴다.

---

### Task 1: TimeQuery + DayPhase (순수 질의 + 상수)

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Time/DayPhase.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Time/TimeQuery.cs`
- Test: `Assets/Tests/Editor/TimeQueryTests.cs`

**Interfaces:**
- Consumes: `VnRuntimeException` (기존, namespace `VNEngine`).
- Produces:
  - `enum DayPhase { Maintenance, Wave }` (namespace `VNEngine`).
  - `static class TimeQuery`: consts `MaxDay=90`, `WaveInterval=10`, `Cycles=9`, `SaveDayInCycle=9`; methods `DayPhase GetPhase(int day)`, `bool IsWaveDay(int day)`, `bool IsSaveDay(int day)`, `int DaysUntilNextWave(int day)`. 모두 순수·결정론, `day < 1`이면 `VnRuntimeException`.

- [ ] **Step 1: Write the failing test**

`Assets/Tests/Editor/TimeQueryTests.cs`:
```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class TimeQueryTests
    {
        [Test] public void Constants_MatchNinetyDayStructure()
        {
            Assert.AreEqual(90, TimeQuery.MaxDay);
            Assert.AreEqual(10, TimeQuery.WaveInterval);
            Assert.AreEqual(9, TimeQuery.Cycles);
            Assert.AreEqual(9, TimeQuery.SaveDayInCycle);
        }

        [Test] public void GetPhase_MultiplesOfTen_AreWaveDays()
        {
            Assert.AreEqual(DayPhase.Wave, TimeQuery.GetPhase(10));
            Assert.AreEqual(DayPhase.Wave, TimeQuery.GetPhase(90));
        }

        [Test] public void GetPhase_NonMultiplesOfTen_AreMaintenance()
        {
            Assert.AreEqual(DayPhase.Maintenance, TimeQuery.GetPhase(1));
            Assert.AreEqual(DayPhase.Maintenance, TimeQuery.GetPhase(9));
            Assert.AreEqual(DayPhase.Maintenance, TimeQuery.GetPhase(11));
        }

        [Test] public void IsWaveDay_TrueOnlyForMultiplesOfTenWithinRange()
        {
            Assert.IsTrue(TimeQuery.IsWaveDay(10));
            Assert.IsTrue(TimeQuery.IsWaveDay(90));
            Assert.IsFalse(TimeQuery.IsWaveDay(9));
            Assert.IsFalse(TimeQuery.IsWaveDay(100)); // 범위 밖
        }

        [Test] public void IsSaveDay_TrueOnEveOfEachWave()
        {
            Assert.IsTrue(TimeQuery.IsSaveDay(9));
            Assert.IsTrue(TimeQuery.IsSaveDay(89));
            Assert.IsFalse(TimeQuery.IsSaveDay(10));
            Assert.IsFalse(TimeQuery.IsSaveDay(1));
        }

        [Test] public void DaysUntilNextWave_CountsToNextMultipleOfTen()
        {
            Assert.AreEqual(9, TimeQuery.DaysUntilNextWave(1));  // 1->10
            Assert.AreEqual(1, TimeQuery.DaysUntilNextWave(9));  // 9->10
            Assert.AreEqual(10, TimeQuery.DaysUntilNextWave(10)); // 10->20
            Assert.AreEqual(0, TimeQuery.DaysUntilNextWave(90));  // 마지막 웨이브 이후 없음
        }

        [Test] public void Queries_RejectNonPositiveDay()
        {
            Assert.Throws<VnRuntimeException>(() => TimeQuery.GetPhase(0));
            Assert.Throws<VnRuntimeException>(() => TimeQuery.IsWaveDay(0));
            Assert.Throws<VnRuntimeException>(() => TimeQuery.IsSaveDay(-1));
            Assert.Throws<VnRuntimeException>(() => TimeQuery.DaysUntilNextWave(0));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: EditMode 스위트(`TimeQueryTests`). Expected: FAIL — `DayPhase`/`TimeQuery` 타입 없음(컴파일 에러).

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/VNEngine/Core/Sim/Time/DayPhase.cs`:
```csharp
namespace VNEngine
{
    // 하루의 페이즈. 정비(자원/여관) 또는 웨이브(전투).
    public enum DayPhase
    {
        Maintenance,
        Wave
    }
}
```

`Assets/Scripts/VNEngine/Core/Sim/Time/TimeQuery.cs`:
```csharp
namespace VNEngine
{
    // 90일 시간구조 순수 질의 — 페이즈/세이브일/다음웨이브. 스킵·자동 진행이 참조.
    // 90일 = 9주기 × 10일. 각 주기 1~9일 정비 / 10일 웨이브. 전부 순수·결정론.
    public static class TimeQuery
    {
        public const int MaxDay = 90;
        public const int WaveInterval = 10;
        public const int Cycles = 9;
        public const int SaveDayInCycle = 9; // 각 주기 9일차(웨이브 전날) 세이브 확인

        public static DayPhase GetPhase(int day)
            => IsWaveDay(day) ? DayPhase.Wave : DayPhase.Maintenance;

        public static bool IsWaveDay(int day)
        {
            Require(day);
            return day % WaveInterval == 0 && day <= MaxDay;
        }

        public static bool IsSaveDay(int day)
        {
            Require(day);
            return day % WaveInterval == SaveDayInCycle;
        }

        // 현재 일자에서 다음 웨이브까지 남은 일수. 마지막 웨이브(90일) 이후엔 0.
        public static int DaysUntilNextWave(int day)
        {
            Require(day);
            if (day >= MaxDay) return 0;
            int nextWave = (day / WaveInterval + 1) * WaveInterval;
            return nextWave - day;
        }

        private static void Require(int day)
        {
            if (day < 1)
                throw new VnRuntimeException($"day must be >= 1: {day}");
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: EditMode 스위트. Expected: PASS (7 tests). 이어서 전체 EditMode 스위트로 회귀 없음 확인.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/VNEngine/Core/Sim/Time/DayPhase.cs Assets/Scripts/VNEngine/Core/Sim/Time/TimeQuery.cs Assets/Tests/Editor/TimeQueryTests.cs
git commit -m "feat(sim): TimeQuery + DayPhase 90-day queries (time-structure task1)"
```

---

### Task 2: MaintenanceRule.ApplyInnTick (정비일 여관 정산, 게이트 前 Decay)

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Time/MaintenanceRule.cs`
- Test: `Assets/Tests/Editor/MaintenanceRuleTests.cs`

**Interfaces:**
- Consumes: `InnIncomeRule.Compute(InnState) -> InnIncome`(필드 `int Gold, Karma, Guests`; `InnIncome.Zero`), `InnUpkeepRule.Decay(InnState) -> InnState`, `CampaignState`, `MetaState`(5-arg ctor `(loopCount, heroes, inn, karmaBank, dungeonLevel)`), `RunState`(4-arg ctor `(day, resources, captives, pullsThisLoop)`).
- Produces:
  - `readonly struct InnTickOutcome { CampaignState Campaign; int Gold; int Karma; }`
  - `static class MaintenanceRule`: `InnTickOutcome ApplyInnTick(CampaignState campaign, string goldResourceId)` — 여관 수급 게이트를 pre-decay Inn에 평가 → 골드를 `run.Resources[goldResourceId]`에 적립 → 인과율을 `meta.KarmaBank`에 적립 → `InnUpkeepRule.Decay` 적용한 새 Inn을 담은 새 `CampaignState` 반환. `Day`/`Captives`/`PullsThisLoop`/`Heroes`/`DungeonLevel`/`LoopCount` 보존. null-arg는 `ArgumentNullException`.

- [ ] **Step 1: Write the failing test**

`Assets/Tests/Editor/MaintenanceRuleTests.cs`. **구현 전 `InnState.cs`에서 생성자 인자 순서를 확인하고 아래 InnState 생성 리터럴을 맞출 것**(필드는 Staff/Decor/MenuLevel; `InnState.Empty`는 (0,0,0)). 아래 기대값은 `Staff=3, Decor=1, MenuLevel=2` 기준: `guests=min(3*2+2,25)=8`, `gold=min(Isqrt(8)*8 + 2*3,300)=min(2*8+6,300)=22`, `karma=guests=8`.

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class MaintenanceRuleTests
    {
        // InnState 생성자 인자 순서는 InnState.cs 확인 후 맞출 것.
        private static InnState Inn(int staff, int decor, int menu) => new InnState(staff, decor, menu);

        private static CampaignState Campaign(InnState inn, int karmaBank, int gold)
        {
            var meta = new MetaState(1, HeroStats.Empty, inn, karmaBank, 1);
            var run = new RunState(5, new Dictionary<string, int> { ["gold"] = gold });
            return new CampaignState(meta, run);
        }

        [Test] public void ApplyInnTick_EarnsIncomeThenDecays()
        {
            var c = Campaign(Inn(3, 1, 2), karmaBank: 10, gold: 100);
            var r = MaintenanceRule.ApplyInnTick(c, "gold");
            Assert.AreEqual(22, r.Gold);
            Assert.AreEqual(8, r.Karma);
            Assert.AreEqual(122, r.Campaign.Run.Resources["gold"]);   // 100 + 22
            Assert.AreEqual(18, r.Campaign.Meta.KarmaBank);           // 10 + 8
            Assert.AreEqual(0, r.Campaign.Meta.Inn.Decor);           // 1 -> Decay -> 0
        }

        [Test] public void ApplyInnTick_GateBeforeDecay_DecorOne_StillEarns()
        {
            // Decor=1: 게이트가 pre-decay(1>0)에서 열려 수급 발생, 그 후 Decay로 0.
            // 만약 Decay가 먼저면 Decor=0에서 게이트 닫혀 수급 0이 됐을 것.
            var c = Campaign(Inn(3, 1, 2), karmaBank: 0, gold: 0);
            var r = MaintenanceRule.ApplyInnTick(c, "gold");
            Assert.Greater(r.Gold, 0);
            Assert.AreEqual(0, r.Campaign.Meta.Inn.Decor);
        }

        [Test] public void ApplyInnTick_DecorZero_NoIncome()
        {
            var c = Campaign(Inn(3, 0, 2), karmaBank: 5, gold: 50);
            var r = MaintenanceRule.ApplyInnTick(c, "gold");
            Assert.AreEqual(0, r.Gold);
            Assert.AreEqual(0, r.Karma);
            Assert.AreEqual(50, r.Campaign.Run.Resources["gold"]);
            Assert.AreEqual(5, r.Campaign.Meta.KarmaBank);
            Assert.AreEqual(0, r.Campaign.Meta.Inn.Decor);
        }

        [Test] public void ApplyInnTick_PreservesDayAndRunFields()
        {
            var c = Campaign(Inn(3, 5, 2), karmaBank: 0, gold: 0);
            var r = MaintenanceRule.ApplyInnTick(c, "gold");
            Assert.AreEqual(5, r.Campaign.Run.Day);
            Assert.AreEqual(1, r.Campaign.Meta.LoopCount);
            Assert.AreEqual(1, r.Campaign.Meta.DungeonLevel);
        }

        [Test] public void ApplyInnTick_DoesNotMutateInput()
        {
            var inn = Inn(3, 5, 2);
            var c = Campaign(inn, karmaBank: 10, gold: 100);
            MaintenanceRule.ApplyInnTick(c, "gold");
            Assert.AreEqual(5, c.Meta.Inn.Decor);       // 원본 Decor 불변
            Assert.AreEqual(10, c.Meta.KarmaBank);      // 원본 bank 불변
            Assert.AreEqual(100, c.Run.Resources["gold"]); // 원본 gold 불변
        }

        [Test] public void ApplyInnTick_NullArgs_Throw()
        {
            var c = Campaign(Inn(3, 1, 2), 0, 0);
            Assert.Throws<System.ArgumentNullException>(() => MaintenanceRule.ApplyInnTick(null, "gold"));
            Assert.Throws<System.ArgumentNullException>(() => MaintenanceRule.ApplyInnTick(c, null));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Expected: FAIL — `MaintenanceRule`/`InnTickOutcome` 타입 없음. (InnState 생성자 시그니처가 다르면 여기서 조정.)

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/VNEngine/Core/Sim/Time/MaintenanceRule.cs`:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    // 정비일 자원 정산(canonical 하루 여관 틱).
    // 07-D 불변식: InnIncomeRule.Compute(게이트=Decor>0)를 반드시 InnUpkeepRule.Decay 前에 평가.
    public static class MaintenanceRule
    {
        public static InnTickOutcome ApplyInnTick(CampaignState campaign, string goldResourceId)
        {
            if (campaign == null) throw new System.ArgumentNullException(nameof(campaign));
            if (goldResourceId == null) throw new System.ArgumentNullException(nameof(goldResourceId));

            var meta = campaign.Meta;
            var run = campaign.Run;

            var income = InnIncomeRule.Compute(meta.Inn);   // 게이트: pre-decay Decor
            var decayedInn = InnUpkeepRule.Decay(meta.Inn); // 그 다음 Decay

            var res = new Dictionary<string, int>(run.Resources.Count);
            foreach (var kv in run.Resources) res[kv.Key] = kv.Value;
            res.TryGetValue(goldResourceId, out int gold);
            res[goldResourceId] = gold + income.Gold;

            var newRun = new RunState(run.Day, res, run.Captives, run.PullsThisLoop);
            var newMeta = new MetaState(meta.LoopCount, meta.Heroes, decayedInn,
                                        meta.KarmaBank + income.Karma, meta.DungeonLevel);
            return new InnTickOutcome(new CampaignState(newMeta, newRun), income.Gold, income.Karma);
        }
    }

    public readonly struct InnTickOutcome
    {
        public CampaignState Campaign { get; }
        public int Gold { get; }
        public int Karma { get; }
        public InnTickOutcome(CampaignState campaign, int gold, int karma)
        {
            Campaign = campaign;
            Gold = gold;
            Karma = karma;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Expected: PASS. 전체 EditMode 스위트로 회귀 없음 확인(특히 InnIncomeRule/InnUpkeepRule 관련).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/VNEngine/Core/Sim/Time/MaintenanceRule.cs Assets/Tests/Editor/MaintenanceRuleTests.cs
git commit -m "feat(sim): MaintenanceRule daily inn tick, gate-before-decay (time-structure task2)"
```

---

### Task 3: DayContext + AdvanceResult + CampaignDayRule.AdvanceDay (시간 코어)

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Time/DayContext.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Time/AdvanceResult.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Time/CampaignDayRule.cs`
- Create: `Assets/Tests/Editor/SimTimeFixtures.cs` (Task 4 재사용 공용 픽스처 헬퍼)
- Test: `Assets/Tests/Editor/CampaignDayRuleTests.cs`

**Interfaces:**
- Consumes: `TimeQuery`(Task 1), `MaintenanceRule.ApplyInnTick`(Task 2), 기존 `CampaignWaveRule.ResolveWave(CampaignState, PlacementPlan, WaveDef, RoomGraph, IReadOnlyList<MonsterDef>, HeroStats, StatCombatWeights, ThreatWeights, IReadOnlyList<UnitClassDef>, ClassMatchup, CaptureRule, int dungeonLevel, string goldResourceId, IRandom) -> WaveOutcome`(struct 필드 `CampaignState Campaign; CombatResult Combat; int GoldGained; int CaptureKarmaGained; int InnGoldGained; int InnKarmaGained`), `IRandom`, `VnRuntimeException`.
- Produces:
  - `sealed class DayContext` — ResolveWave에 필요한 회차 설정 번들(불변, 저장 안 함). 생성자 인자 전부 null-check + 리스트 방어적 복사. `WaveDef WaveForDay(int day)`.
  - `readonly struct AdvanceResult { CampaignState Campaign; DayPhase Phase; bool RegressPending; bool WaveResolved; WaveOutcome Wave; }`
  - `static class CampaignDayRule`: `AdvanceResult AdvanceDay(CampaignState campaign, DayContext ctx, IRandom rng)`.

**AdvanceDay 동작(순서 확정):**
1. null-check(campaign/ctx/rng → `ArgumentNullException`).
2. `newDay = campaign.Run.Day + 1`. `newDay > TimeQuery.MaxDay`면 처리 없이 `AdvanceResult(campaign, GetPhase(campaign.Run.Day), regressPending:true, waveResolved:false, default)` 반환(회귀는 caller가 `LoopEngine.StartNewLoop`로).
3. Day를 newDay로 bump한 `atNewDay` 캠페인 생성(Meta 그대로, Run은 새 `RunState(newDay, run.Resources, run.Captives, run.PullsThisLoop)`).
4. `phase = TimeQuery.GetPhase(newDay)`.
   - `Wave`: `outcome = CampaignWaveRule.ResolveWave(atNewDay, ctx.Plan, ctx.WaveForDay(newDay), ctx.BaseGraph, ctx.MonsterCatalog, atNewDay.Meta.Heroes, ctx.StatWeights, ctx.ThreatWeights, ctx.ClassCatalog, ctx.Matchup, ctx.CaptureRule, atNewDay.Meta.DungeonLevel, ctx.GoldResourceId, rng)` → `AdvanceResult(outcome.Campaign, DayPhase.Wave, false, true, outcome)`.
   - `Maintenance`: `tick = MaintenanceRule.ApplyInnTick(atNewDay, ctx.GoldResourceId)` → `AdvanceResult(tick.Campaign, DayPhase.Maintenance, false, false, default)`.
- **AdvanceDay는 속도·스킵을 전혀 모른다**(하루 전이 + 그날 사건 처리까지만).

- [ ] **Step 1: Write the failing test**

먼저 `Assets/Tests/Editor/SimTimeFixtures.cs`를 만든다. **기존 `Assets/Tests/Editor/CampaignWaveRuleTests.cs`를 읽고 그 결정론적 웨이브 픽스처 빌더(plan/wave/graph/monsterCatalog/statWeights/threatWeights/classCatalog/matchup/captureRule/goldResourceId, hero 시드된 초기 캠페인)를 그대로 재사용/추출**하여 아래 두 헬퍼를 제공한다. (CampaignWaveRuleTests가 이미 유효한 최소 시나리오를 갖고 있으므로 시그니처는 그 파일에서 확정할 것.)

```csharp
using System.Collections.Generic;

namespace VNEngine.Tests
{
    // CampaignWaveRuleTests의 결정론적 픽스처를 시간구조 테스트가 공유하기 위한 헬퍼.
    // ↓ 세부 빌더는 CampaignWaveRuleTests.cs에서 확정해 채운다(같은 값 사용).
    internal static class SimTimeFixtures
    {
        // 유효한 DayContext(웨이브 1개 이상, 골드 자원 id="gold").
        public static DayContext DayContext()
        {
            // CampaignWaveRuleTests의 plan/graph/catalog/weights/matchup/captureRule 재사용.
            // Waves: 최소 9개(각 주기용) 또는 단일 WaveDef를 9회 반복해 채운다.
            throw new System.NotImplementedException("fill from CampaignWaveRuleTests fixtures");
        }

        // 지정 일자의 유효 캠페인(hero 시드, 여관 Decor>0, gold 자원 존재).
        public static CampaignState CampaignAtDay(int day, int gold = 1000, int karmaBank = 0)
        {
            throw new System.NotImplementedException("fill from CampaignWaveRuleTests fixtures");
        }
    }
}
```

`Assets/Tests/Editor/CampaignDayRuleTests.cs`:
```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class CampaignDayRuleTests
    {
        private static IRandom Rng(int seed = 1) => /* 기존 테스트가 쓰는 결정론 IRandom 팩토리(예: new DeterministicRandom(seed)) */;

        [Test] public void AdvanceDay_IncrementsDayByOne()
        {
            var c = SimTimeFixtures.CampaignAtDay(1);
            var r = CampaignDayRule.AdvanceDay(c, SimTimeFixtures.DayContext(), Rng());
            Assert.AreEqual(2, r.Campaign.Run.Day);
        }

        [Test] public void AdvanceDay_MaintenanceDay_AppliesInnTick_NoWave()
        {
            var c = SimTimeFixtures.CampaignAtDay(1); // ->day2 정비
            var r = CampaignDayRule.AdvanceDay(c, SimTimeFixtures.DayContext(), Rng());
            Assert.AreEqual(DayPhase.Maintenance, r.Phase);
            Assert.IsFalse(r.WaveResolved);
            Assert.AreEqual(2, r.Campaign.Run.Day);
            // 여관 Decor가 하루치 Decay 됐는지(입력 Decor - 1) 확인
        }

        [Test] public void AdvanceDay_WaveDay_ResolvesWave()
        {
            var c = SimTimeFixtures.CampaignAtDay(9); // ->day10 웨이브
            var r = CampaignDayRule.AdvanceDay(c, SimTimeFixtures.DayContext(), Rng());
            Assert.AreEqual(DayPhase.Wave, r.Phase);
            Assert.IsTrue(r.WaveResolved);
            Assert.AreEqual(10, r.Campaign.Run.Day);
            // gold가 전투 약탈만큼 증가했는지 확인(r.Wave.GoldGained 등)
        }

        [Test] public void AdvanceDay_Day90To91_SignalsRegress_NoProcessing()
        {
            var c = SimTimeFixtures.CampaignAtDay(90);
            var r = CampaignDayRule.AdvanceDay(c, SimTimeFixtures.DayContext(), Rng());
            Assert.IsTrue(r.RegressPending);
            Assert.IsFalse(r.WaveResolved);
            Assert.AreEqual(90, r.Campaign.Run.Day); // 91 처리 안 함, 그대로
        }

        [Test] public void AdvanceDay_Deterministic_SameSeedSameResult()
        {
            var c = SimTimeFixtures.CampaignAtDay(9);
            var a = CampaignDayRule.AdvanceDay(c, SimTimeFixtures.DayContext(), Rng(42));
            var b = CampaignDayRule.AdvanceDay(c, SimTimeFixtures.DayContext(), Rng(42));
            Assert.AreEqual(a.Campaign.Run.Resources["gold"], b.Campaign.Run.Resources["gold"]);
            Assert.AreEqual(a.Campaign.Run.Captives.Count, b.Campaign.Run.Captives.Count);
        }

        [Test] public void AdvanceDay_DoesNotMutateInputCampaign()
        {
            var c = SimTimeFixtures.CampaignAtDay(9);
            int day = c.Run.Day;
            CampaignDayRule.AdvanceDay(c, SimTimeFixtures.DayContext(), Rng());
            Assert.AreEqual(day, c.Run.Day); // 원본 Day 불변
        }

        [Test] public void AdvanceDay_NullArgs_Throw()
        {
            var c = SimTimeFixtures.CampaignAtDay(1);
            var ctx = SimTimeFixtures.DayContext();
            Assert.Throws<System.ArgumentNullException>(() => CampaignDayRule.AdvanceDay(null, ctx, Rng()));
            Assert.Throws<System.ArgumentNullException>(() => CampaignDayRule.AdvanceDay(c, null, Rng()));
            Assert.Throws<System.ArgumentNullException>(() => CampaignDayRule.AdvanceDay(c, ctx, null));
        }
    }
}
```
(테스트의 `Rng()` 팩토리와 픽스처 세부는 기존 `CampaignWaveRuleTests`/`CombatResolverTests`에서 쓰는 결정론 IRandom과 빌더를 그대로 사용한다.)

- [ ] **Step 2: Run test to verify it fails**

Expected: FAIL — `DayContext`/`AdvanceResult`/`CampaignDayRule` 타입 없음(및 픽스처 `NotImplementedException`).

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/VNEngine/Core/Sim/Time/AdvanceResult.cs`:
```csharp
namespace VNEngine
{
    // AdvanceDay 결과. RegressPending일 때 Campaign은 처리 전(day 90) 캠페인이며 caller가 StartNewLoop.
    public readonly struct AdvanceResult
    {
        public CampaignState Campaign { get; }
        public DayPhase Phase { get; }
        public bool RegressPending { get; }
        public bool WaveResolved { get; }
        public WaveOutcome Wave { get; } // WaveResolved==true 일 때만 유효

        public AdvanceResult(CampaignState campaign, DayPhase phase, bool regressPending,
                             bool waveResolved, WaveOutcome wave)
        {
            Campaign = campaign;
            Phase = phase;
            RegressPending = regressPending;
            WaveResolved = waveResolved;
            Wave = wave;
        }
    }
}
```

`Assets/Scripts/VNEngine/Core/Sim/Time/DayContext.cs`:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    // AdvanceDay가 웨이브날 ResolveWave를 호출하기 위한 회차 설정 번들.
    // 불변·런타임 config(세이브 안 함). Waves[0]=1차 웨이브(10일) … Waves[8]=9차(90일).
    public sealed class DayContext
    {
        public PlacementPlan Plan { get; }
        public IReadOnlyList<WaveDef> Waves { get; }
        public RoomGraph BaseGraph { get; }
        public IReadOnlyList<MonsterDef> MonsterCatalog { get; }
        public StatCombatWeights StatWeights { get; }
        public ThreatWeights ThreatWeights { get; }
        public IReadOnlyList<UnitClassDef> ClassCatalog { get; }
        public ClassMatchup Matchup { get; }
        public CaptureRule CaptureRule { get; }
        public string GoldResourceId { get; }

        public DayContext(PlacementPlan plan, IReadOnlyList<WaveDef> waves, RoomGraph baseGraph,
            IReadOnlyList<MonsterDef> monsterCatalog, StatCombatWeights statWeights, ThreatWeights threatWeights,
            IReadOnlyList<UnitClassDef> classCatalog, ClassMatchup matchup, CaptureRule captureRule, string goldResourceId)
        {
            Plan = plan ?? throw new System.ArgumentNullException(nameof(plan));
            if (waves == null) throw new System.ArgumentNullException(nameof(waves));
            Waves = new List<WaveDef>(waves);
            BaseGraph = baseGraph ?? throw new System.ArgumentNullException(nameof(baseGraph));
            if (monsterCatalog == null) throw new System.ArgumentNullException(nameof(monsterCatalog));
            MonsterCatalog = new List<MonsterDef>(monsterCatalog);
            StatWeights = statWeights ?? throw new System.ArgumentNullException(nameof(statWeights));
            ThreatWeights = threatWeights ?? throw new System.ArgumentNullException(nameof(threatWeights));
            if (classCatalog == null) throw new System.ArgumentNullException(nameof(classCatalog));
            ClassCatalog = new List<UnitClassDef>(classCatalog);
            Matchup = matchup ?? throw new System.ArgumentNullException(nameof(matchup));
            CaptureRule = captureRule ?? throw new System.ArgumentNullException(nameof(captureRule));
            GoldResourceId = goldResourceId ?? throw new System.ArgumentNullException(nameof(goldResourceId));
        }

        // 해당 웨이브날(10·20…90)의 WaveDef. 인덱스 = day/WaveInterval - 1.
        public WaveDef WaveForDay(int day)
        {
            int idx = day / TimeQuery.WaveInterval - 1;
            if (idx < 0 || idx >= Waves.Count)
                throw new VnRuntimeException($"no wave defined for day {day}");
            return Waves[idx];
        }
    }
}
```
**주의:** 위 타입들(`PlacementPlan`/`WaveDef`/`RoomGraph`/`MonsterDef`/`StatCombatWeights`/`ThreatWeights`/`UnitClassDef`/`ClassMatchup`/`CaptureRule`)의 정확한 이름은 `CampaignWaveRule.ResolveWave` 시그니처와 일치한다(Explore 확인). 값 타입(struct)이 섞여 있으면 null-check을 그 타입에 맞게 조정(struct는 null 불가 → default 검사 생략 또는 제거). 구현 시 `CampaignWaveRule.cs`의 파라미터 타입을 그대로 확인해 맞출 것.

`Assets/Scripts/VNEngine/Core/Sim/Time/CampaignDayRule.cs`:
```csharp
namespace VNEngine
{
    // 하루 전이(순수). 속도/스킵을 전혀 모른다.
    // 웨이브날 = CampaignWaveRule.ResolveWave, 정비날 = MaintenanceRule.ApplyInnTick.
    // Day>90 이면 처리 없이 회귀신호만 반환(실제 Regress는 caller가 LoopEngine.StartNewLoop).
    public static class CampaignDayRule
    {
        public static AdvanceResult AdvanceDay(CampaignState campaign, DayContext ctx, IRandom rng)
        {
            if (campaign == null) throw new System.ArgumentNullException(nameof(campaign));
            if (ctx == null) throw new System.ArgumentNullException(nameof(ctx));
            if (rng == null) throw new System.ArgumentNullException(nameof(rng));

            int newDay = campaign.Run.Day + 1;
            if (newDay > TimeQuery.MaxDay)
                return new AdvanceResult(campaign, TimeQuery.GetPhase(campaign.Run.Day),
                                         regressPending: true, waveResolved: false, default);

            var run = campaign.Run;
            var bumped = new RunState(newDay, run.Resources, run.Captives, run.PullsThisLoop);
            var atNewDay = new CampaignState(campaign.Meta, bumped);

            if (TimeQuery.GetPhase(newDay) == DayPhase.Wave)
            {
                var outcome = CampaignWaveRule.ResolveWave(
                    atNewDay, ctx.Plan, ctx.WaveForDay(newDay), ctx.BaseGraph, ctx.MonsterCatalog,
                    atNewDay.Meta.Heroes, ctx.StatWeights, ctx.ThreatWeights, ctx.ClassCatalog, ctx.Matchup,
                    ctx.CaptureRule, atNewDay.Meta.DungeonLevel, ctx.GoldResourceId, rng);
                return new AdvanceResult(outcome.Campaign, DayPhase.Wave, false, true, outcome);
            }

            var tick = MaintenanceRule.ApplyInnTick(atNewDay, ctx.GoldResourceId);
            return new AdvanceResult(tick.Campaign, DayPhase.Maintenance, false, false, default);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Expected: PASS. 전체 EditMode 스위트로 회귀 없음 확인.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/VNEngine/Core/Sim/Time/DayContext.cs Assets/Scripts/VNEngine/Core/Sim/Time/AdvanceResult.cs Assets/Scripts/VNEngine/Core/Sim/Time/CampaignDayRule.cs Assets/Tests/Editor/SimTimeFixtures.cs Assets/Tests/Editor/CampaignDayRuleTests.cs
git commit -m "feat(sim): CampaignDayRule.AdvanceDay day-transition core + DayContext (time-structure task3)"
```

---

### Task 4: TimeController (Step / SkipToNextWave / SkipToDay) + 회귀 연동

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Time/TimeController.cs`
- Test: `Assets/Tests/Editor/TimeControllerTests.cs`

**Interfaces:**
- Consumes: `CampaignDayRule.AdvanceDay`(Task 3), `TimeQuery`(Task 1), `SimTimeFixtures`(Task 3), `LoopEngine.StartNewLoop`(기존), `IRandom`, `VnRuntimeException`.
- Produces:
  - `readonly struct SkipResult { CampaignState Campaign; int DaysAdvanced; }`
  - `static class TimeController`:
    - `AdvanceResult Step(CampaignState c, DayContext ctx, IRandom rng)` — `AdvanceDay` 1회 위임.
    - `SkipResult SkipToNextWave(CampaignState c, DayContext ctx, IRandom rng)` — 다음 날이 정비인 동안 `AdvanceDay` 반복, 다음 날이 웨이브(또는 day90 도달)면 정지 → **웨이브 전날에서 멈춤**(웨이브 미해소).
    - `SkipResult SkipToDay(CampaignState c, DayContext ctx, IRandom rng, int targetDay)` — targetDay까지 정비일만 전진, 사이 웨이브가 있으면 그 전날에서 정지. `targetDay`∉[1,90]이면 `VnRuntimeException`. targetDay ≤ 현재 Day면 no-op.

- [ ] **Step 1: Write the failing test**

`Assets/Tests/Editor/TimeControllerTests.cs`:
```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class TimeControllerTests
    {
        private static IRandom Rng(int seed = 1) => /* 기존 결정론 IRandom 팩토리 */;

        [Test] public void SkipToNextWave_StopsAtWaveEve()
        {
            var c = SimTimeFixtures.CampaignAtDay(3);
            var r = TimeController.SkipToNextWave(c, SimTimeFixtures.DayContext(), Rng());
            Assert.AreEqual(9, r.Campaign.Run.Day);   // 웨이브(10) 전날
            Assert.AreEqual(6, r.DaysAdvanced);        // 4,5,6,7,8,9
        }

        [Test] public void SkipToNextWave_DoesNotResolveWave()
        {
            var c = SimTimeFixtures.CampaignAtDay(3);
            var before = c.Run.Captives.Count;
            var r = TimeController.SkipToNextWave(c, SimTimeFixtures.DayContext(), Rng());
            Assert.AreEqual(9, r.Campaign.Run.Day);            // 10(웨이브)까지 안 감
            Assert.AreEqual(before, r.Campaign.Run.Captives.Count); // 전투 미발생
        }

        [Test] public void SkipToNextWave_Settlement_EqualsStepByStep()
        {
            var ctx = SimTimeFixtures.DayContext();
            var start = SimTimeFixtures.CampaignAtDay(3);

            var skip = TimeController.SkipToNextWave(start, ctx, Rng(7)).Campaign;

            var step = start;
            for (int i = 0; i < 6; i++) step = TimeController.Step(step, ctx, Rng(7)).Campaign;

            Assert.AreEqual(step.Run.Day, skip.Run.Day);
            Assert.AreEqual(step.Run.Resources["gold"], skip.Run.Resources["gold"]); // 정산 동일
            Assert.AreEqual(step.Meta.Inn.Decor, skip.Meta.Inn.Decor);
            Assert.AreEqual(step.Meta.KarmaBank, skip.Meta.KarmaBank);
        }

        [Test] public void SkipToNextWave_AtWaveEve_NoOp()
        {
            var c = SimTimeFixtures.CampaignAtDay(9);
            var r = TimeController.SkipToNextWave(c, SimTimeFixtures.DayContext(), Rng());
            Assert.AreEqual(0, r.DaysAdvanced);
            Assert.AreEqual(9, r.Campaign.Run.Day);
        }

        [Test] public void SkipToDay_ReachesTarget_WhenNoWaveBetween()
        {
            var c = SimTimeFixtures.CampaignAtDay(1);
            var r = TimeController.SkipToDay(c, SimTimeFixtures.DayContext(), Rng(), 5);
            Assert.AreEqual(5, r.Campaign.Run.Day);
            Assert.AreEqual(4, r.DaysAdvanced);
        }

        [Test] public void SkipToDay_StopsAtWaveEve_WhenWaveBeforeTarget()
        {
            var c = SimTimeFixtures.CampaignAtDay(5);
            var r = TimeController.SkipToDay(c, SimTimeFixtures.DayContext(), Rng(), 15);
            Assert.AreEqual(9, r.Campaign.Run.Day);   // 웨이브(10)를 넘지 않음
        }

        [Test] public void SkipToDay_InvalidTarget_Throws()
        {
            var c = SimTimeFixtures.CampaignAtDay(1);
            var ctx = SimTimeFixtures.DayContext();
            Assert.Throws<VnRuntimeException>(() => TimeController.SkipToDay(c, ctx, Rng(), 0));
            Assert.Throws<VnRuntimeException>(() => TimeController.SkipToDay(c, ctx, Rng(), 91));
        }

        [Test] public void SkipToDay_TargetNotFuture_NoOp()
        {
            var c = SimTimeFixtures.CampaignAtDay(5);
            var r = TimeController.SkipToDay(c, SimTimeFixtures.DayContext(), Rng(), 5);
            Assert.AreEqual(0, r.DaysAdvanced);
            Assert.AreEqual(5, r.Campaign.Run.Day);
        }

        [Test] public void Step_AtDay90_SignalsRegress_ThenStartNewLoopResetsDay()
        {
            var c = SimTimeFixtures.CampaignAtDay(90, karmaBank: 33);
            int loop = c.Meta.LoopCount;
            var r = TimeController.Step(c, SimTimeFixtures.DayContext(), Rng());
            Assert.IsTrue(r.RegressPending);

            var engine = /* 기존 LoopEngineTests가 쓰는 LoopEngine 팩토리 */;
            var next = engine.StartNewLoop(r.Campaign);
            Assert.AreEqual(1, next.Run.Day);                 // 런 리셋
            Assert.AreEqual(loop + 1, next.Meta.LoopCount);   // 메타 유지+증가
            Assert.AreEqual(33, next.Meta.KarmaBank);         // 메타 유지
        }
    }
}
```
(`Rng()`·`LoopEngine` 팩토리는 기존 `LoopEngineTests`/`CampaignWaveRuleTests` 패턴 재사용.)

- [ ] **Step 2: Run test to verify it fails**

Expected: FAIL — `TimeController`/`SkipResult` 타입 없음.

- [ ] **Step 3: Write minimal implementation**

`Assets/Scripts/VNEngine/Core/Sim/Time/TimeController.cs`:
```csharp
namespace VNEngine
{
    // 진행 컨트롤러(모듈): AdvanceDay를 어떤 리듬으로/언제까지 호출하느냐. 코어 무변경으로 확장.
    // ★스킵은 정비 구간만 전진하고 웨이브 전날에서 멈춘다(웨이브는 Step으로만 해소) — 스킵=정산방식.
    public static class TimeController
    {
        public static AdvanceResult Step(CampaignState c, DayContext ctx, IRandom rng)
            => CampaignDayRule.AdvanceDay(c, ctx, rng);

        public static SkipResult SkipToNextWave(CampaignState c, DayContext ctx, IRandom rng)
        {
            if (c == null) throw new System.ArgumentNullException(nameof(c));
            if (ctx == null) throw new System.ArgumentNullException(nameof(ctx));
            if (rng == null) throw new System.ArgumentNullException(nameof(rng));

            int advanced = 0;
            while (c.Run.Day < TimeQuery.MaxDay
                   && TimeQuery.GetPhase(c.Run.Day + 1) == DayPhase.Maintenance)
            {
                c = CampaignDayRule.AdvanceDay(c, ctx, rng).Campaign;
                advanced++;
            }
            return new SkipResult(c, advanced);
        }

        public static SkipResult SkipToDay(CampaignState c, DayContext ctx, IRandom rng, int targetDay)
        {
            if (c == null) throw new System.ArgumentNullException(nameof(c));
            if (ctx == null) throw new System.ArgumentNullException(nameof(ctx));
            if (rng == null) throw new System.ArgumentNullException(nameof(rng));
            if (targetDay < 1 || targetDay > TimeQuery.MaxDay)
                throw new VnRuntimeException($"targetDay out of range [1,{TimeQuery.MaxDay}]: {targetDay}");

            int advanced = 0;
            while (c.Run.Day < targetDay
                   && c.Run.Day < TimeQuery.MaxDay
                   && TimeQuery.GetPhase(c.Run.Day + 1) == DayPhase.Maintenance)
            {
                c = CampaignDayRule.AdvanceDay(c, ctx, rng).Campaign;
                advanced++;
            }
            return new SkipResult(c, advanced);
        }
    }

    public readonly struct SkipResult
    {
        public CampaignState Campaign { get; }
        public int DaysAdvanced { get; }
        public SkipResult(CampaignState campaign, int daysAdvanced)
        {
            Campaign = campaign;
            DaysAdvanced = daysAdvanced;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Expected: PASS. 전체 EditMode 스위트로 회귀 없음 확인.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/VNEngine/Core/Sim/Time/TimeController.cs Assets/Tests/Editor/TimeControllerTests.cs
git commit -m "feat(sim): TimeController Step/Skip modes + regress wiring (time-structure task4)"
```

---

### Task 5: 대시보드 연동 (진행 버튼 + Day/페이즈/다음웨이브 표시 + 회귀)

**Files:**
- Modify: `Assets/Scripts/VNEngine/Unity/Sim/SimController.cs`

**Interfaces:**
- Consumes: `TimeController.Step/SkipToNextWave`(Task 4), `TimeQuery.GetPhase/DaysUntilNextWave`(Task 1), `CampaignDayRule`/`AdvanceResult`(Task 3), `LoopEngine.StartNewLoop`(기존), 기존 `SimController`의 `SpawnButton`/`Refresh`/`_campaign` 및 `OnWave`가 이미 만드는 웨이브 픽스처.

이 태스크는 검증용 대시보드(가볍게)다. **`OnWave`가 이미 구성하는 plan/wave/graph/catalog/weights/matchup/captureRule 픽스처를 재사용해 `DayContext`를 만든다.** 필요하면 그 픽스처들을 필드로 추출한다. `SimSlice.unity`를 Play해서 육안 검증.

- [ ] **Step 1: DayContext 필드 구성**

`SimController`에 `private DayContext _dayCtx;`와 `private IRandom _rng;`(기존에 있으면 재사용)를 추가하고, `OnWave`가 쓰던 것과 동일한 픽스처로 `_dayCtx`를 초기화(예: `Start()`/기존 초기화 지점). Waves 리스트는 최소 9개(각 주기용) — 기존 단일 `WaveDef`를 9회 반복해 채워도 검증엔 충분(주석으로 명시).

- [ ] **Step 2: 진행 버튼 3종 추가**

`BuildButtons()`에 기존 패턴대로 추가:
```csharp
SpawnButton("하루", OnStepDay);
SpawnButton("다음 웨이브까지", OnSkipToNextWave);
SpawnButton("빠른재생: 꺼짐", OnToggleFastForward); // 라벨은 토글 상태 따라 갱신
```
핸들러:
```csharp
private void OnStepDay()
{
    var r = TimeController.Step(_campaign, _dayCtx, _rng);
    ApplyAdvance(r);
    Refresh();
}

private void OnSkipToNextWave()
{
    var r = TimeController.SkipToNextWave(_campaign, _dayCtx, _rng);
    _campaign = r.Campaign;
    Refresh();
}

// AdvanceResult 처리: 회귀 대기면 StartNewLoop, 아니면 캠페인 반영.
private void ApplyAdvance(AdvanceResult r)
{
    if (r.RegressPending)
    {
        _campaign = _loopEngine.StartNewLoop(r.Campaign); // 기존 LoopEngine 필드명 확인
        return;
    }
    _campaign = r.Campaign;
}
```
(기존 `OnNextDay`의 수동 day+1/여관 Decay 로직은 `OnStepDay`(=TimeController.Step)로 대체. 기존 `OnNextDay` 버튼이 있으면 제거하거나 `OnStepDay`로 통합. 기존 수동 "웨이브 실행"(`OnWave`) 버튼은 애드혹 검증용으로 유지 가능.)

- [ ] **Step 3: 빠른재생(FastForward) — 표시 계층 틱**

코어를 모르는 Unity 틱으로 구현. `bool _fastForward; float _ffAccum; const float FfInterval = 0.15f;` 추가하고 `Update()`(없으면 신설)에서:
```csharp
private void Update()
{
    if (!_fastForward) return;
    _ffAccum += Time.deltaTime;
    if (_ffAccum < FfInterval) return;
    _ffAccum = 0f;
    var r = TimeController.Step(_campaign, _dayCtx, _rng);
    ApplyAdvance(r);
    // 웨이브 해소·회귀 시 자동 정지(육안 확인 가능하게)
    if (r.WaveResolved || r.RegressPending) _fastForward = false;
    Refresh();
}
```
`OnToggleFastForward`는 `_fastForward` 토글 + 버튼 라벨 갱신(`켜짐/꺼짐`). **속도/틱/Time.deltaTime은 오직 이 Unity 계층에만** — 코어(`Core/Sim/Time`)엔 절대 넣지 않는다.

- [ ] **Step 4: Day/페이즈/다음웨이브 표시**

`Refresh()`의 `StringBuilder`에 한 줄 추가(기존 `일차 {run.Day}` 옆):
```csharp
var day = _campaign.Run.Day;
string phase = TimeQuery.GetPhase(day) == DayPhase.Wave ? "웨이브" : "정비";
int untilWave = TimeQuery.DaysUntilNextWave(day);
sb.AppendLine($"페이즈 {phase} · 다음 웨이브까지 {untilWave}일 · 세이브일 {(TimeQuery.IsSaveDay(day) ? "O" : "-")}");
```

- [ ] **Step 5: 컴파일 + 육안 검증**

Unity 콘솔에서 컴파일 에러 없음 확인(`read_console`). `SimSlice.unity` Play → "하루" 반복 시 Day 증가·정비일 여관 수급·10일마다 웨이브 실행, "다음 웨이브까지"가 9일에서 멈춤, "빠른재생"이 웨이브/회귀에서 자동정지, 90일 후 회귀로 Day=1·LoopCount 증가 확인.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/VNEngine/Unity/Sim/SimController.cs
git commit -m "feat(sim): dashboard time controls - step/skip/fast-forward + day/phase display (time-structure task5)"
```

---

### Task 6: 문서 (07 시간구조 절 + 06 상태 노트)

**Files:**
- Modify: `docs/engine/07-defense-combat.md` (시간구조 절 추가)
- Modify: `docs/engine/06-loop-and-state.md` (착수/구현 상태 노트 — 07-B/C/D 선례 형식)

**Interfaces:** 문서만, 코드/테스트 없음.

- [ ] **Step 1: `07-defense-combat.md`에 "시간구조(90일 루프)" 절 추가**

07-D 콜아웃 스타일로 "구현(시간구조, 2026-07-09)" 절 작성:
- 90일=9주기×10일, 웨이브날=10·20…90, 정비일=여관 수급(게이트-전-Decay)+Decay.
- 코어 = `CampaignDayRule.AdvanceDay(campaign, ctx, rng)` 순수함수 하나(속도/스킵 모름). 웨이브날→`ResolveWave`, 정비날→`MaintenanceRule.ApplyInnTick`, Day>90→회귀신호.
- 순수 질의 `TimeQuery`(GetPhase/IsWaveDay/IsSaveDay/DaysUntilNextWave, 상수 MaxDay=90 등).
- 모듈 `TimeController`(Step/SkipToNextWave/SkipToDay) — 스킵=정산방식, 웨이브 전날 정지(웨이브는 Step으로만 해소).
- FastForward는 코어 아님(Unity 표시 계층).
- **미룬/훅**: 정비일 모험가 확률난입, 코어붕괴 조기회귀 조건(현재 Day>90만) — 후속.
- **결정 기록**: Day는 신규 TimeState 없이 기존 `RunState.Day` 재사용(회차 리셋), `MaintenanceRule`과 `ResolveWave` 여관 스텝 통합은 후속 미룸.

- [ ] **Step 2: `06-loop-and-state.md` 상태 노트 추가**

"착수 상태(2026-07-09): 시간구조 — Day는 Run 소속(회차 리셋), Meta 무변경. `Core/Sim/Time` 신설(TimeQuery/MaintenanceRule/CampaignDayRule/DayContext/AdvanceResult/TimeController)." 를 기존 07-B/C/D 노트 형식에 맞춰 추가. §3 Regress 스펙 근처에 "현행 회귀=`LoopEngine.StartNewLoop`(AdvanceDay가 Day>90 신호 → caller가 호출)" 한 줄 명시.

- [ ] **Step 3: Commit**

```bash
git add docs/engine/07-defense-combat.md docs/engine/06-loop-and-state.md
git commit -m "docs(sim): reflect 90-day time structure (07 §time, 06 status note)"
```

---

## Verification (brief의 EditMode 체크리스트 매핑)

- AdvanceDay가 Day 정확히 +1, 90 넘으면 회귀신호 (Task 3)
- 웨이브날(10·20…)에 ResolveWave 호출, 정비날엔 안 함 (Task 3)
- 정비날 여관수급/내구도 처리, 게이트가 Decay 前 (Task 2, Task 3)
- SkipToNextWave가 웨이브 전날 정지(웨이브날 안 넘김) (Task 4)
- 스킵 정산 = 건너뛴 날 자원변화가 하루씩 진행과 동일 결과 (Task 4)
- SkipToDay(n)이 n 전 웨이브 전날에서 정지 (Task 4)
- 회귀 후 Day=1, 런 리셋/메타 유지 (Task 4 — StartNewLoop 연동)
- AdvanceDay 순수·불변·결정론(같은 시드 같은 결과) (Task 3)
- 코어(AdvanceDay/TimeController)에 속도/스킵 개념 없음 = 속도는 Unity 계층에만 (Global Constraints, Task 5)
- 대시보드 Day/페이즈/다음웨이브 표시 + 진행 버튼 (Task 5)

## Final whole-branch review

Task 6 이후: 최상위 모델로 whole-branch 코드리뷰(`git merge-base main HEAD .. HEAD`), Minor 이월 목록 트리아지, 그다음 `superpowers:finishing-a-development-branch` → `main`에 `--no-ff` 머지. 무관 워킹트리(docs/engine 기타 미커밋 파일 등) 미커밋.
