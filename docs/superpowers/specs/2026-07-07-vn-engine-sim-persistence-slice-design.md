# 설계 사양 — 시뮬 커널 영속화 슬라이스 (적정화 스코프)

날짜: 2026-07-07
선행: 회차 루프 Run/Meta 상태 분리 슬라이스(main 머지 `cdd6bec`). 인계 프롬프트: `docs/engine/persistence_prompt.md`.

## 목표

다회차 캠페인 상태를 **디스크에 저장/복원**하고, 메타 상태를 **VN 변수로 읽기전용 투영**해 `.vns` 서사 분기를 가능하게 한다. 그 전제로 상태 **불변성을 방어적 복사로 강화**한다.

## 스코프 결정 (브레인스토밍 2026-07-07)

인계 프롬프트(`persistence_prompt.md`)는 4작업(방어적복사·Regress 내용로직·디스크세이브·VN투영)을 담았으나, **Regress 내용 로직(작업1)**이 참조하는 필드 대부분 — `Run.Summoned`(몬스터), `Meta.InheritedMonster`(계승), `Meta.DungeonLevel`/`HeroStrMax`(디펜스), `Meta.MigalStage`/`TruthFlags`(서사) — 은 **아직 producer도 consumer도 없는 시스템**(몬스터 로스터·디펜스 서브시스템·미확정 서사 모델)에 의존한다. 지금 설계하면 요구사항 불명 상태의 추측 모델이 되어 재작업 위험이 크다.

→ 유저가 **적정화 스코프** 선택: 지금 확실히 만들 수 있고 그 자체로 완결되는 것만.

**이번 슬라이스 = 방어적 복사 + CampaignState 세이브/로드 왕복 + LoopCount VN 투영(읽기전용).** Regress는 **현행 유지**(LoopCount+1·Run 리셋).

## 두 원칙 (불변)

1. **순수 코어**: `Core/**` 는 `UnityEngine`·`System.IO` 절대 미참조.
2. **불변 상태**: 모든 전이는 새 인스턴스 반환, 입력 불변.

## 컴포넌트

```
Core/Sim/   RunState.cs(수정) · CampaignSaveData.cs(신규) · CampaignSave.cs(신규) · MetaProjection.cs(신규)
Unity/Sim/  CampaignSaveSystem.cs(신규) · SimController.cs(수정: 세이브/로드 버튼)
Tests/Editor/ RunState 방어적복사 · CampaignSave 왕복/가드 · MetaProjection 투영
```

## 1. 방어적 복사 (불변성 강화) — 선결

**문제**: `RunState`가 생성자로 받은 `IReadOnlyDictionary<string,int>`를 **참조 그대로** 보관한다(`RunState.cs:10-14`). 인메모리 단일 실행에선 `TurnEngine`이 항상 새 딕셔너리를 만들어 넘기므로 안 터지지만, 세이브→로드 왕복이나 외부 역직렬화 콜러가 원본 딕셔너리를 쥔 채 수정하면 상태가 새어든다("이전 회차 상태가 현재 회차에 누수").

**해결**: `RunState` 생성자가 `resources`를 **내부 새 `Dictionary`로 깊은 복사**한 뒤 읽기전용으로 노출한다. 호출자가 누구든 불변 보장.

```csharp
public RunState(int day, IReadOnlyDictionary<string, int> resources)
{
    Day = day;
    Resources = new Dictionary<string, int>(resources); // 방어적 복사
}
```

`MetaState`는 적정화 스코프에서 `LoopCount`(int)뿐이라 복사할 컬렉션이 없다 → 이번엔 `RunState`만 대상. (미래에 Meta에 컬렉션 필드가 붙으면 같은 원칙 적용.)

**검증**: 원본 딕셔너리를 생성 후 수정해도 `RunState.Resources`가 불변임을 확인.

## 2. CampaignState 디스크 세이브 (별도 파일)

기존 VN VM 세이브(`SaveData`/`SaveSystem`, 04 문서)는 **그대로 두고** 독립적으로 캠페인 세이브를 추가한다. 현재 시뮬 커널과 VN VM은 독립(05 §5)이므로 한 파일에 묶지 않는다(조기 결합 회피).

### 2a. `CampaignSaveData` (Core/Sim, 신규)

`JsonUtility` 호환 — **딕셔너리 대신 리스트, 원시 타입만**(04 §2 패턴).

```csharp
[System.Serializable]
public sealed class ResEntry { public string id; public int value; }

[System.Serializable]
public sealed class CampaignSaveData
{
    public const int CampaignSaveVersion = 1;
    public int version;
    public int loopCount;                 // Meta.LoopCount
    public int day;                       // Run.Day
    public List<ResEntry> resources = new List<ResEntry>(); // Run.Resources 평면화
}
```

### 2b. `CampaignSave` (Core/Sim, 신규, 순수 static)

```csharp
public static CampaignSaveData Capture(CampaignState c)   // version 채움, resources 리스트화
public static CampaignState  Restore(CampaignSaveData d)  // version 가드 → RunState/MetaState 재구성
```

- `Capture`: `Run.Resources`를 `ResEntry` 리스트로 평면화, `version=CampaignSaveVersion`.
- `Restore`: `version != CampaignSaveVersion` 이면 `VnRuntimeException`(기존 예외 재사용). 리스트를 딕셔너리로 되돌려 `RunState`/`MetaState`/`CampaignState` 재구성. 재구성 시 `RunState` 생성자의 방어적 복사가 자동 적용.
- `programHash` 가드는 **불필요** — 캠페인은 PC/라벨 주소가 없는 순수 데이터라 대본 변경에 영향받지 않는다. `version`만으로 충분.

### 2c. `CampaignSaveSystem` (Unity/Sim, 신규, 디스크 IO)

`SaveSystem`(04 §6) API 미러. 경로 `Application.persistentDataPath/saves/campaign_{slot}.json`. Core엔 IO 없음 — 파일 쓰기는 Unity 레이어만.

```csharp
public static void Write(int slot, CampaignSaveData data)
public static CampaignSaveData Read(int slot)   // 없거나 손상 시 null
public static bool Exists(int slot)
public static void Delete(int slot)
```

### 검증 (EditMode)

- **왕복**: `Capture(c)` → `Restore` → `loopCount`/`day`/`resources` 완전 일치.
- **JsonUtility 왕복**: `Capture` → `JsonUtility.ToJson` → `FromJson` → `Restore` → 일치(디스크 없이 직렬화 계층 검증. JsonUtility는 UnityEngine이므로 이 테스트는 Tests asmdef(UnityEngine 참조)에 둔다 — Core 순수성 무관).
- **버전 가드**: `version` 불일치 데이터를 `Restore` 하면 `VnRuntimeException`.
- **방어적 복사 상호작용**: `Capture` 후 원본 `Run`(딕셔너리)을 수정해도 로드된 상태 무영향.

## 3. LoopCount → VN 변수 투영 (읽기전용·단방향)

### `MetaProjection` (Core/Sim, 신규, 순수 static)

```csharp
public static void Project(MetaState meta, GameState state, string loopCountVar)
    => state.Set(loopCountVar, VnValue.Int(meta.LoopCount));
```

- 방향은 **커널 → VN 단방향, 읽기전용**. VN은 이 변수를 직접 쓰지 않는다(커널이 유일한 진실 소스). 다음 투영마다 덮어써짐.
- 변수명은 **주입**(파라미터) — Core는 테마 중립 유지. "회차" 같은 테마 문자열은 Unity/설정 레이어가 공급.
- 이로써 `.vns`에서 `if 회차 >= 2:` 서사 분기가 **기존 문법으로** 가능(투영 심 완성). 미래에 TruthFlags/MigalStage가 생기면 같은 함수에 매핑 추가.

**검증**: `Project(meta{LoopCount=3}, state, "회차")` → `state.Get("회차") == VnValue.Int(3)`.

## 4. Unity 배선 + 플레이모드 검증 (얇게)

`SimController`(현재 LoopEngine 구동)에 **세이브/로드 버튼** 추가:
- 세이브 버튼 → `CampaignSaveSystem.Write(slot, CampaignSave.Capture(_campaign))`.
- 로드 버튼 → `Read` → 있으면 `CampaignSave.Restore` → `_campaign` 교체 → `Refresh`.
- 프리팹/버튼 배선은 Task 3(새 회차 버튼) 패턴 재사용. 없으면 무시(null-guard).
- (선택) 투영은 상태 라벨과 별개라 UI 필수 아님 — 투영은 Core 테스트로 검증, 배선은 세이브/로드만 필수.

**플레이모드 수동검증**: 약탈 몇 번 → 세이브 → 새 회차·약탈로 상태 변경 → 로드 → **저장 시점 상태(회차/일차/자원)로 정확히 복원**. 콘솔 에러 0.

## 명시적 비(非)스코프 — 의도적 미룸

- Regress 내용 로직: 편지 글귀 누적, 진실플래그·미갈단계 전이, 계승 몹, 던전레벨/용사강도 등 누적 지표. → 서사 모델·디펜스 서브시스템 확정 후 다음 슬라이스.
- 캠페인+VN VM 통합 세이브(한 슬롯). → 두 시스템이 접합되는 슬라이스에서.
- 세이브 슬롯 UI·목록·삭제 관리 화면.

## 테스트 규칙

Tests `Assets/Tests/Editor`, ns `VNEngine.Tests`, NUnit EditMode, UnityMCP `run_tests` assembly `VNEngine.Tests`. 새 `.cs` 추가/수정 후 `refresh_unity scope:"all"` → `read_console`(에러0) → `run_tests`.

## 미래 참고 (최종 리뷰 권고 반영)

방어적 복사가 이번에 들어가므로, 이후 영속화 콜러가 늘어도 상태 누수 없음. Meta에 컬렉션 필드(플래그 셋 등) 추가 시 같은 방어적 복사 원칙을 그 필드에도 적용할 것.
