# 07-A1 주인공 8스탯 시스템 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 8스탯을 데이터 테이블로 정의(하드코딩 금지)하고, 주인공 스탯을 불변 `Dictionary<StatId,int>` 상태로 메타에 귀속시키며(회차 넘어 유지), 인과율→스탯강화를 구간지수 비용의 순수함수로 구현하고, 주요 스탯을 VN 변수로 읽기전용 투영(대상 추가만)한다. **전투/데미지 공식은 비스코프(07-A2).** 스탯을 "값"으로만 다룬다.

**Architecture:** 새 폴더 `Core/Sim/Stats/**` 에 순수 타입(`StatId`, `StatDef`, `StatCatalog`, `HeroStats`, `StatCostCurve`, `StatUpgrade`)을 두고, 06의 `MetaState`·`CampaignSaveData`·`CampaignSave`·`LoopEngine`·`MetaProjection` 을 스탯 수용하도록 확장한다. Unity 레이어·씬·SimController 변경 없음(라이브 시딩·karma 배선은 07-C/A2로 보류 — MetaProjection 배선을 06에서 미룬 것과 동일 패턴).

**Tech Stack:** C# (Unity 2022.3, .NET Standard 2.1), NUnit EditMode, UnityMCP(refresh/console/run_tests), JsonUtility.

## Global Constraints

- `Core/**` (`Assets/Scripts/VNEngine/Core/**`) 는 `UnityEngine`·`System.IO` 절대 미참조 (순수 C#). Unity 어댑터만 IO 허용. **이 슬라이스는 Core만 건드림.**
- 모든 상태 전이·변경은 **불변**: 새 인스턴스 반환, 입력 변형 금지. 컬렉션은 방어적 복사(생성자에서 새 Dictionary).
- 런타임 오류는 기존 `VnRuntimeException` 재사용 (새 예외 타입 금지). 인자 검증은 `ArgumentNullException`/`ArgumentException` 사용(06 관례).
- 직렬화 모델은 `JsonUtility` 호환: **딕셔너리 금지, 리스트+원시 타입만**, `[System.Serializable]`. `StatId` 는 세이브 시 `Value(string)` 로 평면화, 로드 시 재래핑.
- **데이터 주도(하드코딩 금지)**: 스탯 목록·수치·비용곡선을 `public int STR;` 같은 필드나 코드 분기로 박지 말 것. `StatDef`/`StatCostCurve` 데이터를 순회/주입. 확장판에서 데이터만 바꿔 스탯·곡선 교체 가능해야 함.
- `StatId` = `string` 을 감싼 `readonly struct`(값 동등성). `IEquatable<StatId>` + `==`/`!=`/`GetHashCode`/`ToString`. **enum 금지, bare string 금지** — 이 결정은 확정(사용자 승인).
- 알려진 스탯 id 는 `StatIds.STR` 같은 상수로 한 곳에 모으되 **스탯 목록의 소스가 아님**(소스=`StatCatalog` 데이터). 상수는 매직스트링 방지 편의.
- 비용곡선 값은 **초기 추정치(튜닝대상)**: `<100→1, <250→2, <450→3, <650→5, <800→9, <950→16, ≥950→28`. 반드시 `StatCostCurve` 데이터로 구현(하드코딩 분기 금지). 07 문서 §13.3 와 일치.
- Core 클래스 네임스페이스 = `VNEngine`. 테스트: `Assets/Tests/Editor`, ns `VNEngine.Tests`, NUnit EditMode. GameState 생성은 `new GameState(new SeededRandom(1))`.
- 새/수정 `.cs` 후: UnityMCP `refresh_unity` scope:`all` → `read_console`(에러0) → `run_tests` assembly `VNEngine.Tests` (scope:`scripts` 는 false-green 금지).
- 투영은 커널→VN **단방향·읽기전용**. 변수명 주입(테마 중립).
- **비스코프(건들지 말 것):** 전투/데미지 공식, karma 수급/저금(07-C), CreateInitialCampaign 라이브 시딩, MetaProjection 실제 프로덕션 호출 배선, Unity 레이어(SimController/SO/씬), 무관 워킹트리 변경(NotoSansKR asset·기타 미추적 파일). **예외: docs Task 7 은 미추적 `07-defense-combat (2).md` 를 `07-defense-combat.md` 로 채택(사용자 승인).**

---

### Task 1: 스탯 기반 타입 — StatId · StatIds · StatDef · StatCatalog (데이터)

8스탯을 데이터로 정의하는 기반. `StatId`(값타입), 참조 편의 상수, `StatDef`(정의), 1편 기본 테이블 `StatCatalog.Default()`.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Stats/StatId.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Stats/StatIds.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Stats/StatDef.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Stats/StatCatalog.cs`
- Test: `Assets/Tests/Editor/StatDefTests.cs` (create)

**Interfaces:**
- Produces:
  - `readonly struct StatId : IEquatable<StatId> { string Value; }` + `==`/`!=`/`GetHashCode`/`ToString`.
  - `static class StatIds { static readonly StatId STR,INT,DEX,AGI,HP,MP,DEF,LUK; }`
  - `sealed class StatDef { StatId Id; string DisplayName; int StartValue; int Cap; }`
  - `static class StatCatalog { const int DefaultCap=999; IReadOnlyList<StatDef> Default(); }`

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/Editor/StatDefTests.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class StatDefTests
    {
        [Test]
        public void StatIdValueEqualityAndHashing()
        {
            Assert.AreEqual(new StatId("STR"), new StatId("STR"));
            Assert.IsTrue(new StatId("STR") == StatIds.STR);
            Assert.IsTrue(new StatId("STR") != new StatId("INT"));
            Assert.AreEqual(new StatId("STR").GetHashCode(), new StatId("STR").GetHashCode());
        }

        [Test]
        public void StatIdWorksAsDictionaryKey()
        {
            var d = new Dictionary<StatId, int> { { new StatId("STR"), 7 } };
            Assert.IsTrue(d.ContainsKey(StatIds.STR));
            Assert.AreEqual(7, d[new StatId("STR")]);
        }

        [Test]
        public void StatDefHoldsData()
        {
            var def = new StatDef(StatIds.HP, "HP", 50, 999);
            Assert.AreEqual(StatIds.HP, def.Id);
            Assert.AreEqual("HP", def.DisplayName);
            Assert.AreEqual(50, def.StartValue);
            Assert.AreEqual(999, def.Cap);
        }

        [Test]
        public void DefaultCatalogHasEightStatsWithCap999()
        {
            var defs = StatCatalog.Default();
            Assert.AreEqual(8, defs.Count);
            Assert.IsTrue(defs.All(d => d.Cap == 999));
            var ids = defs.Select(d => d.Id).ToList();
            foreach (var id in new[] { StatIds.STR, StatIds.INT, StatIds.DEX, StatIds.AGI, StatIds.HP, StatIds.MP, StatIds.DEF, StatIds.LUK })
                Assert.Contains(id, ids);
        }

        [Test]
        public void DefaultCatalogStartValuesMatchData()
        {
            var by = StatCatalog.Default().ToDictionary(d => d.Id, d => d.StartValue);
            Assert.AreEqual(5, by[StatIds.STR]);
            Assert.AreEqual(5, by[StatIds.INT]);
            Assert.AreEqual(5, by[StatIds.DEX]);
            Assert.AreEqual(5, by[StatIds.AGI]);
            Assert.AreEqual(50, by[StatIds.HP]);
            Assert.AreEqual(30, by[StatIds.MP]);
            Assert.AreEqual(5, by[StatIds.DEF]);
            Assert.AreEqual(5, by[StatIds.LUK]);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`.
Expected: 컴파일 에러 — `StatId`/`StatIds`/`StatDef`/`StatCatalog` 미정의 (RED).

- [ ] **Step 3: StatId 구현**

`Assets/Scripts/VNEngine/Core/Sim/Stats/StatId.cs`:
```csharp
using System;

namespace VNEngine
{
    // 스탯 식별자. string 을 감싼 값 타입 — 새 스탯은 StatDef 데이터로만 추가(enum 편집 불필요),
    // 동시에 타입 안전(자원 id 등 다른 string 과 컴파일타임 구분). Dictionary 키로 값 동등성 보장.
    public readonly struct StatId : IEquatable<StatId>
    {
        public string Value { get; }

        public StatId(string value)
        {
            Value = value;
        }

        public bool Equals(StatId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is StatId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : Value.GetHashCode();
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(StatId left, StatId right) => left.Equals(right);
        public static bool operator !=(StatId left, StatId right) => !left.Equals(right);
    }
}
```

- [ ] **Step 4: StatIds 구현**

`Assets/Scripts/VNEngine/Core/Sim/Stats/StatIds.cs`:
```csharp
namespace VNEngine
{
    // 자주 참조하는 1편 스탯 id 상수. **스탯 목록의 소스가 아님** — 소스는 StatDef 데이터 테이블(StatCatalog).
    // 매직스트링("STR")을 코드 곳곳에 흩뿌리지 않기 위한 참조 편의일 뿐.
    public static class StatIds
    {
        public static readonly StatId STR = new StatId("STR");
        public static readonly StatId INT = new StatId("INT");
        public static readonly StatId DEX = new StatId("DEX");
        public static readonly StatId AGI = new StatId("AGI");
        public static readonly StatId HP = new StatId("HP");
        public static readonly StatId MP = new StatId("MP");
        public static readonly StatId DEF = new StatId("DEF");
        public static readonly StatId LUK = new StatId("LUK");
    }
}
```

- [ ] **Step 5: StatDef 구현**

`Assets/Scripts/VNEngine/Core/Sim/Stats/StatDef.cs`:
```csharp
namespace VNEngine
{
    // 스탯 정의(데이터). 전투 역할은 여기 없음 — 07-A2 공식이 Id 로 간접 참조. 순수 값 정의만.
    public sealed class StatDef
    {
        public StatId Id { get; }
        public string DisplayName { get; }
        public int StartValue { get; }
        public int Cap { get; }

        public StatDef(StatId id, string displayName, int startValue, int cap)
        {
            Id = id;
            DisplayName = displayName;
            StartValue = startValue;
            Cap = cap;
        }
    }
}
```

- [ ] **Step 6: StatCatalog 구현**

`Assets/Scripts/VNEngine/Core/Sim/Stats/StatCatalog.cs`:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    // 1편 기본 8스탯 데이터 테이블. **초기 추정 튜닝값** — 확장판/2편은 다른 테이블을 주입해 스탯을 늘린다.
    // Core 로직(HeroStats/StatUpgrade)은 이 목록을 하드코딩 참조하지 않고 주어진 StatDef 들을 순회할 뿐.
    // StartValue: STR/INT/DEX/AGI/DEF/LUK=5, HP=50, MP=30 (데이터). Cap=999.
    public static class StatCatalog
    {
        public const int DefaultCap = 999;

        public static IReadOnlyList<StatDef> Default() => new List<StatDef>
        {
            new StatDef(StatIds.STR, "STR", 5, DefaultCap),
            new StatDef(StatIds.INT, "INT", 5, DefaultCap),
            new StatDef(StatIds.DEX, "DEX", 5, DefaultCap),
            new StatDef(StatIds.AGI, "AGI", 5, DefaultCap),
            new StatDef(StatIds.HP, "HP", 50, DefaultCap),
            new StatDef(StatIds.MP, "MP", 30, DefaultCap),
            new StatDef(StatIds.DEF, "DEF", 5, DefaultCap),
            new StatDef(StatIds.LUK, "LUK", 5, DefaultCap),
        };
    }
}
```

- [ ] **Step 7: 통과 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`(에러0) → `run_tests` assembly:`VNEngine.Tests` test_names:`VNEngine.Tests.StatDefTests`.
Expected: StatDefTests 5/5 PASS. 전체 스위트 회귀 없음(186/186 이상).

- [ ] **Step 8: 커밋**

```bash
git add "Assets/Scripts/VNEngine/Core/Sim/Stats/StatId.cs" "Assets/Scripts/VNEngine/Core/Sim/Stats/StatIds.cs" "Assets/Scripts/VNEngine/Core/Sim/Stats/StatDef.cs" "Assets/Scripts/VNEngine/Core/Sim/Stats/StatCatalog.cs" "Assets/Tests/Editor/StatDefTests.cs"
git commit -m "feat(sim): 8-stat data foundation (StatId/StatDef/StatCatalog)"
```
> `.meta` 파일이 함께 생성되면 같이 add. `Assets/Fonts/**`·기타 미추적 파일은 절대 add 하지 말 것.

---

### Task 2: HeroStats — 불변 Dictionary 스탯 상태

주인공 스탯을 하드코딩 필드 없이 `Dictionary<StatId,int>` 로 보관하는 불변 상태. 변경은 새 인스턴스 반환, 생성자 방어적 복사.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Stats/HeroStats.cs`
- Test: `Assets/Tests/Editor/HeroStatsTests.cs` (create)

**Interfaces:**
- Consumes (Task 1): `StatId`, `StatDef`, `StatIds`, `StatCatalog`.
- Produces:
  - `sealed class HeroStats`
  - `static readonly HeroStats Empty`
  - `HeroStats(IReadOnlyDictionary<StatId,int> values)` — 방어적 복사.
  - `IReadOnlyDictionary<StatId,int> Values`
  - `bool Has(StatId)`, `bool TryGet(StatId, out int)`, `int Get(StatId)` (없으면 `VnRuntimeException`)
  - `HeroStats WithStat(StatId, int)` — 새 인스턴스.
  - `static HeroStats FromDefs(IEnumerable<StatDef>)` — StartValue 로 시딩.

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/Editor/HeroStatsTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class HeroStatsTests
    {
        [Test]
        public void ConstructorDefensivelyCopiesInput()
        {
            var src = new Dictionary<StatId, int> { { StatIds.STR, 10 } };
            var hs = new HeroStats(src);
            src[StatIds.STR] = 999;                 // 원본 수정
            src[StatIds.INT] = 7;                   // 키 추가
            Assert.AreEqual(10, hs.Get(StatIds.STR), "원본 수정이 새어들면 안 됨");
            Assert.IsFalse(hs.Has(StatIds.INT));
        }

        [Test]
        public void WithStatReturnsNewInstanceAndDoesNotMutateOriginal()
        {
            var hs = new HeroStats(new Dictionary<StatId, int> { { StatIds.STR, 10 } });
            var hs2 = hs.WithStat(StatIds.STR, 42);
            Assert.AreEqual(10, hs.Get(StatIds.STR), "원본 불변");
            Assert.AreEqual(42, hs2.Get(StatIds.STR));
            Assert.AreNotSame(hs, hs2);
        }

        [Test]
        public void WithStatCanAddPreviouslyAbsentStat()
        {
            var hs = HeroStats.Empty.WithStat(StatIds.LUK, 3);
            Assert.AreEqual(3, hs.Get(StatIds.LUK));
        }

        [Test]
        public void GetThrowsForUnknownStat()
        {
            Assert.Throws<VnRuntimeException>(() => HeroStats.Empty.Get(StatIds.STR));
        }

        [Test]
        public void TryGetReportsPresence()
        {
            var hs = new HeroStats(new Dictionary<StatId, int> { { StatIds.STR, 5 } });
            Assert.IsTrue(hs.TryGet(StatIds.STR, out var v));
            Assert.AreEqual(5, v);
            Assert.IsFalse(hs.TryGet(StatIds.INT, out _));
        }

        [Test]
        public void FromDefsSeedsStartValues()
        {
            var hs = HeroStats.FromDefs(StatCatalog.Default());
            Assert.AreEqual(5, hs.Get(StatIds.STR));
            Assert.AreEqual(50, hs.Get(StatIds.HP));
            Assert.AreEqual(30, hs.Get(StatIds.MP));
            Assert.AreEqual(8, hs.Values.Count);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`.
Expected: 컴파일 에러 — `HeroStats` 미정의 (RED).

- [ ] **Step 3: HeroStats 구현**

`Assets/Scripts/VNEngine/Core/Sim/Stats/HeroStats.cs`:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    // 주인공 스탯 상태. 하드코딩 필드 없이 Dictionary<StatId,int> 로 보관(데이터 주도).
    // 불변: 모든 변경은 새 HeroStats 반환. 생성자는 방어적 복사(입력 딕셔너리와 참조 분리).
    public sealed class HeroStats
    {
        private readonly Dictionary<StatId, int> _values;

        public static readonly HeroStats Empty = new HeroStats(new Dictionary<StatId, int>());

        public HeroStats(IReadOnlyDictionary<StatId, int> values)
        {
            if (values == null) throw new System.ArgumentNullException(nameof(values));
            _values = new Dictionary<StatId, int>(values.Count);
            foreach (var kv in values) _values[kv.Key] = kv.Value; // 방어적 복사
        }

        public IReadOnlyDictionary<StatId, int> Values => _values;

        public bool Has(StatId id) => _values.ContainsKey(id);

        public bool TryGet(StatId id, out int value) => _values.TryGetValue(id, out value);

        public int Get(StatId id)
        {
            if (!_values.TryGetValue(id, out var v))
                throw new VnRuntimeException($"Unknown stat: {id}");
            return v;
        }

        public HeroStats WithStat(StatId id, int value)
        {
            var copy = new Dictionary<StatId, int>(_values);
            copy[id] = value;
            return new HeroStats(copy);
        }

        // StatDef 목록의 StartValue 로 초기화(데이터 주도 시딩).
        public static HeroStats FromDefs(IEnumerable<StatDef> defs)
        {
            if (defs == null) throw new System.ArgumentNullException(nameof(defs));
            var dict = new Dictionary<StatId, int>();
            foreach (var d in defs) dict[d.Id] = d.StartValue;
            return new HeroStats(dict);
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`(에러0) → `run_tests` assembly:`VNEngine.Tests` test_names:`VNEngine.Tests.HeroStatsTests`.
Expected: HeroStatsTests 6/6 PASS. 전체 스위트 회귀 없음.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/Scripts/VNEngine/Core/Sim/Stats/HeroStats.cs" "Assets/Tests/Editor/HeroStatsTests.cs"
git commit -m "feat(sim): immutable HeroStats (Dictionary<StatId,int>, defensive copy)"
```

---

### Task 3: MetaState.Heroes 필드 + LoopEngine 캐리포워드

주인공 스탯을 메타에 귀속(회차 넘어 유지). `MetaState` 에 `Heroes` 추가(기존 호출부 back-compat 오버로드), `LoopEngine.StartNewLoop` 에서 Heroes 캐리포워드.

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Sim/MetaState.cs`
- Modify: `Assets/Scripts/VNEngine/Core/Sim/LoopEngine.cs`
- Test: `Assets/Tests/Editor/MetaStateHeroesTests.cs` (create)

**Interfaces:**
- Consumes (Task 2): `HeroStats`, `HeroStats.Empty`, `HeroStats.WithStat`.
- Produces:
  - `MetaState(int loopCount)` (기존, Heroes=Empty) + `MetaState(int loopCount, HeroStats heroes)` (heroes null→Empty). `HeroStats Heroes { get; }`.
  - `LoopEngine.StartNewLoop`: 새 MetaState 에 `campaign.Meta.Heroes` 를 그대로 전달(캐리포워드), Run 만 초기화.
- **불변식:** `CreateInitialCampaign` 은 `new MetaState(1)` 유지(Heroes=Empty, 라이브 시딩 보류). `ExecuteCommand` 는 Meta 통과(기존) — Heroes 자동 유지.

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/Editor/MetaStateHeroesTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class MetaStateHeroesTests
    {
        private static TurnEngine MakeTurnEngine() =>
            new TurnEngine(new List<ResourceDef>(), new List<CommandDef>());

        [Test]
        public void DefaultConstructorGivesEmptyHeroes()
        {
            var meta = new MetaState(1);
            Assert.IsNotNull(meta.Heroes);
            Assert.AreEqual(0, meta.Heroes.Values.Count);
        }

        [Test]
        public void ConstructorStoresHeroes()
        {
            var heroes = HeroStats.Empty.WithStat(StatIds.STR, 42);
            var meta = new MetaState(2, heroes);
            Assert.AreEqual(42, meta.Heroes.Get(StatIds.STR));
            Assert.AreEqual(2, meta.LoopCount);
        }

        [Test]
        public void NullHeroesCoercedToEmpty()
        {
            var meta = new MetaState(1, null);
            Assert.IsNotNull(meta.Heroes);
            Assert.AreEqual(0, meta.Heroes.Values.Count);
        }

        [Test]
        public void StartNewLoopCarriesHeroesForward()
        {
            var engine = new LoopEngine(MakeTurnEngine());
            var heroes = HeroStats.Empty.WithStat(StatIds.STR, 300);
            var campaign = new CampaignState(new MetaState(1, heroes), new RunState(3, new Dictionary<string, int>()));

            var next = engine.StartNewLoop(campaign);

            Assert.AreEqual(2, next.Meta.LoopCount, "회차 증가");
            Assert.AreEqual(300, next.Meta.Heroes.Get(StatIds.STR), "주인공 성장은 회차 넘어 유지(메타)");
        }
    }
}
```
> 주의: `TurnEngine` 생성자 시그니처는 실제 소스(`Assets/Scripts/VNEngine/Core/Sim/TurnEngine.cs`)를 열어 확인하고 맞출 것. 위 `MakeTurnEngine()` 은 빈 자원/커맨드로 초기화하는 형태를 가정 — 실제 시그니처가 다르면 그에 맞춰 최소 구성으로 생성(테스트 목적은 StartNewLoop 의 Heroes 캐리포워드 검증뿐).

- [ ] **Step 2: 실패 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`.
Expected: 컴파일 에러 — `MetaState.Heroes`·2인자 생성자 미정의 (RED). (기존 `new MetaState(int)` 호출부는 back-compat 로 유지되어야 함.)

- [ ] **Step 3: MetaState 확장**

`Assets/Scripts/VNEngine/Core/Sim/MetaState.cs` 전체:
```csharp
namespace VNEngine
{
    public sealed class MetaState
    {
        public int LoopCount { get; }
        public HeroStats Heroes { get; }

        public MetaState(int loopCount) : this(loopCount, HeroStats.Empty) { }

        public MetaState(int loopCount, HeroStats heroes)
        {
            LoopCount = loopCount;
            Heroes = heroes ?? HeroStats.Empty;
        }
    }
}
```

- [ ] **Step 4: LoopEngine.StartNewLoop 캐리포워드**

`Assets/Scripts/VNEngine/Core/Sim/LoopEngine.cs` 의 `StartNewLoop` 만 변경(나머지 유지):
```csharp
        // 회차 전이(최소): LoopCount+1, Run 은 새 초기 Run 으로 리셋.
        // 주인공 성장은 메타 — 회차를 넘어 유지(Heroes 캐리포워드). 계승·편지 등 '내용' 갱신은 이후 슬라이스(Regress).
        public CampaignState StartNewLoop(CampaignState campaign)
        {
            if (campaign == null) throw new System.ArgumentNullException(nameof(campaign));
            var newMeta = new MetaState(campaign.Meta.LoopCount + 1, campaign.Meta.Heroes);
            return new CampaignState(newMeta, _turnEngine.CreateInitialState());
        }
```

- [ ] **Step 5: 통과 확인 (+회귀 없음)**

UnityMCP: `refresh_unity` scope:`all` → `read_console`(에러0) → `run_tests` assembly:`VNEngine.Tests`.
Expected: MetaStateHeroesTests 4/4 PASS. 기존 LoopEngineTests·CampaignSaveTests·MetaProjectionTests 등 전건 PASS(back-compat 생성자 덕에 회귀 없음).

- [ ] **Step 6: 커밋**

```bash
git add "Assets/Scripts/VNEngine/Core/Sim/MetaState.cs" "Assets/Scripts/VNEngine/Core/Sim/LoopEngine.cs" "Assets/Tests/Editor/MetaStateHeroesTests.cs"
git commit -m "feat(sim): heroes belong to MetaState and carry forward across loops"
```

---

### Task 4: 세이브 직렬화 — CampaignSaveData.stats + CampaignSave 왕복

주인공 스탯을 04/06 평면 패턴으로 세이브. `StatId` 는 `Value(string)` 로 평면화. 추가 필드(additive)라 `CampaignSaveVersion` 유지(구세이브는 stats 없음→빈 Heroes, JsonUtility 호환).

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Sim/CampaignSaveData.cs`
- Modify: `Assets/Scripts/VNEngine/Core/Sim/CampaignSave.cs`
- Test: `Assets/Tests/Editor/CampaignSaveTests.cs` (extend — 기존 테스트 유지, 스탯 케이스 추가)

**Interfaces:**
- Consumes (Task 2,3): `HeroStats.Values`, `StatId.Value`, `new StatId(string)`, `MetaState(int, HeroStats)`, `CampaignState.Meta.Heroes`.
- Produces:
  - `[Serializable] StatEntry { string id; int value; }`
  - `CampaignSaveData.stats` (`List<StatEntry>`, 필드 초기화 `= new List<StatEntry>()`).
  - `CampaignSave.Capture` 가 `Meta.Heroes` → `stats` 평면화; `Restore` 가 `stats` → `HeroStats` → `MetaState(loopCount, heroes)`.

- [ ] **Step 1: 실패 테스트 작성 (기존 CampaignSaveTests 에 케이스 추가)**

`Assets/Tests/Editor/CampaignSaveTests.cs` — 기존 5개 테스트는 그대로 두고, `Sample()` 은 유지(스탯 없는 케이스로 하위호환 확인용), 아래 테스트들을 클래스에 추가:
```csharp
        private static CampaignState SampleWithHeroes()
        {
            var heroes = HeroStats.FromDefs(StatCatalog.Default())
                .WithStat(StatIds.STR, 120)
                .WithStat(StatIds.LUK, 7);
            return new CampaignState(
                new MetaState(4, heroes),
                new RunState(2, new System.Collections.Generic.Dictionary<string, int> { { "money", 10 } }));
        }

        [Test]
        public void HeroStatsRoundTripThroughCapture()
        {
            var restored = CampaignSave.Restore(CampaignSave.Capture(SampleWithHeroes()));
            Assert.AreEqual(120, restored.Meta.Heroes.Get(StatIds.STR));
            Assert.AreEqual(7, restored.Meta.Heroes.Get(StatIds.LUK));
            Assert.AreEqual(50, restored.Meta.Heroes.Get(StatIds.HP), "손대지 않은 스탯도 유지");
            Assert.AreEqual(8, restored.Meta.Heroes.Values.Count);
        }

        [Test]
        public void HeroStatsRoundTripThroughJsonUtility()
        {
            var data = CampaignSave.Capture(SampleWithHeroes());
            string json = JsonUtility.ToJson(data);
            var back = JsonUtility.FromJson<CampaignSaveData>(json);
            var restored = CampaignSave.Restore(back);
            Assert.AreEqual(120, restored.Meta.Heroes.Get(StatIds.STR));
            Assert.AreEqual(8, restored.Meta.Heroes.Values.Count);
        }

        [Test]
        public void RestoreDoesNotAliasStatsList()
        {
            var data = CampaignSave.Capture(SampleWithHeroes());
            var restored = CampaignSave.Restore(data);
            data.stats[0].value = 999;                          // 복원 후 원본 세이브데이터 수정
            data.stats.Add(new StatEntry { id = "GHOST", value = 1 });
            Assert.IsFalse(restored.Meta.Heroes.Has(new StatId("GHOST")), "복원 상태가 세이브 리스트를 참조 공유하면 안 됨");
            Assert.AreEqual(120, restored.Meta.Heroes.Get(StatIds.STR));
        }

        [Test]
        public void EmptyHeroesRoundTripsToEmpty()
        {
            var restored = CampaignSave.Restore(CampaignSave.Capture(Sample())); // Sample()=스탯 없음
            Assert.AreEqual(0, restored.Meta.Heroes.Values.Count);
        }
```

- [ ] **Step 2: 실패 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`.
Expected: 컴파일 에러 — `StatEntry`·`CampaignSaveData.stats`·`Meta.Heroes` 관련 미정의/미참조 (RED).

- [ ] **Step 3: CampaignSaveData 확장**

`Assets/Scripts/VNEngine/Core/Sim/CampaignSaveData.cs` 전체:
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

    [System.Serializable]
    public sealed class StatEntry
    {
        public string id;      // StatId.Value 로 평면화
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
        public List<StatEntry> stats = new List<StatEntry>();   // Meta.Heroes 평면화(StatId→string). additive: 구세이브는 빈 리스트→빈 Heroes.
    }
}
```

- [ ] **Step 4: CampaignSave 확장**

`Assets/Scripts/VNEngine/Core/Sim/CampaignSave.cs` 전체:
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
            foreach (var kv in c.Meta.Heroes.Values)
                data.stats.Add(new StatEntry { id = kv.Key.Value, value = kv.Value });
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

            var statDict = new Dictionary<StatId, int>(data.stats != null ? data.stats.Count : 0);
            if (data.stats != null)
                foreach (var e in data.stats)
                    statDict[new StatId(e.id)] = e.value;
            var heroes = new HeroStats(statDict);

            // RunState 생성자가 res 를, HeroStats 생성자가 statDict 를 다시 방어적 복사 → 세이브데이터 리스트와 참조 분리.
            return new CampaignState(new MetaState(data.loopCount, heroes), new RunState(data.day, res));
        }
    }
}
```

- [ ] **Step 5: 통과 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`(에러0) → `run_tests` assembly:`VNEngine.Tests` test_names:`VNEngine.Tests.CampaignSaveTests`.
Expected: CampaignSaveTests 9/9 PASS(기존 5 + 신규 4). 전체 스위트 PASS.

- [ ] **Step 6: 커밋**

```bash
git add "Assets/Scripts/VNEngine/Core/Sim/CampaignSaveData.cs" "Assets/Scripts/VNEngine/Core/Sim/CampaignSave.cs" "Assets/Tests/Editor/CampaignSaveTests.cs"
git commit -m "feat(sim): persist HeroStats in campaign save (flat StatEntry round-trip)"
```

---

### Task 5: StatCostCurve + StatUpgrade — 인과율→스탯강화 (순수, 구간지수 비용)

인과율을 받아 스탯을 올리는 순수 함수. 비용은 데이터 곡선(`StatCostCurve`)으로 주입, Cap 은 `StatDef` 로 주입. 입력 불변.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Stats/StatCostCurve.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Stats/StatUpgrade.cs`
- Test: `Assets/Tests/Editor/StatUpgradeTests.cs` (create)

**Interfaces:**
- Consumes (Task 1,2): `HeroStats`, `HeroStats.TryGet/WithStat`, `StatDef.Id/Cap/StartValue`, `StatId`.
- Produces:
  - `sealed class StatCostCurve` + `readonly struct Band { int UpperExclusive; int Cost; }` + `StatCostCurve(IReadOnlyList<Band>)` + `int CostAt(int currentValue)` + `static StatCostCurve Default()`.
  - `readonly struct StatUpgradeResult { HeroStats Stats; int KarmaSpent; int PointsGained; }`
  - `static StatUpgradeResult StatUpgrade.Upgrade(HeroStats stats, StatDef def, StatCostCurve curve, int karmaAvailable)`.
- **비용 의미:** 값 `v`→`v+1` 비용 = `CostAt(v)`. 밴드는 `current < UpperExclusive` 인 첫 밴드의 Cost. 마지막 밴드 `UpperExclusive=int.MaxValue`.

- [ ] **Step 1: 실패 테스트 작성**

`Assets/Tests/Editor/StatUpgradeTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class StatUpgradeTests
    {
        // 07 문서 §13.3 초기추정 곡선 경계값 대조 (sim_8stat.py stat_cost 기준).
        [TestCase(0, 1)]
        [TestCase(99, 1)]
        [TestCase(100, 2)]
        [TestCase(249, 2)]
        [TestCase(250, 3)]
        [TestCase(449, 3)]
        [TestCase(450, 5)]
        [TestCase(649, 5)]
        [TestCase(650, 9)]
        [TestCase(799, 9)]
        [TestCase(800, 16)]
        [TestCase(949, 16)]
        [TestCase(950, 28)]
        [TestCase(998, 28)]
        public void DefaultCurveCostBoundaries(int cur, int expectedCost)
        {
            Assert.AreEqual(expectedCost, StatCostCurve.Default().CostAt(cur));
        }

        private static StatDef Def(StatId id, int cap = 999) => new StatDef(id, id.Value, 5, cap);

        [Test]
        public void UpgradeSpendsPerPointAndStopsWhenKarmaInsufficient()
        {
            // cur=98, karma=5: 98->99(1) 99->100(1) 100->101(2)=누적4, 다음 2 필요한데 1 남음 -> 정지.
            var stats = new HeroStats(new Dictionary<StatId, int> { { StatIds.STR, 98 } });
            var r = StatUpgrade.Upgrade(stats, Def(StatIds.STR), StatCostCurve.Default(), 5);
            Assert.AreEqual(3, r.PointsGained);
            Assert.AreEqual(4, r.KarmaSpent);
            Assert.AreEqual(101, r.Stats.Get(StatIds.STR));
        }

        [Test]
        public void UpgradeDoesNotMutateInput()
        {
            var stats = new HeroStats(new Dictionary<StatId, int> { { StatIds.STR, 98 } });
            StatUpgrade.Upgrade(stats, Def(StatIds.STR), StatCostCurve.Default(), 100);
            Assert.AreEqual(98, stats.Get(StatIds.STR), "입력 HeroStats 불변");
        }

        [Test]
        public void UpgradeCannotExceedCap()
        {
            var stats = new HeroStats(new Dictionary<StatId, int> { { StatIds.STR, 998 } });
            var r = StatUpgrade.Upgrade(stats, Def(StatIds.STR, cap: 999), StatCostCurve.Default(), 100000);
            Assert.AreEqual(999, r.Stats.Get(StatIds.STR));
            Assert.AreEqual(1, r.PointsGained);
            Assert.AreEqual(28, r.KarmaSpent);
        }

        [Test]
        public void ZeroKarmaGainsNothing()
        {
            var stats = new HeroStats(new Dictionary<StatId, int> { { StatIds.STR, 5 } });
            var r = StatUpgrade.Upgrade(stats, Def(StatIds.STR), StatCostCurve.Default(), 0);
            Assert.AreEqual(0, r.PointsGained);
            Assert.AreEqual(0, r.KarmaSpent);
            Assert.AreSame(stats, r.Stats, "변화 없으면 원본 그대로 반환");
        }

        [Test]
        public void SimCrossCheckKnownKarmaBudget()
        {
            // 5에서 karma=99 투입: 5..99 각 cost1(95점, 누적95), 100->101 cost2(97), 101->102 cost2(99) -> 정지.
            var stats = new HeroStats(new Dictionary<StatId, int> { { StatIds.STR, 5 } });
            var r = StatUpgrade.Upgrade(stats, Def(StatIds.STR), StatCostCurve.Default(), 99);
            Assert.AreEqual(97, r.PointsGained);
            Assert.AreEqual(99, r.KarmaSpent);
            Assert.AreEqual(102, r.Stats.Get(StatIds.STR));
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`.
Expected: 컴파일 에러 — `StatCostCurve`/`StatUpgrade`/`StatUpgradeResult` 미정의 (RED).

- [ ] **Step 3: StatCostCurve 구현**

`Assets/Scripts/VNEngine/Core/Sim/Stats/StatCostCurve.cs`:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    // 구간지수 비용곡선(데이터). 값 v -> v+1 로 올리는 비용 = CostAt(v).
    // 밴드는 오름차순 UpperExclusive 경계 — current < UpperExclusive 인 첫 밴드의 Cost.
    // 마지막 밴드는 상한 없음(UpperExclusive=int.MaxValue). 경계·비용은 전부 데이터(튜닝대상).
    public sealed class StatCostCurve
    {
        public readonly struct Band
        {
            public int UpperExclusive { get; }
            public int Cost { get; }
            public Band(int upperExclusive, int cost)
            {
                UpperExclusive = upperExclusive;
                Cost = cost;
            }
        }

        private readonly Band[] _bands;

        public StatCostCurve(IReadOnlyList<Band> bands)
        {
            if (bands == null || bands.Count == 0)
                throw new System.ArgumentException("bands required", nameof(bands));
            _bands = new Band[bands.Count];
            for (int i = 0; i < bands.Count; i++) _bands[i] = bands[i];
        }

        public int CostAt(int currentValue)
        {
            for (int i = 0; i < _bands.Length; i++)
                if (currentValue < _bands[i].UpperExclusive)
                    return _bands[i].Cost;
            return _bands[_bands.Length - 1].Cost; // 안전망
        }

        // 1편 초기 추정 곡선(튜닝대상): <100->1, <250->2, <450->3, <650->5, <800->9, <950->16, >=950->28.
        public static StatCostCurve Default() => new StatCostCurve(new List<Band>
        {
            new Band(100, 1),
            new Band(250, 2),
            new Band(450, 3),
            new Band(650, 5),
            new Band(800, 9),
            new Band(950, 16),
            new Band(int.MaxValue, 28),
        });
    }
}
```

- [ ] **Step 4: StatUpgrade 구현**

`Assets/Scripts/VNEngine/Core/Sim/Stats/StatUpgrade.cs`:
```csharp
namespace VNEngine
{
    public readonly struct StatUpgradeResult
    {
        public HeroStats Stats { get; }
        public int KarmaSpent { get; }
        public int PointsGained { get; }

        public StatUpgradeResult(HeroStats stats, int karmaSpent, int pointsGained)
        {
            Stats = stats;
            KarmaSpent = karmaSpent;
            PointsGained = pointsGained;
        }
    }

    // 인과율(karma) -> 스탯 강화. 순수 함수: 입력 HeroStats 불변, 새 결과 반환.
    // Cap 은 StatDef, 비용은 StatCostCurve 로 데이터 주입. karma 수급/저금은 07-C.
    public static class StatUpgrade
    {
        public static StatUpgradeResult Upgrade(HeroStats stats, StatDef def, StatCostCurve curve, int karmaAvailable)
        {
            if (stats == null) throw new System.ArgumentNullException(nameof(stats));
            if (def == null) throw new System.ArgumentNullException(nameof(def));
            if (curve == null) throw new System.ArgumentNullException(nameof(curve));

            int cur = stats.TryGet(def.Id, out var v) ? v : def.StartValue;
            int spent = 0;
            int gained = 0;

            while (cur < def.Cap)
            {
                int cost = curve.CostAt(cur);
                if (karmaAvailable - spent < cost) break; // 남은 karma 로 다음 포인트 못 삼
                spent += cost;
                cur += 1;
                gained += 1;
            }

            var newStats = gained > 0 ? stats.WithStat(def.Id, cur) : stats;
            return new StatUpgradeResult(newStats, spent, gained);
        }
    }
}
```

- [ ] **Step 5: 통과 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`(에러0) → `run_tests` assembly:`VNEngine.Tests` test_names:`VNEngine.Tests.StatUpgradeTests`.
Expected: StatUpgradeTests 전건 PASS(경계 14 케이스 + 시나리오 5). 전체 스위트 PASS.

- [ ] **Step 6: 커밋**

```bash
git add "Assets/Scripts/VNEngine/Core/Sim/Stats/StatCostCurve.cs" "Assets/Scripts/VNEngine/Core/Sim/Stats/StatUpgrade.cs" "Assets/Tests/Editor/StatUpgradeTests.cs"
git commit -m "feat(sim): karma->stat upgrade pure function with data-driven cost curve"
```

---

### Task 6: MetaProjection — 주인공 스탯 → VN 변수 (읽기전용 투영 대상 추가)

주인공 주요 스탯·총합을 VN `GameState` 변수로 읽기전용 투영. 변수명 주입. **투영 대상만 추가**(실제 매턴 호출 배선은 서사 슬라이스). 기존 `Project(MetaState,...)` 는 유지.

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Sim/MetaProjection.cs`
- Test: `Assets/Tests/Editor/MetaProjectionTests.cs` (extend — 기존 3개 유지, 스탯 케이스 추가)

**Interfaces:**
- Consumes (Task 2): `HeroStats.Values/TryGet`, `StatId`, `GameState.Set/Get`, `VnValue.Int`.
- Produces:
  - `static void ProjectHeroStats(HeroStats heroes, GameState state, IReadOnlyDictionary<StatId,string> statVars)` — 개별 스탯→주입변수명(없는 스탯은 0).
  - `static void ProjectHeroTotal(HeroStats heroes, GameState state, string totalVar)` — 총스탯합→변수.

- [ ] **Step 1: 실패 테스트 작성 (MetaProjectionTests 에 추가)**

기존 3개 테스트 유지, 아래를 클래스에 추가:
```csharp
        [Test]
        public void ProjectsIndividualHeroStatsIntoInjectedVariables()
        {
            var state = new GameState(new SeededRandom(1));
            var heroes = HeroStats.Empty.WithStat(StatIds.STR, 500).WithStat(StatIds.INT, 12);
            var map = new System.Collections.Generic.Dictionary<StatId, string>
            {
                { StatIds.STR, "주인공_STR" },
                { StatIds.INT, "주인공_INT" },
            };
            MetaProjection.ProjectHeroStats(heroes, state, map);
            Assert.AreEqual(VnValue.Int(500), state.Get("주인공_STR"));
            Assert.AreEqual(VnValue.Int(12), state.Get("주인공_INT"));
        }

        [Test]
        public void AbsentStatProjectsZero()
        {
            var state = new GameState(new SeededRandom(1));
            var map = new System.Collections.Generic.Dictionary<StatId, string> { { StatIds.STR, "주인공_STR" } };
            MetaProjection.ProjectHeroStats(HeroStats.Empty, state, map);
            Assert.AreEqual(VnValue.Int(0), state.Get("주인공_STR"));
        }

        [Test]
        public void ProjectsHeroTotalAsSumOfAllStats()
        {
            var state = new GameState(new SeededRandom(1));
            var heroes = HeroStats.Empty.WithStat(StatIds.STR, 100).WithStat(StatIds.DEF, 25);
            MetaProjection.ProjectHeroTotal(heroes, state, "주인공_전투력");
            Assert.AreEqual(VnValue.Int(125), state.Get("주인공_전투력"));
        }

        [Test]
        public void HeroTotalRejectsEmptyVariableName()
        {
            var state = new GameState(new SeededRandom(1));
            Assert.Throws<System.ArgumentException>(() => MetaProjection.ProjectHeroTotal(HeroStats.Empty, state, ""));
        }
```

- [ ] **Step 2: 실패 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`.
Expected: 컴파일 에러 — `ProjectHeroStats`/`ProjectHeroTotal` 미정의 (RED).

- [ ] **Step 3: MetaProjection 확장**

`Assets/Scripts/VNEngine/Core/Sim/MetaProjection.cs` 전체:
```csharp
using System.Collections.Generic;

namespace VNEngine
{
    // 커널 → VN 단방향·읽기전용 투영. Core 는 테마 중립이라 변수명은 주입받는다.
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

        // 주인공 개별 스탯을 주입된 변수명으로 읽기전용 투영. 없는 스탯은 0.
        public static void ProjectHeroStats(HeroStats heroes, GameState state, IReadOnlyDictionary<StatId, string> statVars)
        {
            if (heroes == null) throw new System.ArgumentNullException(nameof(heroes));
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (statVars == null) throw new System.ArgumentNullException(nameof(statVars));

            foreach (var kv in statVars)
            {
                if (string.IsNullOrEmpty(kv.Value))
                    throw new System.ArgumentException("stat variable name required", nameof(statVars));
                int val = heroes.TryGet(kv.Key, out var v) ? v : 0;
                state.Set(kv.Value, VnValue.Int(val));
            }
        }

        // 총스탯합(전투력 근사)을 변수로 투영.
        public static void ProjectHeroTotal(HeroStats heroes, GameState state, string totalVar)
        {
            if (heroes == null) throw new System.ArgumentNullException(nameof(heroes));
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (string.IsNullOrEmpty(totalVar))
                throw new System.ArgumentException("totalVar required", nameof(totalVar));

            int sum = 0;
            foreach (var kv in heroes.Values) sum += kv.Value;
            state.Set(totalVar, VnValue.Int(sum));
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

UnityMCP: `refresh_unity` scope:`all` → `read_console`(에러0) → `run_tests` assembly:`VNEngine.Tests` test_names:`VNEngine.Tests.MetaProjectionTests`.
Expected: MetaProjectionTests 7/7 PASS(기존 3 + 신규 4). 전체 스위트 PASS.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/Scripts/VNEngine/Core/Sim/MetaProjection.cs" "Assets/Tests/Editor/MetaProjectionTests.cs"
git commit -m "feat(sim): project hero stats + total into read-only VN variables"
```

---

### Task 7: 문서 갱신 — 07 채택+§12 갱신, 05/06 반영

미추적 `07-defense-combat (2).md` 를 정식 `07-defense-combat.md` 로 채택하고 §12(주인공 8스탯)에 구현 상태를 반영. 05/06 엔진 레퍼런스에도 스탯 시스템을 반영.

**Files:**
- Rename+Modify: `docs/engine/07-defense-combat (2).md` → `docs/engine/07-defense-combat.md`
- Modify: `docs/engine/05-simulation-kernel.md`
- Modify: `docs/engine/06-loop-and-state.md`

- [ ] **Step 1: 07 문서 채택(rename)**

```bash
git mv "docs/engine/07-defense-combat (2).md" "docs/engine/07-defense-combat.md" 2>/dev/null || mv "docs/engine/07-defense-combat (2).md" "docs/engine/07-defense-combat.md"
```
> `07-defense-combat (2).md` 는 미추적이라 `git mv` 가 실패할 수 있음 — 그 경우 일반 `mv` 후 Step 5 에서 새 경로를 `git add`.

- [ ] **Step 2: 07 §12(주인공 8스탯)에 구현 상태 반영**

`docs/engine/07-defense-combat.md` 의 `## 12. 주인공 (전투 유닛 + 8스탯)` 헤딩 바로 아래에 구현상태 블록 추가:
```markdown
> **구현 상태(07-A1, 2026-07-07):** 8스탯 **데이터 골격 + 주인공 상태 + 인과율→스탯강화 순수함수 + VN 투영 대상**이 구현됨(전투 제외).
> - `Core/Sim/Stats/`: `StatId`(string 래핑 값타입) · `StatDef`(데이터) · `StatCatalog.Default()`(1편 8스탯 테이블) · `HeroStats`(불변 `Dictionary<StatId,int>`) · `StatCostCurve`(구간지수, §13.3 곡선) · `StatUpgrade`(순수함수).
> - `HeroStats` 는 **메타 귀속**(`MetaState.Heroes`), `LoopEngine.StartNewLoop` 에서 **회차 넘어 캐리포워드**. 세이브(`CampaignSave`)에 평면 직렬화(`StatEntry`).
> - `MetaProjection.ProjectHeroStats/ProjectHeroTotal` 로 주요 스탯·총합을 VN 변수에 읽기전용 투영(변수명 주입).
> - **미구현(다음 슬라이스):** 전투/데미지 공식(07-A2), 인과율 수급/저금(07-C), 초기 라이브 시딩·투영 실제 배선. 곡선 수치는 초기 추정(플레이테스트 튜닝).
> 스펙: `docs/superpowers/plans/2026-07-07-vn-engine-8stat-slice.md`.
```

- [ ] **Step 3: 05 코어 모델에 스탯 타입 반영**

`docs/engine/05-simulation-kernel.md` 코어 모델 표/목록에 행 추가(기존 서식에 맞춰): `StatDef{Id,DisplayName,StartValue,Cap}`, `HeroStats(불변 Dictionary<StatId,int>)`, `StatCostCurve.CostAt`, `StatUpgrade.Upgrade(순수)`, `MetaProjection.ProjectHeroStats/Total`. 한 줄 요약: "주인공 8스탯이 메타(`MetaState.Heroes`)에 귀속되고 세이브·VN 투영에 연결됨(전투는 07)."

- [ ] **Step 4: 06 착수 노트 갱신**

`docs/engine/06-loop-and-state.md` 상단 착수노트 블록 아래에 한 줄 추가:
```markdown
> **착수 상태(2026-07-07, 8스탯)**: 주인공 8스탯 **데이터 골격**이 올라옴 — `MetaState.Heroes`(불변 `HeroStats`, 회차 넘어 캐리포워드),
> 인과율→스탯강화 순수함수(`StatUpgrade`+데이터 곡선 `StatCostCurve`), 세이브 평면직렬화, VN 스탯 투영(`MetaProjection`). **전투·karma수급·라이브시딩은 미구현.**
> 스펙: `docs/superpowers/plans/2026-07-07-vn-engine-8stat-slice.md`.
```

- [ ] **Step 5: 커밋**

```bash
git add "docs/engine/07-defense-combat.md" "docs/engine/05-simulation-kernel.md" "docs/engine/06-loop-and-state.md"
git rm --cached "docs/engine/07-defense-combat (2).md" 2>/dev/null || true
git commit -m "docs(sim): reflect 8-stat system; adopt 07-defense-combat doc"
```
> 다른 미추적 파일(`sim_*.py`, `08-inheritance-skills.md`, `persistence_prompt.md`, `prompt_07A1.md`, `gemini-*.md`)·`Assets/Fonts/**` 는 add 하지 말 것.

---

## Self-Review

**1. Spec coverage (프롬프트 4작업 매핑):**
- 작업1 StatDef 데이터(8스탯/Cap/StartValue) → Task 1. ✅
- 작업2 HeroStats(Dictionary/불변) + MetaState 귀속 + 세이브 → Task 2(HeroStats) + Task 3(MetaState/캐리포워드) + Task 4(직렬화). ✅
- 작업3 StatUpgrade(구간지수, StatCostCurve 데이터) → Task 5. ✅
- 작업4 메타→VN 투영 대상 확장(배선 보류) → Task 6. ✅
- 문서 갱신 → Task 7. ✅

**2. 데이터 주도(하드코딩 금지) 확인:** 스탯 목록=`StatCatalog` 데이터, 스탯 상태=`Dictionary<StatId,int>`(필드 없음), 비용=`StatCostCurve` 데이터(분기 하드코딩 없음), Cap=`StatDef` 주입, 투영 변수명=주입. ✅

**3. 불변성:** `HeroStats` 생성자 방어복사 + `WithStat` 새 인스턴스, `StatUpgrade` 입력 불변, `MetaState`/`CampaignState` 전이 새 인스턴스. 세이브 왕복 비앨리어싱(Task4 RestoreDoesNotAliasStatsList). ✅

**4. 스코프 경계:** Unity 레이어/씬/SimController/SO 무변경(Core만). 라이브 시딩·karma 배선·전투 비스코프. 07 문서 채택만 예외(사용자 승인). ✅

**5. Type consistency:** `StatId(string)`, `StatDef(StatId,string,int,int)`, `HeroStats(IReadOnlyDictionary<StatId,int>)`, `HeroStats.WithStat/TryGet/Get/Values`, `MetaState(int)`/`MetaState(int,HeroStats)`, `StatEntry{id,value}`, `CampaignSaveData.stats`, `StatCostCurve(IReadOnlyList<Band>)`/`CostAt`, `StatUpgrade.Upgrade(HeroStats,StatDef,StatCostCurve,int)→StatUpgradeResult`, `MetaProjection.ProjectHeroStats/ProjectHeroTotal` — 태스크 간 시그니처 일치. ✅

**6. 곡선 검증 기준:** §13.3 / 사용자 제공 곡선과 일치. 경계 14 케이스로 대조(99→1,100→2,449→3,450→5,799→9,800→16,949→16,950→28 포함). ✅
