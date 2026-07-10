# 06 — 회차 루프 & 메타/런 상태 분리 (설계 사양)

> 이 문서의 **전체 범위는 아직 구현 안 됨**(얇은 버전만 착수 — 아래 착수 상태 참고). 05번(시뮬 커널 첫 슬라이스) 위에 얹을 **다음 슬라이스**의 설계 사양이다.
> 기존 엔진의 두 원칙을 그대로 따른다: **① 순수 코어(UnityEngine 비의존) ② 불변 상태(순수 함수형 턴)**.
> 목표: 「둥지짓는 드래곤」류 다회차 루프의 뼈대. **회귀(regression)를 1급 개념으로** 만든다.

> **착수 상태(2026-07-06)**: 이 문서의 **얇은 버전**이 구현됨 — RunState/MetaState/CampaignState 2층 컨테이너 +
> `LoopEngine.StartNewLoop`(LoopCount+1, Run 리셋)까지. `Regress`의 계승·편지·진실플래그·VN 투영, 디스크 세이브,
> RunState 디펜스 필드는 **여전히 미구현**(이 문서의 나머지 = 다음 슬라이스들). 구현 스펙:
> `docs/superpowers/specs/2026-07-06-vn-engine-sim-loop-state-slice-design.md`.

> **착수 상태(2026-07-07)**: 여기에 더해 **디스크 영속화**(`CampaignSave` + Unity `CampaignSaveSystem`, 별도 파일 `campaign_N.json`)와
> **메타→VN 읽기전용 투영**(`MetaProjection`: LoopCount→변수)이 구현됨. RunState는 방어적 복사로 불변 강화됨.
> `Regress` 내용 로직(계승·편지·진실플래그·미갈)과 캠페인+VN VM 통합 세이브는 **여전히 미구현**. 스펙:
> `docs/superpowers/specs/2026-07-07-vn-engine-sim-persistence-slice-design.md`.

> **착수 상태(2026-07-07, 8스탯)**: 주인공 8스탯 **데이터 골격**이 올라옴 — `MetaState.Heroes`(불변 `HeroStats`, 회차 넘어 캐리포워드),
> 인과율→스탯강화 순수함수(`StatUpgrade`+데이터 곡선 `StatCostCurve`), 세이브 평면직렬화, VN 스탯 투영(`MetaProjection`). **전투·karma수급·라이브시딩은 미구현.**
> 스펙: `docs/superpowers/plans/2026-07-07-vn-engine-8stat-slice.md`.

> **착수 상태(2026-07-08, 07-B)**: §1.1의 `RunState.Captives`가 실제로 구현됨 — `RunState.Captives`
> (`IReadOnlyList<Captive>`, 생성자 미지정 시 빈 리스트 기본값)와 `Combat/CaptiveLedger.Accumulate(run, result)`
> (순수함수, `CombatResult.Captured`를 누적한 **새** `RunState` 반환, 입력 불변)가 그 실체. **세이브 직렬화는
> 미포함**(`CampaignSave`/`CampaignSaveData`에 `Captives` 필드 없음 — §4 세이브 통합 대상에서 여전히 제외)이고,
> `CaptiveLedger.Accumulate`를 실제로 호출하는 배선(전투 해결 후 자동 누적)도 아직 없다(현재는 독립적으로
> 테스트되는 순수함수 뿐). §1.1 표의 나머지 행(`Rooms`/`Summoned`/`WaveProgress`)은 여전히 **미구현**.
> 상세: [07 디펜스 전투](07-defense-combat.md) §4·§14·§15.3.
> 스펙: `docs/superpowers/plans/2026-07-08-vn-engine-07b-placement-capture.md`.

> **착수 상태(2026-07-08, 07-D)**: `MetaState.Inn`(`InnState`: `Staff`/`Decor`/`MenuLevel`, 불변, `Empty`·
> `WithDecor(int)`)가 실제로 구현됨 — §1.2 메타 상태 표의 "여관상태(직원/내구/메뉴)"가 여기 해당(07 문서 §7
> 참고). `Heroes`와 동일한 패턴으로 메타 귀속·회차 캐리포워드: `MetaState`가 3-arg 생성자
> (`loopCount, heroes, inn`)를 얻었고(1-arg/2-arg 생성자는 하위호환 유지, 미지정 시 `Inn=InnState.Empty`),
> `LoopEngine.StartNewLoop`가 `Inn`을 다음 회차로 그대로 이관한다(§3 "메타 갱신"의 누적 원칙과 동일 — 여관은
> 회귀해도 소실되지 않음). 세이브: `CampaignSaveData`에 `innStaff`/`innDecor`/`innMenuLevel`(int) 3필드
> 추가(§4 "평면 구조" 원칙 그대로) — **additive, `CampaignSaveVersion` 불변**(구세이브에 없는 필드는 기본값
> 0으로 역직렬화 → `Decor=0`이 되어 여관 수입 게이트가 닫힌 안전한 기본 상태로 자연 폴백). 수급 산식
> (`InnIncomeRule.Compute`)·자연감소(`InnUpkeepRule.Decay`) 자체는 순수함수로 존재하나, 이걸 실제로 매 턴/루프
> 호출해 `RunState` 자원에 반영하는 배선과 `MetaProjection`(§5) 투영은 여전히 **미구현**.
> 상세: [07 디펜스 전투](07-defense-combat.md) §7.
> 스펙: `docs/superpowers/plans/2026-07-08-vn-engine-07d-inn-income.md`.

> **착수 상태(2026-07-09, 07-C)**: §1.2 메타 상태 표의 `DungeonLevel` 행이 이제 실제로 채워짐 — `MetaState`가
> 5-arg 생성자(`loopCount, heroes, inn, karmaBank, dungeonLevel`)를 얻었고(1~4-arg 생성자는 하위호환 유지,
> `dungeonLevel` 미지정 시 기본값 1 — 0이면 `DungeonLevelRule.LevelUpCost`가 예외를 던지므로 안전한 최소값으로
> 폴백), `LoopEngine.StartNewLoop`가 이를 `Inn`/`Heroes`와 동일하게 **회차 넘어 그대로 이관**한다(§1.2 "메타 =
> 회차를 넘어 유지" 원칙 그대로). 여기에 더해 `KarmaBank`(인과율 저금 잔액)도 같은 5-arg 생성자로 신설됨 — 06
> §1.2 표엔 아직 명시적 행이 없으나(추가 대상), 07 §14/§15.3 "인과율 저금(bank) 잔액 → 메타" 상태귀속 규칙이
> 여기 해당하고 `StartNewLoop`가 동일하게 캐리포워드한다. §1.1 런 상태 표에는 `PullsThisLoop`(가챠 뽑기 횟수
> 카운터, 07 §6.3)가 추가됨 — `RunState`가 4번째 생성자 인자로 받으며(3-arg 이하는 하위호환, 기본값 0),
> `StartNewLoop`가 새 `RunState`를 만들 때 이 값을 넘기지 않으므로 **회차마다 자동 0 리셋**(왕복 테스트로
> 5→0 검증됨) — "메타에 두면 가챠가 막힌다"는 07 §6.3 경고가 타입 배치로 강제됨. §1.1 표의 `Captives` 행도
> **세이브 직렬화**까지 완결됨 — `CampaignSaveData.captives`(`CaptiveEntry[]`) 추가로 07-B가 남긴 "세이브 미포함"
> 갭이 닫힘(additive, `CampaignSaveVersion` 불변 — 구세이브는 빈 목록으로 역직렬화). 구세이브 호환: `karmaBank`는
> 필드 부재 시 JsonUtility 기본값 0, `dungeonLevel`은 0/음수를 1로 보정(`DungeonLevelRule`의 하한과 정합).
> `LoopEngine.CreateInitialCampaign`이 이제 `HeroStats.FromDefs(StatCatalog.Default())`로 **8스탯 라이브 시딩**을
> 수행함(기존엔 캠페인 시작 시 스탯이 비어있는 `HeroStats.Empty`였음 — 07 §12의 07-A1 "미구현: 초기 라이브
> 시딩" 항목이 여기서 닫힘). `HeroStats.FromDefs`는 `StatDef.Id.Value`가 null/empty면 즉시
> `VnRuntimeException`을 던져 깨진 스탯 데이터가 조용히 통과하지 못하게 막는다. §5 "VN 접합"의 메타→VN 투영
> 함수군에 `MetaProjection.ProjectKarmaBank`(인과율 잔고)와 `MetaProjection.ProjectResources`(런 자원 임의
> id→변수명 매핑)가 신설됨 — **단, 이번 슬라이스는 이 두 투영 함수를 실제로 매 턴 호출하는 배선은 하지
> 않는다**(06의 기존 정책 그대로: 투영은 순수 함수로만 준비하고, 언제 호출할지는 VN-서사 슬라이스가 결정 —
> §5 "서사 → 커널 호출"과 대칭적인 미배선).
> 상세: [07 디펜스 전투](07-defense-combat.md) §5·§6·§7·§9.
> 스펙: `docs/superpowers/plans/2026-07-09-vn-engine-07c-economy-wiring.md`.

> **착수 상태(2026-07-09, 시간구조)**: 90일=9주기×10일 진행 커널이 신설됨 — `Day`는 이 문서 §1.1 그대로
> **런 소속**(기존 `RunState.Day` 재사용, 신규 `TimeState` 없음, 회차 리셋)이고 `MetaState`는 무변경. `Core/Sim/Time`
> 신설: `TimeQuery`(페이즈/웨이브일/세이브일 순수 질의) · `MaintenanceRule`(정비일 여관틱) · `CampaignDayRule`
> (`AdvanceDay` 하루 전이 코어) · `DayContext`(웨이브 해결용 설정 번들) · `AdvanceResult`(전이 결과) ·
> `TimeController`(`Step`/`SkipToNextWave`/`SkipToDay` 진행 모듈, 스킵=정산 방식). 속도/스킵 개념은 코어에 없고
> Unity `SimController`의 "빠른재생" 표시 계층에서만 다룬다.
> 상세: [07 디펜스 전투](07-defense-combat.md) §11 시간구조 콜아웃.
> 스펙: `docs/superpowers/plans/2026-07-09-vn-engine-time-structure.md`.

관련: [03 아키텍처](03-architecture-and-execution.md) · [04 세이브/로드](04-save-load-format.md) · [05 시뮬 커널](05-simulation-kernel.md)

---

## 0. 왜 이걸 먼저 하는가 (구현 순서 정당화)

05번 §6은 다음 슬라이스 후보로 [클램프/승패, 효과 수식화, **디펜스 전투**, 관계/호감도, 세이브 통합]을 나열한다.
이 중 **디펜스 전투를 먼저 만들면 안 된다.** 이유:

- 이 게임의 뼈대는 **회차 루프**다. 90일을 여러 번 반복하며 서사가 진행된다(1회차 생존→자폭 회귀, 2회차 첩보, …).
- 지금 `SimState = { Week, Resources }` 하나뿐이라, 상태가 "회차마다 리셋되는 것"과 "회차를 넘어 유지되는 것"으로
  **나뉘어 있지 않다.**
- 이 분리 없이 디펜스·가챠·포로를 `SimState`에 쌓으면, 나중에 "회귀"를 구현할 때 **무엇을 지우고 무엇을 남길지**가
  전부 엉킨다. → 재작업 발생.
- 반대로 지금 두 층으로 분리해두면, 이후 모든 시스템(디펜스/가챠/포로/편지)이 "런에 속하나 메타에 속하나"만
  정하면 자연히 제자리를 찾는다.

**결론: 상태 분리 → (그 위에) 디펜스 → 가챠 → VN 접합 순서.**

---

## 1. 두 개의 상태 층

### 1.1 런 상태 (RunState) — 매 회차 리셋
한 번의 90일 플레이 동안만 유효. 회귀하면 **버려진다**.

| 데이터 | 설명 |
|---|---|
| `Day` | 현재 일차 (1~90). 05의 `Week`를 일 단위로 대체 |
| `Resources` | 골드·마석 등 회차 내 자원 (05의 Resources 계승) |
| `Rooms` | 방 배치 상태 (1→2→3→4→5코어). 각 방의 몹/함정 (→ 07 디펜스 문서에서 상세) |
| `Summoned` | 이번 회차에 소환·배치된 몬스터 목록 |
| `Captives` | 현재 포획한 포로 상태 (해당 회차 진행분) |
| `WaveProgress` | 10일 주기 제국군 웨이브 진행도 |

### 1.2 메타 상태 (MetaState) — 회차를 넘어 유지
회귀해도 **살아남는다**. 이 게임의 "영속성"은 전부 여기 있다.

| 데이터 | 설명 |
|---|---|
| `LoopCount` | 현재 회차 번호 (1부터). 서사 분기의 최상위 키 |
| `InheritedMonster` | 다음 회차로 데려가는 몹 1마리(로그라이트 계승). 없으면 null |
| `LetterLines` | 편지 글귀 누적 리스트. 회차마다 한 줄씩 추가 (1회차 "여관을 부탁한다" → 2회차 "황제를 믿지마" → …) |
| `TruthFlags` | 해금된 세계관 진실 플래그 집합 (예: `knows_sacrifice_lie`, `knows_emperor`) |
| `MigalStage` | 미갈 서사 단계 (등장/신뢰/배신/부재/각성) |
| `UnlockedRoster` | 히로인별 해금 진행도 (아그네스 서큐조건 충족여부, 마르타 던전레벨 도달, 드보라 STR달성 등) |
| `DungeonLevel` | 던전 레벨 (획득물+인과율해소+경험치 누적). 마르타 심화 해금 조건 |
| `HeroStrMax` | 주인공 STR 도달 최고치. 드보라 심화 해금 조건 |

> **핵심 규칙**: "이 데이터가 회귀 후에도 남아야 하나?"가 런/메타를 가르는 유일한 기준.
> 남아야 하면 메타, 리셋돼야 하면 런. 애매하면 "유저가 회귀 후 잃으면 화날까?"로 판단.

---

## 2. 코어 모델 (순수 C#, `Core/Sim/Loop/**`)

기존 `SimState`(05)를 `RunState`로 개명·확장하고, `MetaState`를 신설한다. 둘을 묶는 `CampaignState`가 최상위.

```csharp
// 최상위 — 세이브 단위
public sealed class CampaignState {
    public MetaState Meta;   // 회차 넘어 유지
    public RunState  Run;    // 현재 회차 (null이면 회차 시작 전)
}
```

- 05의 설계 철학 유지: **불변**. 모든 전이 함수는 새 인스턴스를 반환(입력 불변).
- 05의 `TurnEngine`은 `RunState` 안에서 하루 진행(자원 증감)을 담당하도록 이관. `Week→Day` 용어만 교체.

### 2.1 데이터 주도 유지
05의 원칙(테마 중립·데이터 주입)을 그대로 계승한다. 커널은 "미갈"도 "연옥"도 모른다.
- 편지 글귀, 히로인 해금 조건, 진실 플래그 이름 등 **구체 내용은 전부 데이터(ScriptableObject / JSON)로 주입**.
- 커널은 "LoopCount에 따라 어떤 데이터를 활성화하나"라는 **규칙만** 실행.

---

## 3. 회귀 (Regression) — 이 게임의 1급 연산

회귀는 이 커널에서 가장 중요한 전이다. **런을 버리고, 메타를 갱신하며, 새 런을 시작**한다.

> **현행 회귀(시간구조, 2026-07-09)**: `LoopEngine.StartNewLoop`가 실제 회귀 수행자 — `CampaignDayRule.AdvanceDay`는
> `Day>90`이면 처리 없이 `AdvanceResult.RegressPending=true`만 반환하고, caller(Unity `SimController`)가 이 신호를
> 보고 `LoopEngine.StartNewLoop`를 호출한다. 아래 `Regress(state, input)` 시그니처·계승/편지/진실플래그 내용 로직은
> 여전히 미구현(위 착수 상태 노트 참고) — 현행은 "런 리셋+메타 유지"만 수행하는 얇은 버전.

```csharp
CampaignState Regress(CampaignState s, RegressionInput input)
```

순수 함수. 다음을 수행한 **새 CampaignState**를 반환:

1. **계승 처리**: `input.MonsterToInherit`(유저가 고른 몹 1마리)를 `Meta.InheritedMonster`에 기록.
   나머지 `Run.Summoned`는 폐기.
2. **메타 갱신**:
   - `Meta.LoopCount += 1`
   - `Meta.LetterLines`에 새 회차 글귀 추가 (데이터 테이블 `letters[LoopCount]` 참조)
   - `Meta.DungeonLevel`, `Meta.HeroStrMax` 등 누적 지표는 **회차 성과를 반영해 갱신**(리셋 아님)
   - 새로 밝혀진 `TruthFlags` / `MigalStage` 전이 반영
3. **런 재생성**: `Run = CreateInitialRun(Meta)`.
   - 계승 몹이 있으면 새 런의 소환 풀/초기 배치에 반영.
   - Day=1, 자원 초기값, 방 초기화.
4. 반환.

> **왜 순수 함수인가**: 회귀 = "상태 A → 상태 B" 매핑이 명확해야 테스트·세이브·디버깅이 쉽다.
> "이 회귀가 정확히 뭘 남기고 뭘 지웠나"를 입력·출력 비교로 단정할 수 있어야 한다(05의 불변 철학과 동일).

### 3.1 회귀 트리거 (서사와의 접합)
- 1회차: 90일 경과 → 코어 폭주 → 강제 회귀 (`RegressionInput`은 계승 선택 UI 후 확정).
- 2회차+: 회차별 서사 조건 충족 시 회귀. 트리거 자체는 `.vns` 서사에서 발생시키고, 커널의 `Regress`를 호출.
- 미갈이 코어를 깨는 3·4회차: 서사 이벤트가 `Regress`를 호출하되 `RegressionInput`에 특수 플래그.

---

## 4. 세이브/로드 통합 (04번 위에 얹기)

04번 세이브는 **VN VM 상태**(PC+콜스택+GameState)를 다룬다. 이제 저장 단위가 둘로 늘어난다:

| 저장 대상 | 무엇 | 기존/신규 |
|---|---|---|
| VN 진행 | PC, 콜스택, GameState 변수, 무대 | 기존 (04) |
| 캠페인 | `CampaignState`(Meta + Run) | **신규** |

- `CampaignState`는 05의 `SimState`처럼 **평면 구조**로 설계 → 04의 리스트+원시타입 직렬화 패턴 그대로 적용 가능.
- `MetaState`는 **회차를 넘어 유지되므로**, VN 세이브 슬롯과 별개로 "캠페인 파일"로도 관리 가능(설계 선택).
  - 권장: 한 세이브 = `{ 캠페인(Meta+Run) + VN VM 상태 }` 를 함께 찍는다. 로드 시 둘 다 복원.
- `programHash` 호환성 가드(04 §5)는 그대로. 대본 바뀌면 로드 거부.

---

## 5. VN 접합 (05 §5의 "미구현 접합점" 해소 시작)

05는 "자원↔변수, 커맨드↔.vns 씬 접합이 미구현"이라 했다. 회차 루프가 이 접합을 요구한다:

- **메타 플래그 → .vns 조건 노출**: `Meta.LoopCount`, `TruthFlags`, `MigalStage` 등을 VN `GameState` 변수로
  **투영(project)**해서, `.vns`에서 `if 회차 >= 2:` `if knows_emperor:` 처럼 서사 분기에 쓴다.
  → 접합 방향: **커널 메타 상태 → VN 변수(읽기 전용 투영)**. VN이 이 변수를 직접 쓰진 않고 읽기만.
- **서사 → 커널 호출**: `.vns` 이벤트(예: 회귀 결심, 미갈 배신)가 커널의 `Regress` 등 전이를 호출.
  → 이건 새 `.vns` 명령이나 콜백 훅이 필요(03 §7 "새 명령 추가" 절차 따름). 다음 슬라이스에서 상세.

> **투영 규칙 예시**: `LoopCount → 변수 "회차"`, `TruthFlags{knows_emperor} → 변수 "황제진실"(0/1)`.
> 이렇게 하면 편지·서사 분기가 전부 기존 `.vns` if/menu 문법(01)으로 표현된다. 새 서사 언어 불필요.

---

## 6. 구현 체크리스트 (클로드 코드용)

```
[ ] Core/Sim/Loop/RunState.cs      — 05 SimState 확장·개명 (Week→Day, Rooms/Summoned/Captives/WaveProgress 필드 추가)
[ ] Core/Sim/Loop/MetaState.cs     — 신규. LoopCount/InheritedMonster/LetterLines/TruthFlags/MigalStage/UnlockedRoster/DungeonLevel/HeroStrMax
[ ] Core/Sim/Loop/CampaignState.cs — Meta+Run 묶음 (세이브 단위)
[ ] Core/Sim/Loop/LoopEngine.cs    — CreateInitialRun(Meta), Regress(state, input), AdvanceDay(state, ...) 순수 함수
[ ] Core/Sim/Loop/RegressionInput.cs — 계승 몹 선택, 특수 플래그
[ ] 데이터: letters 테이블, truthflag 정의, 히로인 해금조건 데이터 (SO 또는 JSON)
[ ] Unity/Sim/Loop/** — SO 정의 + Controller (05의 SimController 패턴)
[ ] 세이브 통합: CampaignState를 04 패턴으로 직렬화, VN 세이브와 함께 저장/복원
[ ] 메타→VN 변수 투영 함수 (읽기 전용)
[ ] EditMode 테스트:
       - Regress가 런만 리셋하고 메타는 보존하는가
       - 계승 몹이 다음 회차에 넘어가는가
       - LoopCount에 따라 편지 글귀가 정확히 누적되는가
       - 회귀 왕복 후 메타 지표(던전레벨/STR)가 유지되는가
```

### 설계 불변식 (테스트로 강제할 것)
1. `Regress` 후 `Meta.LoopCount`는 정확히 +1.
2. `Regress`는 입력 `CampaignState`를 변형하지 않는다(불변).
3. 회귀해도 `Meta`의 모든 필드는 규칙대로 유지/누적된다(임의 소실 없음).
4. `Run`의 어떤 필드도 회귀 후 새 런으로 새어나가지 않는다(계승 몹 제외 — 이건 Meta 경유).
5. 커널은 구체 테마 문자열("미갈" 등)을 코드에 하드코딩하지 않는다(데이터 주도).

---

## 7. 다음 슬라이스 예고 (이 문서 이후)

이 상태 분리가 서면, 그 위에:
- **07 디펜스 전투**: `RunState.Rooms`를 대상으로 몹 배치·웨이브 경로·코어 도달 판정.
- **08 가챠 소환 + 계승**: 마석 소비 → 레어리티 테이블 → `RunState.Summoned` 추가, 계승 마킹은 `Meta.InheritedMonster`.
- **09 VN 접합 명령**: `.vns`에서 커널 전이를 호출하는 명령/훅.

각 시스템은 "런에 속하나 메타에 속하나"가 이미 정해져 있으므로, 제자리에 얹기만 하면 된다.
