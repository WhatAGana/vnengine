# 시뮬레이션 커널 스펙

이 프로젝트의 최종 목표는 순수 VN이 아니라 **경영/타워디펜스 시뮬 + 데이팅 VN 하이브리드**입니다
(북극성: 「둥지짓는 드래곤」의 시스템 구조를 차용한 새 게임). 그 게임의 심장인 **주(週) 단위 턴 루프**의
첫 수직 슬라이스가 이 시뮬 커널입니다. 이 문서는 현재 구현된 범위를 정의합니다.

- 순수 코어: `Assets/Scripts/VNEngine/Core/Sim/**` (UnityEngine 비의존)
- Unity 어댑터: `Assets/Scripts/VNEngine/Unity/Sim/**`
- 예제 씬: `Assets/Scenes/SimSlice.unity`, 데이터: `Assets/Sim/*.asset`

> **현재 범위(첫 슬라이스)**: `커맨드 선택 → 자원 증감 → 다음 주차`. 클램프/최소값/파산·승패 판정, 효과 수식화,
> 디펜스 전투, 관계/호감도, 건설/업그레이드는 **아직 없음**(다음 슬라이스 후보). §6 참고.

---

## 1. 설계 원칙

- **테마 중립·데이터 주도**: 커널은 "재보/마력/공포" 같은 구체 테마를 모릅니다. 자원·커맨드를 **데이터로 주입**받아
  규칙만 실행합니다. 다른 게임에서도 데이터만 갈아끼워 재사용하는 것이 목표.
- **VN 코어와 같은 분리**: 순수 C# 커널(`Core/Sim`) ↔ Unity 어댑터(`Unity/Sim`). VM처럼 커널은 화면을 모릅니다.
- **순수 함수형 턴**: `ExecuteCommand`는 상태를 변형하지 않고 **새 `SimState`를 반환**합니다(불변). 세이브·되감기·테스트에 유리.

---

## 2. 코어 모델

| 타입 | 필드 | 의미 |
|---|---|---|
| `ResourceDef` | Id, DisplayName, StartValue | 자원 정의(예: money "재보" 시작 100) |
| `ResourceDelta` (struct) | ResourceId, Amount | 자원 증감량(예: money +50) |
| `CommandDef` | Id, DisplayName, Effects(List&lt;ResourceDelta&gt;) | 커맨드 = 자원 델타 묶음 |
| `SimState` | Week, Resources(Id→int) | 한 시점의 상태(불변) |
| `TurnEngine` | (규칙 소유) | 검증·초기상태·커맨드 실행 |

---

## 3. `TurnEngine` — 규칙 엔진

### 생성 + 검증
```csharp
new TurnEngine(IReadOnlyList<ResourceDef> resources, IReadOnlyList<CommandDef> commands)
```
생성 시 다음을 검증하고, 위반하면 `VnRuntimeException`:
- **자원 id 중복** → `Duplicate resource id`
- **커맨드 id 중복** → `Duplicate command id`
- 커맨드 효과가 **정의되지 않은 자원**을 참조 → `Command '<id>' references undefined resource`

### 초기 상태
```csharp
SimState CreateInitialState()
```
- `Week = 1`, 각 자원을 `StartValue`로 채운 상태를 반환.

### 커맨드 실행 (순수)
```csharp
SimState ExecuteCommand(SimState state, string commandId)
```
- 현재 자원을 복제한 뒤, 커맨드의 각 `ResourceDelta`를 **더함**(`값 += Amount`).
- **주차를 +1** 한 새 `SimState`를 반환(입력 state는 불변).
- 정의되지 않은 커맨드면 `VnRuntimeException`(`Unknown command`).
- **클램프 없음**: 자원이 음수가 될 수 있습니다(파산·최소값 판정은 아직 미구현).

---

## 4. Unity 어댑터

- `ResourceDefinitionSO` / `CommandDefinitionSO`: ScriptableObject로 자원·커맨드를 인스펙터에서 정의하고
  `ToDef()`로 순수 `ResourceDef`/`CommandDef`로 변환.
- `SimController` (MonoBehaviour):
  - 인스펙터의 SO 리스트 → def로 변환 → `TurnEngine` 구성(배선 오류면 여기서 콘솔 에러).
  - 커맨드마다 버튼을 생성, 클릭 시 `ExecuteCommand` 실행.
  - 상태 라벨(`주차: N   재보: X   마력: Y`)을 매 실행 후 갱신.

### 예제 데이터 (`Assets/Sim/`)
| 종류 | 정의 |
|---|---|
| 자원 | Money(재보, 시작 100) / Magic(마력, 시작 50) |
| 커맨드 | Raid(약탈: money +50, magic −20) / Rest(휴식: magic +30) / Build(건설: money −40) |

라이브 검증: 주차1(재보100/마력50) → 약탈 → 주차2(150/30) → 휴식 → 주차3(마력60).

---

## 5. VN 코어와의 관계

- 지금은 **독립**입니다. 시뮬 커널은 `.vns` VM과 코드 공유가 없습니다(자원↔변수, 커맨드↔.vns 씬 접합은 미구현).
- 향후 접합점: 커맨드 실행 시 `.vns` 씬 재생(턴 사이 서사), 커맨드 효과를 평면 int 델타 대신 **기존 표현식 엔진(ExprEval)**으로
  수식화 → VN `GameState`와 공유. 이때 [02 표현식](02-expression-language.md)·[03 실행 모델](03-architecture-and-execution.md)을 재사용합니다.

---

## 6. 미구현 (다음 슬라이스 후보)

같은 `Core/Sim` + `Unity/Sim` 패턴으로 확장 예정:
- 클램프/최소값/**파산·승패 판정**(기한 내 목표 달성 = 엔딩).
- 커맨드 효과 **수식화**(ExprEval 재사용, VN GameState 접합).
- **디펜스 전투**(던전 방·함정·몬스터 배치·요격) — 게임의 핵심 루프.
- 관계/호감도 + 해금 플래그, 건설/업그레이드.
- SimState **세이브/로드** 통합(평면 구조라 기존 세이브 패턴에 얹기 쉬움 → [04](04-save-load-format.md)).

---

관련 문서: **[README](README.md)** · **[03 아키텍처 & 실행 모델](03-architecture-and-execution.md)**
