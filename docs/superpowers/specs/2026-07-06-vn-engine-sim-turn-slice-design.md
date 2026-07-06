# VN 엔진 — 시뮬레이션 커널 첫 수직 슬라이스 설계

- **날짜**: 2026-07-06
- **범위**: 경영/디펜스 시뮬 커널의 첫 수직 슬라이스 (커맨드 → 자원 증감 → 다음 턴)
- **토대**: 기존 VN VM(P0 코어/DSL) + save/load 위에 얹는 첫 시뮬 모듈
- **관련 방향**: 「둥지짓는 드래곤」 구조를 차용한 모듈러 경영/디펜스 시뮬 + 데이팅 VN 하이브리드 (재현 아님, 구조 차용)

## 목표

"턴 하나를 실제로 클릭해 돌려보는" end-to-end 를 최소로 구현한다. 플레이어가 커맨드 버튼을 누르면 자원 수치가 변하고 주차(週)가 +1 된다. 이 슬라이스는 이후 모든 시뮬 모듈(파산·승패·디펜스·관계·건설)이 얹힐 **재사용 가능한 첫 벽돌**이다.

### 확정된 결정 (브레인스토밍)

1. **첫 마일스톤**: 얕은 수직 슬라이스 (end-to-end 한 줄기).
2. **최소 루프**: 커맨드 → 자원 증감 → 다음 턴. VN↔시뮬 접합은 이 슬라이스 밖.
3. **데이터 주입**: ScriptableObject (커널은 순수 C#, 테마는 SO).
4. **완료 기준**: 클릭 가능한 최소 UI + 커널 EditMode 테스트.
5. **아키텍처**: 기존 `Core/`(UnityEngine 비의존) ↔ `Unity/`(어댑터) 분리 규율을 그대로 미러링.
6. **커맨드 효과**: 평면 정수(int) 델타. 수식 효과·값 타입 확장(VnValue)·클램프·파산은 의도적 미룸.

## 아키텍처

```
Assets/Scripts/VNEngine/
├─ Core/Sim/                     ← 순수 C# (UnityEngine 비의존, VNEngine.Core asmdef)
│   ├─ ResourceDef.cs            자원 정의 (불변 데이터)
│   ├─ CommandDef.cs             커맨드 정의 (+ ResourceDelta)
│   ├─ SimState.cs               가변 런타임 상태 (자원 현재값 + week)
│   └─ TurnEngine.cs             규칙 (커맨드 실행 → 델타 적용 → week+1)
└─ Unity/Sim/                    ← Unity 어댑터 (VNEngine.Unity asmdef)
    ├─ ResourceDefinitionSO.cs   ScriptableObject → ResourceDef
    ├─ CommandDefinitionSO.cs    ScriptableObject → CommandDef
    └─ SimController.cs          MonoBehaviour: SO 로드 → TurnEngine 구동 → UI 갱신
```

- `Core/Sim/` 은 기존 `Core/` 와 동일한 규율: **UnityEngine·System.IO 참조 금지**. 순수 C# 로직만.
- asmdef: 순수 커널은 기존 `VNEngine.Core.asmdef`가 커버하는 `Core/` **하위 폴더**(`Core/Sim/`)에 둔다 — 새 asmdef 없이 커버되고, `VnException` 등 기존 Core 타입을 그대로 쓴다. 별도 재사용 패키징이 필요해지면 그때 `VNEngine.Sim`으로 분리.
- Unity 어댑터는 기존 `VNEngine.Unity.asmdef`가 커버하는 `Unity/Sim/` 폴더.

## 데이터 모델 (순수 C#)

```csharp
// 불변 정의
public sealed class ResourceDef {
    public string Id;            // "money", "magic"
    public string DisplayName;   // "재보", "마력"
    public int StartValue;
}

public readonly struct ResourceDelta {
    public string ResourceId;    // "money"
    public int Amount;           // +50, -20
}

public sealed class CommandDef {
    public string Id;                              // "raid", "rest"
    public string DisplayName;                     // "약탈", "휴식"
    public IReadOnlyList<ResourceDelta> Effects;   // 자원별 증감
}

// 가변 런타임 상태
public sealed class SimState {
    public int Week;                               // 주차 (1부터)
    public IReadOnlyDictionary<string,int> Resources;  // id → 현재값
}
```

**설계 의도**: 정의(Def)는 불변 데이터일 뿐이고 규칙은 전부 `TurnEngine`에 모인다. 값은 슬라이스 단순화를 위해 정수(int)로 통일. `SimState`는 평면 구조라 이후 save/load 패턴에 자연스럽게 얹힌다.

## 턴 규칙 (`TurnEngine`)

```csharp
public sealed class TurnEngine {
    public TurnEngine(IReadOnlyList<ResourceDef> resources,
                      IReadOnlyList<CommandDef> commands);

    // 시작 상태: 각 자원 = StartValue, Week = 1
    public SimState CreateInitialState();

    // 커맨드 실행: 델타 적용 → Week+1 → 새 SimState 반환
    public SimState ExecuteCommand(SimState state, string commandId);
}
```

규칙:
- `ExecuteCommand`는 **순수 함수** — 입력 `state`를 변형하지 않고 새 `SimState`를 반환(테스트·향후 undo/save 유리).
- 델타 적용: 커맨드의 각 `Effect`에 대해 `Resources[id] += Amount`.
- **클램프·최소값 없음**(음수 허용). 파산 규칙은 다음 슬라이스.
- 커맨드 실행마다 `Week += 1`.
- 델타가 **정의되지 않은 자원 id**를 가리키면 → `VnException`(기존 엔진 예외 타입 재사용)으로 즉시 실패. 조용한 무시 안 함.
- 존재하지 않는 `commandId` → `VnException`.

## 에러 처리

- 커널은 불변식 위반 시(정의 안 된 자원/커맨드) 던진다.
- `SimController`가 시작 시 SO를 검증: 자원 id 중복, 커맨드가 참조하는 자원 id 미존재 → 명확한 메시지로 로그. 예외는 Unity 레이어에서 잡아 콘솔 에러로 표시.

## Unity 어댑터 & UI

- `ResourceDefinitionSO` / `CommandDefinitionSO`: 인스펙터 편집, `ToDef()`로 순수 C# 정의 변환.
- `SimController` (MonoBehaviour):
  - 인스펙터에 자원 SO 목록 + 커맨드 SO 목록 배선
  - `Start()`에서 `TurnEngine` 생성 + `CreateInitialState()`
  - 자원값 라벨 갱신 + 커맨드 버튼 클릭 → `ExecuteCommand` → 라벨 재갱신
- **최소 UI**: 자원별 Text(예: "재보: 100  마력: 50  주차: 3") + 커맨드 버튼 2~3개. 그래픽 다듬기 없음(YAGNI).

### 슬라이스용 SO 에셋 (테마 데이터 예시)
- 자원: `Money`(재보, 시작 100), `Magic`(마력, 시작 50)
- 커맨드:
  - `약탈(raid)`: 재보 +50, 마력 −20
  - `휴식(rest)`: 마력 +30
  - (선택) `건설(build)`: 재보 −40

## 테스트 (`VNEngine.Tests`, EditMode)

`TurnEngineTests` (TDD, RED→GREEN):
1. `CreateInitialState` — 각 자원이 StartValue, Week=1
2. `ExecuteCommand` — 델타가 정확히 적용, Week+1
3. 순수성 — 원본 state 불변(반환값만 변경)
4. 다중 자원 델타 한 커맨드에 정확 적용
5. 음수 허용(클램프 없음 확인)
6. 미정의 자원 id → `VnException`
7. 미정의 commandId → `VnException`

## 완료 시 결과물

- 코드 8개: 순수 C# 4 + Unity 어댑터 3 + 테스트 1
- SO 에셋: 자원 2 + 커맨드 2~3
- 씬에 최소 UI 패널: 자원 라벨 + 커맨드 버튼
- Play 모드에서 버튼 클릭 → 자원 수치 변화 + 주차 증가를 눈으로 확인

## 슬라이스 범위 밖 (명시적 미룸)

- VN↔시뮬 접합 (커맨드 실행 시 .vns 씬 재생)
- 파산/클램프/최소값 규칙, 승패·엔딩 평가기
- 수식 효과(ExprEval 재사용), 값 타입 확장(int→VnValue)
- save/load 통합
- 디펜스 전투(던전 방·함정·몬스터 배치·요격), 관계/호감도+해금, 건설/업그레이드

각각은 이후 슬라이스에서 같은 `Sim/`(순수 커널) + `Unity/Sim/`(어댑터) 패턴으로 하나씩 얹는다.
