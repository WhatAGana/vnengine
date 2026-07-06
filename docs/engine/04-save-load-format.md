# 세이브/로드 포맷

VM 상태가 `PC + 콜스택 + GameState` 로 표현되는 "컴파일 후 실행" 설계 덕분에, 세이브는 그 상태를 통째로
평탄한 직렬화 객체(`SaveData`)로 찍고, 로드는 되돌리기만 하면 됩니다. 이 문서는 그 포맷과 규칙을 정의합니다.

- 모델: `Assets/Scripts/VNEngine/Core/Runtime/SaveData.cs`
- 캡처/복원: `Interpreter.CaptureSave` / `Interpreter.RestoreSave`
- 디스크 IO: `Assets/Scripts/VNEngine/Unity/SaveSystem.cs`
- 호환성 해시: `Assets/Scripts/VNEngine/Core/Runtime/VnHash.cs` (FNV-1a)

---

## 1. 언제 저장할 수 있나

- **입력 대기 중(`IsWaiting`)에만** 저장 가능합니다 — 즉 대사(Line) 또는 선택지(Choice)를 띄우고 멈춰 있을 때.
  실행 도중(명령을 연속 처리하는 순간)에는 저장하지 않습니다. `CaptureSave`를 대기 아닐 때 부르면 예외.
- `VNRunner.SaveToSlot(slot)`은 대기 중이 아니면 경고 로그 후 `false` 반환.

---

## 2. `SaveData` 포맷

`JsonUtility`로 직렬화 가능하도록 **딕셔너리 대신 리스트, 원시 타입만** 사용합니다.

| 필드 | 타입 | 의미 |
|---|---|---|
| `version` | int | 세이브 포맷 버전. 현재 `1` (`SaveFormatVersion`) |
| `programHash` | string | 저장 시점 대본의 FNV-1a 지문 (호환성 가드) |
| `vars` | List&lt;VarEntry&gt; | 변수 스냅샷. `VarEntry{ name, kind(0=Int,1=Bool), value(Bool은 0/1) }` |
| `rngState` | int | PRNG의 `uint` 상태 비트패턴 |
| `pc` | int | 프로그램 카운터 |
| `callStack` | List&lt;int&gt; | 콜스택. **top-first 순서**로 저장 |
| `pending` | int | 대기 종류: 0=None, 1=Line, 2=Choice |
| `lineSpeaker` / `lineColor` / `lineText` | string | 마지막으로 표시된 대사(Line·Choice 양쪽에서 사용) |
| `choiceLabels` / `choiceTargets` | List&lt;string&gt; / List&lt;int&gt; | Choice 대기일 때 표시 중이던 선택지 라벨·점프 타깃 |
| `background` | string | 무대 배경 이름 |
| `stage` | List&lt;StageChar&gt; | 슬롯 점유. `StageChar{ position, character }` |

---

## 3. 캡처 규칙 (`CaptureSave`)

- 변수는 `GameState.Snapshot`을 순회해 종류·값으로 기록.
- RNG 상태(`IRandom.State`)를 그대로 기록 → 로드 후 난수열이 이어짐.
- 콜스택은 `Stack<int>` 열거(top-first)를 그대로 저장.
- **마지막 표시 대사**(`lineSpeaker/Color/Text`)는 Line 대기뿐 아니라 **Choice 대기일 때도** 저장합니다.
  선택지 메뉴 "위에 떠 있던 대사"를 로드 후에도 똑같이 재현하기 위함입니다.
- Choice 대기면 표시 중이던 선택지 라벨·타깃을 저장(조건 필터링이 적용된 "실제 표시 목록").

---

## 4. 복원 규칙 (`RestoreSave`)

- 변수·RNG·PC·콜스택을 되돌림(콜스택은 top-first 저장이므로 역순으로 push).
- 무대: 논리 상태를 재구성한 뒤 `IStageView.Clear()`로 화면을 비우고 배경·슬롯을 **다시 재생**.
- 대기 화면 재현:
  - Line 대기 → 저장된 대사를 `ShowLine`.
  - Choice 대기 → (저장된 대사가 있으면) 먼저 `ShowLine`으로 메뉴 위 대사 복원 후 `ShowChoices`.
- **null 문자열 주의**: `JsonUtility`는 null 문자열을 `""`로 직렬화하므로, 복원 시 빈 문자열을 null로 되돌립니다
  (`NullIfEmpty`). 나레이션 화자(null) 등이 여기 해당.

---

## 5. 호환성 가드

- 로드 전 `SaveSystem.IsCompatible(data, 현재 programHash)`로 검사합니다.
- **대본이 바뀌면**(programHash 불일치) PC·라벨 주소가 어긋날 수 있으므로 로드를 거부합니다(경고 후 `false`).
- 포맷 `version` 불일치도 비호환 처리.
- 손상된 JSON은 읽기 시 null 반환 → 안전하게 실패.

---

## 6. 디스크 IO (`SaveSystem`)

- 저장 경로는 `Application.persistentDataPath` 하위 슬롯 파일 — **모바일 안전**(플랫폼 무관 쓰기 가능 경로).
- 코어(`Core/**`)에는 IO가 없습니다. 직렬화는 순수 모델(`SaveData`)까지만, 파일 쓰기는 Unity 레이어에서만.

---

## 7. 로드 순서 안전장치 (`VNRunner.LoadFromSlot`)

교체용 인터프리터를 **먼저 빌드**한 뒤 기존 실행 루프를 중단·교체합니다. 빌드(로드+컴파일)가 실패하면
현재 진행 중인 게임이 그대로 유지되어 "로드 실패로 화면이 얼어붙는" 상황을 방지합니다.

---

관련 문서: **[03 아키텍처 & 실행 모델](03-architecture-and-execution.md)**
