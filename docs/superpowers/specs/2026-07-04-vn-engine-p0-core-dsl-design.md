# VN 엔진 P0 — 코어 재설계 + DSL 설계

- **날짜**: 2026-07-04
- **프로젝트**: VN_Prototype (Unity 2022.3.27f1)
- **하위 프로젝트**: P0 (전체 로드맵의 첫 조각)
- **상태**: 설계 승인 대기 → 구현 계획(writing-plans)으로 이어짐

---

## 1. 배경 & 목표

### 최종 목표
**둥지짓는 드래곤** 같은 게임 한 편을 완성한다. 이 게임은 순수 VN이 아니라 **분기 서사 + 스탯/자원 관리 + 세대 진행**이 섞인 텍스트 기반 시뮬레이션이다. 따라서 엔진은 "서사 + 본격 시뮬레이션" 전체를 책임진다.

### 현재 상태
- 씬 `Main.unity`: `Main Camera`, `Canvas`(대화 UI), `EventSystem`, `DialogueManager`, `Background`(정적 스프라이트), 캐릭터 슬롯 `LeftSlot/CenterSlot/RightSlot`.
- 로직 전부가 **단일 785줄 MonoBehaviour** `DialogueManager.cs`에 뭉쳐 있다: JSON 로드, 타자기 효과, 캐릭터 3슬롯 show/hide, 선택지, 정수 변수 + effect(`"요르+10"`), 조건 분기(`"요르>=50"`), 정수 goto/end 흐름 제어, JsonUtility의 goto-누락 버그를 우회하는 수제 JSON 스캐너.
- 시나리오는 `StreamingAssets/dialogues.json` — 정수 인덱스 기반 goto/branches. 방대한 분기 서사에는 손으로 쓰기 고통스럽고 실수가 잦다.

### 문제
1. 모놀리식 구조 → 확장·테스트 불가. 배경/오디오/세이브/시뮬레이션을 얹을 경계가 없다.
2. JSON + 정수 인덱스 → 작가가 시나리오를 못 쓴다.
3. 변수 시스템이 int·단일비교·`+/-`뿐 → 시뮬레이션 판정을 표현할 수 없다.

### 전체 로드맵 (맥락)
엔진은 독립 하위 프로젝트로 나눠 순서대로 쌓는다. 각 하위 프로젝트는 자체 spec → plan → 구현 사이클을 가진다.

| 순서 | 하위 프로젝트 | 내용 |
|---|---|---|
| **P0 (이 문서)** | 코어 재설계 + DSL | 모듈 분리, DSL 파서, 인터프리터. 현재 기능 전부 재현 + 표현식 엔진 |
| P1 | 프레젠테이션 | 배경 전환, 캐릭터 표정/트랜지션, 백로그·오토·스킵, 페이드/디졸브 |
| P2 | 오디오 | BGM/SFX/보이스, 크로스페이드 |
| P3 | 세이브·설정·타이틀 | 상태 직렬화, 세이브 슬롯, 설정 메뉴, 타이틀 화면, persistent 데이터 |
| P4 | 시뮬레이션 | 자원 생산/소비 루프, 세대 진행, 스탯 기반 판정 훅 |

**P0의 완료 = 지금의 요르 데이트 시나리오가 새 DSL로 똑같이 돌아가고, 표현식 엔진과 회귀 테스트가 갖춰진 상태.**

---

## 2. DSL 언어 사양 (`.vns`)

들여쓰기 기반, Ren'Py 계열. 파일 확장자 `.vns`. UTF-8.

### 2.1 명령 전체 (P0 범위)

```
# 주석은 '#' 로 시작, 줄 끝까지

# ── 캐릭터 정의 (파일 상단, 선택) ──
character 요르 name:"요르 (숲의 요정)" color:"#8fd3ff"
character 나  name:"나"

# ── 라벨: 점프 대상 (정수 인덱스 대체) ──
label 주말_약속:
    bg 공원                 # 배경 (P0에선 파싱/상태기록만, 실제 전환은 P1)
    show 요르 left          # 슬롯에 캐릭터 등장 (left/center/right)
    hide 민지               # 캐릭터 퇴장

    요르 "주말에 뭐 할래?"   # 화자 대사 (id 또는 리터럴 이름)
    "…정적이 흘렀다."        # 나레이션 (화자 없음)
    요르 "남은 골드는 [골드]개야."  # 텍스트 보간: [변수] → 현재 값

    # ── 선택지 (조건부 선택지 포함) ──
    menu:
        "같이 영화 보자":
            $ 요르 += 30
            jump 데이트
        "금화를 준다 (10 필요)" if 골드 >= 10:   # 조건 거짓이면 숨김
            $ 골드 -= 10
            jump 뇌물
        "귀찮은데 집에":
            $ 요르 -= 10
            jump 거절

label 데이트:
    # ── 조건 분기 (branches 대체) ──
    if 요르 >= 50 and 만난적:
        요르 "우리 사귈래?"
    elif 요르 >= 0:
        요르 "그냥 친구로 지내자."
    else:
        요르 "우리 안 맞는 것 같아."

    # ── 반복 (자원 틱/세대 루프용) ──
    while 남은턴 > 0:
        $ 골드 = 골드 + 채집량
        $ 남은턴 -= 1

    return                  # 스크립트/서브루틴 종료

# ── 서브루틴 ──
call 공통_인트로            # 실행 후 다음 줄로 복귀
jump 다른라벨               # 복귀 없이 점프
```

### 2.2 명령 요약표

| 명령 | 문법 | 의미 |
|---|---|---|
| 주석 | `# ...` | 무시 |
| 캐릭터 정의 | `character <id> name:"..." color:"#hex"` | 표시명·이름색 정의. `color`는 선택 |
| 라벨 | `label <name>:` | 점프 대상 이름 |
| 대사 | `<id\|"이름"> "텍스트"` | 화자 있는 대사 |
| 나레이션 | `"텍스트"` | 화자 없는 대사 |
| 배경 | `bg <name>` | 배경 상태 기록 (P0: 렌더는 P1) |
| 등장 | `show <char> <left\|center\|right>` | 슬롯에 스프라이트 |
| 퇴장 | `hide <char>` | 슬롯에서 제거 |
| 선택지 | `menu:` + `"라벨"[ if <조건>]:` 블록 | 분기 선택 |
| 대입 | `$ <var> = <식>` / `+= -= *= /=` | 변수 변경 |
| 조건 | `if <식>:` / `elif <식>:` / `else:` | 조건 분기 |
| 반복 | `while <식>:` | 조건 참인 동안 반복 |
| 점프 | `jump <label>` | 복귀 없는 이동 |
| 호출 | `call <label>` | 복귀하는 이동 |
| 복귀 | `return` | 서브루틴/스크립트 종료 |

### 2.3 표현식 엔진

**타입 (P0)**: 정수(int), 불(bool). *실수(float)는 나중에 표현식 엔진에 얹는다 — P0 범위 밖.* 문자열은 `character` 정의값 등 리터럴로만 쓰이고 변수 타입으로는 P0에서 미지원.

**정의 안 된 변수** = `0`(정수 문맥) / `false`(불 문맥). 현재 동작(`TryGetValue` → 0)과 동일하게 "시작 시 0" 보장.

**연산자**
- 산술: `+ - * / %` — 정수 연산. **나눗셈은 정수 나눗셈** (`7 / 2 == 3`). `%`는 나머지.
- 비교: `>= <= > < == !=` — 변수 대 상수, **변수 대 변수** 모두.
- 논리: `and`, `or`, `not`.
- 괄호 `( )` 로 우선순위 지정. 표준 우선순위: `단항 not/-` > `* / %` > `+ -` > 비교 > `and` > `or`.
- 내장 함수: `random(a, b)` — a 이상 b 이하 정수 난수(양 끝 포함).

**대입**: `$ var = <식>`, 그리고 복합대입 `+= -= *= /=` (`$ 골드 += 채집량 * 2`).

**텍스트 보간**: 대사 문자열 안 `[변수]` → 실행 시점 값으로 치환. `[[` 는 리터럴 `[`.

### 2.4 다중 파일 & 전역 라벨

- `Assets/StreamingAssets/scripts/` (경로는 구현 시 확정) 아래 모든 `.vns` 를 로드.
- `label` 은 **전역 네임스페이스**. `jump`/`call` 은 파일 경계를 넘어 라벨을 찾는다.
- 라벨 이름 중복 → 로드 시 에러(명확한 메시지).
- 진입점: 지정된 시작 라벨(기본 `start`) 또는 인스펙터에서 설정.

---

## 3. 아키텍처 & 실행 모델

### 3.1 파일 구조

`Assets/Scripts/VNEngine/` (신규 네임스페이스 `VNEngine`):

```
Parsing/
  Token.cs          # 토큰 종류 정의
  Lexer.cs          # 텍스트 → 토큰 (들여쓰기(INDENT/DEDENT) 인식)
  Command.cs        # AST 노드: Say/Narration/Show/Hide/Bg/Menu/Set/If/While/Jump/Call/Return/Label/CharacterDef
  Parser.cs         # 토큰 → Command 트리
Runtime/
  Compiler.cs       # 중첩 블록(if/menu/while) → 평탄한 Instruction 배열 + 점프 주소
  Instruction.cs    # 평탄화된 실행 단위 (opcode + 인자 + 점프 타깃)
  Interpreter.cs    # PC + 콜스택으로 Instruction 실행. 화면/오디오를 모름
  GameState.cs      # 변수 저장소 (Dictionary<string,int> + bool). 세이브 대상
  Expression.cs     # 표현식 AST
  ExpressionEval.cs # 표현식 평가기 (파서 + 평가)
Presentation/
  IDialogueView.cs  # 인터페이스: Say(speaker,text) 대기, ShowChoices 대기, 정리
  IStageView.cs     # 인터페이스: ShowCharacter/HideCharacter/SetBackground
  DialogueViewUnity.cs  # TMP 기반 실제 구현 (타자기, 선택지 버튼)
  StageViewUnity.cs     # SpriteRenderer 슬롯 기반 실제 구현
VNRunner.cs         # MonoBehaviour 진입점 (DialogueManager 대체). 로드→컴파일→인터프리터 구동
```

### 3.2 핵심 결정 — "컴파일 후 실행"

중첩된 `if`/`menu`/`while` 블록을 **평탄한 Instruction 배열**로 컴파일한다. 인터프리터는 `PC`(정수 인덱스)와 콜스택만으로 실행한다.

- `if/elif/else` → 조건부 점프(`JumpIfFalse`) + 무조건 점프로 컴파일.
- `while` → 조건 검사 + 뒤로 점프(backward jump). **무한루프 방지**: 인터프리터 한 번 구동당 스텝 상한(예: 100,000) 초과 시 에러 + 중단.
- `menu` → 선택지 표시 명령 + 각 선택지 본문 블록 + 본문 끝 점프.
- `call`/`return` → 콜스택에 복귀 PC를 push/pop.

**이유**: 세이브/로드(P3)가 거의 공짜가 된다 — 저장 대상이 `PC + 콜스택 + GameState` 뿐. 트리를 재귀로 걷는 방식이면 실행 위치를 직렬화하기 어렵다.

### 3.3 "VM은 화면을 모른다"

인터프리터는 구체 Unity 타입 대신 인터페이스에만 의존한다:
- `IDialogueView.Say(speaker, text)` — 대사 표시하고 **사용자 진행 입력까지 대기** (코루틴/async 완료로 신호).
- `IDialogueView.ShowChoices(choices)` — 선택지 표시하고 **선택 인덱스 반환**.
- `IStageView.ShowCharacter/HideCharacter/SetBackground`.

인터프리터는 코루틴(또는 async) 스텝 루프로 돌며, 대사/선택지에서 View 완료를 `yield`/`await` 한다. TMP·코루틴·버튼 인스턴싱은 전부 `*Unity.cs` 구현 안에만 존재.

### 3.4 진행 입력

현재처럼 화면 클릭으로 진행. `VNRunner`(또는 `DialogueViewUnity`)가 입력을 받아 진행 신호를 View 완료로 변환. `EventSystem.IsPointerOverGameObject()` 로 UI 위 클릭은 버튼이 처리(현재 동작 유지).

---

## 4. 테스트 전략

파서·컴파일러·인터프리터·표현식 평가기는 **순수 C#(Unity 의존 0)** → Unity Test Framework **EditMode 테스트**로 검증. `Assets/Tests/Editor/` 에 테스트 어셈블리(asmdef) 구성.

- **Lexer/Parser 테스트**: `.vns` 텍스트 → 기대 Command 트리 / 파싱 에러 메시지.
- **Expression 테스트**: `(용기+지혜)*2`, `골드>=10 and 만난적`, 정수 나눗셈 `7/2==3`, `%`, 연산자 우선순위, 변수 대 변수 비교, `random` 범위, 미정의 변수 = 0.
- **Compiler 테스트**: if/menu/while → 올바른 점프 주소.
- **Interpreter 테스트**: `FakeDialogueView`/`FakeStageView` 주입 → 스크립트 끝까지 구동 후 **(a) 방출된 대사 순서 (b) 선택 경로 (c) 최종 변수 값** 검증. 분기·jump·call/return·while·조건부 선택지가 화면 없이 맞는지 확인. `random` 은 시드 주입 가능한 난수원으로 결정론적 테스트.

지금 785줄 모놀리식은 이런 테스트가 원천 불가능하다.

---

## 5. 마이그레이션 & 완료 기준

### 마이그레이션
1. 기존 `dialogues.json`의 요르 데이트 시나리오를 `intro.vns` 로 이식.
2. 씬 `Main`: `DialogueManager` 컴포넌트 → `VNRunner` 로 교체. `Canvas`·슬롯·`EventSystem` 재사용. `DialogueViewUnity`/`StageViewUnity` 를 씬 오브젝트에 배치·연결.
3. `DialogueManager.cs` 는 파리티 확인 전까지 **남겨둔다**. 확인 후 삭제.
4. `StreamingAssets/dialogues.json` 은 파리티 확인 후 제거(또는 참고용 보관).

### 완료(Done) 기준
1. **파리티**: 요르 데이트 시나리오가 새 DSL(`intro.vns`)로 기존과 동일하게 재현 (대사 순서, 선택지, 호감도 분기 결과 일치).
2. **테스트**: EditMode 테스트 전부 통과 (파서·표현식·컴파일러·인터프리터).
3. **클린 빌드**: Unity 콘솔 컴파일 에러 0, 신규 코드 경고 0.
4. **모듈 경계**: 인터프리터가 UnityEngine UI/TMP 타입을 직접 참조하지 않음(인터페이스 경유).

---

## 6. 범위 밖 (명시적 제외)

- 페이드/디졸브, 캐릭터 표정 애니메이션, 백로그·오토·스킵 → **P1**.
- 오디오 명령(BGM/SFX/보이스) → **P2**. `bg` 는 P0에서 상태만 기록(렌더 없음).
- 세이브/로드, 설정 메뉴, 타이틀 화면, `persistent` 데이터 → **P3**.
- 자원 루프·세대 진행·전투 판정 시스템 → **P4** (`while`/표현식/`random` 은 P0에서 미리 깔아둠).
- 실수(float) 타입 → 나중에 표현식 엔진에 확장.
- 문자열 변수 → 나중.

---

## 7. 열린 질문 (구현 계획에서 확정)

- `.vns` 스크립트 폴더 위치: `StreamingAssets/scripts/` vs `Resources/` vs 에디터 임포트. (StreamingAssets 유력 — 현재 방식과 일관)
- 인터프리터 구동: Unity 코루틴 vs async/await. (코루틴 유력 — Unity 친화적, 현재 코드와 일관)
- 진입 라벨 지정 방식: 고정 `start` vs `VNRunner` 인스펙터 필드.
