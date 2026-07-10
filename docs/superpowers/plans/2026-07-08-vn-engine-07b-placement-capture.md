# 07-B 배치 예산제 & 방 그래프 & 포획 정식화 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A2 전투 코어 위에 방 그래프 구조·몹 배치 예산제·던전레벨 방상한·포획 조건 정식화(네임드/잡졸 구분)를 얹어, 07-C 인과율 수급이 의존할 "무엇이 포획되나"를 실체화한다.

**Architecture:** 전부 순수 C#(`Assets/Scripts/VNEngine/Core/Sim/**`, UnityEngine·System.IO 미참조). A2가 선형 순회하던 `IReadOnlyList<RoomNode>`를 `RoomGraph`(그래프, 선형은 특수케이스)로 승격하고, `CombatResolver`가 그래프 경로를 순회하도록 재배선. 배치는 데이터 주도(`MonsterDef.Cost`, 예산=현재방수×3)로 검증·적용하며, 포획은 인라인 `CanBeCaptured && HasTrap`를 데이터 주도 `CaptureRule`로 정식화한다. 상태는 불변 — 모든 Resolve/Rule은 순수 함수.

**Tech Stack:** C# (Unity 2021+ EditMode/NUnit), 기존 `VNEngine` 네임스페이스, `IRandom`/`SeededRandom` 결정론 난수, `VNEngine.Core.asmdef`(하위폴더 재귀 포함).

## Global Constraints

- **순수 코어:** `Core/Sim/**`의 신규 타입은 `UnityEngine`·`System.IO`를 참조하지 않는다(기존 Combat 코드와 동일).
- **불변 상태:** 모든 Resolve/Rule/Validator는 입력을 변형하지 않고 새 값/결과를 반환한다. 컬렉션은 생성자에서 방어적 복사.
- **데이터 주도:** 코스트·상한·예산계수·포획 트리거는 전부 데이터/규칙 객체로. 매직넘버·테마 문자열 하드코딩 금지(상수는 명명된 `const`로 규칙 타입 안에).
- **정수 결정론:** 부동소수 금지. 난수는 `IRandom` 경유, 호출 순서 고정(같은 시드+같은 입력 → 같은 결과).
- **네임스페이스:** 프로덕션 타입은 `namespace VNEngine`, 테스트는 `namespace VNEngine.Tests`.
- **파일 배치:** 전투/배치/포획 = `Assets/Scripts/VNEngine/Core/Sim/Combat/`, 던전레벨 규칙 = `Assets/Scripts/VNEngine/Core/Sim/Economy/`(신규 폴더, asmdef 재귀 포함). 테스트 = `Assets/Tests/Editor/`.
- **컴파일 확인:** 각 태스크 후 UnityMCP `read_console`로 컴파일 에러 0 확인 후 `run_tests`(EditMode). Unity 에디터가 켜져 있어야 함.
- **커밋:** 각 태스크 종료 시 커밋. 메시지 말미에 프로젝트 규약(Co-Authored-By / Claude-Session) 부착. 무관 워킹트리 파일(docs/engine/*, Fonts, gemini-*.md 등) 미커밋 유지.

## Design Resolutions (프롬프트 "확정" 위에서 코드 간극을 해소한 판단 — 리뷰 게이트에서 확인)

1. **MonsterDef.Id = `UnitClassId`** (별도 `MonsterId` 타입 안 만듦). 몹의 Id가 곧 `ClassMatchup`의 방어측 병종 키가 됨(잡졸/모험가 상성 등록은 후속 튜닝, 미등록쌍은 중립100). 몹 전용 id 문자열("Imp" 등)을 `MonsterIds` 상수로.
2. **코어는 암묵적**(그래프에 별도 코어 노드 없음). 전투 경로(`RoomGraph.Path`)의 **마지막 노드 = 코어앞1칸**(주인공 요격 지점, A2 모델 그대로). 분기 노드에서는 `NextRooms[0]`을 따라가 결정론적 단일 경로.
3. **주인공 배치 제약(엄격 해석):** 주인공 배치룸은 `graph.CoreFrontRoom`(경로 마지막 방)이어야만 유효. 그 외 방 지정 시 거부 → 검증 규칙 "코어앞1칸 외 배치 거부"를 그대로 실현.
4. **예산 = `graph.Rooms.Count × 3`**(현재 열린 방 전체 기준, 주인공 방 포함). 방 늘리면 예산 증가.
5. **포획 트리거 3종 전부 실동작:** Trap(방 함정), HeroSubdue(코어앞 주인공 처치), CapturingMonster(마지막 타격 몹이 포획형=서큐버스류). CapturingMonster는 `MonsterDef.IsCapturingMonster`→방어 `Attacker.IsCapturingMonster`로 전달돼 리졸버가 실제로 판정.
6. **RunState에 `Captives` 추가**(런 귀속, 기본 빈 리스트). 2-arg `RunState(day,resources)` 생성자 유지 → `CampaignSave`/기존 테스트 무회귀. **포로의 세이브 직렬화는 이 슬라이스 비스코프**(07-C/이후) — 인메모리 배선까지만.
7. **DungeonLevel은 이 슬라이스에서도 전달 파라미터**(리졸버 인자/규칙 입력)로 유지. `MetaState`에 필드 추가·라이브 시딩은 A2와 동일하게 미룸.

---

### Task 1: 방 그래프 (RoomId · RoomGraph · RoomNode 확장 · CombatResolver 재배선)

A2의 선형 `IReadOnlyList<RoomNode>` 순회를 `RoomGraph`(그래프, 선형=특수케이스) 순회로 승격. 리졸버 시그니처가 바뀌므로 A2 CombatResolverTests를 `RoomGraph.Linear(...)`로 마이그레이션(무회귀 확인).

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/RoomId.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/RoomGraph.cs`
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Combat/RoomNode.cs` (Id·NextRooms 추가, 2-arg 생성자 유지)
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Combat/CombatResolver.cs` (`IReadOnlyList<RoomNode> rooms` → `RoomGraph graph`, 내부는 `graph.Path` 선형 순회)
- Modify: `Assets/Tests/Editor/CombatResolverTests.cs` (모든 `rooms`/`new List<RoomNode>` → `RoomGraph.Linear(...)`)
- Test: `Assets/Tests/Editor/RoomGraphTests.cs` (신규)

**Interfaces:**
- Produces:
  - `readonly struct RoomId : IEquatable<RoomId> { string Value; }` (UnitClassId와 동일 패턴: 값동등성, `==`/`!=`, `GetHashCode`, `ToString`).
  - `RoomNode(RoomId id, IReadOnlyList<Attacker> defenders, bool hasTrap, IReadOnlyList<RoomId> nextRooms)` (전체 생성자) + `RoomNode(IReadOnlyList<Attacker> defenders, bool hasTrap)` (편의: Id=default, NextRooms=빈리스트). 프로퍼티: `RoomId Id`, `IReadOnlyList<Attacker> Defenders`, `bool HasTrap`, `IReadOnlyList<RoomId> NextRooms`.
  - `RoomGraph`: `IReadOnlyList<RoomNode> Rooms`, `RoomId Entry`, `bool IsEmpty`, `IReadOnlyList<RoomNode> Path`(entry→terminal, `NextRooms[0]` 추종), `RoomNode CoreFrontRoom`(Path 마지막 or null), `static RoomGraph Linear(IReadOnlyList<RoomNode> contentRooms)`.
- Consumes: `Attacker`(A2, 불변 struct).

- [ ] **Step 1: RoomId 실패 테스트 작성** — `RoomGraphTests.cs`에 값동등성 확인.

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class RoomGraphTests
    {
        private static RoomNode Content(bool hasTrap, params Attacker[] defs)
            => new RoomNode(new List<Attacker>(defs), hasTrap);

        [Test]
        public void RoomIdEqualityByValue()
        {
            Assert.AreEqual(new RoomId("a"), new RoomId("a"));
            Assert.AreNotEqual(new RoomId("a"), new RoomId("b"));
            Assert.IsTrue(new RoomId("a") == new RoomId("a"));
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — Run(UnitMCP run_tests, filter RoomGraphTests): FAIL(RoomId 미정의/컴파일 에러).

- [ ] **Step 3: RoomId 구현** — `RoomId.cs`. `UnitClassId.cs`를 그대로 본떠 작성(string 래핑, `IEquatable<RoomId>`, `==`/`!=`/`GetHashCode`/`ToString`).

- [ ] **Step 4: RoomNode 확장 구현** — 아래로 교체.

```csharp
using System;
using System.Collections.Generic;

namespace VNEngine
{
    // 던전 경로상의 방 노드(불변 스냅샷). 배치된 방어몹(Attacker) + 함정 플래그 + 그래프 링크(NextRooms).
    // 선형 배치는 NextRooms가 1개인 특수케이스. 배치/코스트/포획 규칙은 07-B의 다른 타입이 담당.
    public sealed class RoomNode
    {
        public RoomId Id { get; }
        public IReadOnlyList<Attacker> Defenders { get; }
        public bool HasTrap { get; }
        public IReadOnlyList<RoomId> NextRooms { get; }

        public RoomNode(RoomId id, IReadOnlyList<Attacker> defenders, bool hasTrap, IReadOnlyList<RoomId> nextRooms)
        {
            if (defenders == null) throw new ArgumentNullException(nameof(defenders));
            if (nextRooms == null) throw new ArgumentNullException(nameof(nextRooms));
            Id = id;
            Defenders = new List<Attacker>(defenders);       // 방어적 복사
            HasTrap = hasTrap;
            NextRooms = new List<RoomId>(nextRooms);         // 방어적 복사
        }

        // 편의 생성자: 방 "내용물"만(그래프 링크 없음). RoomGraph.Linear가 Id/NextRooms를 재구성한다.
        public RoomNode(IReadOnlyList<Attacker> defenders, bool hasTrap)
            : this(default, defenders, hasTrap, System.Array.Empty<RoomId>()) { }
    }
}
```

- [ ] **Step 5: RoomGraph 실패 테스트 추가** — 선형·분기·빈그래프 순회.

```csharp
        [Test]
        public void LinearGraphPathMatchesInputOrder()
        {
            var r0 = Content(false, new Attacker(UnitClassIds.Tank, 10, 1, 1, false));
            var r1 = Content(true);
            var graph = RoomGraph.Linear(new List<RoomNode> { r0, r1 });

            Assert.AreEqual(2, graph.Path.Count);
            Assert.IsFalse(graph.Path[0].HasTrap);
            Assert.IsTrue(graph.Path[1].HasTrap);
            Assert.AreEqual(graph.Path[1].Id, graph.CoreFrontRoom.Id, "코어앞1칸 = 경로 마지막 방");
        }

        [Test]
        public void EmptyLinearGraphHasEmptyPathAndNullCoreFront()
        {
            var graph = RoomGraph.Linear(new List<RoomNode>());
            Assert.IsTrue(graph.IsEmpty);
            Assert.AreEqual(0, graph.Path.Count);
            Assert.IsNull(graph.CoreFrontRoom);
        }

        [Test]
        public void BranchingGraphFollowsFirstNextDeterministically()
        {
            // entry -> {a, b}; a -> (terminal), b -> (terminal). NextRooms[0]=a 를 따라감.
            var entry = new RoomNode(new RoomId("entry"), new List<Attacker>(), false,
                new List<RoomId> { new RoomId("a"), new RoomId("b") });
            var a = new RoomNode(new RoomId("a"), new List<Attacker>(), true, new List<RoomId>());
            var b = new RoomNode(new RoomId("b"), new List<Attacker>(), false, new List<RoomId>());
            var graph = new RoomGraph(new List<RoomNode> { entry, a, b }, new RoomId("entry"));

            var path = graph.Path;
            Assert.AreEqual(2, path.Count);
            Assert.AreEqual(new RoomId("entry"), path[0].Id);
            Assert.AreEqual(new RoomId("a"), path[1].Id, "분기에서 NextRooms[0] 추종");
            Assert.IsTrue(path[1].HasTrap);
        }

        [Test]
        public void GraphConstructorRejectsUnknownNextRoom()
        {
            var entry = new RoomNode(new RoomId("entry"), new List<Attacker>(), false,
                new List<RoomId> { new RoomId("missing") });
            Assert.Throws<System.ArgumentException>(
                () => new RoomGraph(new List<RoomNode> { entry }, new RoomId("entry")));
        }
```

- [ ] **Step 6: 실패 확인** — Run(RoomGraphTests): FAIL(RoomGraph 미정의).

- [ ] **Step 7: RoomGraph 구현** — `RoomGraph.cs`.

```csharp
using System;
using System.Collections.Generic;

namespace VNEngine
{
    // 방 노드의 그래프. 전투 경로(Path)는 Entry에서 NextRooms[0]을 따라 terminal(NextRooms 빈 방)까지.
    // 선형 던전은 각 방 NextRooms가 1개인 특수케이스. 코어는 암묵적 — Path 마지막 방이 "코어앞1칸".
    public sealed class RoomGraph
    {
        public IReadOnlyList<RoomNode> Rooms { get; }
        public RoomId Entry { get; }
        public IReadOnlyList<RoomNode> Path { get; }   // 생성 시 1회 계산(불변)
        public bool IsEmpty => Rooms.Count == 0;
        public RoomNode CoreFrontRoom => Path.Count == 0 ? null : Path[Path.Count - 1];

        public RoomGraph(IReadOnlyList<RoomNode> rooms, RoomId entry)
        {
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));
            var copy = new List<RoomNode>(rooms);
            Rooms = copy;
            Entry = entry;

            var byId = new Dictionary<RoomId, RoomNode>(copy.Count);
            foreach (var r in copy)
            {
                if (r == null) throw new ArgumentException("rooms must not contain null", nameof(rooms));
                byId[r.Id] = r;
                foreach (var nx in r.NextRooms) { /* 존재검증은 아래 별도 루프에서(전체 등록 후) */ }
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
            return new RoomGraph(nodes, contentRooms.Count == 0 ? default : new RoomId("r0"));
        }
    }
}
```

- [ ] **Step 8: 실패 확인** — Run(RoomGraphTests): PASS.

- [ ] **Step 9: CombatResolver 재배선** — `ResolveWave`의 `IReadOnlyList<RoomNode> rooms` 파라미터를 `RoomGraph graph`로 교체(위치·순서 동일). 본문에서 `if (rooms == null)` → `if (graph == null)`, `ResolveIntruder(...)`에 넘기던 `rooms`를 `graph.Path`로 교체. `ResolveIntruder` 시그니처의 `IReadOnlyList<RoomNode> rooms`는 그대로 두고 호출부만 `graph.Path` 전달. 나머지 로직·주석·상수 불변.

```csharp
// 시그니처(발췌):
public static CombatResult ResolveWave(
    RunState run, WaveDef wave, RoomGraph graph, HeroStats hero,
    StatCombatWeights statWeights, ThreatWeights threatWeights,
    IReadOnlyList<UnitClassDef> classCatalog, ClassMatchup matchup,
    int dungeonLevel, int loopCount, IRandom rng)
// ...
if (graph == null) throw new ArgumentNullException(nameof(graph));
// ...
var outcome = ResolveIntruder(attacker, graph.Path, heroProfile, heroHitParams, matchup, rng);
```

- [ ] **Step 10: A2 CombatResolverTests 마이그레이션** — `CombatResolverTests.cs`에서 각 `rooms` 지역변수(`new List<RoomNode>{...}` 또는 `new List<RoomNode>()`)를 그대로 `RoomGraph.Linear(...)`로 감싸 전달. 예:
  - `var rooms = new List<RoomNode> { room };` 호출부 → `CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), ...)`.
  - `var rooms = new List<RoomNode>();` → `RoomGraph.Linear(rooms)`.
  - `NullArgumentsThrowArgumentNullException`의 `rooms`(null 인자) 케이스: `RoomGraph` null 전달로 교체 — `ResolveWave(EmptyRun(), wave, null, hero, ...)`가 `ArgumentNullException` 던지는지 확인(파라미터명 `graph`).

- [ ] **Step 11: 전체 EditMode 실행** — Run(all): 302 → 신규 포함 전건 PASS(A2 전투 결과 무회귀 확인). read_console 컴파일 에러 0.

- [ ] **Step 12: 커밋** — `feat(sim): room graph + resolver traversal (07-B task1)`.

---

### Task 2: MonsterDef · MonsterCatalog · Attacker.IsCapturingMonster

방어측 몹 카탈로그(코스트·기본능력치·포획형 플래그)를 데이터로 도입하고, 포획형 몹 식별을 위해 `Attacker`에 방어측 플래그 `IsCapturingMonster`를 추가.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/MonsterDef.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/MonsterCatalog.cs` (`MonsterIds` + `MonsterCatalog.Default()`)
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Combat/Attacker.cs` (IsCapturingMonster 추가, 5-arg 편의 생성자 유지)
- Test: `Assets/Tests/Editor/MonsterCatalogTests.cs` (신규)

**Interfaces:**
- Produces:
  - `MonsterDef { UnitClassId Id; string DisplayName; int Cost; int BaseHp; int BaseAtk; int BaseDef; bool IsCapturingMonster; }` (불변, 생성자 가드: Cost≥1, Base*≥1, Id.Value 비어있지 않음).
  - `MonsterIds` 정적 상수: Imp/Goblin/Orc/HighOrc/Succubus/DeathKnight/HighDemon (각 `UnitClassId`).
  - `MonsterCatalog.Default() : IReadOnlyList<MonsterDef>` (7종).
  - `Attacker`에 `bool IsCapturingMonster` 프로퍼티 + `Attacker(classId, hp, atk, def, canBeCaptured, isCapturingMonster)` 전체 생성자. `Attacker(classId, hp, atk, def, canBeCaptured)` 편의(→ isCapturingMonster=false).
- Consumes: `UnitClassId`(A2).

- [ ] **Step 1: 실패 테스트 작성** — `MonsterCatalogTests.cs`.

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class MonsterCatalogTests
    {
        private static Dictionary<UnitClassId, MonsterDef> ById()
        {
            var d = new Dictionary<UnitClassId, MonsterDef>();
            foreach (var m in MonsterCatalog.Default()) d[m.Id] = m;
            return d;
        }

        [Test]
        public void DefaultCatalogHasSevenMonstersWithSpecifiedCosts()
        {
            var by = ById();
            Assert.AreEqual(7, by.Count);
            Assert.AreEqual(1, by[MonsterIds.Imp].Cost);
            Assert.AreEqual(1, by[MonsterIds.Goblin].Cost);
            Assert.AreEqual(2, by[MonsterIds.Orc].Cost);
            Assert.AreEqual(3, by[MonsterIds.HighOrc].Cost);
            Assert.AreEqual(3, by[MonsterIds.Succubus].Cost);
            Assert.AreEqual(4, by[MonsterIds.DeathKnight].Cost);
            Assert.AreEqual(5, by[MonsterIds.HighDemon].Cost);
        }

        [Test]
        public void SuccubusIsCapturingMonsterOthersAreNot()
        {
            var by = ById();
            Assert.IsTrue(by[MonsterIds.Succubus].IsCapturingMonster);
            Assert.IsFalse(by[MonsterIds.Imp].IsCapturingMonster);
            Assert.IsFalse(by[MonsterIds.DeathKnight].IsCapturingMonster);
        }

        [Test]
        public void MonsterDefRejectsNonPositiveCost()
        {
            Assert.Throws<System.ArgumentException>(
                () => new MonsterDef(new UnitClassId("X"), "X", 0, 10, 10, 10, false));
        }

        [Test]
        public void AttackerCarriesCapturingMonsterFlagWithBackCompatDefault()
        {
            var plain = new Attacker(MonsterIds.Imp, 10, 5, 5, false);
            Assert.IsFalse(plain.IsCapturingMonster, "5-arg 편의 생성자는 false 기본");
            var cap = new Attacker(MonsterIds.Succubus, 10, 5, 5, false, isCapturingMonster: true);
            Assert.IsTrue(cap.IsCapturingMonster);
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — Run(MonsterCatalogTests): FAIL(미정의).

- [ ] **Step 3: Attacker 확장 구현** — `IsCapturingMonster` 추가.

```csharp
namespace VNEngine
{
    public readonly struct Attacker
    {
        public UnitClassId ClassId { get; }
        public int Hp { get; }
        public int Atk { get; }
        public int Def { get; }
        public bool CanBeCaptured { get; }
        public bool IsCapturingMonster { get; }   // 방어측 몹이 포획형(서큐버스류)인가. 침입자는 항상 false.

        public Attacker(UnitClassId classId, int hp, int atk, int def, bool canBeCaptured, bool isCapturingMonster)
        {
            ClassId = classId; Hp = hp; Atk = atk; Def = def;
            CanBeCaptured = canBeCaptured; IsCapturingMonster = isCapturingMonster;
        }

        // 편의: 포획형 아님(기존 호출부·침입자 기본).
        public Attacker(UnitClassId classId, int hp, int atk, int def, bool canBeCaptured)
            : this(classId, hp, atk, def, canBeCaptured, false) { }
    }
}
```

- [ ] **Step 4: MonsterDef 구현** — `MonsterDef.cs`. 생성자 가드: `Id.Value` 비어있지 않음, `Cost≥1`, `BaseHp/BaseAtk/BaseDef≥1`(전부 `ArgumentException`). 프로퍼티 7개.

- [ ] **Step 5: MonsterCatalog 구현** — `MonsterCatalog.cs`. `MonsterIds`(7 상수) + `Default()`(초기 추정 튜닝값). 클래스 상단 주석에 "값은 구조검증용 초기 추정, 플레이테스트 실측 튜닝" 명시.

```csharp
using System.Collections.Generic;

namespace VNEngine
{
    // 방어측 몹 id 상수. **소스는 MonsterCatalog 데이터 테이블**(이 상수는 참조 편의).
    public static class MonsterIds
    {
        public static readonly UnitClassId Imp = new UnitClassId("Imp");
        public static readonly UnitClassId Goblin = new UnitClassId("Goblin");
        public static readonly UnitClassId Orc = new UnitClassId("Orc");
        public static readonly UnitClassId HighOrc = new UnitClassId("HighOrc");
        public static readonly UnitClassId Succubus = new UnitClassId("Succubus");
        public static readonly UnitClassId DeathKnight = new UnitClassId("DeathKnight");
        public static readonly UnitClassId HighDemon = new UnitClassId("HighDemon");
    }

    // 1편 방어몹 데이터. Cost=레어도, Base*=배치시 방어 Attacker 능력치(초기 추정 — 실측 튜닝 대상).
    // Succubus만 포획형(마지막 타격 시 포획 트리거 — 아그네스 해금 훅). Skills[]는 08 스코프.
    public static class MonsterCatalog
    {
        public static IReadOnlyList<MonsterDef> Default() => new List<MonsterDef>
        {
            new MonsterDef(MonsterIds.Imp,         "임프",     1,  30,  15, 10, false),
            new MonsterDef(MonsterIds.Goblin,      "고블린",   1,  35,  18, 12, false),
            new MonsterDef(MonsterIds.Orc,         "오크",     2,  70,  35, 25, false),
            new MonsterDef(MonsterIds.HighOrc,     "하이오크", 3, 110,  55, 40, false),
            new MonsterDef(MonsterIds.Succubus,    "서큐버스", 3,  80,  60, 30, true),
            new MonsterDef(MonsterIds.DeathKnight, "데스나이트", 4, 160,  90, 70, false),
            new MonsterDef(MonsterIds.HighDemon,   "고위마족", 5, 220, 130, 90, false),
        };
    }
}
```

- [ ] **Step 6: 통과 확인 + 무회귀** — Run(all): 신규 PASS. 기존 Attacker 사용처(A2 리졸버/테스트)는 5-arg 편의 생성자로 그대로 컴파일. read_console 에러 0.

- [ ] **Step 7: 커밋** — `feat(sim): monster catalog + cost + capturing-monster flag (07-B task2)`.

---

### Task 3: 배치 예산제 (PlacementPlan · PlacementValidator · PlacementBuilder)

예산 = 현재방수×3, 코스트합 검증, 주인공 코어앞1칸 제약. 검증 통과한 플랜을 방어 Attacker로 실체화(그래프에 배치).

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/Placement.cs` (`MonsterPlacement`, `PlacementPlan`, `PlacementError`, `PlacementResult`)
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/PlacementValidator.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/PlacementBuilder.cs`
- Test: `Assets/Tests/Editor/PlacementTests.cs` (신규)

**Interfaces:**
- Consumes: `RoomGraph`/`RoomId`/`RoomNode`(T1), `MonsterDef`/`MonsterCatalog`/`MonsterIds`(T2), `Attacker`(T2).
- Produces:
  - `struct MonsterPlacement { RoomId Room; UnitClassId Monster; }` (값타입, 공개필드).
  - `PlacementPlan { IReadOnlyList<MonsterPlacement> Monsters; bool HasHero; RoomId HeroRoom; }` (HeroRoom은 HasHero=true일 때만 유효 — nullable 회피 위해 bool 플래그).
  - `enum PlacementError { None, OverBudget, UnknownMonster, InvalidRoom, HeroRoomNotCoreFront }`.
  - `readonly struct PlacementResult { bool IsValid; int Budget; int TotalCost; PlacementError Error; }`.
  - `PlacementValidator.Validate(PlacementPlan plan, RoomGraph graph, IReadOnlyList<MonsterDef> catalog) : PlacementResult`. 상수 `const int BudgetPerRoom = 3;` (규칙 타입 안).
  - `PlacementBuilder.Apply(PlacementPlan plan, RoomGraph graph, IReadOnlyList<MonsterDef> catalog) : RoomGraph` — 각 방의 Defenders를 배치된 몹(MonsterDef→Attacker)으로 채운 새 그래프. (검증은 호출자 책임 — Apply는 유효 플랜 가정, 알수없는 몹/방은 `VnRuntimeException`.)

- [ ] **Step 1: 실패 테스트 작성** — `PlacementTests.cs`.

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class PlacementTests
    {
        // 방 3개 선형 그래프(r0,r1,r2). CoreFront = r2.
        private static RoomGraph ThreeRooms()
            => RoomGraph.Linear(new List<RoomNode>
            {
                new RoomNode(new List<Attacker>(), false),
                new RoomNode(new List<Attacker>(), false),
                new RoomNode(new List<Attacker>(), false),
            });

        private static IReadOnlyList<MonsterDef> Cat() => MonsterCatalog.Default();

        private static MonsterPlacement Place(string room, UnitClassId m)
            => new MonsterPlacement { Room = new RoomId(room), Monster = m };

        private static PlacementPlan Plan(params MonsterPlacement[] ms)
            => new PlacementPlan { Monsters = new List<MonsterPlacement>(ms), HasHero = false };

        [Test]
        public void BudgetIsRoomCountTimesThree()
        {
            var r = PlacementValidator.Validate(Plan(), ThreeRooms(), Cat());
            Assert.AreEqual(9, r.Budget);          // 3방 × 3
            Assert.IsTrue(r.IsValid);
        }

        [Test]
        public void MoreRoomsIncreasesBudget()
        {
            var four = RoomGraph.Linear(new List<RoomNode>
            {
                new RoomNode(new List<Attacker>(), false), new RoomNode(new List<Attacker>(), false),
                new RoomNode(new List<Attacker>(), false), new RoomNode(new List<Attacker>(), false),
            });
            Assert.AreEqual(12, PlacementValidator.Validate(Plan(), four, Cat()).Budget);
        }

        [Test]
        public void CostSumWithinBudgetIsValid()
        {
            // 서큐(3)+데스나이트(4)+오크(2)=9 == 예산9.
            var plan = Plan(Place("r0", MonsterIds.Succubus), Place("r1", MonsterIds.DeathKnight), Place("r2", MonsterIds.Orc));
            var r = PlacementValidator.Validate(plan, ThreeRooms(), Cat());
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual(9, r.TotalCost);
            Assert.AreEqual(PlacementError.None, r.Error);
        }

        [Test]
        public void CostSumOverBudgetIsRejected()
        {
            // 고위마족(5)+데스나이트(4)+오크(2)=11 > 9.
            var plan = Plan(Place("r0", MonsterIds.HighDemon), Place("r1", MonsterIds.DeathKnight), Place("r2", MonsterIds.Orc));
            var r = PlacementValidator.Validate(plan, ThreeRooms(), Cat());
            Assert.IsFalse(r.IsValid);
            Assert.AreEqual(PlacementError.OverBudget, r.Error);
        }

        [Test]
        public void UnknownMonsterIsRejected()
        {
            var plan = Plan(Place("r0", new UnitClassId("Dragon")));
            Assert.AreEqual(PlacementError.UnknownMonster,
                PlacementValidator.Validate(plan, ThreeRooms(), Cat()).Error);
        }

        [Test]
        public void PlacementInUnknownRoomIsRejected()
        {
            var plan = Plan(Place("nope", MonsterIds.Imp));
            Assert.AreEqual(PlacementError.InvalidRoom,
                PlacementValidator.Validate(plan, ThreeRooms(), Cat()).Error);
        }

        [Test]
        public void HeroInCoreFrontRoomIsValid()
        {
            var plan = new PlacementPlan { Monsters = new List<MonsterPlacement>(), HasHero = true, HeroRoom = new RoomId("r2") };
            Assert.IsTrue(PlacementValidator.Validate(plan, ThreeRooms(), Cat()).IsValid);
        }

        [Test]
        public void HeroOutsideCoreFrontRoomIsRejected()
        {
            var plan = new PlacementPlan { Monsters = new List<MonsterPlacement>(), HasHero = true, HeroRoom = new RoomId("r0") };
            Assert.AreEqual(PlacementError.HeroRoomNotCoreFront,
                PlacementValidator.Validate(plan, ThreeRooms(), Cat()).Error);
        }

        [Test]
        public void ApplyPlacesDefendersIntoRooms()
        {
            var plan = Plan(Place("r0", MonsterIds.Imp), Place("r0", MonsterIds.Goblin), Place("r2", MonsterIds.Succubus));
            var built = PlacementBuilder.Apply(plan, ThreeRooms(), Cat());
            Assert.AreEqual(2, built.Path[0].Defenders.Count, "r0에 임프+고블린");
            Assert.AreEqual(0, built.Path[1].Defenders.Count);
            Assert.AreEqual(1, built.Path[2].Defenders.Count);
            Assert.IsTrue(built.Path[2].Defenders[0].IsCapturingMonster, "서큐버스는 포획형 플래그 전달");
            Assert.AreEqual(MonsterIds.Succubus, built.Path[2].Defenders[0].ClassId);
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — Run(PlacementTests): FAIL(미정의).

- [ ] **Step 3: Placement 타입 구현** — `Placement.cs` (MonsterPlacement/PlacementPlan/PlacementError/PlacementResult). `PlacementResult`는 readonly struct.

- [ ] **Step 4: PlacementValidator 구현** — `PlacementValidator.cs`.

```csharp
using System.Collections.Generic;

namespace VNEngine
{
    // 배치 예산제 검증(순수 쿼리). 예산 = 방수 × BudgetPerRoom. 몹 코스트합 ≤ 예산 + 개체/방 유효성 + 주인공 코어앞1칸.
    // UI 게이트용이라 예외가 아니라 PlacementResult(Error 코드)로 반환.
    public static class PlacementValidator
    {
        public const int BudgetPerRoom = 3;

        public static PlacementResult Validate(PlacementPlan plan, RoomGraph graph, IReadOnlyList<MonsterDef> catalog)
        {
            if (plan.Monsters == null) throw new System.ArgumentNullException(nameof(plan));
            if (graph == null) throw new System.ArgumentNullException(nameof(graph));
            if (catalog == null) throw new System.ArgumentNullException(nameof(catalog));

            var budget = graph.Rooms.Count * BudgetPerRoom;

            var costById = new Dictionary<UnitClassId, int>(catalog.Count);
            foreach (var m in catalog) costById[m.Id] = m.Cost;
            var roomIds = new HashSet<RoomId>();
            foreach (var r in graph.Rooms) roomIds.Add(r.Id);

            var total = 0;
            foreach (var p in plan.Monsters)
            {
                if (!costById.TryGetValue(p.Monster, out var cost))
                    return new PlacementResult(false, budget, total, PlacementError.UnknownMonster);
                if (!roomIds.Contains(p.Room))
                    return new PlacementResult(false, budget, total, PlacementError.InvalidRoom);
                total += cost;
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
    }
}
```

- [ ] **Step 5: PlacementBuilder 구현** — `PlacementBuilder.cs`. 방별 배치 몹을 모아 MonsterDef→Attacker(BaseHp/Atk/Def, canBeCaptured:false, isCapturingMonster:def.IsCapturingMonster)로 변환, 원 그래프의 링크(Id/HasTrap/NextRooms)를 보존한 새 RoomNode 목록으로 `new RoomGraph(...)` 반환. 알수없는 몹/방은 `VnRuntimeException`.

```csharp
using System.Collections.Generic;

namespace VNEngine
{
    // 검증 통과한 PlacementPlan을 실제 방어 배치(그래프의 각 방 Defenders)로 실체화. 순수 함수(새 그래프 반환).
    public static class PlacementBuilder
    {
        public static RoomGraph Apply(PlacementPlan plan, RoomGraph graph, IReadOnlyList<MonsterDef> catalog)
        {
            if (plan.Monsters == null) throw new System.ArgumentNullException(nameof(plan));
            if (graph == null) throw new System.ArgumentNullException(nameof(graph));
            if (catalog == null) throw new System.ArgumentNullException(nameof(catalog));

            var defById = new Dictionary<UnitClassId, MonsterDef>(catalog.Count);
            foreach (var m in catalog) defById[m.Id] = m;

            var perRoom = new Dictionary<RoomId, List<Attacker>>();
            foreach (var p in plan.Monsters)
            {
                if (!defById.TryGetValue(p.Monster, out var def))
                    throw new VnRuntimeException($"Unknown monster in placement: {p.Monster}");
                if (!perRoom.TryGetValue(p.Room, out var list)) { list = new List<Attacker>(); perRoom[p.Room] = list; }
                list.Add(new Attacker(def.Id, def.BaseHp, def.BaseAtk, def.BaseDef, false, def.IsCapturingMonster));
            }

            var rebuilt = new List<RoomNode>(graph.Rooms.Count);
            foreach (var room in graph.Rooms)
            {
                var defs = perRoom.TryGetValue(room.Id, out var placed) ? placed : new List<Attacker>();
                rebuilt.Add(new RoomNode(room.Id, defs, room.HasTrap, room.NextRooms));
            }
            return new RoomGraph(rebuilt, graph.Entry);
        }
    }
}
```

- [ ] **Step 6: 통과 확인** — Run(all): PlacementTests PASS + 무회귀. read_console 에러 0.

- [ ] **Step 7: 커밋** — `feat(sim): placement budget validator + builder (07-B task3)`.

---

### Task 4: 던전레벨 → 방 상한 (DungeonRoomRule)

`rooms_cap = 3 + dungeonLevel*2`, 건설 가능 여부(천장 안에서). 골드 소모는 07-C — 여기선 상한 규칙만.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Economy/DungeonRoomRule.cs`
- Test: `Assets/Tests/Editor/DungeonRoomRuleTests.cs` (신규)

**Interfaces:**
- Produces: `DungeonRoomRule.RoomsCap(int dungeonLevel) : int` (= `BaseRooms + dungeonLevel*RoomsPerLevel`, 상수 `BaseRooms=3`, `RoomsPerLevel=2`), `DungeonRoomRule.CanBuildRoom(int currentRoomCount, int dungeonLevel) : bool` (= `currentRoomCount < RoomsCap(dl)`). `dungeonLevel<1`이면 `ArgumentOutOfRangeException`.

- [ ] **Step 1: 실패 테스트 작성** — `DungeonRoomRuleTests.cs`.

```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class DungeonRoomRuleTests
    {
        [Test]
        public void RoomsCapFollowsCurve()
        {
            Assert.AreEqual(5, DungeonRoomRule.RoomsCap(1)); // 3 + 1*2
            Assert.AreEqual(7, DungeonRoomRule.RoomsCap(2)); // 3 + 2*2
            Assert.AreEqual(9, DungeonRoomRule.RoomsCap(3));
        }

        [Test]
        public void CanBuildRoomOnlyBelowCap()
        {
            Assert.IsTrue(DungeonRoomRule.CanBuildRoom(4, 1), "cap5, 현재4 → 건설 가능");
            Assert.IsFalse(DungeonRoomRule.CanBuildRoom(5, 1), "cap5, 현재5 → 천장");
        }

        [Test]
        public void NonPositiveDungeonLevelThrows()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DungeonRoomRule.RoomsCap(0));
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — Run(DungeonRoomRuleTests): FAIL.

- [ ] **Step 3: 구현** — `DungeonRoomRule.cs`. 클래스 주석에 "상한과 실제 건설 분리 — 골드 소모는 07-C" 명시.

```csharp
namespace VNEngine
{
    // 던전레벨 = 지을 수 있는 방 개수 천장. 실제 건설(골드 소모)은 07-C — 여기선 상한 규칙만(순수).
    public static class DungeonRoomRule
    {
        public const int BaseRooms = 3;
        public const int RoomsPerLevel = 2;

        public static int RoomsCap(int dungeonLevel)
        {
            if (dungeonLevel < 1) throw new System.ArgumentOutOfRangeException(nameof(dungeonLevel));
            return BaseRooms + dungeonLevel * RoomsPerLevel;
        }

        public static bool CanBuildRoom(int currentRoomCount, int dungeonLevel)
            => currentRoomCount < RoomsCap(dungeonLevel);
    }
}
```

- [ ] **Step 4: 통과 확인** — Run(all): PASS. read_console 에러 0.

- [ ] **Step 5: 커밋** — `feat(sim): dungeon-level room cap rule (07-B task4)`.

---

### Task 5: 포획 조건 정식화 (CaptureRule) + 리졸버 재배선

인라인 `CanBeCaptured && HasTrap`를 데이터 주도 `CaptureRule`(Trap/HeroSubdue/CapturingMonster 트리거)로 승격. 리졸버가 처치 시점의 컨텍스트를 만들어 규칙에 위임.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/CaptureRule.cs` (`CaptureTrigger`[Flags], `CaptureContext`, `CaptureRule`)
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Combat/CombatResolver.cs` (포획 판정을 `CaptureRule`로; `ResolveWave`에 `CaptureRule captureRule` 파라미터 추가)
- Modify: `Assets/Tests/Editor/CombatResolverTests.cs` (호출부에 `CaptureRule.Default()` 인자 추가; 신규 CapturingMonster/HeroSubdue 테스트)
- Test: `Assets/Tests/Editor/CaptureRuleTests.cs` (신규)

**Interfaces:**
- Produces:
  - `[Flags] enum CaptureTrigger { None=0, Trap=1, HeroSubdue=2, CapturingMonster=4 }`.
  - `readonly struct CaptureContext { CaptureTrigger Present; }` (처치 시점에 존재하는 트리거 비트).
  - `CaptureRule { CaptureTrigger Enabled; bool ShouldCapture(bool canBeCaptured, CaptureContext ctx); static CaptureRule Default(); }` — Default는 세 트리거 전부 Enabled. `ShouldCapture = canBeCaptured && (Enabled & ctx.Present) != 0`.
- Consumes: `Attacker.CanBeCaptured`/`IsCapturingMonster`(T2), `RoomNode.HasTrap`, 리졸버 내부 흐름(T1).

- [ ] **Step 1: CaptureRule 실패 테스트 작성** — `CaptureRuleTests.cs`.

```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class CaptureRuleTests
    {
        private static CaptureContext Ctx(CaptureTrigger t) => new CaptureContext(t);

        [Test]
        public void CapturableWithAnyEnabledTriggerIsCaptured()
        {
            var rule = CaptureRule.Default();
            Assert.IsTrue(rule.ShouldCapture(true, Ctx(CaptureTrigger.Trap)));
            Assert.IsTrue(rule.ShouldCapture(true, Ctx(CaptureTrigger.HeroSubdue)));
            Assert.IsTrue(rule.ShouldCapture(true, Ctx(CaptureTrigger.CapturingMonster)));
        }

        [Test]
        public void NonCapturableNeverCaptured()
        {
            Assert.IsFalse(CaptureRule.Default().ShouldCapture(false, Ctx(CaptureTrigger.Trap)));
        }

        [Test]
        public void NoTriggerPresentMeansKilled()
        {
            Assert.IsFalse(CaptureRule.Default().ShouldCapture(true, Ctx(CaptureTrigger.None)));
        }

        [Test]
        public void DisabledTriggerDoesNotCapture()
        {
            var trapOnly = new CaptureRule(CaptureTrigger.Trap);
            Assert.IsFalse(trapOnly.ShouldCapture(true, Ctx(CaptureTrigger.HeroSubdue)), "HeroSubdue 비활성 → 포획 안 됨");
            Assert.IsTrue(trapOnly.ShouldCapture(true, Ctx(CaptureTrigger.Trap)));
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — Run(CaptureRuleTests): FAIL.

- [ ] **Step 3: CaptureRule 구현** — `CaptureRule.cs`.

```csharp
using System;

namespace VNEngine
{
    // 포획 트리거(비트플래그). 처치의 "마지막 타격"이 어떤 조건에서 났는지.
    [Flags]
    public enum CaptureTrigger { None = 0, Trap = 1, HeroSubdue = 2, CapturingMonster = 4 }

    // 처치 시점에 존재한 트리거 집합.
    public readonly struct CaptureContext
    {
        public CaptureTrigger Present { get; }
        public CaptureContext(CaptureTrigger present) { Present = present; }
    }

    // 포획 규칙(데이터 주도). 포획가능 개체가 활성 트리거 중 하나로 처치되면 처치 대신 포획.
    // 1편 Default = Trap/HeroSubdue/CapturingMonster 전부 활성(예시 트리거 — 실측/서사로 조정).
    public sealed class CaptureRule
    {
        public CaptureTrigger Enabled { get; }
        public CaptureRule(CaptureTrigger enabled) { Enabled = enabled; }

        public static CaptureRule Default()
            => new CaptureRule(CaptureTrigger.Trap | CaptureTrigger.HeroSubdue | CaptureTrigger.CapturingMonster);

        public bool ShouldCapture(bool canBeCaptured, CaptureContext ctx)
            => canBeCaptured && (Enabled & ctx.Present) != CaptureTrigger.None;
    }
}
```

- [ ] **Step 4: 실패 확인** — Run(CaptureRuleTests): PASS.

- [ ] **Step 5: 리졸버 재배선 실패 테스트** — `CombatResolverTests.cs`에 헬퍼 시그니처를 `CaptureRule` 인자 받도록 갱신하고, 서큐버스(포획형) 방어몹이 포획가능 침입자를 처치하면 함정 없이도 포획됨을 검증하는 신규 테스트 추가.

```csharp
        [Test]
        public void CapturingMonsterFinishCapturesEvenWithoutTrap()
        {
            var capturable = ClassOf("Cap", 100, 100, 100, canBeCaptured: true);
            var catalog = new List<UnitClassDef> { capturable };
            var wave = OneWave(capturable.Id, 1);
            // 함정 없음, 방어몹은 포획형(서큐버스류) + 즉사 화력.
            var succ = new Attacker(MonsterIds.Succubus, 999, 100000, 10, false, isCapturingMonster: true);
            var graph = RoomGraph.Linear(new List<RoomNode> { new RoomNode(new List<Attacker> { succ }, hasTrap: false) });

            var result = CombatResolver.ResolveWave(EmptyRun(), wave, graph, Stats(), StatCombatWeights.Default(),
                FixedThreat(10), catalog, NeutralMatchup(), CaptureRule.Default(), dungeonLevel: 1, loopCount: 1, rng: new SeededRandom(5));

            Assert.AreEqual(1, result.Captured.Count, "포획형 몹의 마지막 타격 → 함정 없이도 포획");
            Assert.AreEqual(0, result.Killed.Count);
        }
```

Note: 기존 A2 리졸버 테스트 전부(약 13개) 호출부에 `CaptureRule.Default()` 인자 삽입(위치는 `matchup` 다음, `dungeonLevel` 앞 — 아래 Step 6 시그니처 참조). null-가드 테스트에는 `captureRule=null` 케이스 1줄 추가.

- [ ] **Step 6: 리졸버 구현** — `ResolveWave` 시그니처에 `CaptureRule captureRule` 추가(`matchup` 다음), null 가드 추가. 내부 `ResolveIntruder`가 포획 판정을 `CaptureRule`로 위임.

```csharp
// 시그니처(발췌):
public static CombatResult ResolveWave(
    RunState run, WaveDef wave, RoomGraph graph, HeroStats hero,
    StatCombatWeights statWeights, ThreatWeights threatWeights,
    IReadOnlyList<UnitClassDef> classCatalog, ClassMatchup matchup,
    CaptureRule captureRule, int dungeonLevel, int loopCount, IRandom rng)
{
    // ... 기존 가드 ...
    if (captureRule == null) throw new ArgumentNullException(nameof(captureRule));
    // ... heroProfile/threatBase/heroHitParams 동일 ...
    // ResolveIntruder 호출에 captureRule 전달:
    var outcome = ResolveIntruder(attacker, graph.Path, heroProfile, heroHitParams, matchup, captureRule, rng);
}

// ResolveIntruder(방어몹 처치 분기):
if (hp <= 0)
{
    var trigger = CaptureTrigger.None;
    if (room.HasTrap) trigger |= CaptureTrigger.Trap;
    if (defender.IsCapturingMonster) trigger |= CaptureTrigger.CapturingMonster;
    var captured = captureRule.ShouldCapture(attacker.CanBeCaptured, new CaptureContext(trigger));
    return new IntruderResolution(captured ? IntruderOutcome.Captured : IntruderOutcome.Killed, attacker);
}

// 주인공 코어앞 처치 분기:
if (hp <= 0)
{
    var captured = captureRule.ShouldCapture(attacker.CanBeCaptured, new CaptureContext(CaptureTrigger.HeroSubdue));
    return new IntruderResolution(captured ? IntruderOutcome.Captured : IntruderOutcome.Killed, attacker);
}
```

주의: 기존 A2 포획 테스트(`CapturableIntruderFinishedInTrapRoomIsCaptured` 등)는 Trap 트리거로 그대로 통과(무회귀). 단 `EmptyRoomsStrongHeroKillsBeforeCore`는 주인공 처치인데 침입자 `canBeCaptured:false`라 여전히 Killed(무회귀). `canBeCaptured:true`인 주인공 처치 케이스가 A2에 없었으므로 HeroSubdue로 인한 포획은 신규 동작 — 이는 문서 §4.5 "심부유인+주인공 제압"의 의도된 실현(무회귀 아님, 신규 기능). A2 테스트 중 이에 걸리는 것이 없는지 Step 7에서 확인.

- [ ] **Step 7: 전체 실행 + 무회귀 확인** — Run(all): 신규 PASS + A2 전건 PASS. 만약 A2 테스트가 HeroSubdue 신규포획으로 깨지면(포획가능 침입자가 주인공에게 처치되는 기존 케이스), 해당 테스트의 기대값을 "신규 포획 동작"에 맞게 갱신하고 주석으로 사유 표기. read_console 에러 0.

- [ ] **Step 8: 커밋** — `feat(sim): formalize capture into data-driven CaptureRule (07-B task5)`.

---

### Task 6: 네임드/잡졸 구분 + RunState.Captives 배선

침입자 네임드 플래그, 포로 원장(Captive/ResetPolicy), RunState에 Captives 누적. 07-C 인과율 수급의 전제(네임드=히로인 훅, 잡졸=감옥 경제).

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Combat/Attacker.cs` (IsNamed 추가, 생성자 확장)
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Combat/WaveDef.cs` (`Entry.IsNamed` 추가)
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Combat/AttackerFactory.cs` (`Create(cls, threatBase, isNamed, rng)`)
- Modify: `Assets/Scripts/VNEngine/Core/Sim/Combat/CombatResolver.cs` (`AttackerFactory.Create`에 `entry.IsNamed` 전달)
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/Captive.cs` (`ResetPolicy` enum, `Captive` struct)
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/CaptiveLedger.cs`
- Modify: `Assets/Scripts/VNEngine/Core/Sim/RunState.cs` (Captives 추가, 2-arg 생성자 유지)
- Modify: `Assets/Tests/Editor/CombatResolverTests.cs` (AttackerFactory 호출부가 있으면 갱신; 없으면 무변경)
- Test: `Assets/Tests/Editor/CaptiveLedgerTests.cs`, `Assets/Tests/Editor/AttackerFactoryTests.cs`(네임드 케이스 추가), `Assets/Tests/Editor/RunStateTests.cs`(Captives 기본 빈 확인)

**Interfaces:**
- Produces:
  - `Attacker`에 `bool IsNamed`. 전체 생성자 `Attacker(classId, hp, atk, def, canBeCaptured, isCapturingMonster, isNamed)`; 6-arg(T2)는 `isNamed=false`, 5-arg는 `isCapturingMonster=false, isNamed=false`.
  - `WaveDef.Entry { UnitClassId ClassId; int Count; bool IsNamed; }`.
  - `AttackerFactory.Create(UnitClassDef cls, int threatBase, bool isNamed, IRandom rng)` (호출순서 HP→ATK→DEF 불변, isNamed는 rng 미소비).
  - `enum ResetPolicy { Unspecified, ResetEachLoop, PersistAcrossLoops }` (플래그 자리만 — 로직 강제 안 함, 기본 Unspecified).
  - `readonly struct Captive { UnitClassId ClassId; bool IsNamed; ResetPolicy ResetPolicy; }`.
  - `CaptiveLedger.Accumulate(RunState run, CombatResult result) : RunState` — result.Captured를 Captive(ResetPolicy=Unspecified)로 변환해 run.Captives 뒤에 append한 새 RunState.
  - `RunState`에 `IReadOnlyList<Captive> Captives`. 생성자 `RunState(int day, IReadOnlyDictionary<string,int> resources)`(=빈 Captives) + `RunState(int day, IReadOnlyDictionary<string,int> resources, IReadOnlyList<Captive> captives)`.

- [ ] **Step 1: 실패 테스트 작성** — `CaptiveLedgerTests.cs`.

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class CaptiveLedgerTests
    {
        private static RunState EmptyRun() => new RunState(1, new Dictionary<string, int>());

        [Test]
        public void AccumulateSplitsNamedAndMobIntoCaptives()
        {
            var named = new Attacker(new UnitClassId("Martha"), 10, 10, 10, canBeCaptured: true, isCapturingMonster: false, isNamed: true);
            var mob = new Attacker(new UnitClassId("Grunt"), 10, 10, 10, canBeCaptured: true, isCapturingMonster: false, isNamed: false);
            var result = new CombatResult(false, new List<Attacker>(), new List<Attacker> { named, mob });

            var run2 = CaptiveLedger.Accumulate(EmptyRun(), result);

            Assert.AreEqual(2, run2.Captives.Count);
            var byName = new Dictionary<string, bool>();
            foreach (var c in run2.Captives) byName[c.ClassId.Value] = c.IsNamed;
            Assert.IsTrue(byName["Martha"], "네임드 플래그 보존");
            Assert.IsFalse(byName["Grunt"], "잡졸은 IsNamed=false");
        }

        [Test]
        public void AccumulateAppendsToExistingCaptives()
        {
            var first = CaptiveLedger.Accumulate(EmptyRun(),
                new CombatResult(false, new List<Attacker>(), new List<Attacker> { new Attacker(new UnitClassId("A"), 1, 1, 1, true) }));
            var second = CaptiveLedger.Accumulate(first,
                new CombatResult(false, new List<Attacker>(), new List<Attacker> { new Attacker(new UnitClassId("B"), 1, 1, 1, true) }));
            Assert.AreEqual(2, second.Captives.Count);
        }

        [Test]
        public void AccumulateDoesNotMutateInputRun()
        {
            var run = EmptyRun();
            CaptiveLedger.Accumulate(run,
                new CombatResult(false, new List<Attacker>(), new List<Attacker> { new Attacker(new UnitClassId("A"), 1, 1, 1, true) }));
            Assert.AreEqual(0, run.Captives.Count, "원본 RunState 불변");
        }

        [Test]
        public void CaptiveDefaultResetPolicyIsUnspecified()
        {
            var run = CaptiveLedger.Accumulate(EmptyRun(),
                new CombatResult(false, new List<Attacker>(), new List<Attacker> { new Attacker(new UnitClassId("A"), 1, 1, 1, true, false, true) }));
            Assert.AreEqual(ResetPolicy.Unspecified, run.Captives[0].ResetPolicy, "리셋정책은 플래그 자리만(미결)");
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — Run(CaptiveLedgerTests): FAIL.

- [ ] **Step 3: Attacker.IsNamed 추가** — 전체 생성자 3-단(7-arg 전체 / 6-arg→isNamed=false / 5-arg→둘 다 false). `IsNamed` 프로퍼티.

```csharp
public Attacker(UnitClassId classId, int hp, int atk, int def, bool canBeCaptured, bool isCapturingMonster, bool isNamed)
{ ClassId=classId; Hp=hp; Atk=atk; Def=def; CanBeCaptured=canBeCaptured; IsCapturingMonster=isCapturingMonster; IsNamed=isNamed; }

public Attacker(UnitClassId classId, int hp, int atk, int def, bool canBeCaptured, bool isCapturingMonster)
    : this(classId, hp, atk, def, canBeCaptured, isCapturingMonster, false) { }

public Attacker(UnitClassId classId, int hp, int atk, int def, bool canBeCaptured)
    : this(classId, hp, atk, def, canBeCaptured, false, false) { }
```

- [ ] **Step 4: Captive/ResetPolicy 구현** — `Captive.cs`. `Captive` readonly struct(ClassId/IsNamed/ResetPolicy), 생성자에서 ResetPolicy 지정(기본 Unspecified 오버로드 or 명시). 주석: "ResetPolicy는 서사 미확정 — 플래그 자리만, 로직 강제 금지".

- [ ] **Step 5: RunState.Captives 추가** — 2-arg 생성자 보존.

```csharp
using System.Collections.Generic;

namespace VNEngine
{
    public sealed class RunState
    {
        public int Day { get; }
        public IReadOnlyDictionary<string, int> Resources { get; }
        public IReadOnlyList<Captive> Captives { get; }

        public RunState(int day, IReadOnlyDictionary<string, int> resources)
            : this(day, resources, System.Array.Empty<Captive>()) { }

        public RunState(int day, IReadOnlyDictionary<string, int> resources, IReadOnlyList<Captive> captives)
        {
            Day = day;
            var copy = new Dictionary<string, int>(resources.Count);
            foreach (var kv in resources) copy[kv.Key] = kv.Value; // 방어적 복사
            Resources = copy;
            Captives = new List<Captive>(captives ?? System.Array.Empty<Captive>()); // 방어적 복사
        }
    }
}
```

- [ ] **Step 6: CaptiveLedger 구현** — `CaptiveLedger.cs`. result.Captured → Captive(c.ClassId, c.IsNamed, ResetPolicy.Unspecified) 변환, run.Captives 복사 후 append, `new RunState(run.Day, run.Resources, merged)` 반환. 순수 함수(입력 불변).

- [ ] **Step 7: WaveDef.Entry.IsNamed + AttackerFactory + 리졸버 배선** —
  - `WaveDef.Entry`에 `bool IsNamed;` 필드 추가(기본 false — 기존 initializer는 미지정 시 false).
  - `AttackerFactory.Create(cls, threatBase, isNamed, rng)`로 시그니처 확장. HP/ATK/DEF 산출(rng 3콜) 후 `new Attacker(cls.Id, hp, atk, def, cls.CanBeCaptured, false, isNamed)` 반환(방어측 아님 → isCapturingMonster=false). isNamed는 rng 미소비(결정론 순서 불변).
  - `CombatResolver`의 `AttackerFactory.Create(cls, threatBase, rng)` 호출을 `AttackerFactory.Create(cls, threatBase, entry.IsNamed, rng)`로.

- [ ] **Step 8: AttackerFactoryTests 네임드 케이스 추가** — `Create(..., isNamed:true, ...)` → `attacker.IsNamed==true`, `isNamed:false` → false. 기존 결정론 테스트가 있으면 `isNamed:false`로 인자 보정(rng 순서 무변경 확인).

```csharp
        [Test]
        public void CreatePropagatesNamedFlagWithoutConsumingRng()
        {
            var cls = new UnitClassDef(new UnitClassId("N"), "N", 100, 100, 100, true);
            var a = AttackerFactory.Create(cls, 30, isNamed: true, rng: new SeededRandom(1));
            var b = AttackerFactory.Create(cls, 30, isNamed: false, rng: new SeededRandom(1));
            Assert.IsTrue(a.IsNamed);
            Assert.IsFalse(b.IsNamed);
            Assert.AreEqual(a.Hp, b.Hp, "isNamed는 능력치/rng에 영향 없음");
            Assert.AreEqual(a.Atk, b.Atk);
            Assert.AreEqual(a.Def, b.Def);
        }
```

- [ ] **Step 9: RunStateTests 보강** — 기존 2-arg 생성자 테스트 유지 + Captives 기본 빈 확인 1건.

```csharp
        [Test]
        public void CaptivesDefaultsToEmpty()
        {
            var run = new RunState(1, new Dictionary<string, int>());
            Assert.AreEqual(0, run.Captives.Count);
        }
```

- [ ] **Step 10: 전체 실행 + 무회귀** — Run(all): 신규 PASS + 전건 PASS. `CampaignSave`(2-arg RunState 사용)·`LoopEngine` 무회귀 확인. read_console 에러 0.

- [ ] **Step 11: 커밋** — `feat(sim): named/mob captives + RunState.Captives ledger (07-B task6)`.

---

### Task 7: 문서 갱신 (07 §4·§13·§14)

구현 상태를 문서에 반영(A2 방식과 동일하게 "구현/미구현" 명확히).

**Files:**
- Modify: `Assets/../docs/engine/07-defense-combat.md` (§4.1~4.3·§4.5 구현상태 갱신, §5.1·§13.1·§14 포로/네임드 구현분 표기)
- Modify: `Assets/../docs/engine/06-loop-and-state.md` (RunState.Captives 실구현 반영 — §상태표)

- [ ] **Step 1: 07 §4 갱신** — §4.1(방 그래프/rooms_cap: `RoomGraph`/`DungeonRoomRule.RoomsCap`로 구현, 코어 암묵적·NextRooms[0] 결정론 순회 명시), §4.2(배치 예산제: `PlacementValidator`/`PlacementBuilder`, 예산=방수×3, 코스트합 검증, 주인공 코어앞1칸=`CoreFrontRoom`), §4.3(코스트=레어도: `MonsterDef.Cost`+`MonsterCatalog` 7종 데이터화), §4.5(포획: 인라인 → `CaptureRule` 데이터주도, Trap/HeroSubdue/CapturingMonster 3트리거). 각 항에 "구현(07-B, 2026-07-08):" 블록 추가.

- [ ] **Step 2: 07 §13·§14 갱신** — §13.1(주인공 배치 제약: `CoreFrontRoom`만 유효, `PlacementValidator`로 강제), §13(네임드 히로인: `Attacker.IsNamed`+`WaveDef.Entry.IsNamed` 생성단계 플래그, `ResetPolicy` 자리만·로직 미강제), §14/§15.3(포로 방면량=런 귀속: `RunState.Captives`+`CaptiveLedger` 구현, 세이브 직렬화는 07-C/이후 미룸).

- [ ] **Step 3: 06 상태표 갱신** — RunState `Captives`가 실제 구현됨(빈 리스트 기본, `CaptiveLedger.Accumulate`로 누적, 세이브 직렬화 미포함) 반영.

- [ ] **Step 4: 미구현/플레이스홀더 명시** — 문서에 이 슬라이스가 남긴 후속 스코프 명확히: (a) 몹 base 능력치의 던전레벨 스케일(현재 flat), `AvgPlacedMonsterLevel` 여전히 0; (b) 방 건설의 골드 소모(07-C); (c) 웨이브 크기 생성곡선 5~60(07-B/C); (d) 포로 세이브 직렬화; (e) 웨이브 종류(제국/모험가) 구분; (f) `CreateInitialCampaign` 라이브 시딩·Unity 배선.

- [ ] **Step 5: 커밋** — `docs(sim): reflect 07-B placement/capture implementation status`.

---

## 최종 통합 & 리뷰

- [ ] **전체 EditMode 그린** — Run(all): 신규 테스트 포함 전건 PASS. read_console 컴파일 에러/경고 0.
- [ ] **sonnet 태스크별 리뷰** — subagent-driven 규약대로 각 태스크 종료 시 리뷰 게이트.
- [ ] **opus 전건 whole-branch 리뷰** — 머지 전 end-to-end 검토(순수성·불변·결정론·데이터주도 불변식, Design Resolutions 타당성).
- [ ] **main --no-ff 머지** — 전건 통과 후. 무관 워킹트리 파일 미커밋 유지.

## Self-Review (플랜 작성자 체크 결과)

- **스펙 커버리지:** 프롬프트 작업분해 1(방그래프)→T1, 2(예산제)→T3(+데이터 T2), 3(rooms_cap)→T4, 4(포획+네임드+Captives)→T5·T6, 5(문서)→T7. 검증항목 8개 전부 대응 테스트 존재(그래프 무회귀=T1 Step11, 분기순회=T1, 코스트합거부=T3, 예산=방수×3=T3, 주인공 코어앞 외 거부=T3, rooms_cap=T4, 포획조건=T5, 네임드/잡졸 Captives=T6, 결정론/불변/순수성=각 태스크 유지).
- **플레이스홀더 스캔:** 모든 코드 스텝에 실제 코드/시그니처 명시, TBD·"적절히" 없음. 후속 스코프는 Design Resolutions/T7 Step4에 명시적 표기(구현 회피가 아니라 의도적 defer).
- **타입 일관성:** `RoomGraph.Path`/`CoreFrontRoom`, `Attacker` 3단 생성자(5/6/7-arg), `ResolveWave` 파라미터 순서(matchup→captureRule→dungeonLevel), `PlacementError`/`PlacementResult`, `CaptiveLedger.Accumulate` 반환 `RunState` — 태스크 간 명칭·시그니처 일치 확인. `MonsterDef.Id=UnitClassId` 단일화로 `MonsterId` 미도입.
- **리스크:** T1·T5의 `CombatResolverTests` 마이그레이션(호출부 다수) — subagent가 컴파일 반복으로 수렴, 리뷰 게이트에서 무회귀 확인. T6의 Attacker 3단 생성자 — 기존 5-arg 호출부 전부 무변경 보장.
