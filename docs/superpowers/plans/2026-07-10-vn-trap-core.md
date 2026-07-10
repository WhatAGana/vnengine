# T1 함정 코어(Trap Core) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 함정방을 "포획존 boolean"에서 "직접 데미지원 + 포획몹 전용 방 + 포획몹 격퇴 시에만 포획"으로 재정의한다(초회차 안전판: 함정만 있으면 데미지만, 포획 없음).

**Architecture:** 함정 데미지는 데이터 주도 골격(`TrapDef` 종류 카탈로그 + `TrapRule.Damage = Base + Level*PerLevel`, `TrapConfig`=종류×레벨)으로 만들되 지금 노출은 1종·레벨1 고정(8스탯 `StatDef` 패턴과 동일 — 골격은 확장형, 노출은 단순). `TrapConfig`는 **던전 그래프의 속성**이므로 `RoomGraph`에 얹어 `CombatResolver`가 읽는다(시그니처 스레딩 회피). 포획은 `Trap AND CapturingMonster`(둘 다) 또는 `HeroSubdue`일 때만 성립하도록 `CaptureRule` 시맨틱을 OR→조합으로 바꾼다. 배치 검증은 "함정방=포획몹만, 일반방=일반몹만"을 강제한다.

**Tech Stack:** C# (Unity 2022 EditMode/NUnit), 순수 코어(`Assets/Scripts/VNEngine/Core/Sim/`), 정수 연산 전용(부동소수점 금지), 결정론(같은 시드+같은 입력→같은 결과, 함정 데미지는 rng 미사용).

## Global Constraints

- **정수 연산만.** 부동소수점 금지(기존 `DamageFormula`/`ThreatFormula` 관례).
- **순수함수·불변.** 코어 규칙은 입력(RoomGraph/Attacker/HeroStats/…)을 절대 변경하지 않고 새 값으로 반환. HP 감소는 로컬 변수로만 추적.
- **결정론.** rng 호출 순서 고정. 함정 데미지는 rng를 소비하지 않는다(순서 불변 유지).
- **각 커밋은 컴파일 + 전체 EditMode 그린.** Unity는 단일 어셈블리 컴파일이라 시그니처 불일치는 전체를 깨뜨린다. 이 계획은 시그니처 변경을 회피(TrapConfig를 RoomGraph에 탑재, 오버로드로 기존 호출부 보존)하도록 순서를 짰다.
- **네임스페이스** `VNEngine`(코어), 테스트는 `VNEngine.Tests`.
- **파일당 `.meta`.** Unity는 새 `.cs`마다 `.meta`를 자동 생성한다 — 새 스크립트 생성 후 반드시 Unity 컴파일(리프레시)로 `.meta` 생성 및 컴파일 확인.
- **함정 밸런스 상수는 플레이스홀더**(튜닝 대상): `TrapDef` 기본 `Base=10, PerLevel=5`(레벨1 → 데미지 15), `TrapLevel=1` 고정. 레벨업·다종류·UI는 이번 스코프 밖(후속 슬라이스).

---

## File Structure

**신규 (Core/Sim/Combat/):**
- `TrapDef.cs` — 함정 종류 정의(데이터): `Id`, `DisplayName`, `Base`, `PerLevel`. `MonsterDef`/`StatDef` 패턴.
- `TrapRule.cs` — `static int Damage(TrapDef def, int level)` = `Base + level*PerLevel`(정수, min 검증). 순수.
- `TrapCatalog.cs` — `TrapIds`(id 상수) + `Default()`(기본 1종 "가시함정"). `MonsterCatalog` 패턴.
- `TrapConfig.cs` — 한 웨이브에 적용될 함정 인스턴스(종류×레벨). `Def`, `Level`, `Damage`(생성 시 `TrapRule.Damage`로 산출). `Default()`(가시함정 lvl1), `None()`(데미지 0 — 함정 효과 없는 테스트/방용).

**수정 (Core/Sim/Combat/):**
- `RoomGraph.cs` — `TrapConfig Trap` 속성 + ctor/`Linear` 오버로드(기존 시그니처는 `TrapConfig.Default()`로 위임). `Trap` 방어적 비-null 보장.
- `PlacementBuilder.cs` — 그래프 재구성 시 `graph.Trap` 보존.
- `PlacementValidator.cs` — `MonsterDef.IsCapturingMonster` ↔ `RoomNode.HasTrap` 교차검증. 신규 에러 2종.
- `Placement.cs` — `PlacementError`에 `MonsterInTrapRoom`, `CapturerInNormalRoom` 추가.
- `CaptureRule.cs` — `ShouldCapture` 시맨틱: `(Trap & CapturingMonster) | HeroSubdue`(활성 마스크 적용).
- `CombatResolver.cs` — `ResolveIntruder`가 함정방 진입 시 방어몹 루프 **전에** `graph.Trap.Damage` 적용. 함정 단독 처치=포획 없음(Trap-only 트리거).

**수정 (Unity):**
- `Unity/Sim/SimController.cs` — 고정 픽스처: HighDemon(비포획)을 함정방 r0에 배치 → 무효. 포획몹(Succubus)로 교체 + threatBase 튜닝.
- `Unity/Demo/PlacementSmokeBootstrap.cs` — 함정방 r0에 포획몹만 배치 가능하게 시나리오·안내 정정 + threatBase 튜닝.

**수정 (Tests/Editor/):**
- `CaptureRuleTests.cs`, `CombatResolverTests.cs`, `PlacementTests.cs`, `CampaignWaveRuleTests.cs` — 새 규칙 반영.
- 신규 `TrapRuleTests.cs` — 함정 데미지 공식.
- `RoomGraphTests.cs` — Trap 전파 테스트 추가.

---

## Task 1: 함정 데이터 프리미티브(TrapDef / TrapRule / TrapCatalog / TrapConfig)

순수 additive. 기존 코드 무영향 → 전체 그린 유지.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/TrapDef.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/TrapRule.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/TrapCatalog.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/TrapConfig.cs`
- Test: `Assets/Tests/Editor/TrapRuleTests.cs`

**Interfaces:**
- Produces:
  - `sealed class TrapDef { TrapId Id; string DisplayName; int Base; int PerLevel; TrapDef(TrapId,string,int,int) }`
  - `struct TrapId { string Value; TrapId(string) }` (UnitClassId와 동형 — 아래 코드대로 TrapCatalog에 정의)
  - `static class TrapRule { int Damage(TrapDef def, int level) }`
  - `static class TrapCatalog { IReadOnlyList<TrapDef> Default(); }` + `static class TrapIds { TrapId Spike }`
  - `sealed class TrapConfig { TrapDef Def; int Level; int Damage; TrapConfig(TrapDef,int); static TrapConfig Default(); static TrapConfig None(); }`

- [ ] **Step 1: `TrapRuleTests.cs` 작성(실패 테스트)**

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class TrapRuleTests
    {
        [Test]
        public void DamageIsBasePlusLevelTimesPerLevel()
        {
            var def = new TrapDef(new TrapId("Spike"), "가시함정", baseDamage: 10, perLevel: 5);
            Assert.AreEqual(15, TrapRule.Damage(def, 1));  // 10 + 1*5
            Assert.AreEqual(20, TrapRule.Damage(def, 2));  // 10 + 2*5
            Assert.AreEqual(10, TrapRule.Damage(def, 0));  // 10 + 0
        }

        [Test]
        public void DamageNullDefThrows()
        {
            Assert.Throws<ArgumentNullException>(() => TrapRule.Damage(null, 1));
        }

        [Test]
        public void DamageNegativeLevelThrows()
        {
            var def = new TrapDef(new TrapId("Spike"), "가시함정", 10, 5);
            Assert.Throws<ArgumentException>(() => TrapRule.Damage(def, -1));
        }

        [Test]
        public void CatalogDefaultHasSpike()
        {
            var cat = TrapCatalog.Default();
            Assert.AreEqual(1, cat.Count);
            Assert.AreEqual(TrapIds.Spike, cat[0].Id);
        }

        [Test]
        public void ConfigDefaultComputesDamageFromRule()
        {
            var cfg = TrapConfig.Default();
            Assert.AreEqual(TrapRule.Damage(cfg.Def, cfg.Level), cfg.Damage);
            Assert.AreEqual(1, cfg.Level);
        }

        [Test]
        public void ConfigNoneHasZeroDamage()
        {
            Assert.AreEqual(0, TrapConfig.None().Damage);
        }

        [Test]
        public void TrapDefRejectsNonPositiveBase()
        {
            Assert.Throws<ArgumentException>(() => new TrapDef(new TrapId("X"), "x", baseDamage: 0, perLevel: 5));
        }
    }
}
```

- [ ] **Step 2: 컴파일 실패 확인**

Unity 리프레시 후 `read_console`로 확인. Expected: FAIL — `TrapDef`/`TrapRule`/`TrapCatalog`/`TrapConfig`/`TrapId` 미정의 컴파일 에러.

- [ ] **Step 3: `TrapDef.cs` 작성**

```csharp
using System;

namespace VNEngine
{
    // 함정 종류 정의(데이터). Damage = Base + Level*PerLevel (TrapRule). 종류 확장은 TrapCatalog에 추가.
    // 지금은 1종(가시함정)·레벨1 고정 — 레벨업/다종류는 후속 슬라이스(골격만 확장형).
    public sealed class TrapDef
    {
        public TrapId Id { get; }
        public string DisplayName { get; }
        public int Base { get; }        // 레벨0 기준 데미지
        public int PerLevel { get; }    // 레벨당 증가량

        public TrapDef(TrapId id, string displayName, int baseDamage, int perLevel)
        {
            if (string.IsNullOrEmpty(id.Value)) throw new ArgumentException("TrapId.Value must not be null or empty", nameof(id));
            if (baseDamage < 1) throw new ArgumentException("baseDamage must be at least 1", nameof(baseDamage));
            if (perLevel < 0) throw new ArgumentException("perLevel must be non-negative", nameof(perLevel));
            Id = id;
            DisplayName = displayName;
            Base = baseDamage;
            PerLevel = perLevel;
        }
    }
}
```

- [ ] **Step 4: `TrapRule.cs` 작성**

```csharp
using System;

namespace VNEngine
{
    // 함정 데미지 공식(순수, 정수). Damage = Base + Level*PerLevel.
    public static class TrapRule
    {
        public static int Damage(TrapDef def, int level)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (level < 0) throw new ArgumentException("level must be non-negative", nameof(level));
            return def.Base + level * def.PerLevel;
        }
    }
}
```

- [ ] **Step 5: `TrapCatalog.cs` 작성**

```csharp
using System;
using System.Collections.Generic;

namespace VNEngine
{
    // 함정 종류 id(값 타입 — UnitClassId 동형). 소스는 TrapCatalog 데이터 테이블.
    public readonly struct TrapId : IEquatable<TrapId>
    {
        public string Value { get; }
        public TrapId(string value) { Value = value; }
        public bool Equals(TrapId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TrapId o && Equals(o);
        public override int GetHashCode() => Value == null ? 0 : Value.GetHashCode();
        public override string ToString() => Value;
    }

    public static class TrapIds
    {
        public static readonly TrapId Spike = new TrapId("Spike");
    }

    // 1편 함정 데이터. 지금은 1종(가시함정). Base/PerLevel는 구조검증용 초기 추정 — 플레이테스트 실측 튜닝.
    public static class TrapCatalog
    {
        public static IReadOnlyList<TrapDef> Default() => new List<TrapDef>
        {
            new TrapDef(TrapIds.Spike, "가시함정", baseDamage: 10, perLevel: 5),
        };
    }
}
```

- [ ] **Step 6: `TrapConfig.cs` 작성**

`None()`은 데미지 0을 강제해야 하는데 기본 종의 `Base>=1` 때문에 `TrapRule.Damage`로는 0을 만들 수 없다 → Damage만 0으로 두는 private 무인자 생성자를 쓴다.

```csharp
using System;

namespace VNEngine
{
    // 한 웨이브(=한 던전 그래프)에 적용될 함정 인스턴스: 종류(Def) × 레벨. Damage는 TrapRule로 산출.
    // 함정은 그래프의 속성 → RoomGraph가 이 값을 들고, CombatResolver가 함정방 데미지로 읽는다.
    // Level은 진행/캠페인 축(지금은 1 고정). None()은 함정 데미지 0(함정 boolean만, 데미지 없는 방/테스트용).
    public sealed class TrapConfig
    {
        public TrapDef Def { get; }
        public int Level { get; }
        public int Damage { get; }

        public TrapConfig(TrapDef def, int level)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            if (level < 0) throw new ArgumentException("level must be non-negative", nameof(level));
            Level = level;
            Damage = TrapRule.Damage(def, level);
        }

        // Damage를 명시적으로 0으로 두는 내부 생성자(None 전용). Def/Level은 기본 종·0으로 채우되 데미지만 0.
        private TrapConfig()
        {
            Def = TrapCatalog.Default()[0];
            Level = 0;
            Damage = 0;
        }

        // 기본: 가시함정 레벨1. 함정방이 실제로 데미지를 주는 표준 설정.
        public static TrapConfig Default() => new TrapConfig(TrapCatalog.Default()[0], 1);

        // 함정 데미지 0(함정 boolean만). 데미지를 배제하고 포획/배치 로직만 검사할 때.
        public static TrapConfig None() => new TrapConfig();
    }
}
```

- [ ] **Step 7: Unity 리프레시 → `read_console`로 컴파일 성공 확인**

`isCompiling=false` 대기 후 콘솔 에러 0.

- [ ] **Step 8: EditMode 테스트 실행 — TrapRuleTests 통과 확인**

`run_tests`(EditMode, 필터 `TrapRuleTests`). Expected: 7 pass. 전체 스위트도 회귀 없음(additive).

- [ ] **Step 9: 커밋**

```bash
git add Assets/Scripts/VNEngine/Core/Sim/Combat/TrapDef.cs Assets/Scripts/VNEngine/Core/Sim/Combat/TrapRule.cs Assets/Scripts/VNEngine/Core/Sim/Combat/TrapCatalog.cs Assets/Scripts/VNEngine/Core/Sim/Combat/TrapConfig.cs Assets/Tests/Editor/TrapRuleTests.cs
git add Assets/Scripts/VNEngine/Core/Sim/Combat/TrapDef.cs.meta Assets/Scripts/VNEngine/Core/Sim/Combat/TrapRule.cs.meta Assets/Scripts/VNEngine/Core/Sim/Combat/TrapCatalog.cs.meta Assets/Scripts/VNEngine/Core/Sim/Combat/TrapConfig.cs.meta Assets/Tests/Editor/TrapRuleTests.cs.meta
git commit -m "feat(sim): trap damage data primitives (TrapDef/TrapRule/TrapCatalog/TrapConfig)"
```

---

## Task 2: RoomGraph가 TrapConfig를 탑재

`RoomGraph`에 `Trap` 속성 추가. 기존 ctor/`Linear` 호출부는 **오버로드로 보존**(기본값 `TrapConfig.Default()`). CombatResolver는 아직 안 읽으므로 **행위 무변화 → 전체 그린**. `PlacementBuilder`가 재구성 시 `Trap` 보존.

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Combat/RoomGraph.cs`
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Combat/PlacementBuilder.cs:26-32`
- Test: `Assets/Tests/Editor/RoomGraphTests.cs` (테스트 추가)

**Interfaces:**
- Consumes: `TrapConfig` (Task 1)
- Produces: `RoomGraph.Trap { get; }` (비-null 보장); `RoomGraph(rooms, entry, TrapConfig)`; `RoomGraph.Linear(rooms, TrapConfig)`; 기존 `RoomGraph(rooms,entry)`·`Linear(rooms)`는 `TrapConfig.Default()` 위임.

- [ ] **Step 1: RoomGraphTests에 Trap 전파 실패 테스트 추가**

`Assets/Tests/Editor/RoomGraphTests.cs` 파일에 아래 메서드 2개를 클래스 내부에 추가(기존 테스트 유지):

```csharp
        [Test]
        public void LinearDefaultsToDefaultTrapConfig()
        {
            var g = RoomGraph.Linear(new System.Collections.Generic.List<RoomNode>
            {
                new RoomNode(new System.Collections.Generic.List<Attacker>(), hasTrap: true),
            });
            Assert.IsNotNull(g.Trap);
            Assert.AreEqual(TrapConfig.Default().Damage, g.Trap.Damage);
        }

        [Test]
        public void LinearCarriesExplicitTrapConfig()
        {
            var cfg = TrapConfig.None();
            var g = RoomGraph.Linear(new System.Collections.Generic.List<RoomNode>
            {
                new RoomNode(new System.Collections.Generic.List<Attacker>(), hasTrap: true),
            }, cfg);
            Assert.AreSame(cfg, g.Trap);
            Assert.AreEqual(0, g.Trap.Damage);
        }
```

- [ ] **Step 2: 컴파일 실패 확인**

Unity 리프레시 → `read_console`. Expected: FAIL — `RoomGraph.Trap` / `Linear(rooms, cfg)` 미정의.

- [ ] **Step 3: RoomGraph.cs 수정**

`RoomGraph.cs`를 아래로 교체(전체):

```csharp
using System;
using System.Collections.Generic;

namespace VNEngine
{
    // 방 노드의 그래프. 전투 경로(Path)는 Entry에서 NextRooms[0]을 따라 terminal(NextRooms 빈 방)까지.
    // 선형 던전은 각 방 NextRooms가 1개인 특수케이스. 코어는 암묵적 — Path 마지막 방이 "코어앞1칸".
    // 함정 설정(Trap)은 그래프의 속성 — 함정방(RoomNode.HasTrap)이 입히는 데미지를 CombatResolver가 읽는다.
    public sealed class RoomGraph
    {
        public IReadOnlyList<RoomNode> Rooms { get; }
        public RoomId Entry { get; }
        public IReadOnlyList<RoomNode> Path { get; }   // 생성 시 1회 계산(불변)
        public TrapConfig Trap { get; }                // 함정방이 입히는 데미지 설정(비-null)
        public bool IsEmpty => Rooms.Count == 0;
        public RoomNode CoreFrontRoom => Path.Count == 0 ? null : Path[Path.Count - 1];

        public RoomGraph(IReadOnlyList<RoomNode> rooms, RoomId entry)
            : this(rooms, entry, TrapConfig.Default()) { }

        public RoomGraph(IReadOnlyList<RoomNode> rooms, RoomId entry, TrapConfig trap)
        {
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));
            Trap = trap ?? throw new ArgumentNullException(nameof(trap));
            var copy = new List<RoomNode>(rooms);
            Rooms = copy;
            Entry = entry;

            var byId = new Dictionary<RoomId, RoomNode>(copy.Count);
            foreach (var r in copy)
            {
                if (r == null) throw new ArgumentException("rooms must not contain null", nameof(rooms));
                byId[r.Id] = r;
            }
            foreach (var r in copy)
                foreach (var nx in r.NextRooms)
                    if (!byId.ContainsKey(nx))
                        throw new ArgumentException($"NextRoom '{nx}' not found in graph (room '{r.Id}')", nameof(rooms));

            Path = ComputePath(byId, entry);
        }

        // NextRooms[0]을 따라 terminal까지. 사이클은 방문집합으로 차단(그래프 데이터 오류 방어).
        private static IReadOnlyList<RoomNode> ComputePath(Dictionary<RoomId, RoomNode> byId, RoomId entry)
        {
            var path = new List<RoomNode>();
            if (byId.Count == 0) return path;
            if (!byId.TryGetValue(entry, out var cur))
                throw new ArgumentException($"Entry room '{entry}' not found in graph", nameof(entry));

            var visited = new HashSet<RoomId>();
            while (cur != null)
            {
                if (!visited.Add(cur.Id))
                    throw new VnRuntimeException($"Cycle detected in RoomGraph at '{cur.Id}'");
                path.Add(cur);
                if (cur.NextRooms.Count == 0) break;
                cur = byId[cur.NextRooms[0]];
            }
            return path;
        }

        // 방 "내용물" 목록을 받아 r0->r1->...->rN 선형 체인으로 링크한 그래프. Path == 입력 순서.
        public static RoomGraph Linear(IReadOnlyList<RoomNode> contentRooms)
            => Linear(contentRooms, TrapConfig.Default());

        public static RoomGraph Linear(IReadOnlyList<RoomNode> contentRooms, TrapConfig trap)
        {
            if (contentRooms == null) throw new ArgumentNullException(nameof(contentRooms));
            var nodes = new List<RoomNode>(contentRooms.Count);
            for (int i = 0; i < contentRooms.Count; i++)
            {
                var id = new RoomId("r" + i);
                var next = i + 1 < contentRooms.Count
                    ? new List<RoomId> { new RoomId("r" + (i + 1)) }
                    : (IReadOnlyList<RoomId>)Array.Empty<RoomId>();
                nodes.Add(new RoomNode(id, contentRooms[i].Defenders, contentRooms[i].HasTrap, next));
            }
            return new RoomGraph(nodes, contentRooms.Count == 0 ? default : new RoomId("r0"), trap);
        }
    }
}
```

- [ ] **Step 4: PlacementBuilder.cs — 재구성 시 Trap 보존**

`PlacementBuilder.cs:32`의 `return new RoomGraph(rebuilt, graph.Entry);` 를 아래로 교체:

```csharp
            return new RoomGraph(rebuilt, graph.Entry, graph.Trap);
```

- [ ] **Step 5: Unity 리프레시 → 컴파일 성공 확인(`read_console`)**

- [ ] **Step 6: EditMode 전체 실행 — 신규 2 테스트 통과 + 회귀 없음**

`run_tests`(EditMode 전체). Expected: 기존 459+ 그대로 통과 + `LinearDefaultsToDefaultTrapConfig`, `LinearCarriesExplicitTrapConfig` 통과. (CombatResolver가 Trap을 아직 안 읽으므로 행위 무변화.)

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/VNEngine/Core/Sim/Combat/RoomGraph.cs Assets/Scripts/VNEngine/Core/Sim/Combat/PlacementBuilder.cs Assets/Tests/Editor/RoomGraphTests.cs
git commit -m "feat(sim): RoomGraph carries TrapConfig (defaulted overloads, PlacementBuilder preserves)"
```

---

## Task 3: 배치 검증 — 함정방=포획몹만, 일반방=일반몹만

`PlacementValidator`가 `RoomNode.HasTrap` ↔ `MonsterDef.IsCapturingMonster`를 교차검증. 이 변경은 "HighDemon(비포획)을 함정방에 배치"를 무효화하므로 `CampaignWaveRuleTests`의 함정방 픽스처(HighDemon)를 포획몹(Succubus)로 마이그레이션해야 그린 유지(포획 시맨틱은 아직 OR이라 Succubus in 함정방 → 여전히 포획).

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Combat/Placement.cs:20-27`
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Combat/PlacementValidator.cs`
- Test: `Assets/Tests/Editor/PlacementTests.cs`
- Test: `Assets/Tests/Editor/CampaignWaveRuleTests.cs` (함정 픽스처 마이그레이션)

**Interfaces:**
- Consumes: `MonsterDef.IsCapturingMonster`, `RoomNode.HasTrap`
- Produces: `PlacementError.MonsterInTrapRoom`, `PlacementError.CapturerInNormalRoom`; 검증 규칙 = 함정방엔 포획몹만(또는 빈방), 일반방엔 일반몹만(또는 빈방).

- [ ] **Step 1: PlacementTests에 실패 테스트 추가**

`PlacementTests.cs`의 헬퍼 아래(예: `Plan` 헬퍼 다음)에 함정방 3방 그래프 헬퍼를 추가하고, 테스트 4개를 추가한다. 먼저 헬퍼:

```csharp
        // r0=함정방, r1·r2=일반방. (포획몹은 r0에만, 일반몹은 r1/r2에만 배치 가능.)
        private static RoomGraph TrapAtR0()
            => RoomGraph.Linear(new List<RoomNode>
            {
                new RoomNode(new List<Attacker>(), hasTrap: true),
                new RoomNode(new List<Attacker>(), hasTrap: false),
                new RoomNode(new List<Attacker>(), hasTrap: false),
            });
```

테스트:

```csharp
        [Test]
        public void NonCapturingMonsterInTrapRoomIsRejected()
        {
            // 오크(비포획, cost2)를 함정방 r0에 → 거부.
            var plan = Plan(Place("r0", MonsterIds.Orc));
            Assert.AreEqual(PlacementError.MonsterInTrapRoom,
                PlacementValidator.Validate(plan, TrapAtR0(), Cat()).Error);
        }

        [Test]
        public void CapturingMonsterInTrapRoomIsValid()
        {
            // 서큐버스(포획, cost3)를 함정방 r0에 → 허용.
            var plan = Plan(Place("r0", MonsterIds.Succubus));
            var r = PlacementValidator.Validate(plan, TrapAtR0(), Cat());
            Assert.IsTrue(r.IsValid, "포획몹은 함정방에 배치 가능");
        }

        [Test]
        public void CapturingMonsterInNormalRoomIsRejected()
        {
            // 서큐버스(포획)를 일반방 r1에 → 거부.
            var plan = Plan(Place("r1", MonsterIds.Succubus));
            Assert.AreEqual(PlacementError.CapturerInNormalRoom,
                PlacementValidator.Validate(plan, TrapAtR0(), Cat()).Error);
        }

        [Test]
        public void NonCapturingMonsterInNormalRoomIsValid()
        {
            // 오크(비포획)를 일반방 r1에 → 허용.
            var plan = Plan(Place("r1", MonsterIds.Orc));
            Assert.IsTrue(PlacementValidator.Validate(plan, TrapAtR0(), Cat()).IsValid);
        }
```

- [ ] **Step 2: 기존 PlacementTests의 무효화 케이스 수정**

`ThreeRooms()`는 전부 일반방. 아래 두 테스트가 **서큐버스를 일반방에 배치**하므로 새 규칙 위반 → 수정한다:

`ApplyPlacesDefendersIntoRooms` — `PlacementBuilder.Apply`는 검증을 안 하므로 그대로 통과하지만, 의미 명확화를 위해 서큐버스를 유지해도 무방(Apply는 배치만). **변경하지 않는다**(Apply 경로는 검증 우회 — 테스트 의도는 "배치 실체화"). 그대로 둔다.

`ValidateAndApply_ValidPlan_ReturnsGraphWithDefenders` — `ValidateAndApply`는 검증을 거치므로 서큐버스(포획)를 일반방 r2에 두면 이제 `CapturerInNormalRoom`으로 throw. **수정**: 함정방 그래프 + 서큐버스를 함정방에 배치하도록 교체:

```csharp
        [Test]
        public void ValidateAndApply_ValidPlan_ReturnsGraphWithDefenders()
        {
            // 임프(일반)→일반방 r1, 서큐버스(포획)→함정방 r0.
            var plan = Plan(Place("r1", MonsterIds.Imp), Place("r0", MonsterIds.Succubus));
            var built = PlacementBuilder.ValidateAndApply(plan, TrapAtR0(), Cat());
            Assert.AreEqual(1, built.Path[0].Defenders.Count, "r0(함정방)에 서큐버스");
            Assert.AreEqual(1, built.Path[1].Defenders.Count, "r1(일반방)에 임프");
            Assert.IsTrue(built.Path[0].Defenders[0].IsCapturingMonster);
        }
```

- [ ] **Step 3: 컴파일/실패 확인**

Unity 리프레시 → `read_console` 컴파일 OK. `run_tests`(EditMode, `PlacementTests`). Expected: 신규 4 FAIL(`MonsterInTrapRoom`/`CapturerInNormalRoom` 미정의 또는 규칙 미구현), 수정한 `ValidateAndApply_ValidPlan...`도 FAIL.

- [ ] **Step 4: Placement.cs — 에러 코드 2종 추가**

`Placement.cs`의 `PlacementError` enum을 아래로 교체:

```csharp
    public enum PlacementError
    {
        None,
        OverBudget,
        UnknownMonster,
        InvalidRoom,
        HeroRoomNotCoreFront,
        MonsterInTrapRoom,      // 일반몹(비포획)을 함정방에 배치 시도
        CapturerInNormalRoom,   // 포획몹을 일반방에 배치 시도
    }
```

- [ ] **Step 5: PlacementValidator.cs — 교차검증 구현**

`PlacementValidator.Validate`를 아래로 교체(전체 메서드):

```csharp
        public static PlacementResult Validate(PlacementPlan plan, RoomGraph graph, IReadOnlyList<MonsterDef> catalog)
        {
            if (plan.Monsters == null) throw new System.ArgumentNullException(nameof(plan));
            if (graph == null) throw new System.ArgumentNullException(nameof(graph));
            if (catalog == null) throw new System.ArgumentNullException(nameof(catalog));

            var budget = graph.Rooms.Count * BudgetPerRoom;

            var defById = new Dictionary<UnitClassId, MonsterDef>(catalog.Count);
            foreach (var m in catalog) defById[m.Id] = m;
            var roomById = new Dictionary<RoomId, RoomNode>();
            foreach (var r in graph.Rooms) roomById[r.Id] = r;

            var total = 0;
            foreach (var p in plan.Monsters)
            {
                if (!defById.TryGetValue(p.Monster, out var def))
                    return new PlacementResult(false, budget, total, PlacementError.UnknownMonster);
                if (!roomById.TryGetValue(p.Room, out var room))
                    return new PlacementResult(false, budget, total, PlacementError.InvalidRoom);

                // 함정방=포획몹만, 일반방=일반몹만.
                if (room.HasTrap && !def.IsCapturingMonster)
                    return new PlacementResult(false, budget, total, PlacementError.MonsterInTrapRoom);
                if (!room.HasTrap && def.IsCapturingMonster)
                    return new PlacementResult(false, budget, total, PlacementError.CapturerInNormalRoom);

                total += def.Cost;
            }
            if (total > budget)
                return new PlacementResult(false, budget, total, PlacementError.OverBudget);

            if (plan.HasHero)
            {
                var coreFront = graph.CoreFrontRoom;
                if (coreFront == null || !plan.HeroRoom.Equals(coreFront.Id))
                    return new PlacementResult(false, budget, total, PlacementError.HeroRoomNotCoreFront);
            }
            return new PlacementResult(true, budget, total, PlacementError.None);
        }
```

- [ ] **Step 6: CampaignWaveRuleTests — 함정 픽스처 마이그레이션(HighDemon→Succubus)**

`CampaignWaveRuleTests.cs`의 `HeroPlusTrapDefenderPlan()`이 HighDemon(비포획)을 함정방 r0에 배치 → 이제 무효. 포획몹(Succubus, cost3, 예산9 이내)로 교체한다. 메서드를 아래로 교체:

```csharp
        // 서큐버스(포획몹, atk60, cost3)를 함정방 r0에 + 주인공을 r2(코어앞1칸)에 배치.
        // (포획몹은 함정방에만 배치 가능 — 새 배치규칙.) 함정방+포획몹 격퇴 → 포획.
        private static PlacementPlan HeroPlusTrapDefenderPlan()
            => new PlacementPlan
            {
                Monsters = new List<MonsterPlacement> { new MonsterPlacement { Room = new RoomId("r0"), Monster = MonsterIds.Succubus } },
                HasHero = true,
                HeroRoom = new RoomId("r2"),
            };
```

> **주의(다음 태스크 예고):** 이 픽스처의 포획 테스트들은 현재 `FixedThreat(10)`이다. Task 5에서 함정 데미지(기본 15)가 들어오면 threatBase=10 침입자(hp≈5..15)가 **함정에 선사(先死)** → 포획 실패한다. **Task 3에서는 threatBase를 바꾸지 말 것**(아직 함정 데미지 없음 → Succubus가 포획, 그린). threatBase·기대 약탈값 조정은 Task 5에서 수행한다.

- [ ] **Step 7: 컴파일 + 테스트**

Unity 리프레시 → `read_console` OK. `run_tests`(EditMode 전체). Expected: PlacementTests 신규 4 + 수정본 통과, CampaignWaveRuleTests 전부 통과(Succubus in 함정방 → OR 규칙으로 여전히 포획), 나머지 회귀 없음.

- [ ] **Step 8: 커밋**

```bash
git add Assets/Scripts/VNEngine/Core/Sim/Combat/Placement.cs Assets/Scripts/VNEngine/Core/Sim/Combat/PlacementValidator.cs Assets/Tests/Editor/PlacementTests.cs Assets/Tests/Editor/CampaignWaveRuleTests.cs
git commit -m "feat(sim): placement validator enforces trap-room=capturer-only, normal-room=non-capturer"
```

---

## Task 4: CaptureRule 조합 시맨틱 (Trap AND CapturingMonster) | HeroSubdue

포획을 "활성 트리거 아무거나(OR)"에서 "(함정 AND 포획몹) 또는 주인공제압"으로 바꾼다. 함정 단독·포획몹 단독은 포획 안 됨. 이 변경은 `CombatResolverTests`의 2개 포획 시나리오(비포획 Tank in 함정방 → 포획 기대 / 포획몹 without 함정 → 포획 기대)를 무효화하므로 같은 커밋에서 고친다. (아직 함정 데미지는 없음 → 선사 없음.)

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Combat/CaptureRule.cs`
- Test: `Assets/Tests/Editor/CaptureRuleTests.cs` (재작성)
- Test: `Assets/Tests/Editor/CombatResolverTests.cs` (2개 포획 시나리오 수정)

**Interfaces:**
- Produces: `CaptureRule.ShouldCapture(bool, CaptureContext)` — 포획 조건 = `canBeCaptured && ( (Trap&Enabled & CapturingMonster&Enabled 둘 다 present) || (HeroSubdue&Enabled present) )`.

- [ ] **Step 1: CaptureRuleTests 재작성(실패 테스트)**

`CaptureRuleTests.cs` 전체를 아래로 교체:

```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class CaptureRuleTests
    {
        private static CaptureContext Ctx(CaptureTrigger t) => new CaptureContext(t);

        [Test]
        public void TrapAndCapturingMonsterTogetherCapture()
        {
            Assert.IsTrue(CaptureRule.Default().ShouldCapture(true, Ctx(CaptureTrigger.Trap | CaptureTrigger.CapturingMonster)));
        }

        [Test]
        public void HeroSubdueAloneCaptures()
        {
            Assert.IsTrue(CaptureRule.Default().ShouldCapture(true, Ctx(CaptureTrigger.HeroSubdue)));
        }

        [Test]
        public void TrapAloneDoesNotCapture()
        {
            Assert.IsFalse(CaptureRule.Default().ShouldCapture(true, Ctx(CaptureTrigger.Trap)),
                "함정만으로는 포획 안 됨(초회차 안전판: 데미지만)");
        }

        [Test]
        public void CapturingMonsterAloneDoesNotCapture()
        {
            Assert.IsFalse(CaptureRule.Default().ShouldCapture(true, Ctx(CaptureTrigger.CapturingMonster)),
                "포획몹이라도 함정방이 아니면 포획 안 됨");
        }

        [Test]
        public void NonCapturableNeverCaptured()
        {
            Assert.IsFalse(CaptureRule.Default().ShouldCapture(false, Ctx(CaptureTrigger.Trap | CaptureTrigger.CapturingMonster)));
        }

        [Test]
        public void NoTriggerPresentMeansKilled()
        {
            Assert.IsFalse(CaptureRule.Default().ShouldCapture(true, Ctx(CaptureTrigger.None)));
        }

        [Test]
        public void DisabledTrapDisablesMonsterCaptureButSubdueStillWorks()
        {
            // Trap 비활성 → 함정+포획몹 조합도 포획 불가. HeroSubdue는 활성이라 유효.
            var rule = new CaptureRule(CaptureTrigger.HeroSubdue | CaptureTrigger.CapturingMonster);
            Assert.IsFalse(rule.ShouldCapture(true, Ctx(CaptureTrigger.Trap | CaptureTrigger.CapturingMonster)),
                "Trap 트리거 비활성 → 몹 포획 경로 차단");
            Assert.IsTrue(rule.ShouldCapture(true, Ctx(CaptureTrigger.HeroSubdue)));
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

`run_tests`(EditMode, `CaptureRuleTests`). Expected: `TrapAloneDoesNotCapture`, `CapturingMonsterAloneDoesNotCapture`, `DisabledTrap...` FAIL(현재 OR 규칙이라 Trap 단독으로도 포획).

- [ ] **Step 3: CaptureRule.cs 수정**

`CaptureRule.cs`의 클래스 본문을 아래로 교체(`CaptureTrigger`/`CaptureContext`는 유지):

```csharp
    // 포획 규칙(데이터 주도). 포획가능 개체가 다음 중 하나로 처치될 때 처치 대신 포획:
    //   (1) 함정방 AND 포획몹(둘 다 성립) — 함정방의 포획몹이 격퇴. (함정만/포획몹만으로는 포획 안 됨.)
    //   (2) 주인공 제압(HeroSubdue) — 코어앞1칸 유인 제압.
    // Enabled 마스크가 각 트리거의 활성 여부를 데이터로 통제(예: HeroSubdue 끄기, 몹포획경로 끄기).
    public sealed class CaptureRule
    {
        public CaptureTrigger Enabled { get; }
        public CaptureRule(CaptureTrigger enabled) { Enabled = enabled; }

        public static CaptureRule Default()
            => new CaptureRule(CaptureTrigger.Trap | CaptureTrigger.HeroSubdue | CaptureTrigger.CapturingMonster);

        public bool ShouldCapture(bool canBeCaptured, CaptureContext ctx)
        {
            if (!canBeCaptured) return false;
            var active = Enabled & ctx.Present; // 활성 트리거만 유효
            bool monsterCapture = (active & CaptureTrigger.Trap) != CaptureTrigger.None
                                  && (active & CaptureTrigger.CapturingMonster) != CaptureTrigger.None;
            bool subdueCapture = (active & CaptureTrigger.HeroSubdue) != CaptureTrigger.None;
            return monsterCapture || subdueCapture;
        }
    }
```

- [ ] **Step 4: CombatResolverTests — 무효화된 포획 시나리오 2개 수정**

(a) `CapturableIntruderFinishedInTrapRoomIsCaptured` — 방어몹이 **비포획 Tank**라 이제 포획 불가. 포획몹으로 교체하고, 함정 데미지가 아직 없으니 threatBase는 그대로 두되(선사 없음) 방어몹을 포획형으로:

```csharp
        [Test]
        public void CapturableIntruderFinishedInTrapRoomIsCaptured()
        {
            var capturable = ClassOf("Capturable", 100, 100, 100, canBeCaptured: true);
            var catalog = new List<UnitClassDef> { capturable };
            var wave = OneWave(capturable.Id, count: 1);
            // 함정방 + 포획형 방어몹(즉사 화력). 함정 AND 포획몹 → 포획.
            var succ = new Attacker(MonsterIds.Succubus, hp: 999, atk: 100000, def: 10, canBeCaptured: false, isCapturingMonster: true);
            var trapRoom = new RoomNode(new List<Attacker> { succ }, hasTrap: true);
            var rooms = new List<RoomNode> { trapRoom };
            var threatWeights = FixedThreat(10);

            var result = CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms, TrapConfig.None()), Stats(), StatCombatWeights.Default(), threatWeights, catalog, NeutralMatchup(), CaptureRule.Default(), dungeonLevel: 1, loopCount: 1, rng: new SeededRandom(2));

            Assert.AreEqual(1, result.Captured.Count);
            Assert.AreEqual(0, result.Killed.Count);
            Assert.IsFalse(result.CoreHit);
        }
```

> `TrapConfig.None()`을 명시해 이 테스트가 **함정 데미지 없이** 포획 트리거 조합만 검사하도록 고정한다(Task 5에서 함정 데미지가 켜져도 이 테스트 의미 불변).

(b) `CapturingMonsterFinishCapturesEvenWithoutTrap` — 이름·의미가 새 규칙과 반대. **일반방의 포획몹은 포획 못 함**을 검증하도록 재작성(rename):

```csharp
        [Test]
        public void CapturingMonsterWithoutTrapDoesNotCapture()
        {
            var capturable = ClassOf("Cap", 100, 100, 100, canBeCaptured: true);
            var catalog = new List<UnitClassDef> { capturable };
            var wave = OneWave(capturable.Id, 1);
            // 함정 없음 + 포획형 방어몹. 새 규칙: 함정 AND 포획몹이어야 포획 → 함정 없으면 처치.
            // (배치검증이라면 애초에 포획몹은 일반방에 못 두지만, 여기선 resolver 단독 거동을 직접 검증.)
            var succ = new Attacker(MonsterIds.Succubus, 999, 100000, 10, false, isCapturingMonster: true);
            var graph = RoomGraph.Linear(new List<RoomNode> { new RoomNode(new List<Attacker> { succ }, hasTrap: false) }, TrapConfig.None());

            var result = CombatResolver.ResolveWave(EmptyRun(), wave, graph, Stats(), StatCombatWeights.Default(),
                FixedThreat(10), catalog, NeutralMatchup(), CaptureRule.Default(), dungeonLevel: 1, loopCount: 1, rng: new SeededRandom(5));

            Assert.AreEqual(0, result.Captured.Count, "함정방이 아니면 포획몹도 포획 못 함");
            Assert.AreEqual(1, result.Killed.Count);
        }
```

- [ ] **Step 5: 컴파일 + 테스트**

Unity 리프레시 → `read_console` OK. `run_tests`(EditMode 전체). Expected: CaptureRuleTests 7 통과, 수정한 CombatResolverTests 2개 통과, 나머지 회귀 없음.

> 확인 포인트: 다른 CombatResolverTests의 함정방 케이스(`SameSeedSameInputs...`, `NonCapturableIntruderInTrapRoomIsStillKilled`, `ResolveWaveDoesNotMutateInputHeroStats`)는 방어몹이 비포획(Tank)이라 새 규칙에서도 처치(포획 없음) — 여전히 그린. CampaignWaveRuleTests는 Succubus in 함정방 → Trap+CapturingMonster → 포획 유지, 그린.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/VNEngine/Core/Sim/Combat/CaptureRule.cs Assets/Tests/Editor/CaptureRuleTests.cs Assets/Tests/Editor/CombatResolverTests.cs
git commit -m "feat(sim): capture requires (trap AND capturing-monster) or hero-subdue"
```

---

## Task 5: CombatResolver가 함정 데미지 적용(방어몹 전, 그래프 Trap에서)

`ResolveIntruder`가 함정방 진입 시 방어몹 루프 **전에** `graph.Trap.Damage`를 hp에 적용한다. 함정 단독 처치는 `Trap`-only 트리거 → 포획 없음(Killed). 시그니처 변경 없음(그래프에서 읽음). `RoomGraph.Linear`가 기본 `TrapConfig.Default()`이므로 **함정 데미지가 없던 기존 함정방 테스트들이 이제 데미지를 받는다** → 기대값이 뒤집히는 곳(특히 CampaignWaveRuleTests 포획 시나리오의 선사)을 이 태스크에서 재튜닝한다.

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Combat/CombatResolver.cs:130-177`
- Test: `Assets/Tests/Editor/CombatResolverTests.cs` (함정 데미지 테스트 추가 + 선사 케이스 점검)
- Test: `Assets/Tests/Editor/CampaignWaveRuleTests.cs` (threatBase·기대 약탈값 재튜닝)

**Interfaces:**
- Consumes: `RoomGraph.Trap.Damage` (Task 2), `CaptureRule` 조합 규칙 (Task 4).
- Produces: 함정방 진입 시 `hp -= graph.Trap.Damage`; 함정 선사 → `Killed`(Trap-only, 포획 없음). 방어몹이 마지막 타격 시 트리거 = `Trap`(함정방)`|CapturingMonster`(포획몹) → 조합 규칙으로 포획 판정.

- [ ] **Step 1: CombatResolverTests — 함정 데미지 실패 테스트 추가**

`CombatResolverTests.cs`에 아래 테스트 3개 추가:

```csharp
        [Test]
        public void TrapDamageAloneKillsWeakIntruderWithoutCapture()
        {
            // 함정방(포획몹 없음, 방어몹 0) + 포획가능 침입자. 함정 데미지만으로 즉사 → 처치(포획 아님).
            var capturable = ClassOf("Capturable", 100, 100, 100, canBeCaptured: true);
            var catalog = new List<UnitClassDef> { capturable };
            var wave = OneWave(capturable.Id, count: 1);
            // 가시함정 lvl1 = 15 데미지. threatBase=10 → hp≈5..15 → 함정에 즉사.
            var graph = RoomGraph.Linear(new List<RoomNode> { new RoomNode(new List<Attacker>(), hasTrap: true) }, TrapConfig.Default());

            var result = CombatResolver.ResolveWave(EmptyRun(), wave, graph, Stats(), StatCombatWeights.Default(),
                FixedThreat(10), catalog, NeutralMatchup(), CaptureRule.Default(), dungeonLevel: 1, loopCount: 1, rng: new SeededRandom(7));

            Assert.AreEqual(1, result.Killed.Count, "함정 단독 처치 = 데미지만, 포획 없음(초회차 안전판)");
            Assert.AreEqual(0, result.Captured.Count);
            Assert.IsFalse(result.CoreHit);
        }

        [Test]
        public void TrapDamageDoesNotKillStrongIntruderThenPassesThrough()
        {
            // 함정 15 < 침입자 hp(threatBase=50 → 45..55). 방어몹 없음 → 함정만으론 못 죽이고 무력한 주인공도 못 막음 → 코어 도달.
            var grunt = ClassOf("Grunt", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 1);
            var graph = RoomGraph.Linear(new List<RoomNode> { new RoomNode(new List<Attacker>(), hasTrap: true) }, TrapConfig.Default());

            var result = CombatResolver.ResolveWave(EmptyRun(), wave, graph, Stats(), StatCombatWeights.Default(),
                FixedThreat(50), catalog, NeutralMatchup(), CaptureRule.Default(), dungeonLevel: 1, loopCount: 1, rng: new SeededRandom(8));

            Assert.IsTrue(result.CoreHit, "함정 데미지가 hp보다 작으면 통과");
            Assert.AreEqual(0, result.Killed.Count);
        }

        [Test]
        public void StrongIntruderSurvivesTrapThenCapturedByCapturingMonster()
        {
            // 함정방 + 포획몹. 함정 15로는 못 죽는 강한 침입자(threatBase=50)를 포획몹이 격퇴 → 함정 AND 포획몹 → 포획.
            var capturable = ClassOf("Capturable", 100, 100, 100, canBeCaptured: true);
            var catalog = new List<UnitClassDef> { capturable };
            var wave = OneWave(capturable.Id, count: 1);
            var succ = new Attacker(MonsterIds.Succubus, hp: 999, atk: 100000, def: 10, canBeCaptured: false, isCapturingMonster: true);
            var graph = RoomGraph.Linear(new List<RoomNode> { new RoomNode(new List<Attacker> { succ }, hasTrap: true) }, TrapConfig.Default());

            var result = CombatResolver.ResolveWave(EmptyRun(), wave, graph, Stats(), StatCombatWeights.Default(),
                FixedThreat(50), catalog, NeutralMatchup(), CaptureRule.Default(), dungeonLevel: 1, loopCount: 1, rng: new SeededRandom(6));

            Assert.AreEqual(1, result.Captured.Count);
            Assert.AreEqual(0, result.Killed.Count);
        }
```

- [ ] **Step 2: 실패 확인**

`run_tests`(EditMode, `CombatResolverTests`). Expected: 위 3개 중 함정 데미지 의존(특히 `TrapDamageAloneKills...`, `StrongIntruderSurvivesTrap...`) FAIL(아직 함정 데미지 미적용 → 첫 케이스는 코어 도달, 셋째는 함정 없이 succ 즉사라 우연히 통과할 수도 있으나 첫째·둘째로 미적용을 확실히 잡는다).

- [ ] **Step 3: CombatResolver.cs — 함정 데미지 적용**

`CombatResolver.cs`에서 (a) `ResolveIntruder` 호출에 `graph.Trap`을 넘기고, (b) `ResolveIntruder`에 함정 데미지 블록을 추가한다.

(a) `ResolveWave` 내 89행 호출을 아래로 교체:

```csharp
                    var outcome = ResolveIntruder(attacker, graph.Path, graph.Trap, heroProfile, heroHitParams, matchup, captureRule, rng);
```

(b) `ResolveIntruder` 시그니처와 방순회 루프를 아래로 교체(전체 메서드):

```csharp
        // 침입자 1체가 방1 -> ... -> 코어앞1칸(주인공)까지 선형 통과하는 과정을 해결한다.
        // 함정방은 진입 시(방어몹 요격 전) trap.Damage 를 입힌다 — 함정 단독 처치는 Trap-only 트리거라 포획 없음.
        // HP 감소는 로컬 변수(hp)로만 추적 — 인자로 받은 attacker(및 rooms) 는 절대 변경하지 않는다. 함정 데미지는 rng 미사용(결정론 유지).
        private static IntruderResolution ResolveIntruder(
            Attacker attacker,
            IReadOnlyList<RoomNode> rooms,
            TrapConfig trap,
            HeroCombatProfile heroProfile,
            HitParams heroHitParams,
            ClassMatchup matchup,
            CaptureRule captureRule,
            IRandom rng)
        {
            var hp = attacker.Hp;

            foreach (var room in rooms)
            {
                // 함정 데미지(방어몹 요격 전, 방당 1회). 함정 단독 처치 = Trap-only → 포획 판정에 위임(조합규칙상 처치).
                if (room.HasTrap && trap.Damage > 0)
                {
                    hp -= trap.Damage;
                    if (hp <= 0)
                    {
                        var trapCaptured = captureRule.ShouldCapture(attacker.CanBeCaptured, new CaptureContext(CaptureTrigger.Trap));
                        return new IntruderResolution(
                            trapCaptured ? IntruderOutcome.Captured : IntruderOutcome.Killed,
                            attacker);
                    }
                }

                foreach (var defender in room.Defenders)
                {
                    var pct = matchup.Multiplier(defender.ClassId, attacker.ClassId);
                    var dmg = DamageFormula.Apply(defender.Atk, attacker.Def, pct);
                    hp -= dmg;

                    if (hp <= 0)
                    {
                        var trigger = CaptureTrigger.None;
                        if (room.HasTrap) trigger |= CaptureTrigger.Trap;
                        if (defender.IsCapturingMonster) trigger |= CaptureTrigger.CapturingMonster;
                        var captured = captureRule.ShouldCapture(attacker.CanBeCaptured, new CaptureContext(trigger));
                        return new IntruderResolution(
                            captured ? IntruderOutcome.Captured : IntruderOutcome.Killed,
                            attacker);
                    }
                }
            }

            // 코어앞1칸: 주인공 단독 요격(1회 타격만 — 생존시 코어 도달).
            var heroDmg = DamageFormula.Resolve(heroProfile.PhysicalAttack, attacker.Def, HeroMatchupPct, heroHitParams, rng);
            hp -= heroDmg;

            if (hp <= 0)
            {
                var captured = captureRule.ShouldCapture(attacker.CanBeCaptured, new CaptureContext(CaptureTrigger.HeroSubdue));
                return new IntruderResolution(captured ? IntruderOutcome.Captured : IntruderOutcome.Killed, attacker);
            }

            return new IntruderResolution(IntruderOutcome.ReachedCore, attacker);
        }
```

- [ ] **Step 4: CampaignWaveRuleTests — 포획 시나리오 재튜닝(선사 방지)**

함정방 r0 + Succubus 픽스처에 이제 함정 데미지(15)가 들어간다. 포획 테스트들이 `FixedThreat(10)`(hp≈5..15)이면 함정에 선사 → 포획 실패. **threatBase를 50으로 올려**(hp≈45..55, 함정15 생존 → Succubus 포획) 기대 약탈값을 갱신한다.

수정 대상(각 테스트의 `FixedThreat(10)` → `FixedThreat(50)`, 그리고 기대 약탈/카르마 인자 `10` → `50`):

- `ResolveWave_CapturesCreditHalfLootPlusKarma`:
  - `var threatWeights = FixedThreat(10);` → `FixedThreat(50);`
  - `var expectedGold = 3 * LootRule.LootGold(10, true);` → `LootGold(50, true);`
  - `var expectedKarma = 3 * LootRule.CaptureKarma(10);` → `CaptureKarma(50);`
- `ResolveWave_AccumulatesCaptivesIntoRun`: `FixedThreat(10)` → `FixedThreat(50)` (포로 수만 검증 — 값 무관하나 선사 방지 위해 상향).
- `ResolveWave_Deterministic`: `FixedThreat(10)` → `FixedThreat(50)` (양쪽 동일 시드 비교 — 포획 경로가 실제로 돌도록 상향).

> `ResolveWave_InvalidPlacement_Throws`는 배치 무효 검증이라 무관(변경 없음). Kills 계열(`ThreeEmptyRooms`, 함정 없음)은 함정 데미지 무영향 — 변경 없음.

- [ ] **Step 5: 컴파일 + 전체 테스트**

Unity 리프레시 → `read_console` OK. `run_tests`(EditMode 전체). Expected: 신규 함정 데미지 테스트 3 통과, CampaignWaveRuleTests 포획 3종 통과(재튜닝), 전체 회귀 없음.

> 점검: `CombatResolverTests.SameSeedSameInputsProduceIdenticalCombatResult`는 함정방 + Tank(비포획) hp999. 이제 함정 데미지 15가 들어가지만 결정론(a==b)만 검증하므로 통과. `NonCapturableIntruderInTrapRoomIsStillKilled`(threatBase10, 함정15)는 함정 선사로 Killed — 여전히 통과. `ResolveWaveDoesNotMutateInputHeroStats`(threatBase30, 함정15)는 불변성만 검증 — 통과.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/VNEngine/Core/Sim/Combat/CombatResolver.cs Assets/Tests/Editor/CombatResolverTests.cs Assets/Tests/Editor/CampaignWaveRuleTests.cs
git commit -m "feat(sim): trap rooms deal graph.Trap damage before defenders (trap-only kill = no capture)"
```

---

## Task 6: Unity 데모 픽스처 정정(SimController / PlacementSmokeBootstrap)

두 데모가 **HighDemon(비포획)을 함정방 r0에 배치**한다 → 새 배치검증에서 `MonsterInTrapRoom`으로 런타임 throw. 포획몹(Succubus)을 함정방에 두도록 시나리오를 정정하고, 함정 데미지(15)에 침입자가 선사하지 않도록 threatBase를 상향한다. EditMode 대상 아님 → **컴파일(read_console) + Play 스모크**로 확인.

**Files:**
- Modify: `Assets/Scripts/VNEngine/Unity/Sim/SimController.cs:142-159, 235`
- Modify: `Assets/Scripts/VNEngine/Unity/Demo/PlacementSmokeBootstrap.cs:69, 156-168` 및 관련 시나리오

**Interfaces:**
- Consumes: 새 배치규칙(포획몹=함정방), 함정 데미지(graph.Trap 기본값).

- [ ] **Step 1: SimController 고정 픽스처 정정**

`SimController.cs:149-157`의 `_dayPlan`에서 함정방 r0의 방어몹을 HighDemon(비포획)에서 **Succubus(포획)**로 교체. `MonsterPlacement` 라인을 아래로:

```csharp
                    new MonsterPlacement { Room = new RoomId("r0"), Monster = MonsterIds.Succubus },
```

그리고 `_dayThreatWeights`(159행)의 baseOffset을 함정 선사 방지 + 포획 시연을 위해 상향:

```csharp
            _dayThreatWeights = new ThreatWeights(wHero: 0, wLoop: 0, wPlaced: 0, wDungeon: 0, baseOffset: 55);
```

주석(15, 144, 235행)의 "HighDemon 함정방"을 "Succubus(포획) 함정방"으로 정정한다. (문구만 — 동작 무관.)

> 근거: 광신도(zealot, 포획가능)가 함정방 r0의 Succubus에게 격퇴 → 함정 AND 포획몹 → 포획. 제국병(비포획)은 처치. baseOffset=55는 함정15 생존 + Succucus(atk60) 격퇴가 성립하는 값(PlacementSmoke 튜닝과 동일 계열).

- [ ] **Step 2: PlacementSmokeBootstrap 정정**

`PlacementSmokeBootstrap.cs:69`는 방1(i==0)을 함정방으로 만든다. 새 규칙상 **함정방엔 포획몹만** 배치 가능하다. 스모크 UI는 유저가 임의 배치하므로, over-budget처럼 배치검증 에러(`MonsterInTrapRoom`/`CapturerInNormalRoom`)를 상단 메시지로 노출해야 한다. 최소 변경:

1. `OnRunWave`(163행)의 `CampaignWaveRule.ResolveWave` 호출은 시그니처 불변(그래프에서 Trap 읽음) → **호출부 변경 없음**. 단, `MakePlan(_plan)`이 무효 배치(예: 함정방에 일반몹)면 `ValidateAndApply`가 throw → 기존 try/catch가 잡아 메시지 표시하는지 확인하고, 아니면 catch에서 `VnRuntimeException` 메시지를 상단 상태줄에 표시하도록 보강.
2. 안내 문구(방1 헤더/함정방 표기)를 "함정방 = 포획몹(서큐버스)만"으로 정정.
3. 초기 데모 배치가 있다면 함정방 r0에는 Succucus만 놓이도록 조정. threatBase(threatBaseOffset 노브)는 기존 55 유지(선사 방지 확인).

> 구현 주의: PlacementSmokeBootstrap의 정확한 배치 편집 로직은 파일을 읽고(69행 주변, `Render`, `OnPlaceInRoom`) 함정방-포획몹 제약을 UI 레벨에서 안내하도록 최소 보강한다. 코어 규칙은 이미 검증이 강제하므로, UI는 "거부 메시지 표시"만 확실히 하면 된다.

- [ ] **Step 3: 컴파일 확인**

Unity 리프레시 → `read_console`. Expected: 에러 0.

- [ ] **Step 4: Play 스모크 — SimSlice / PlacementSmoke**

- `SimSlice.unity`(SimController) Play → "웨이브 실행": 처치(제국병) + 포획(광신도)이 실제로 발생하는지 결과줄 확인. 함정 선사로 전원 처치만 나오면 baseOffset을 더 올린다(예: 60~70).
- `PlacementSmoke.unity` Play → 함정방(방1)에 일반몹 배치 시 거부 메시지, Succucus 배치 시 허용 + "웨이브 실행"으로 포획 발생 확인.
- 스크린샷을 `Assets/Screenshots/`에 저장(선택).

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/VNEngine/Unity/Sim/SimController.cs Assets/Scripts/VNEngine/Unity/Demo/PlacementSmokeBootstrap.cs
git commit -m "fix(sim-demo): trap rooms hold capturers (Succubus) not non-capturers; tune threatBase for trap survival"
```

---

## Self-Review

**1. Spec coverage(유저 확정 규칙 대조):**
- "함정방=데미지" → Task 1(TrapRule) + Task 5(CombatResolver 적용). ✓
- "함정방=포획몹만, 일반방=일반몹만" → Task 3(PlacementValidator). ✓
- "포획몹은 함정방에서만 작동, 격퇴 시 포획" → Task 4(조합 규칙) + Task 5(트리거). ✓
- "함정만 = 데미지만, 포획 없음(안전판)" → Task 5(Trap-only 처치). ✓
- "함정 데미지 = TrapBase + TrapLevel*perLevel, TrapDef 종류·TrapLevel 골격 확장형, 지금 1종·lvl1" → Task 1. ✓
- 가챠/종류별 확률/named 식별 → **스코프 밖**(T2/T3 후속). 계획에 미포함(의도적).

**2. Placeholder scan:** 없음 — 모든 코드 스텝에 완전한 코드. Task 6 Step 2만 "파일 읽고 최소 보강" 서술형(Unity UI 코드는 파일 의존) — 코어 규칙은 이미 강제되므로 UI는 거부 메시지 노출만 필요.

**3. Type consistency:** `TrapConfig`(Def/Level/Damage), `TrapDef`(Id/DisplayName/Base/PerLevel), `TrapRule.Damage(def,level)`, `RoomGraph.Trap`, `PlacementError.{MonsterInTrapRoom,CapturerInNormalRoom}`, `CaptureRule.ShouldCapture` 조합 — 태스크 간 이름 일치 확인됨. `RoomGraph.Linear(rooms, TrapConfig)` 오버로드 시그니처 Task 2 정의 → Task 4/5 테스트에서 동일 사용. ✓

**미결/리스크:**
- Task 6의 threatBase 값(55)은 Play로 검증해 조정(플레이스홀더). EditMode 무관.
- `TrapConfig.None()`의 private 무인자 생성자는 Damage=0 강제용 — Def는 기본종으로 채우되 Damage만 0(문서화됨).
- 세이브: `TrapConfig`는 런타임 config(그래프 속성, 세이브 안 함) — 세이브 스키마 변경 없음. `SaveCaptureTests` 무영향.
