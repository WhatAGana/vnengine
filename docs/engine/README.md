# VN 엔진 스펙 & 매뉴얼

이 폴더는 지금까지 구현된 **VN 엔진**(스토리 파서 + 실행 VM + 시뮬레이션 커널)의 사양서와 사용 설명서입니다.
작가(대본 작성자)와 개발자가 각각 필요한 문서만 골라 읽을 수 있도록 나눠 두었습니다.

> 이 문서들은 **실제 구현된 코드의 동작**을 기준으로 씁니다. 설계 초안(`docs/superpowers/specs/`)의
> "하고 싶은 것"이 아니라, 현재 코드가 **실제로 하는 것**을 기술합니다. 구현이 바뀌면 이 문서도 갱신합니다.

---

## 이 엔진이 하는 일 (한눈에)

`.vns` 라는 전용 대본 언어로 스토리·선택지·조건 분기·변수·반복을 쓰면,
엔진이 그것을 **바이트코드처럼 평탄한 명령 배열로 컴파일**한 뒤 **작은 가상 머신(VM)**으로 실행합니다.
VM은 화면·오디오를 전혀 모르고, `IDialogueView` / `IStageView` 인터페이스로만 바깥과 통신합니다.
덕분에 Unity 없이 순수 C#으로 대본 로직을 테스트할 수 있고, 세이브/로드가 거의 공짜로 붙습니다.

```
.vns 텍스트
   │  LineReader        논리 줄로 분해(들여쓰기 측정·주석 제거)
   ▼
논리 줄(LogicalLine)
   │  Parser            들여쓰기 기반 → AST(Command 트리)
   ▼
AST(Command)
   │  Compiler          중첩 블록(if/menu/while) → 평탄한 Instruction[] + 라벨 주소
   ▼
VnProgram(바이트코드)
   │  Interpreter       PC + 콜스택으로 실행. 대사/선택지에서 멈춰 입력 대기
   ▼
IDialogueView / IStageView  ← Unity 구현이 실제 화면에 그림
```

---

## 문서 지도

| # | 문서 | 대상 | 내용 |
|---|---|---|---|
| — | **[README](README.md)** (이 문서) | 전원 | 전체 개요·문서 지도 |
| 01 | **[.vns 대본 언어 레퍼런스](01-vns-language-reference.md)** | **작가** | 모든 명령·선택지·분기·반복 문법. 스토리 입력의 정식 설명서 |
| 02 | **[표현식 언어 스펙](02-expression-language.md)** | 작가·개발자 | 변수·연산자·우선순위·`random()`·타입 규칙 |
| 03 | **[아키텍처 & 실행 모델](03-architecture-and-execution.md)** | 개발자 | 파서→컴파일러→VM 파이프라인, 명령어셋(opcode), 프레젠테이션 계약, 로딩 |
| 04 | **[세이브/로드 포맷](04-save-load-format.md)** | 개발자 | `SaveData` 직렬화 포맷·호환성 가드·복원 규칙 |
| 05 | **[시뮬레이션 커널 스펙](05-simulation-kernel.md)** | 개발자 | 턴 루프·자원·커맨드(경영/디펜스 시뮬의 토대) |

---

## 코드 위치

| 레이어 | 경로 | Unity 의존 |
|---|---|---|
| 순수 코어 (파서·VM·표현식·시뮬) | `Assets/Scripts/VNEngine/Core/**` | **없음** (순수 C#) |
| Unity 프레젠테이션·로딩 | `Assets/Scripts/VNEngine/Unity/**` | 있음 |
| `.vns` 임포터 (에디터 전용) | `Assets/Scripts/VNEngine/Editor/VnsImporter.cs` | 있음 |
| 예제 대본 | `Assets/Resources/scripts/intro.vns` | — |

**핵심 원칙**: `Core/**` 는 `UnityEngine`·`System.IO`를 절대 참조하지 않습니다. 그래야 EditMode 단위 테스트가
화면 없이 돌고, Android/iOS/WebGL에서 안전합니다. IO·렌더는 전부 `Unity/**` 레이어에만 있습니다.

---

## 빠른 시작 (작가용)

`Assets/Resources/scripts/` 아래에 `.vns` 파일을 만들고 다음처럼 씁니다.

```vns
character 요르 name:"요르" color:"#8fd3ff"

label start:
    bg city_sunset
    show 요르 left
    요르 "주말에 뭐 할래?"
    menu:
        "같이 영화 보자":
            $ 호감도 += 30
            jump after
        "귀찮은데 집에":
            $ 호감도 -= 10
            jump after

label after:
    if 호감도 >= 30:
        요르 "너랑 있으면 즐거워."
    else:
        요르 "그냥 친구로 지내자."
    return
```

씬의 `VNRunner` 컴포넌트가 시작 라벨(`start`)부터 실행합니다. 전체 문법은 **[01번 문서](01-vns-language-reference.md)** 참고.
