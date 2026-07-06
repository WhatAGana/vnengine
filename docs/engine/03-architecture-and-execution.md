# 아키텍처 & 실행 모델

이 문서는 `.vns` 텍스트가 실제로 실행되기까지의 **내부 파이프라인**과, 그것을 돌리는 **가상 머신(VM)**,
그리고 화면과의 **경계(프레젠테이션 계약)**를 설명합니다. 대본을 쓰는 데는 필요 없지만, 엔진을 확장·디버깅하거나
새 명령을 추가할 때 필요한 개발자용 문서입니다.

---

## 1. 설계 원칙 두 가지

### 1.1 "컴파일 후 실행"
중첩된 `if`/`menu`/`while` 블록을 **평탄한 `Instruction[]` 배열**로 컴파일한 뒤, VM은 프로그램 카운터(PC)와
콜스택만으로 실행합니다. 트리를 재귀로 걷지 않습니다.

**이유**: 실행 위치가 정수 하나(PC) + 정수 리스트(콜스택)로 표현되므로 **세이브/로드가 거의 공짜**입니다
([04번 문서](04-save-load-format.md)). 트리 재귀 방식이면 "지금 어디를 실행 중인지"를 직렬화하기 어렵습니다.

### 1.2 "VM은 화면을 모른다"
인터프리터(`Interpreter`)는 Unity 타입을 전혀 참조하지 않고 `IDialogueView`·`IStageView` **인터페이스**로만 바깥과
통신합니다. 그래서:
- `Core/**` 전체가 순수 C# → EditMode 단위 테스트가 화면 없이 돌아감.
- Android/iOS/WebGL에서 안전(플랫폼 의존 0).
- 화면 구현을 Fake로 갈아끼워 대사 순서·분기 결과를 자동 검증 가능.

---

## 2. 파이프라인 개요

```
 .vns 텍스트
    │
    ▼  LineReader.Read(source, file)          Core/Parsing/LineReader.cs
 List<LogicalLine>   ── 들여쓰기 측정, 주석·빈 줄 제거, 탭 거부
    │
    ▼  Parser.Parse(lines)                    Core/Parsing/Parser.cs
 List<Command>       ── 들여쓰기 기반 AST (Say/Menu/If/While/Jump/...)
    │
    ▼  Compiler.Compile(files)                Core/Runtime/Compiler.cs
 VnProgram           ── 평탄한 Instruction[] + 라벨→주소 표 + 캐릭터 표
    │
    ▼  Interpreter.Tick() 루프                Core/Runtime/Interpreter.cs
 IDialogueView / IStageView                   ── 화면·입력
```

Unity 레이어에서 이 파이프라인을 묶는 것이 `VnScriptLoader`(로드+컴파일)와 `VNRunner`(코루틴 구동)입니다.

---

## 3. 단계별 상세

### 3.1 LineReader — 텍스트를 논리 줄로
`Core/Parsing/LineReader.cs`. 원본 텍스트를 `LogicalLine(Indent, Text, LineNumber, File)` 리스트로 변환합니다.

- `\r\n`·`\n` 양쪽 처리(줄 끝 `\r` 제거).
- 선행 스페이스 수를 세어 `Indent`로 기록. 들여쓰기 영역에 **탭이 있으면 예외**.
- 줄 끝 주석 제거: 큰따옴표 밖이면서 **앞에 스페이스가 있는** `#`부터 잘라냄.
- 빈 줄·전체 주석 줄은 결과에서 제외.
- `LineNumber`는 1부터. 에러 메시지의 위치 표기에 쓰입니다.

### 3.2 Parser — 논리 줄을 AST로
`Core/Parsing/Parser.cs`. 재귀적으로 블록을 파싱합니다. 핵심은 `ParseBlock(lines, minIndent)` —
들여쓰기가 `minIndent` 이상인 줄을 모으고, 더 얕아지면 블록을 닫습니다.

- 첫 단어로 명령을 분기: `character`/`label`/`bg`/`show`/`hide`/`jump`/`call`/`return`/`menu`/`if`/`while`.
- `$`로 시작 → `SetCommand`. `"`로 시작 → 나레이션 `SayCommand`. 그 외 `<화자> "..."` → 화자 있는 `SayCommand`.
- `menu`/`if`/`while`은 자식 블록을 재귀 파싱. `elif`/`else`는 `if`와 같은 들여쓰기에서만 이어붙임.
- 산출 AST 노드는 `Core/Parsing/Command.cs`:

  | 노드 | 필드 |
  |---|---|
  | `CharacterDefCommand` | Id, DisplayName, Color |
  | `LabelCommand` | Name |
  | `SayCommand` | SpeakerRef(null=나레이션), Text |
  | `BgCommand` / `ShowCommand` / `HideCommand` | Name / Character+Position / Character |
  | `SetCommand` | Var, Value(Expr) |
  | `JumpCommand` / `CallCommand` / `ReturnCommand` | Label / Label / — |
  | `IfCommand` | Branches(List&lt;IfBranch{Condition, Body}&gt;) — else는 Condition=null |
  | `WhileCommand` | Condition(Expr), Body |
  | `MenuCommand` | Choices(List&lt;MenuChoiceNode{Label, Condition, Body}&gt;) |

  모든 노드는 `Line`·`File`을 달고 있어 이후 에러에 위치를 붙입니다.

### 3.3 Compiler — AST를 바이트코드로
`Core/Runtime/Compiler.cs`. `List<Command>`(여러 파일도 가능)를 `VnProgram`으로 평탄화합니다.

- **선형 방출**: 각 명령을 `Instruction` 하나 이상으로 방출해 `code` 리스트에 추가.
- **라벨 해석**: `label`은 현재 코드 위치를 `labels[name]`에 기록. 점프·메뉴 타깃은 처음엔 심볼(`TargetLabel`) 문자열로
  두었다가, 방출이 끝난 뒤 한 번에 정수 인덱스(`Target`)로 치환. 없는 라벨 참조 → 컴파일 에러.
- **제어 구조 컴파일** (합성 라벨 `@L0`, `@L1`… 사용):
  - `if/elif/else` → 각 조건마다 `JumpIfFalse(다음분기)` + 본문 + `Jump(끝)`. else는 조건 없이 본문만.
  - `while` → `시작:` `JumpIfFalse(끝)` 본문 `Jump(시작)` `끝:`.
  - `menu` → `Menu(옵션들)` + 각 선택지 본문 블록 + 본문 끝마다 `Jump(메뉴 다음)`.
  - `call`/`return`/`jump` → 각각 `Op.Call`/`Op.Return`/`Op.Jump`.
- 마지막에 `Op.Halt`를 자동 추가 → 코드 끝 도달 시 정상 종료.
- 중복 `character`·중복 `label`은 여기서 컴파일 에러.

### 3.4 명령어 집합 (opcode)
`Core/Runtime/Instruction.cs`. `VnProgram = { Instruction[] Code, Dictionary<string,int> Labels, Dictionary<string,CharacterDef> Characters }`.

| Op | 인자 | 동작 |
|---|---|---|
| `Say` | StrA=화자ref, StrB=텍스트 | 화자 해석 + 보간 후 `ShowLine`, **Line 대기** |
| `Bg` | StrA=배경 | 무대 배경 설정 + `SetBackground` |
| `Show` | StrA=캐릭터, StrB=위치 | 슬롯 점유 + `ShowCharacter` |
| `Hide` | StrA=캐릭터 | 슬롯 해제 + `HideCharacter` |
| `Set` | StrA=변수, ExprA=식 | 식 평가 후 변수 대입 |
| `Jump` | Target | PC = Target |
| `JumpIfFalse` | ExprA=조건, Target | 조건이 거짓이면 PC=Target, 아니면 다음 |
| `Menu` | Menu=옵션리스트 | 조건 통과 옵션만 표시, **Choice 대기** |
| `Call` | Target | 복귀주소(PC+1) push 후 PC=Target |
| `Return` | — | 콜스택 pop해 복귀. 비었으면 종료 |
| `Halt` | — | 종료 |

### 3.5 Interpreter — 가상 머신
`Core/Runtime/Interpreter.cs`. 상태: `PC`, `Stack<int> 콜스택`, `GameState`(변수+RNG), `StageState`(무대), 대기 종류(`Pending`).

**실행 모델**: `Tick()` 한 번은 **다음에 멈출 지점까지** 명령을 연속 실행합니다. 멈추는 지점은 셋:
- **Say** → `Pending.Line`: 대사를 보이고 플레이어 입력 대기. `IDialogueView.IsLineComplete`가 참이 되면 다음 `Tick`에 진행.
- **Menu** → `Pending.Choice`: 선택지를 보이고 선택 대기. `HasChoice`가 참이 되면 `ChosenIndex`로 해당 본문으로 점프.
- **Halt / 코드 끝 / 빈 콜스택 return** → `IsFinished = true`.

그 사이의 `bg`/`show`/`hide`/`set`/`jump`/`if`/`while` 등은 **한 Tick 안에서 연속 처리**됩니다.

- **무한 루프 가드**: 한 Tick의 명령 실행 수가 `MaxStepsPerTick`(기본 100,000)을 넘으면 런타임 에러.
  이는 "소스 루프 1회"가 아니라 "한 Tick의 전체 명령 수"입니다. 대사 없는 거대 루프(향후 시뮬 루프 등)는 이 상한을
  건드릴 수 있으니, 필요하면 상한을 올리거나 루프 중간에 yield를 넣는 설계가 필요합니다.
- **화자 해석**: `Say`의 StrA가 캐릭터 표에 있으면 DisplayName+Color로, 없으면 이름 그대로, null이면 나레이션.
- **선택지 필터링**: `Menu` 실행 시 조건이 참인 옵션만 추려 표시. 하나도 없으면 런타임 에러.
- **세이브/로드 훅**: `CaptureSave`/`RestoreSave`가 이 상태 전부를 직렬화·복원 → [04번 문서](04-save-load-format.md).

### 3.6 GameState · 표현식
- `Core/Runtime/GameState.cs`: `Dictionary<string, VnValue>` 변수 저장소 + `IRandom`. 없는 변수 읽기 = `Int(0)`.
- `Core/Values/VnValue.cs`: Int/Bool 태그드 값(struct). `Truthy`·`Equals`(종류+값) 정의.
- 표현식 파서/평가는 [02번 문서](02-expression-language.md).
- `Core/Runtime/TextInterpolator.cs`: 대사 안 `[변수]`를 실행 시점 값으로 치환(`[[`는 리터럴 `[`).

---

## 4. 프레젠테이션 계약 (화면 경계)

VM이 의존하는 두 인터페이스. 구현체는 자유롭게 갈아끼울 수 있습니다(실제 Unity 구현 / 테스트용 Fake).

### `IDialogueView` — 대사·선택지
```csharp
void ShowLine(string speakerName, string colorHex, string text);
bool IsLineComplete { get; }              // 대사 진행 입력이 끝났는가
void ShowChoices(IReadOnlyList<string> labels);
bool HasChoice { get; }                   // 선택이 됐는가
int  ChosenIndex { get; }                 // 고른 인덱스(표시된 목록 기준)
void ClearChoices();
```
- `ShowLine`의 `speakerName`/`colorHex`가 null이면 나레이션(화자 없음).
- VM은 `ShowLine` 후 매 Tick `IsLineComplete`를 폴링하다가 참이 되면 진행. 선택지도 동일하게 `HasChoice`로 폴링.
- Unity 구현: `Assets/Scripts/VNEngine/Unity/Presentation/DialogueViewUnity.cs` (TMP 타자기 효과 + 선택지 버튼).

### `IStageView` — 무대
```csharp
void SetBackground(string name);
void ShowCharacter(string name, string position);   // position: left|center|right
void HideCharacter(string name);
void Clear();                                        // 로드 시 무대 리셋
```
- Unity 구현: `Assets/Scripts/VNEngine/Unity/Presentation/StageViewUnity.cs` (배경 이름→스프라이트 매핑, 슬롯 SpriteRenderer).

---

## 5. Unity 로딩 · 구동

### 5.1 `.vns` 임포트 — ScriptedImporter
`Assets/Scripts/VNEngine/Editor/VnsImporter.cs`. `.vns` 파일을 **`TextAsset`으로** 임포트합니다.
그래서 `StreamingAssets` 파일 IO(안드로이드에서 불가) 없이 `Resources.LoadAll<TextAsset>`로 모든 플랫폼에서 로드됩니다.

### 5.2 로드 + 컴파일 — `VnScriptLoader`
`Assets/Scripts/VNEngine/Unity/VnScriptLoader.cs`.
- `Resources.LoadAll<TextAsset>(subfolder)`로 폴더 내 모든 `.vns`를 수집, **이름 오디널 순 정렬**.
- 각각 `LineReader → Parser`로 파싱한 뒤 `Compiler.Compile(모든 파일)`로 하나의 `VnProgram` 생성.
- 동시에 **programHash**(FNV-1a, 파일 이름+본문 이어붙임)를 산출 → 세이브 호환성 가드에 사용.
- `.vns`가 하나도 없으면 예외.

### 5.3 구동 — `VNRunner`
`Assets/Scripts/VNEngine/Unity/VNRunner.cs` (MonoBehaviour, `DialogueManager` 후신).
- 인스펙터 필드: `dialogueView`, `stageView`, `scriptsResourcesFolder`(기본 `scripts`), `entryLabel`(기본 `start`), `randomSeed`(기본 12345).
- `Start()`에서 로드+컴파일 → `Interpreter` 생성 → `entryLabel`부터 시작 → 코루틴 `RunLoop`이 매 프레임 `Tick()`.
- `SaveToSlot(int)` / `LoadFromSlot(int)`: 입력 대기 중에만 저장 가능. 로드는 새 인터프리터를 먼저 만든 뒤 교체(실패 시 현재 실행 보존).

---

## 6. 테스트 관점

- 파서·컴파일러·인터프리터·표현식은 전부 순수 C# → **EditMode 테스트**(`VNEngine.Tests`)로 검증.
- `FakeDialogueView`/`FakeStageView`를 주입하면 스크립트를 화면 없이 끝까지 돌려 **(a) 방출 대사 순서 (b) 선택 경로
  (c) 최종 변수값**을 단정할 수 있습니다.
- `random`은 시드 주입 PRNG라 결정론적. 그래서 분기·`while`·조건부 선택지·call/return을 자동 회귀 테스트 가능.

---

## 7. 새 명령을 추가하려면 (확장 체크리스트)

1. `Core/Parsing/Command.cs`에 AST 노드 추가.
2. `Core/Parsing/Parser.cs`의 `ParseStatement` 분기에 파싱 추가.
3. `Core/Runtime/Instruction.cs`의 `Op`에 opcode 추가(필요 시).
4. `Core/Runtime/Compiler.cs`의 `EmitCommand`에 방출 로직 추가.
5. `Core/Runtime/Interpreter.cs`의 `Tick` switch에 실행 로직 추가.
6. 화면 효과가 필요하면 `IDialogueView`/`IStageView`에 메서드 추가 + Unity/Fake 양쪽 구현.
7. 세이브 대상 상태가 늘면 `SaveData` + `CaptureSave`/`RestoreSave` 갱신([04](04-save-load-format.md)).
8. EditMode 테스트 추가.

---

관련 문서: **[01 대본 언어](01-vns-language-reference.md)** · **[02 표현식](02-expression-language.md)** · **[04 세이브/로드](04-save-load-format.md)**
