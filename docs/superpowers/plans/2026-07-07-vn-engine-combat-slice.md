# 07-A2 전투 코어 (Combat Core) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 06(회차 루프)+07-A1(8스탯) 위에, A1의 `HeroStats`를 읽어 **실제 웨이브 전투**를 순수 코어로 구현한다. 병종/상극(데이터) → 스탯→전투역할 파생(가중 전투력) → 침입자 위협 기준선(ThreatBase) → 개체 능력치 생성 → 분기형 데미지 공식 → 웨이브 전투 해결(처치/포획/코어피격)까지. **방 배치 예산제(07-B)·골드/인과율 환산(07-C)·여관(07-D)은 비스코프.** 방은 선형(1→2→3→4→5코어) 최소 가정.

**Architecture:** 새 폴더 `Core/Sim/Combat/**` 에 순수 타입만 추가. 06/07-A1의 기존 파일은 **읽기만**(HeroStats/RunState/MetaState/SeededRandom 참조), 수정 없음. Unity 레이어·씬·SimController·세이브 스키마 변경 없음(전투 결과의 골드/인과율 환산·영속화는 07-C).

**Tech Stack:** C# (Unity 2022.3, .NET Standard 2.1), NUnit EditMode, UnityMCP(refresh/console/run_tests). 정수 연산 전용(부동소수 금지 — 결정론·02 Int 호환).

## Global Constraints

- `Core/**` (`Assets/Scripts/VNEngine/Core/**`) 는 `UnityEngine`·`System.IO` 절대 미참조 (순수 C#). **이 슬라이스는 `Core/Sim/Combat/**` 신규 파일만 추가**(기존 파일 수정 없음 — Task 7 문서 제외).
- 모든 상태는 **불변**: 입력(`HeroStats`/`RunState`) 변형 금지, 결과는 새 값 반환. 컬렉션은 방어적 복사.
- **결정론 필수**: 모든 난수는 A1/02의 `SeededRandom`(`IRandom`, xorshift32) 경유. **부동소수점 금지** — 계수·배수는 전부 **정수 퍼센트**(예 150 = ×1.5, `x * pct / 100` 정수나눗셈). 같은 시드 + 같은 입력 → 같은 결과.
- 런타임 오류는 기존 `VnRuntimeException` 재사용(새 예외 타입 금지). 인자 검증은 `ArgumentNullException`/`ArgumentException`(06/A1 관례).
- **데이터 주도(하드코딩 금지)**: 병종 프로파일·상극·가중치·공식 계수를 코드 분기나 리터럴로 박지 말 것. 데이터 객체를 주입/순회. 확장판에서 데이터만 바꿔 밸런스 교체 가능해야 함.

- **⚠️ 두 가중치 테이블을 절대 섞지 말 것 (유저 확정, 이 슬라이스 최상위 제약):**
  1. **ThreatBase 가중치** — `w_hero, w_loop, w_placed, w_dungeon, baseOffset`. "침입자가 얼마나 강한가". `ThreatFormula` 소유(Task 4).
  2. **`StatCombatWeights`** — 주인공 스탯이 전투 역할에 어떻게 기여하나(STR→물리공격, INT→마법공격, DEX+LUK→명중/치명, AGI→회피, DEF→방어, HP→체력, MP→자원, 그리고 종합 CombatPower). **이 게임 밸런스의 핵심이라 독립 데이터 테이블**(Task 3). 전투 공식·`ThreatFormula`가 이걸 **참조**. 플레이테스트 튜닝 시 이 테이블 하나만 만지면 되도록.
  - 두 테이블은 서로 다른 파일·타입. `ThreatFormula`의 HeroLevel 입력은 `StatCombatWeights`에서 파생된 CombatPower 스칼라를 받는다(raw 스탯 합 절대 금지 — A1 opus 리뷰 권고 핵심).

- **가중 전투력 = A1 raw합 문제의 해결점**: `ProjectHeroTotal`(A1)은 raw 합이라 HP50+MP30이 지배했다. A2는 스탯을 **역할별 파생치**(`HeroCombatProfile`)로 환산해, 물리 빌드(STR)와 마법 빌드(INT)가 의미를 갖게 한다. **STR만 올리면 물리공격만 오르고 HP는 물리공격에 기여 안 함**(raw합 아님) — 이걸 테스트로 못박는다.
- 데이터값은 전부 **초기 추정 튜닝값**: 리뷰어는 값의 "밸런스 정확성"이 아니라 **구조**(테이블 분리·데이터 주도·정수결정론·불변·raw합 아님)를 검증한다.
- Core 네임스페이스 = `VNEngine`. 테스트: `Assets/Tests/Editor`, ns `VNEngine.Tests`, NUnit EditMode.
- 새/수정 `.cs` 후: UnityMCP `refresh_unity` scope:`all` → `read_console`(에러0) → `run_tests` assembly `VNEngine.Tests`(scope:`scripts`의 false-green 금지). UnityMCP 미가용 시 controller에 BLOCKED 보고.
- 방 모델은 **선형 최소 가정**: 배치 예산제(07-B)가 아직 없으므로, 방은 `IReadOnlyList<RoomNode>`(선형)로 가정하고 각 방에 배치몹(`Attacker`)을 둔다. 자료구조는 그래프 확장 가능하게 열어두되(코스트/예산/분기 경로는 07-B), 이 슬라이스는 **전투 규칙 자체**에만 집중.
- **비스코프(건들지 말 것):** 배치 예산제·코스트·방 개수 상한(07-B), 골드/인과율 환산·레벨업·약탈(07-C), 여관(07-D), 웨이브 크기 생성 곡선(5~60, 07-B/C), CreateInitialCampaign 라이브 시딩, MetaProjection 전투 배선, Unity 레이어(SimController/SO/씬), 세이브 스키마 변경, 무관 워킹트리 변경(NotoSansKR asset·기타 미추적 py/md). **예외: Task 7 문서 갱신.**

---

### Task 1: 병종 정의 — UnitClassDef · UnitClassId · UnitClassCatalog (데이터)

침입자 병종을 데이터로 정의. 능력치 배수는 **정수 퍼센트**(round(ThreatBase×배수) 대신 `ThreatBase*pct/100`).

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/UnitClassId.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/UnitClassDef.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/UnitClassCatalog.cs`
- Test: `Assets/Tests/Editor/UnitClassDefTests.cs` (create)

**Interfaces:**
- Produces:
  - `readonly struct UnitClassId : IEquatable<UnitClassId> { string Value; }` — StatId와 동일 패턴(값동등성, `==`/`!=`/`GetHashCode`/`ToString`, null-safe). enum·bare string 금지.
  - `sealed class UnitClassDef { UnitClassId Id; string DisplayName; int HpPct; int AtkPct; int DefPct; bool CanBeCaptured; }` — 생성자 인자검증(id.Value null/empty → ArgumentException, 배수 음수 → ArgumentException).
  - `static class UnitClassIds { static readonly UnitClassId Tank, Mage, Paladin, Archer, Priest; }` (참조 편의 상수; 소스는 카탈로그).
  - `static class UnitClassCatalog { IReadOnlyList<UnitClassDef> Default(); }` — 1편 5병종(초기추정 튜닝값):
    - 탱커(Tank): Hp150 Atk60 Def150, CanBeCaptured=false
    - 마법사(Mage): Hp70 Atk150 Def60, CanBeCaptured=true
    - 성기사(Paladin): Hp100 Atk100 Def100, CanBeCaptured=false
    - 궁수(Archer): Hp70 Atk100 Def70, CanBeCaptured=true
    - 성직(Priest): Hp60 Atk60 Def60, CanBeCaptured=true

- [ ] **Step 1: 실패 테스트 작성** — UnitClassId 값동등성/딕셔너리키/null-safe; Catalog가 5병종 반환·각 DisplayName·CanBeCaptured 플래그·배수값 정확; UnitClassDef 인자검증(빈 id, 음수 배수 → 예외). RED 확인(컴파일 에러 or fail).
- [ ] **Step 2: 구현** — UnitClassId(StatId 미러), UnitClassDef(검증), UnitClassIds 상수, UnitClassCatalog.Default(). GREEN.
- [ ] **Step 3: 리팩터 + 커밋** — refresh/console(에러0)/run_tests 전건 green. 커밋.

---

### Task 2: 상극 테이블 — ClassMatchup (데이터, 정수%)

병종 간 상성 배수. `Multiplier(atk, def)` = 정수 퍼센트, 미등록 쌍은 기본 100(중립). 주인공(무병종) 공격은 항상 100.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/ClassMatchup.cs`
- Test: `Assets/Tests/Editor/ClassMatchupTests.cs` (create)

**Interfaces:**
- Produces:
  - `sealed class ClassMatchup` — 생성자 `ClassMatchup(IReadOnlyList<Entry> entries)`, `Entry { UnitClassId Atk; UnitClassId Def; int Percent; }`. 내부 `Dictionary<(UnitClassId,UnitClassId),int>` 또는 중첩 dict.
  - `int Multiplier(UnitClassId atk, UnitClassId def)` — 등록 쌍이면 그 퍼센트, 아니면 **100**. 기본 100은 하드코딩 상수 아님(미등록=중립 규칙).
  - `static ClassMatchup Default()` — 예시 상성(초기추정): 궁수→마법사 150, 궁수→탱커 70, 마법사→탱커 70, 성기사→마법사 130. 나머지 100. 권장범위 50~200(문서 §2.2).
- Constraints: 퍼센트 음수 → ArgumentException. 동일 (atk,def) 중복 등록 → ArgumentException(모호성 차단).

- [ ] **Step 1: 실패 테스트** — 등록쌍 정확 퍼센트; 미등록쌍 100; 중복등록/음수 예외; Default()의 예시 4쌍 검증.
- [ ] **Step 2: 구현.** GREEN.
- [ ] **Step 3: 커밋** — refresh/console/run_tests green.

---

### Task 3: 스탯→전투역할 파생 — StatCombatWeights · HeroCombatProfile (밸런스 핵심 테이블)

**이 슬라이스의 밸런스 핵심.** 주인공 8스탯을 역할별 전투 파생치로 환산하는 **독립 데이터 테이블**. ⚠️ ThreatBase 가중치(Task 4)와 절대 섞지 말 것.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/CombatRole.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/HeroCombatProfile.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/StatCombatWeights.cs`
- Test: `Assets/Tests/Editor/StatCombatWeightsTests.cs` (create)

**Interfaces:**
- Produces:
  - `enum CombatRole { PhysicalAttack, MagicAttack, Defense, HitRating, CritRating, Evasion, Health, SkillResource, CombatPower }` — 역할은 **전투 규칙상 고정 집합**(콘텐츠 확장 대상 아님)이므로 enum 허용. (스탯 쪽은 여전히 StatId 데이터.)
  - `readonly struct HeroCombatProfile` — 각 역할 int 필드(`PhysicalAttack`, `MagicAttack`, `Defense`, `HitRating`, `CritRating`, `Evasion`, `Health`, `SkillResource`, `CombatPower`) + `int Get(CombatRole role)`.
  - `sealed class StatCombatWeights` — role별 `Dictionary<StatId,int>` 기여 퍼센트 보관. 생성자 방어복사.
    - `HeroCombatProfile Derive(HeroStats hero)` — 각 role 값 = `Σ over stats ( hero.Get이 아닌 TryGet(없으면0) × weightPct / 100 )` 정수합.
    - `static StatCombatWeights Default()` — 1편 초기추정(튜닝대상):
      - PhysicalAttack: STR 100
      - MagicAttack: INT 100
      - Defense: DEF 100
      - HitRating: DEX 100, LUK 50
      - CritRating: DEX 50, LUK 100
      - Evasion: AGI 100
      - Health: HP 100
      - SkillResource: MP 100
      - CombatPower: STR 30, INT 30, DEX 20, AGI 20, DEF 30, HP 10, MP 10, LUK 10 (종합 위협환산용 — ThreatFormula가 이 스칼라를 HeroLevel로 씀)
- Constraints: 불변(입력 HeroStats 변형 금지). 부재 스탯은 0 기여(TryGet). 하드코딩 분기 금지 — 전부 weight 순회.

- [ ] **Step 1: 실패 테스트** — 핵심 3가지 반드시 포함:
  - **raw합 아님 검증(A1 권고 핵심):** HP만 큰 hero → PhysicalAttack=0(HP는 물리공격 기여 없음). STR만 올린 hero vs 안 올린 hero → PhysicalAttack만 증가, MagicAttack/Health 불변.
  - HitRating = DEX×100/100 + LUK×50/100 정확 합산.
  - CombatPower가 8스탯 가중합(데이터대로)임을 한 케이스로 검증. 부재 스탯 0 기여.
- [ ] **Step 2: 구현.** GREEN.
- [ ] **Step 3: 커밋** — green.

---

### Task 4: 침입자 위협 기준선 — ThreatFormula (ThreatBase 가중치 테이블)

침입자 강도 스칼라. ⚠️ StatCombatWeights(Task 3)와 **별도 파일·별도 테이블**. HeroLevel 입력은 Task 3의 CombatPower 스칼라(raw합 금지).

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/ThreatWeights.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/ThreatFormula.cs`
- Test: `Assets/Tests/Editor/ThreatFormulaTests.cs` (create)

**Interfaces:**
- Produces:
  - `sealed class ThreatWeights { int WHero; int WLoop; int WPlaced; int WDungeon; int BaseOffset; }` — 생성자 주입, `static Default()` = WHero=2, WLoop=8, WPlaced=1, WDungeon=3, BaseOffset=20 (초기추정 튜닝값). **오직 이 5개 항만**(스탯→역할 가중치 절대 포함 금지).
  - `static class ThreatFormula`:
    - `static int Compute(ThreatWeights w, int heroPower, int loopCount, int avgPlacedMonsterLevel, int dungeonLevel)` = `w.WHero*heroPower + w.WLoop*(loopCount-1) + w.WPlaced*avgPlacedMonsterLevel + w.WDungeon*dungeonLevel + w.BaseOffset`. 결과 `max(1, ...)`.
    - `static int HeroPowerOf(StatCombatWeights weights, HeroStats hero)` = `weights.Derive(hero).CombatPower` (편의 래퍼 — 호출자가 Task 3 참조로 환산했음을 명시).
- Constraints: 정수 연산. loopCount<1 방어(0 이하면 (loopCount-1) 음수 → 그대로 두되 최종 max(1)). null 가드.

- [ ] **Step 1: 실패 테스트** — 공식 정확(각 항 기여 분리 검증); loopCount=1이면 WLoop항=0; heroPower가 raw합 아닌 CombatPower로 들어옴을 HeroPowerOf 경유로 확인(STR 올리면 ThreatBase 증가, HP만 올리면 CombatPower의 HP가중(10)만큼만 — raw합보다 훨씬 작게); max(1) 하한.
- [ ] **Step 2: 구현.** GREEN.
- [ ] **Step 3: 커밋** — green.

---

### Task 5: 개체 능력치 생성 — Attacker · AttackerFactory

침입자 개체 능력치 = ThreatBase × 병종 프로파일(%) + 편차(±5, 시드결정론), min1 클램프.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/Attacker.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/AttackerFactory.cs`
- Test: `Assets/Tests/Editor/AttackerFactoryTests.cs` (create)

**Interfaces:**
- Produces:
  - `struct Attacker { UnitClassId ClassId; int Hp; int Atk; int Def; bool CanBeCaptured; }` — 값타입(불변 데이터 컨테이너). 전투 중 HP 감소는 로컬 복사로 처리(원본 불변) 또는 resolver가 로컬 int로 추적.
  - `static class AttackerFactory`:
    - `static Attacker Create(UnitClassDef cls, int threatBase, IRandom rng)`:
      - `Hp  = max(1, threatBase * cls.HpPct  / 100 + rng.Range(-5,5))`
      - `Atk = max(1, threatBase * cls.AtkPct / 100 + rng.Range(-5,5))`
      - `Def = max(1, threatBase * cls.DefPct / 100 + rng.Range(-5,5))`
      - `CanBeCaptured = cls.CanBeCaptured`. rng는 세 번 호출(HP,ATK,DEF 순 — 순서 고정으로 결정론).
- Constraints: rng null 가드. 정수나눗셈. rng 호출 순서 고정(결정론 재현).

- [ ] **Step 1: 실패 테스트** — 동일 시드 → 동일 Attacker(재현); ±5 편차 범위 내; threatBase 작아도 min1 클램프(예 threatBase=1, 프로파일 60% → 0+편차 → max1); 프로파일 배수 정확 반영(탱커 Hp150 vs 성직 Hp60 상대); CanBeCaptured 전파.
- [ ] **Step 2: 구현.** GREEN.
- [ ] **Step 3: 커밋** — green.

---

### Task 6: 데미지 공식 — DamageFormula (분기형 + 상극 + 명중/치명/회피)

분기형 데미지 + 상극 배수 + DEX 명중/치명·AGI 회피(정수 계수). 전부 정수.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/DamageFormula.cs`
- Test: `Assets/Tests/Editor/DamageFormulaTests.cs` (create)

**Interfaces:**
- Produces:
  - `static class DamageFormula`:
    - `static int Raw(int atk, int def)` — `atk >= def ? atk*2 - def : atk*atk/def` (정수나눗셈).
    - `static int Apply(int atk, int def, int matchupPct)` — `max(1, Raw(atk,def) * matchupPct / 100)`.
    - 명중/치명/회피(정수 판정, 데이터 계수): `struct HitParams { int HitRating; int Evasion; int CritRating; int CritMultiplierPct; int HitRollMax; }` 또는 개별 인자. 규칙:
      - 명중 판정: `rng.Range(1, hitRollMax)` 대비 `hitRating - evasion` 비교로 명중/빗나감(빗나감 → 0 데미지 반환, "0 데미지 금지"는 명중시에만 적용).
      - 치명 판정: 명중 시 `rng.Range(1, hitRollMax)` 대비 critRating → 치명 시 `dmg * critMultiplierPct / 100`.
    - `static int Resolve(int atk, int def, int matchupPct, HitParams hp, IRandom rng)` — 명중판정→빗나가면 0, 명중이면 `Apply` 후 치명배수. 계수·배수·롤최대는 데이터.
- Constraints: 정수 전용. `Raw`에서 ATK=DEF → dmg=ATK(>0). def>0 보장(호출자 min1). rng 순서 고정.
- 검증 필수: ATK=DEF일 때 Raw>0(분기형); ATK<DEF일 때 atk²/def(0 안 됨); 상극 150 → 정확 1.5배(정수버림); matchup 후 max(1); 회피>명중이면 빗나감 가능(시드로 결정론).

- [ ] **Step 1: 실패 테스트** — Raw 분기 양쪽; ATK=DEF dmg=ATK; 상극 150/70 정확; max(1) 하한; 명중/빗나감 결정론(시드); 치명배수 적용. 순수 산술(Raw/Apply)은 rng 없이, Resolve만 rng.
- [ ] **Step 2: 구현.** GREEN.
- [ ] **Step 3: 커밋** — green.

---

### Task 7: 웨이브 전투 해결 — RoomNode · WaveDef · CombatResolver · CombatResult (순수함수)

선형 방경로를 침입자가 통과, 각 방 몹·주인공(코어앞1칸)이 요격. 처치/포획/코어피격 분기. **순수함수·결정론.**

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/RoomNode.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/WaveDef.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/CombatResult.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Combat/CombatResolver.cs`
- Test: `Assets/Tests/Editor/CombatResolverTests.cs` (create)

**Interfaces:**
- Produces:
  - `sealed class RoomNode { IReadOnlyList<Attacker> Defenders; bool HasTrap; }` — 방에 배치된 방어몹(Attacker) + 함정 여부(포획 트리거 최소구현). 방어복사·불변. (예산/코스트는 07-B — 여기선 배치 결과만 받음.)
  - `sealed class WaveDef { IReadOnlyList<Entry> Intruders; }`, `Entry { UnitClassId ClassId; int Count; }` — 웨이브 침입자 구성. 크기 생성곡선(5~60)은 비스코프, 여기선 주어진 구성만.
  - `sealed class CombatResult { bool CoreHit; IReadOnlyList<Attacker> Killed; IReadOnlyList<Attacker> Captured; }` — 골드/인과율 환산 없음(07-C).
  - `static class CombatResolver`:
    - `static CombatResult ResolveWave(RunState run, WaveDef wave, IReadOnlyList<RoomNode> rooms, HeroStats hero, StatCombatWeights statWeights, ThreatWeights threatWeights, UnitClassCatalog/lookup, ClassMatchup matchup, int dungeonLevel, int loopCount, IRandom rng)`:
      - 각 침입자 생성: heroPower=ThreatFormula.HeroPowerOf(statWeights,hero); threatBase=ThreatFormula.Compute(...); Attacker=AttackerFactory.Create(class, threatBase, rng).
      - 침입자가 방1→…→코어 순서로 통과. 각 방에서 방어몹/함정이 요격(DamageFormula). 침입자 HP≤0 → 처치(단 CanBeCaptured+함정 등 조건 → 포획).
      - 주인공은 **코어앞1칸**에서 요격: hero 파생치(HeroCombatProfile: PhysicalAttack=ATK, Defense=DEF, HitRating/CritRating/Evasion)로 DamageFormula.Resolve.
      - 코어 도달(모든 요격 생존) → CoreHit=true(그 침입자에 한해). 처치/포획된 침입자는 Killed/Captured에 누적.
    - 포획 규칙(최소): `CanBeCaptured && 함정으로 마지막 타격` → Captured, else Killed. (정교한 조건은 후속.)
- Constraints: **순수함수** — 입력 RunState/HeroStats/rooms/wave 전부 불변, 결과만 반환. **같은 시드+같은 배치+같은 웨이브 → 같은 CombatResult**. rng 호출 순서 고정. 방은 선형 순회.

- [ ] **Step 1: 실패 테스트** — 반드시 포함:
  - **결정론:** 동일 시드/입력 2회 → 동일 CombatResult(CoreHit·Killed·Captured 일치).
  - **주인공 코어앞1칸 요격:** 방을 빈 채로 두고 강한 주인공 → 침입자 코어 전에 처치(CoreHit=false, Killed 포함). 약한 주인공+빈 방 → CoreHit=true.
  - **포획 분기:** CanBeCaptured=true 침입자가 함정 방에서 마지막 타격 → Captured(Killed 아님). CanBeCaptured=false는 함정이어도 Killed.
  - **가중 전투력 경유(A1 권고):** 주인공 STR만 올리면 처치력 상승(물리공격 파생), HP만 올리면 처치력 그대로(물리공격 기여 없음) — resolver가 raw합 아닌 프로파일을 씀을 end-to-end로 못박음.
  - **코어 도달:** 방어 전무 + 무력한 주인공 → CoreHit=true.
- [ ] **Step 2: 구현.** GREEN.
- [ ] **Step 3: 커밋** — refresh/console(에러0)/run_tests 전건 green.

---

### Task 8: 문서 갱신 (07 §2·3·4·12·13 구현상태)

**Files:**
- Modify: `docs/engine/07-defense-combat.md` — §2(병종/상극/ThreatBase), §3(데미지), §4(웨이브 해결)에 **구현됨** 표기 + 실제 타입명(UnitClassDef/ClassMatchup/StatCombatWeights/ThreatWeights/ThreatFormula/AttackerFactory/DamageFormula/CombatResolver) 매핑. §12 구현상태 블록에 A2 완료 추가. **두 가중치 테이블 분리** 설계 결정 명시(ThreatWeights vs StatCombatWeights).
- Modify: `docs/engine/05-simulation-kernel.md` — Core 모델 목록에 Combat 타입 추가(정확성 보강 한정).

- [ ] **Step 1: 문서 갱신** — 미구현 항목(배치예산제/골드·인과율 환산/여관/웨이브크기곡선/라이브시딩)은 **비스코프로 정확 표기**(과장 금지). 실제 코드와 대조.
- [ ] **Step 2: 커밋.**

---

## 완료 기준 (Definition of Done)

- 8태스크 전부 리뷰 ✅(spec+quality). 전건 테스트 green(A1 228 + Combat 신규).
- 두 가중치 테이블(`ThreatWeights` / `StatCombatWeights`) 별도 파일·타입으로 분리, 서로 참조만.
- 가중 전투력 raw합 아님이 Task3·Task7 테스트로 end-to-end 못박힘.
- Core 순수성 유지(UnityEngine/System.IO 미참조), 정수결정론, 불변.
- 최종 whole-branch 리뷰(opus) Ready to merge. main `--no-ff` 머지.
- 비스코프(07-B/C/D) 미침범, 무관 워킹트리 미커밋.
