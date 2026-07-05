# VN 엔진 — 세이브/로드 설계

- **날짜**: 2026-07-05
- **프로젝트**: VN_Prototype (Unity 2022.3.27f1)
- **하위 프로젝트**: 세이브/로드 (원 로드맵상 P3의 상태 직렬화 부분을 앞당김)
- **상태**: 설계 승인 대기 → 구현 계획(writing-plans)으로 이어짐
- **선행**: P0(코어+DSL) 완료·`main` 머지됨(HEAD `c59cd99`). 본 설계는 그 위에 얹힌다.

---

## 1. 배경 & 목표

P0에서 인터프리터를 **"컴파일 후 실행"** 모델로 만들면서, 실행 상태를 `PC + 콜스택 + GameState`로 최소화했다(P0 스펙 178줄: "세이브/로드가 거의 공짜"). 이 설계는 그 약속을 실제 기능으로 구현한다.

### 목표
- 실행 중인 게임의 **완전한 상태를 스냅샷**해서 디스크에 저장하고, 나중에 그대로 복원해 이어서 진행한다.
- 복원 시 **화면(배경·캐릭터)까지** 저장 시점 그대로 되돌린다.
- `random()` 결과가 세이브/로드 이후에도 **결정적으로 재현**된다.
- **모바일 안전**(Android/iOS): 런타임 파일 IO는 `Application.persistentDataPath`만 사용.
- 여러 **세이브 슬롯** 지원.

### 비목표 (이번 범위 밖)
- 세이브/로드 **UI**(슬롯 메뉴, 썸네일) — "실물 구현" 단계에서 별도로.
- **전역 persistent 데이터**(해금·갤러리·설정 — 세이브 간 공유) — 후속.
- **롤백/되감기** — 본 스냅샷 모델이 구조적으로 지원하지만 이번엔 구현 안 함.
- **스크립트 변경 후 세이브 하위호환**(출시 후 패치 대응) — 이번엔 해시 불일치 시 거부. 라벨 앵커 업그레이드 경로는 §8 참조.

### 범용성
핵심 구조(입력 대기 지점에서 VM 상태 스냅샷 + RNG + 화면 상태를 슬롯에 저장)는 Ren'Py·Naninovel 등 일반 VN 엔진의 정석 방식과 동일하다. 유일하게 단순화한 지점은 위치 앵커(§8).

---

## 2. 결정 요약

| 항목 | 결정 |
|---|---|
| 범위 | 엔진 코어 + 디스크 저장(슬롯, 모바일 경로). UI 없음 |
| 화면 복원 | O — 스테이지 상태를 인터프리터가 추적 |
| 난수 | `System.Random` → 결정적 PRNG(xorshift32)로 교체, 상태 int 저장 |
| 위치 앵커 | `pc`(명령어 배열 인덱스) + `programHash` 불일치 시 로드 거부 |
| 저장 시점 | 입력 대기 중(`Pending == Line`/`Choice`)일 때만 |
| 직렬화 | Core는 순수 `SaveData` 모델 산출, Unity가 `JsonUtility`로 JSON 변환 |
| 저장 위치 | `Application.persistentDataPath/saves/slot_{n}.json` |

---

## 3. 저장 데이터 모델 (`SaveData`)

Core에 위치하는 플레인 `[System.Serializable]` 클래스. Dictionary·struct 대신 `JsonUtility` 친화적인 List·기본형만 사용. (Core는 `noEngineReferences`이므로 `JsonUtility` 자체는 Unity 계층에서 호출.)

```
[Serializable] class SaveData
    int      version           // 세이브 포맷 버전 (상수, 현재 1)
    string   programHash       // 로드된 .vns 스크립트 지문 (호환성 가드)

    // ── 변수 ──
    List<VarEntry> vars         // VarEntry { string name; int kind; int value }
                                //   kind: 0=Int, 1=Bool (value는 Bool이면 0/1)

    // ── 난수 ──
    int      rngState          // 결정적 PRNG 내부 상태(uint)의 비트패턴.
                               //   JsonUtility가 uint를 못 다루므로 int로 저장,
                               //   경계에서 unchecked 캐스팅(int↔uint)

    // ── 실행 위치 ──
    int      pc
    List<int> callStack         // 바깥→안쪽 순서 보존

    // ── 대기 상태(입력 대기 화면 재현용) ──
    int      pending            // 0=None(비허용), 1=Line, 2=Choice
    // pending == Line:
    string   lineSpeaker        // 이미 해석된 표시용 화자명(null 가능=나레이션)
    string   lineColor          // 이미 해석된 색(null 가능)
    string   lineText           // 이미 보간된 최종 텍스트
    // pending == Choice:
    List<string> choiceLabels   // 화면에 뜬 선택지 라벨(조건 통과분만)
    List<int>    choiceTargets  // 각 라벨의 점프 대상 pc (choiceLabels와 동일 순서/길이)

    // ── 스테이지(화면) 상태 ──
    string   background         // 현재 배경 이름(null 가능)
    List<StageChar> stage       // StageChar { string position; string character }
```

**설계 근거**:
- 대기 화면을 **이미 렌더된 값**(해석된 화자·보간된 텍스트, 조건 통과한 선택지)으로 저장하면, 로드 시 해당 명령을 **재실행하지 않고** 화면만 다시 그릴 수 있다. pc는 이미 다음 명령을 가리키므로(§4.1) 재실행은 불가능/부정확하다.
- `pending == None`은 저장 대상이 아니다(§4.1 저장 시점 규칙).

---

## 4. 컴포넌트 설계

### 4.1 Core — `Interpreter` 확장

**저장 시점 규칙**: 저장은 **입력 대기 중**일 때만 허용. `Tick()`이 대사(Say)나 선택지(Menu)를 띄우고 반환한 직후가 이 상태다. 노출:
```
public bool IsWaiting => _pending != Pending.None;
```
이때 `_pc`는 이미 Say의 다음 명령을 가리킨다(현행 코드 line 82). 그래서 대기 화면은 pc가 아니라 §3의 렌더된 값으로 저장한다. 이를 위해 인터프리터는 **마지막으로 표시한 대사**(speaker/color/text)를 내부 필드에 보관한다. 선택지는 `_activeOptions`(라벨+대상)에서 얻는다.

```
public SaveData CaptureSave(string programHash)   // IsWaiting == false면 VnException
public void     RestoreSave(SaveData data)        // 아래 §4.4 흐름
```

`RestoreSave`는 pc·콜스택·pending·스테이지·RNG를 복원하고, 대기 화면을 뷰에 다시 그린다.

### 4.2 Core — `StageState` (신규) + 인터프리터 추적

인터프리터가 `Bg`/`Show`/`Hide` 명령을 **실행할 때 뷰 호출과 함께** 논리적 스테이지 상태를 갱신한다(순수 Core, 테스트 가능).

```
class StageState
    string Background
    // position("left"/"center"/"right") → character 이름
    Dictionary<string,string> Slots

    void SetBackground(string name)          // Background = name
    void Show(string character, string pos)  // character를 다른 슬롯에서 제거 후 Slots[pos]=character
    void Hide(string character)              // character가 있는 슬롯 제거
```
`Show`의 슬롯 축출 규칙은 현행 `StageViewUnity.ShowCharacter`(같은 슬롯의 다른 캐릭터 축출, 캐릭터 슬롯 이동)와 일치시킨다. 이로써 §7의 죽은 필드 `_currentBackground`도 자연스럽게 해소된다(상태를 뷰가 아닌 Core가 소유).

### 4.3 Core — `IStageView.Clear()` 추가 + 결정적 RNG

- `IStageView`에 `void Clear();` 추가 — 표시 중인 모든 캐릭터 제거 + 배경 초기화. 로드 시 화면 리셋 후 재적용에 사용. `StageViewUnity`(실제)와 `FakeStageView`(테스트) 양쪽 구현.
- `SeededRandom` 내부를 `System.Random` → **xorshift32**(상태 `uint` 하나)로 교체. `IRandom`에 상태 접근 추가:
```
interface IRandom
    int  Range(int minInclusive, int maxInclusive)   // 기존, 양끝 포함 유지
    uint State { get; set; }                          // 신규: 세이브/복원용
```
`SaveData`는 이 `uint`를 int 비트패턴으로 담는다(§3, `CaptureSave`/`RestoreSave` 경계에서 `unchecked` 캐스팅).
`Range`의 관측 가능한 계약(양끝 포함, max<min이면 min)은 유지하되 내부 알고리즘만 교체. 기존 `ExprEvalTests`의 경계 테스트는 재검증한다(시퀀스 값은 바뀌므로 값 고정 테스트가 있으면 갱신, 경계·범위 테스트는 유지).

### 4.4 Unity — `SaveSystem` (신규)

디스크 IO + 직렬화 담당. `SaveData` ↔ JSON은 `JsonUtility`.

```
static class SaveSystem
    string SlotPath(int slot)                  // {persistentDataPath}/saves/slot_{slot}.json
    void   Write(int slot, SaveData data)      // 디렉터리 생성 후 JSON 기록
    SaveData Read(int slot)                     // 파일 없으면 null
    bool   Exists(int slot)
    void   Delete(int slot)
    IEnumerable<int> ListSlots()
```
`saves/` 디렉터리는 `Directory.CreateDirectory`로 최초 1회 생성(persistentDataPath는 모든 타깃에서 쓰기 가능).

### 4.5 Unity — `VNRunner` 리팩터 + 저장/로드 진입점

현행 `Start()` 코루틴을 **① 인터프리터 빌드**와 **② 실행 루프**로 분리하고, 프로그램 해시를 보관한다.
```
public void SaveToSlot(int slot)    // _interp.IsWaiting 확인 → CaptureSave(hash) → SaveSystem.Write
public bool LoadFromSlot(int slot)  // Read → version/hash 가드 → 인터프리터 빌드 → RestoreSave → 실행 루프 재개
```
`programHash`는 `VnScriptLoader`가 로드한 스크립트 텍스트(정렬·연결)에 대한 안정적 해시(예: FNV-1a)로 산출해 `VnProgram` 또는 로더가 노출한다.

---

## 5. 데이터 흐름

**저장** (`SaveToSlot`):
1. `_interp.IsWaiting`가 아니면 거부(로그/무시).
2. `CaptureSave(programHash)` → 변수·RNG상태·pc·콜스택·대기화면·스테이지 수집 → `SaveData`.
3. `SaveSystem.Write(slot, data)` → JSON 직렬화 후 파일 기록.

**로드** (`LoadFromSlot`):
1. `SaveSystem.Read(slot)` → 없으면 false.
2. `data.version != CURRENT_VERSION` 또는 `data.programHash != programHash` → **거부**(명확한 에러 로그, false 반환).
3. 인터프리터·GameState 새로 빌드(같은 컴파일된 프로그램).
4. `RestoreSave(data)`:
   - GameState 변수 복원, RNG `State` 복원.
   - `_pc`·`_callStack`·`_pending`·`_activeOptions`·`StageState` 복원.
   - `stageView.Clear()` → `SetBackground(bg)` → 슬롯별 `ShowCharacter(char,pos)` 재생.
   - 대기 화면 재생: Line이면 `dialogueView.ShowLine(...)`, Choice면 `dialogueView.ShowChoices(labels)`.
5. 실행 루프 재개.

---

## 6. 오류 처리

- **저장 시점 위반**: `CaptureSave`가 `IsWaiting==false`면 `VnException`. `VNRunner.SaveToSlot`은 이를 잡아 경고 로그 후 무시(크래시 금지).
- **버전/해시 불일치**: 로드 거부, 명확한 로그(`"save slot N incompatible: script changed"`), false 반환. pc 오점프 방지.
- **손상된 JSON / 누락 필드**: `Read`가 파싱 실패를 잡아 null 반환(로그). 부분 손상으로 인한 예외가 게임을 죽이지 않게.
- **choiceLabels/choiceTargets 길이 불일치** 등 내부 불변식 위반: 복원 시 `VnException`.

---

## 7. 기존 코드 개선 (이 작업에 수반되는 것만)

- `StageViewUnity._currentBackground`(P0 리뷰의 죽은 필드): 스테이지 상태를 Core `StageState`가 소유하게 되면서 정리. 뷰는 `Clear()` 구현을 추가.
- `SeededRandom`: `System.Random` 의존 제거(직렬화 불가 문제 해결) → xorshift32.

---

## 8. 향후 업그레이드 경로 (구조는 지금 열어둠)

- **라벨 앵커 하위호환**: 위치를 `pc` 대신 `가장 가까운 라벨 + 이후 명령 오프셋`으로 저장하면 출시 후 스크립트 패치에도 세이브가 살아남는다. `SaveData`에 앵커 필드를 추가하고 `Restore`에서 라벨→pc로 환산하면 되며, 나머지 구조는 그대로. 컴파일러/인터프리터가 명령↔라벨 매핑을 노출해야 함.
- **롤백**: `CaptureSave` 스냅샷을 매 대기 지점마다 스택에 쌓으면 되감기 구현. 본 설계의 캡처를 재사용.
- **전역 persistent 데이터**: 슬롯과 별개의 단일 파일(`persistent.json`)에 해금·설정 저장. `SaveSystem`에 병렬 API 추가.

---

## 9. 테스트 계획 (EditMode)

- **스냅샷 왕복**(Core): 임의 진행 상태에서 `CaptureSave` → 새 인터프리터에 `RestoreSave` → pc·콜스택·변수·대기상태 동일.
- **RNG 결정성**: 저장 후 로드한 뒤의 `random()` 시퀀스가 저장하지 않고 계속 진행한 경우와 **동일**함을 검증.
- **스테이지 상태 추적**: Show/Hide/슬롯 이동/축출/배경 변경 후 `StageState`가 정확. 로드 후 `Clear`+재적용 호출을 `FakeStageView`로 검증.
- **대기 화면 재현**: Line/Choice 각각 저장→복원 시 뷰에 올바른 인자로 재표시(Fake 뷰 기록 검증).
- **SaveData JSON 왕복**(EditMode, `JsonUtility`): 직렬화→역직렬화 후 모든 필드 동일.
- **호환성 가드**: version 불일치·programHash 불일치 로드가 거부됨.
- **저장 시점 규칙**: `IsWaiting==false`에서 `CaptureSave`가 예외.

**완료 기준**: 위 테스트가 모두 통과하고, 실제 `intro.vns`를 진행하다 저장→로드하면 변수·위치·화면·이후 난수가 모두 이어진다(스모크).
