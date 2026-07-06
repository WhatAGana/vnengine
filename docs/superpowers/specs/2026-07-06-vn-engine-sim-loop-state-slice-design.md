# VN 엔진 — 회차 루프: 상태 분리 슬라이스 설계

- **날짜**: 2026-07-06
- **범위**: 경영/디펜스 시뮬 커널의 다음 수직 슬라이스 — 평면 `SimState`를 **Run/Meta 두 층으로 분리**하고 최소 회차 전이(`StartNewLoop`)로 그 분리를 실증
- **토대**: 05번 시뮬 커널 슬라이스(`docs/superpowers/specs/2026-07-06-vn-engine-sim-turn-slice-design.md`) 위
- **관련 문서**: `docs/engine/05-simulation-kernel.md`, `docs/engine/06-loop-and-state.md`(이 설계는 06의 **얇은 버전**), `docs/engine/04-save-load-format.md`

---

## 1. 목표 & 정당화

이 게임의 뼈대는 **회차 루프**(같은 기간을 여러 번 반복하며 서사가 진행)다. 그런데 현재 상태는
`SimState = { Week, Resources }` 하나뿐이라 "회차마다 리셋되는 것"과 "회차를 넘어 유지되는 것"이 **나뉘어 있지 않다.**
이 분리 없이 디펜스·계승·포로를 한 상태에 쌓으면 나중에 회귀를 구현할 때 무엇을 지우고 무엇을 남길지가 엉킨다(06 §0).

**이 슬라이스는 그 두 층 컨테이너와 층을 가로지르는 최소 전이 하나만** 만든다. 디펜스 내용, 회귀의 실제 내용
로직(계승·편지·진실 플래그·VN 투영), 디스크 세이브는 **명시적으로 미룬다**(§7).

### 확정된 결정 (브레인스토밍)
1. **얇은 상태 분리만** — Run/Meta/Campaign 컨테이너 + `Week→Day` 개명. 06의 회귀 내용 로직은 07/08 이후로 미룸.
2. **최소 `StartNewLoop` 포함** — Run 리셋 + `Meta.LoopCount +1`만 하는 순수 함수. 이걸로 "왕복 후 Run 리셋·Meta 유지"를 테스트로 증명.
3. **인메모리만** — 디스크 세이브/로드는 다음 슬라이스(04 평면 직렬화 패턴 그대로 얹음).
4. **구조 = LoopEngine 파사드** — `TurnEngine`은 회차 내 규칙만, 새 `LoopEngine`이 캠페인 층 파사드로 `TurnEngine`을 소유. 모든 전이가 `CampaignState`를 반환. `SimController`는 `LoopEngine`만 다룸.
5. **순수 코어·불변 유지** — 05와 동일 규율. `Core/**`는 UnityEngine·System.IO 금지. 전이 함수는 새 인스턴스 반환.

---

## 2. 아키텍처 & 파일

```
Assets/Scripts/VNEngine/
├─ Core/Sim/                       ← 순수 C# (VNEngine.Core asmdef, UnityEngine 비의존)
│   ├─ RunState.cs        [개명]   SimState → RunState (Week → Day). 매 회차 리셋
│   ├─ MetaState.cs       [신규]   회차 넘어 유지 (이번엔 LoopCount만)
│   ├─ CampaignState.cs   [신규]   { Meta, Run } = 최상위 상태 단위(향후 세이브 단위)
│   ├─ LoopEngine.cs      [신규]   캠페인 층 파사드 (TurnEngine 소유)
│   ├─ TurnEngine.cs      [수정]   회차 내 규칙만. 반환형 RunState, week→day 문구
│   ├─ ResourceDef.cs / CommandDef.cs / ResourceDelta.cs   변경 없음
└─ Unity/Sim/                      ← Unity 어댑터 (VNEngine.Unity asmdef)
    ├─ SimController.cs   [수정]   LoopEngine + CampaignState 구동. "새 회차" 버튼 추가
    └─ ResourceDefinitionSO.cs / CommandDefinitionSO.cs    변경 없음
```

- `Core/Sim/`은 기존 `VNEngine.Core.asmdef`가 커버(새 asmdef 불필요). `VnRuntimeException` 등 기존 타입 재사용.
- **개명 파급**: `SimState → RunState`(`Week → Day`)는 기계적이지만 `TurnEngine`·`TurnEngineTests`·`SimController`·
  `docs/engine/05-simulation-kernel.md`를 함께 갱신해야 함. 구현 첫 단계에서 `SimState`/`Week` 참조를 grep로 전수 확인.
  시뮬 세이브는 05에서 미뤄져 `SaveData`에 `SimState`가 없으므로 세이브 코드는 무영향.

---

## 3. 데이터 모델 (순수 C#, 불변)

```csharp
// 매 회차 리셋 — 한 번의 회차 플레이 동안만 유효
public sealed class RunState {
    public int Day { get; }                                   // 1부터. 05의 Week를 일 단위로 대체
    public IReadOnlyDictionary<string,int> Resources { get; } // id → 현재값
    public RunState(int day, IReadOnlyDictionary<string,int> resources);
}

// 회차를 넘어 유지 — 이 게임의 영속성
public sealed class MetaState {
    public int LoopCount { get; }                             // 현재 회차 번호(1부터)
    public MetaState(int loopCount);
}

// 최상위 상태 단위 (향후 세이브 단위)
public sealed class CampaignState {
    public MetaState Meta { get; }                            // 회차 넘어 유지
    public RunState  Run  { get; }                            // 현재 회차
    public CampaignState(MetaState meta, RunState run);
}
```

**설계 의도**
- `RunState`는 사실상 `SimState`의 개명(+`Week→Day`). 디펜스용 필드(`Rooms`/`Summoned`/`Captives`/`WaveProgress`)는
  07 디펜스 슬라이스에서 추가 — 지금 넣지 않음(YAGNI).
- `MetaState`는 지금 **`LoopCount`만** 가진다. 06이 나열한 `InheritedMonster`/`LetterLines`/`TruthFlags`/`MigalStage`/
  `UnlockedRoster`/`DungeonLevel`/`HeroStrMax`는 각 내용 시스템(계승=08, 편지/진실=서사 접합)이 실제로 생길 때 추가.
- 전부 불변(생성자 주입, 읽기 전용 프로퍼티). 전이는 새 인스턴스 생성.

---

## 4. 전이 규칙 — `LoopEngine` (파사드)

`LoopEngine`이 캠페인 층의 유일한 전이 표면이다. `TurnEngine`을 소유하고, 모든 메서드는 **새 `CampaignState`를 반환**한다.

```csharp
public sealed class LoopEngine {
    public LoopEngine(TurnEngine turnEngine);   // 회차 내 규칙 엔진 주입(소유)

    // 캠페인 시작: Meta.LoopCount = 1, Run = 초기 Run(Day=1, 자원 StartValue)
    public CampaignState CreateInitialCampaign();

    // 회차 내 커맨드 실행: TurnEngine으로 Run만 진행(Day+1), 새 Run으로 감싼 캠페인 반환.
    // Meta는 그대로 통과.
    public CampaignState ExecuteCommand(CampaignState campaign, string commandId);

    // 회차 전이(최소): Meta.LoopCount +1, Run은 새 초기 Run으로 리셋.
    // 계승·편지·진실플래그 등 '내용' 갱신은 이 슬라이스에 없음 → 훗날 Regress가 이 함수를 확장.
    public CampaignState StartNewLoop(CampaignState campaign);
}
```

### `TurnEngine` 변경 (최소)
- 반환형 `SimState → RunState`, `CreateInitialState()`는 `Day=1`로 초기 `RunState` 생성(현 `Week=1` 대체).
- `ExecuteCommand(RunState, id)`: 델타 적용 후 **`Day+1`**(현 `Week+1` 대체). 로직·검증(중복 id, 미정의 자원/커맨드
  참조 → `VnRuntimeException`)·클램프 없음(음수 허용)은 **그대로**.
- 즉 `TurnEngine`은 여전히 "한 회차 안의 규칙"만 안다. Meta·Campaign을 모른다.

### 전이 규칙 요약
| 함수 | Meta | Run | 비고 |
|---|---|---|---|
| `CreateInitialCampaign` | LoopCount=1 | Day=1, 자원 StartValue | |
| `ExecuteCommand` | 불변(통과) | Day+1, 델타 적용 | TurnEngine에 위임 |
| `StartNewLoop` | LoopCount+1 | 새 초기 Run으로 리셋 | 계승/내용 갱신 없음(미룸) |

- 미정의 `commandId` → `VnRuntimeException`(TurnEngine 경유).
- 순수성: 입력 `CampaignState`/`RunState`/`MetaState`를 변형하지 않음.

---

## 5. Unity 어댑터 & UI

`SimController`(MonoBehaviour)를 `LoopEngine` + `CampaignState` 구동으로 전환한다.
- `Start()`: SO들 → def 변환 → `TurnEngine` 생성 → `LoopEngine` 생성 → `CreateInitialCampaign()`로 초기 캠페인.
- 커맨드 버튼: 클릭 → `LoopEngine.ExecuteCommand(campaign, id)` → 캠페인 갱신 → 라벨 재갱신.
- **"새 회차" 버튼 신규**: 클릭 → `LoopEngine.StartNewLoop(campaign)` → 라벨 재갱신. 분리를 **눈으로** 확인(자원 리셋, 회차 증가).
- 상태 라벨: `회차: {LoopCount}   일차: {Day}   {자원명}: {값} …`.
- `ResourceDefinitionSO`/`CommandDefinitionSO`는 변경 없음. SO 에셋(Money/Magic, Raid/Rest/Build)도 재사용.
- 잘못된 SO 배선이면 `TurnEngine` 생성자 예외가 콘솔 에러로 표면화(05와 동일, 별도 검증 없음).

**완료 기준(05 관례 계승)**: Play 모드에서 커맨드로 자원·일차 변화 확인 + "새 회차" 클릭 시 자원이 초기값으로
리셋되고 회차가 +1 되는 것을 눈으로 확인 + 커널 EditMode 테스트 그린.

---

## 6. 테스트 (`VNEngine.Tests`, EditMode, TDD)

`LoopEngineTests` (신규):
1. `CreateInitialCampaign` — `Meta.LoopCount == 1`, `Run.Day == 1`, 자원이 StartValue.
2. `ExecuteCommand` — Run에 델타 적용 + `Day+1`, **`Meta` 불변**(LoopCount 그대로).
3. `ExecuteCommand` 순수성 — 입력 `CampaignState` 변형 없음(반환값만 변경).
4. `StartNewLoop` — `Meta.LoopCount +1`, `Run`이 초기값으로 리셋(Day=1, 자원 StartValue).
5. `StartNewLoop` 순수성 — 입력 불변.
6. **왕복 불변식** — 커맨드 몇 번(자원·Day 변함) → `StartNewLoop` → Run은 리셋됐지만 `Meta.LoopCount`는 유지·증가.
7. 미정의 `commandId` → `VnRuntimeException`.

`TurnEngineTests` (갱신): 기존 9개의 `Week` 단정을 `Day`로 교체(로직 동일, 개명만).

### 설계 불변식 (테스트로 강제)
- `StartNewLoop` 후 `Meta.LoopCount`는 정확히 +1.
- 모든 전이는 입력 상태를 변형하지 않는다(불변).
- `ExecuteCommand`는 `Meta`를 절대 건드리지 않는다.
- `Run`의 어떤 값도 `StartNewLoop`를 넘어 새 Run으로 새어나가지 않는다(이번 슬라이스엔 계승 자체가 없음).
- 커널은 구체 테마 문자열을 하드코딩하지 않는다(데이터 주도 — 자원/커맨드는 SO 주입).

---

## 7. 범위 밖 (명시적 미룸)

- **디스크 세이브/로드**: `CampaignState`를 04 평면 직렬화 패턴으로 저장/복원 — 다음 슬라이스.
- **회귀 내용 로직**: 계승 몹(`InheritedMonster`), 편지 누적(`LetterLines`), 진실 플래그(`TruthFlags`), 미갈 단계,
  히로인 해금, 던전레벨/STR 누적 — 각 내용 시스템이 생길 때. `StartNewLoop`가 훗날 `Regress(state, input)`로 확장됨.
- **RunState 디펜스 필드**: `Rooms`/`Summoned`/`Captives`/`WaveProgress` — 07 디펜스 슬라이스.
- **VN 접합**: 메타→VN 변수 투영, `.vns`에서 커널 전이 호출 — 09 슬라이스.
- **Day 구조**: 90일 상한·10일 웨이브 주기 등은 넣지 않음. `Day`는 상한 없는 카운터(클램프 없음 철학 유지).
- **효과 수식화/클램프/승패 판정**: 05에서 미룬 것 그대로 유지.

각각은 이후 슬라이스에서 같은 `Core/Sim`(순수) + `Unity/Sim`(어댑터) 패턴으로 얹는다.

---

## 8. 완료 시 결과물

- 순수 코어: `RunState`(개명) + `MetaState`·`CampaignState`·`LoopEngine`(신규) + `TurnEngine`(개명 반영).
- Unity: `SimController` 전환 + "새 회차" 버튼. SO/에셋 재사용.
- 테스트: `LoopEngineTests`(7) + `TurnEngineTests` 갱신(9). EditMode 그린.
- 문서: `docs/engine/05-simulation-kernel.md` 갱신(Week→Day, Run/Meta 층 반영), `docs/engine/06-loop-and-state.md`에
  "얇은 버전 착수" 상태 반영.
