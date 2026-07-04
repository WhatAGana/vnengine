# VN_Prototype SETUP 가이드 (초보자용)

이 문서는 Unity 에디터를 거의 처음 다뤄 본다는 가정으로, 메뉴 클릭 하나하나까지 적어 두었다.
아래 순서대로 따라가면 **JSON에서 대사를 읽어 타자기 효과로 출력하고, 화면을 클릭하면 다음 줄로 넘어가는** 최소 비주얼노벨이 동작한다.

- Unity 버전: **2022.3.27f1** (이미 이 버전으로 프로젝트가 만들어져 있다)
- 템플릿: 2D Built-in
- 사용 패키지: **TextMeshPro** (Unity 기본 포함)

---

## 0. 준비된 파일 확인

아래 3개 파일이 이미 만들어져 있어야 한다. Unity 에디터의 **Project 창**에서 확인한다.

- `Assets/Scripts/DialogueManager.cs`
- `Assets/StreamingAssets/dialogues.json`
- `SETUP.md` (이 문서)

만약 `Assets/StreamingAssets` 폴더가 Project 창에 보이지 않으면, 에디터를 한 번 포커스했다가 돌아오면 새로 고침 된다.

---

## 1. 프로젝트 열기

1. **Unity Hub** 실행.
2. **Open** 버튼 클릭 → `D:\Prototypeing\VN_Prototype\VN_Prototype` 폴더 선택 → **Select Folder**.
3. 처음 열면 컴파일과 임포트가 1~3분 걸릴 수 있다. 진행 막대가 끝날 때까지 기다린다.

---

## 2. TextMeshPro 기본 리소스 가져오기

처음 TMP를 사용할 때 한 번만 하면 된다.

1. 상단 메뉴 **Window → TextMeshPro → Import TMP Essential Resources** 클릭.
2. 뜨는 창에서 오른쪽 아래 **Import** 버튼 클릭.
3. 잠시 후 Project 창에 `Assets/TextMesh Pro` 폴더가 생긴다. 창을 닫는다.

> ⚠️ 이 단계를 빼먹으면 TMP 텍스트 컴포넌트를 만들 때 폰트가 없다는 경고가 뜬다.

---

## 3. 한글 폰트 준비 (한글 대사를 쓸 거면 필수)

기본 TMP 폰트(LiberationSans)는 **한글이 □(두부)로 깨져 보인다.** 한글 Font Asset을 만들어 준다.

### 3-1. ttf 파일 준비
1. **Noto Sans KR** 등 무료 한글 폰트 ttf 파일을 다운로드한다.
   - 예: <https://fonts.google.com/noto/specimen/Noto+Sans+KR> → **Get font → Download all**
   - 압축을 풀어 `NotoSansKR-Regular.ttf` 같은 파일 하나만 골라 둔다.
2. Unity Project 창에서 `Assets` 우클릭 → **Create → Folder** → 이름을 `Fonts` 로 정한다.
3. 윈도우 탐색기에서 ttf 파일을 드래그해서 Unity의 `Assets/Fonts` 폴더 위에 놓는다.

### 3-2. Font Asset 생성
1. Project 창에서 방금 넣은 `NotoSansKR-Regular` 를 **우클릭**.
2. **Create → TextMeshPro → Font Asset** 클릭.
3. `NotoSansKR-Regular SDF` 라는 파일이 같은 폴더에 생긴다. 이것이 TMP가 실제로 사용할 폰트다.

> 💡 한글 글자 수가 많으면 Atlas를 키워야 한다. 일단 기본 설정으로 두고, 글자가 깨져 보이면 나중에 Font Asset의 **Atlas Resolution**을 4096으로 올린다.

---

## 4. 씬 열기

1. Project 창에서 `Assets/Scenes/SampleScene` 더블 클릭.
2. 상단 **Hierarchy** 창에 `Main Camera` 정도만 있는 빈 씬이 보이면 정상.

---

## 5. UI 만들기

### 5-1. Canvas 만들기
1. **Hierarchy** 창의 빈 공간에서 우클릭 → **UI → Canvas** 클릭.
2. `Canvas`, `EventSystem` 두 개가 자동으로 생성된다. 그대로 둔다.
3. 새로 생긴 `Canvas` 를 클릭해서 선택.
4. 오른쪽 **Inspector** 창의 **Canvas Scaler** 컴포넌트:
   - **UI Scale Mode** → `Scale With Screen Size` 로 변경
   - **Reference Resolution** → X: `1920`, Y: `1080` 입력
   - **Match** 슬라이더 → `0.5` 로

### 5-2. 대사창 패널 만들기
1. Hierarchy에서 `Canvas` 를 **우클릭** → **UI → Image** 클릭. 이름이 `Image` 로 추가된다.
2. `Image` 클릭한 채 F2 키 (또는 우클릭 → Rename) → 이름을 **`DialoguePanel`** 로 바꾼다.
3. `DialoguePanel` 선택 상태에서 Inspector 의 **Rect Transform** 설정:
   - 좌측 위 **앵커 프리셋 박스**(네모 안에 십자 모양 아이콘) 클릭.
   - **Alt 키를 누른 채** 맨 아래 줄 가운데(bottom-stretch) 클릭. (가로 stretch + 아래 정렬)
   - 그러면 패널이 화면 아래쪽에 가로로 길게 붙는다.
   - **Height** 값을 `300` 으로 설정.
   - **Pos Y** 값을 `30` 으로 설정 (화면 바닥에서 30 픽셀 위로 띄움).
   - **Left**, **Right** 값을 각각 `80` 으로 설정 (좌우 여백).
4. Inspector 의 **Image** 컴포넌트:
   - **Color** 클릭 → 색 선택창에서 RGBA `0, 0, 0, 180` 정도로 (반투명 검정).

### 5-3. 화자 이름 텍스트 만들기
1. Hierarchy에서 `DialoguePanel` **우클릭** → **UI → Text - TextMeshPro** 클릭.
2. 이름을 **`SpeakerText`** 로 바꾼다.
3. `SpeakerText` 선택 → Inspector 의 **Rect Transform**:
   - 앵커 프리셋 → **top-left** 클릭.
   - **Pos X** `30`, **Pos Y** `-20`
   - **Width** `400`, **Height** `60`
4. **TextMeshPro - Text (UI)** 컴포넌트:
   - **Text Input** 칸 비우거나 `이름` 같은 임시 문자 입력.
   - **Font Asset** → 3-2에서 만든 `NotoSansKR-Regular SDF` 를 드래그해서 넣는다.
   - **Font Size** → `36`
   - **Color** → 노란빛 (예: RGBA `255, 220, 100, 255`)

### 5-4. 대사 본문 텍스트 만들기
1. Hierarchy에서 `DialoguePanel` **우클릭** → **UI → Text - TextMeshPro** 클릭.
2. 이름을 **`DialogueText`** 로 바꾼다.
3. `DialogueText` 선택 → Inspector 의 **Rect Transform**:
   - 앵커 프리셋 → **Alt 키 누른 채 stretch-stretch** (오른쪽 맨 아래) 클릭. 부모 패널 전체에 채워진다.
   - **Left** `30`, **Right** `30`, **Top** `90`, **Bottom** `20`
4. **TextMeshPro - Text (UI)** 컴포넌트:
   - Text Input 비우기.
   - **Font Asset** → `NotoSansKR-Regular SDF` 드래그.
   - **Font Size** → `40`
   - **Color** → 흰색 (RGBA `255, 255, 255, 255`)
   - **Vertex → Wrapping** → `Enabled` (기본값 그대로면 OK)

---

## 6. DialogueManager 오브젝트 만들기

1. Hierarchy의 빈 공간에서 **우클릭 → Create Empty** 클릭.
2. 새 GameObject 이름을 **`DialogueManager`** 로 바꾼다.
3. `DialogueManager` 선택 상태에서 Inspector 아래쪽의 **Add Component** 버튼 클릭.
4. 검색창에 `DialogueManager` 입력 → 같은 이름 스크립트 클릭. 컴포넌트가 붙는다.

### 6-1. 인스펙터 연결
`DialogueManager` 컴포넌트의 칸을 다음과 같이 채운다. Hierarchy에서 해당 오브젝트를 잡고 그대로 빈 칸에 **드래그**하면 된다.

| 필드 | 끌어다 놓을 오브젝트 |
|---|---|
| **Dialogue Panel** | Hierarchy의 `DialoguePanel` |
| **Speaker Text** | Hierarchy의 `SpeakerText` |
| **Dialogue Text** | Hierarchy의 `DialogueText` |

설정 값:
- **Json File Name** → `dialogues.json` (기본값 그대로)
- **Typing Speed** → `0.04` (기본값 그대로, 더 빠르게 하려면 `0.02` 정도)
- **Hide Panel On End** → 체크 안 함 (체크하면 마지막 대사 뒤 패널이 사라진다)

---

## 7. Raycast 차단 끄기 (선택이지만 추천)

대사창을 클릭해도 다음 대사로 잘 넘어가게 하려면, UI 이미지/텍스트가 클릭 이벤트를 가로채지 않게 둬야 한다. 이 프로젝트는 `Input.GetMouseButtonDown` 으로 직접 클릭을 받기 때문에 사실상 영향이 없지만, 안전하게 다음을 끄자.

1. `DialoguePanel` 선택 → Inspector 의 **Image** 컴포넌트 → **Raycast Target** 체크 해제.
2. `SpeakerText`, `DialogueText` 각각 선택 → **TextMeshPro - Text (UI)** 의 **Extra Settings → Raycast Target** 체크 해제.

---

## 8. 저장하고 실행

1. **Ctrl + S** 로 씬 저장.
2. 상단 가운데 ▶ **Play** 버튼 클릭.
3. Game 뷰에 첫 줄 `"비가 추적추적 내리는 늦은 저녁이었다."` 가 한 글자씩 타자기 효과로 출력되면 성공.
4. Game 뷰를 **클릭**해 본다.
   - 타자 중 클릭하면 → 그 줄의 전체 텍스트가 바로 표시.
   - 한 줄이 모두 표시된 뒤 클릭 → 다음 줄로 진행.
5. 마지막 줄 뒤 클릭하면 `— 끝 —` 이 표시된다.

---

## 9. 대사 바꿔 보기

1. Unity 에디터 외부에서 `Assets/StreamingAssets/dialogues.json` 을 메모장이 아닌 **UTF-8 지원 에디터**(VS Code, Notepad++)로 연다. (메모장은 BOM을 붙여서 파싱이 깨질 수 있다.)
2. `lines` 배열에 항목을 추가하거나 수정한다. 형식은 다음과 같다:
   ```json
   { "speaker": "캐릭터 이름", "text": "대사 내용" }
   ```
3. 저장 후 Unity 에서 다시 ▶ Play. 코드 재컴파일 없이 바로 반영된다.

---

## 10. 자주 나는 문제

| 증상 | 원인 / 해결 |
|---|---|
| 글자가 □ 로 보임 | 한글 Font Asset을 만들지 않았거나, `SpeakerText` / `DialogueText` 의 Font Asset이 기본(LiberationSans) 상태. 3장 다시 진행. |
| Console에 `JSON 파일을 찾을 수 없다` | `Assets/StreamingAssets/dialogues.json` 경로/철자 확인. `Json File Name` 필드 값도 확인. |
| 글자가 안 나타남 / Console에 `dialogueText 가 비어 있다` | 6-1에서 Dialogue Text 칸에 `DialogueText` 오브젝트를 끌어다 놓지 않았다. |
| 타자기 효과가 너무 빠르거나 느림 | `DialogueManager` 의 **Typing Speed** 조절. 값이 작을수록 빠르다. |
| 클릭해도 반응이 없음 | 씬에 `EventSystem` 이 있는지 확인 (5-1에서 자동 생성됨). 또는 Game 뷰에 포커스가 있는지 확인. |
| 한글이 깨지지 않지만 자모 분리됨 | Noto Sans KR **Regular** ttf를 사용했는지 확인. Black/Thin 등 일부 weight는 글리프 누락이 있을 수 있다. |

---

## 11. 캐릭터 등록하기

대사창 위에 캐릭터 스프라이트를 띄우는 기능이다. 아래 순서대로 한 번만 세팅해 두면, 이후로는 JSON만 고쳐도 등장/퇴장이 동작한다.

### 11-1. 캐릭터 스프라이트 임포트
1. Project 창에서 `Assets` 우클릭 → **Create → Folder** → 이름 `Characters`.
2. 윈도우 탐색기에서 PNG 파일들(예: `민지.png`, `지훈.png`)을 `Assets/Characters` 폴더 위로 드래그.
3. Project 창에서 임포트된 스프라이트를 클릭 → Inspector:
   - **Texture Type** → `Sprite (2D and UI)` (2D 템플릿이면 기본값)
   - **Pixels Per Unit** → `100` (기본). 캐릭터가 너무 크거나 작으면 200, 50 등으로 조절.
   - **Apply** 버튼 클릭.

### 11-2. 슬롯(위치) GameObject 만들기
캐릭터가 설 위치를 카메라가 보는 월드 공간에 미리 잡아 둔다.

1. Hierarchy 빈 공간 우클릭 → **Create Empty** → 이름 `LeftSlot`.
2. Inspector → **Transform** → **Position**: X = `-5`, Y = `-1`, Z = `0`
3. 같은 방법으로 `CenterSlot` 만들고 Position: X = `0`, Y = `-1`, Z = `0`
4. 같은 방법으로 `RightSlot` 만들고 Position: X = `5`, Y = `-1`, Z = `0`

> 💡 기본 카메라(Orthographic Size 5, 16:9)에서 화면 가로는 약 −9 ~ +9. 캐릭터 크기에 따라 X 값을 조절해라.

### 11-3. DialogueManager 에 슬롯 연결
1. Hierarchy 에서 `DialogueManager` 선택.
2. Inspector 의 **Character Slots** 섹션에 Hierarchy 의 슬롯을 끌어다 놓는다:
   - **Left Slot** ← `LeftSlot`
   - **Center Slot** ← `CenterSlot`
   - **Right Slot** ← `RightSlot`

### 11-4. Characters 리스트에 캐릭터 등록
1. `DialogueManager` 인스펙터의 **Characters** 항목 옆 ▶ 화살표 클릭해서 펼친다.
2. **Size** 칸에 등록할 캐릭터 수를 입력 (예: `2`).
3. `Element 0` 펼치기:
   - **Name** → 정확히 JSON 에서 쓸 이름과 같게 (예: `민지`)
   - **Sprite** → Project 창에서 `Assets/Characters/민지.png` 를 드래그
4. `Element 1` 도 같은 방식으로 채운다 (`지훈` 등).

> 💡 **Character Scale** (기본 `0.35`) 필드 하나로 등장하는 모든 캐릭터의 크기가 일괄 조절된다. 원본 PNG가 너무 커서 화면을 꽉 채우면 더 작게(예: `0.2`), 너무 작으면 더 크게(예: `0.6`) 조정. Sprite 별로 따로 맞추고 싶다면 PNG 임포트 설정의 **Pixels Per Unit** 으로 보정해라.

### 11-5. 배경을 깔고 싶다면 (선택)
1. Hierarchy 우클릭 → **2D Object → Sprites → Square** (또는 배경 스프라이트를 드래그해 GameObject 생성).
2. 이름을 `Background` 로, Position Z = `0`, 크기 적절히 키운다.
3. Inspector 의 **Sprite Renderer → Order in Layer** → `0` (또는 음수).
   - `DialogueManager` 의 **Character Sorting Order** (기본 `5`) 보다 작은 값이면 캐릭터가 배경 앞에 보인다.
   - Canvas(Screen Space - Overlay) 의 대사창은 자동으로 캐릭터/배경 위에 그려진다.

---

## 12. JSON 작성 예시 (캐릭터 포함)

`Assets/StreamingAssets/dialogues.json` 의 각 줄에 다음 **선택 필드**를 더할 수 있다.

| 필드 | 의미 | 값 |
|---|---|---|
| `show` | 이 줄에서 등장시킬 캐릭터 이름 | Characters 리스트의 **Name** 과 정확히 일치 |
| `position` | 등장 위치 | `"left"` / `"center"` / `"right"` (없으면 `center`) |
| `hide` | 이 줄에서 퇴장시킬 캐릭터 이름 | Characters 리스트의 **Name** |

세 필드 모두 **있어도 되고 없어도 된다.** 한 줄에서 `hide` 와 `show` 를 같이 쓸 수도 있다. 같은 위치에 다른 캐릭터가 `show` 되면 기존 캐릭터는 자동으로 치워진다.

### 12-1. 전체 예시

```json
{
  "lines": [
    { "speaker": "민지", "text": "안녕! 오랜만이야.", "show": "민지", "position": "left" },
    { "speaker": "지훈", "text": "어, 진짜 오랜만이네.",     "show": "지훈", "position": "right" },
    { "speaker": "민지", "text": "근데 나 이제 가 봐야 해.", "hide": "민지" },
    { "speaker": "지훈", "text": "잘 가." }
  ]
}
```

동작 흐름:
1. 1번 줄 → `민지` 가 **왼쪽**에 등장, 대사 출력.
2. 2번 줄 → `지훈` 이 **오른쪽**에 등장 (민지는 그대로 왼쪽에 남음).
3. 3번 줄 → `민지` 퇴장.
4. 4번 줄 → 새 등장/퇴장 없음. 지훈만 화면에 남아 대사.

### 12-2. 위치 교체 예시

같은 위치에 다른 캐릭터가 오면 이전 캐릭터는 자동 제거된다.

```json
{
  "lines": [
    { "speaker": "민지", "text": "내가 먼저야.", "show": "민지", "position": "center" },
    { "speaker": "지훈", "text": "내가 자리 차지!", "show": "지훈", "position": "center" }
  ]
}
```

2번 줄에서 `지훈` 이 center 에 오면 `민지` 는 자동으로 사라진다.

### 12-3. 자주 나는 실수

| 증상 | 원인 |
|---|---|
| Console 에 `'민지' 캐릭터가 등록돼 있지 않다` | Characters 리스트의 **Name** 과 JSON 의 `show`/`hide` 문자열이 다르다. 대소문자/공백/한자/한글 차이 확인. |
| Console 에 `'foo' Slot Transform 이 인스펙터에 연결돼 있지 않다` | `position` 값이 left/center/right 이 아니거나, 해당 Slot 필드를 비워 뒀다. |
| 캐릭터가 대사창 앞에 겹쳐 보임 | Canvas Render Mode 가 `Screen Space - Overlay` 인지 확인. 일반적으로 자동으로 위에 그려진다. |
| 캐릭터가 배경에 가려져 안 보임 | 배경 SpriteRenderer 의 **Order in Layer** 를 `DialogueManager` 의 **Character Sorting Order** 보다 작게. |

---

## 13. 한글 폰트 아틀라스 미리 굽기 (글자 깨짐 해결)

대사 중 일부 글자가 □ 두부로 보이면, **그 글자가 Font Asset 의 아틀라스에 없기 때문**이다. 아래처럼 해결한다.

### 13-1. 왜 깨지는가
- TMP Font Asset 의 **Atlas Population Mode** 가 두 가지다.
  - **Dynamic** : 런타임에 글자가 필요해지면 ttf 원본에서 즉시 글리프를 떠 와 아틀라스에 넣는다. 보통 잘 되지만, **빌드 환경 / 모바일 / 아틀라스 부족** 같은 상황에서 실패하면 □ 가 보인다.
  - **Static** : 빌드 전에 구워 둔 글자만 표시. 안 구운 글자는 무조건 □.
- 가장 안전한 방법은 **JSON 에 등장하는 모든 글자를 미리 한 번 굽고, Atlas 를 채워 둔 채로 두는 것**이다. Dynamic 으로 둬도 미리 구워 두면 런타임 부담이 사라진다.

### 13-2. 방법 A: 빠른 임시 해결 — JSON 통째로 굽기

자동 추출 없이 1분 안에 끝내고 싶을 때.

1. `Assets/StreamingAssets/dialogues.json` 을 텍스트 에디터로 열어 **본문 전체 복사** (Ctrl+A → Ctrl+C).
2. Unity 상단 메뉴 **Window → TextMeshPro → Font Asset Creator** 클릭.
3. 창이 뜨면 다음과 같이 설정:
   - **Source Font File** → `NotoSansKR-Regular` ttf 드래그.
   - **Atlas Resolution** → `4096 x 4096` (한글이 많으면 필수).
   - **Character Set** → `Custom Characters` 로 변경.
   - **Custom Character List** 칸에 1번에서 복사한 JSON 본문 **그대로 붙여넣기** (`{`, `:`, 따옴표 등 섞여 있어도 OK — TMP 가 중복/중요하지 않은 문자를 알아서 처리).
   - **Render Mode** → `SDFAA` (기본값).
4. **Generate Font Atlas** 버튼 클릭. 진행 후 `Missing Characters` 칸이 비어 있는지 확인.
5. **Save** 버튼 클릭 → 기존 `NotoSansKR-Regular SDF.asset` 파일을 덮어쓴다 (또는 다른 이름으로 저장하고 SpeakerText/DialogueText 에 새로 연결).
6. ▶ Play 로 확인. 두부가 사라지면 끝.

> ⚠️ Missing Characters 가 남아 있으면 그 글자들은 여전히 □ 가 된다. ttf 자체가 해당 글리프를 안 갖고 있다는 뜻 — Noto Sans KR Regular 가 아닌 다른 weight 를 잘못 쓰고 있을 가능성이 높다.

### 13-3. 방법 B: 자동 추출 스크립트 (권장)

JSON 이 길어지거나 자주 수정되면 매번 통째로 붙여 넣는 게 번거롭다. 이미 프로젝트에 `Assets/Editor/GlyphExtractor.cs` 가 들어 있어서 메뉴 한 번이면 글자 목록이 추출된다.

1. Unity 상단 메뉴 **Tools → VN → Extract Glyphs from dialogues.json** 클릭.
2. Console 에 `N 개 글자를 추출해 Assets/Fonts/glyphs.txt 에 저장했다.` 로그가 뜬다.
3. Project 창에서 `Assets/Fonts/glyphs.txt` 가 핑(노란 테두리)된다. 확인.
4. **Window → TextMeshPro → Font Asset Creator** 열기.
   - **Source Font File** → `NotoSansKR-Regular` ttf.
   - **Atlas Resolution** → `4096 x 4096`.
   - **Character Set** → `Characters from File`.
   - **Character File** → `Assets/Fonts/glyphs.txt` 드래그.
   - **Render Mode** → `SDFAA`.
5. **Generate Font Atlas** → **Save** (덮어쓰기).
6. ▶ Play 로 확인.

이 스크립트는 다음을 추출한다:
- 모든 줄의 `speaker`, `text`
- `show`, `hide` 의 캐릭터 이름
- 시스템 메시지에서 쓰는 `— 끝 —`, `[대사 JSON 로드 실패]` 의 글자들

공백/제어 문자는 제외하고 정렬해서 BOM 없는 UTF-8 로 저장한다.

### 13-4. Atlas Population Mode 어떻게 둘까

방법 A 든 B 든, Save 한 Font Asset 을 Project 창에서 선택하고 Inspector 의 **Atlas Population Mode** 를 결정한다.

| 모드 | 장점 | 단점 | 권장 상황 |
|---|---|---|---|
| **Dynamic** (기본) | 새 글자도 런타임에 자동 추가 | 빌드 환경에서 누락 위험 | 개발 중 / JSON 자주 바뀜 — 일단 미리 구워 두고 Dynamic 유지 |
| **Static** | 빠르고 예측 가능, 빌드 안전 | 새 글자 추가 시 다시 굽기 필요 | 출시 직전 / 대사 확정 후 |

### 13-5. 나중에 JSON 에 글자 추가했을 때

대사를 늘리거나 새 캐릭터 이름이 들어오면 새 글자가 생긴다. 다음 절차:

1. 다시 **Tools → VN → Extract Glyphs from dialogues.json** 실행.
2. **Window → TextMeshPro → Font Asset Creator** 열기.
3. **Character File** 에 갱신된 `glyphs.txt` 드래그.
4. **Update Atlas Texture** (새로 만들면 SDF 설정이 초기화될 수 있어, Save As 한 기존 에셋을 Update 하는 게 안전).
5. **Save** 로 덮어쓰기.

Static 모드를 쓸 거면 빌드 전에 한 번 이 절차를 돌리는 걸 습관화하자.

---

## 14. 선택지(분기) + 흐름 제어 (`goto` / `end`)

대사 줄에 `choices` 배열을 더하면 그 줄에서 클릭 진행이 멈추고 화면에 버튼이 뜬다. 버튼을 누르면 지정한 `goto` 인덱스(0부터)의 줄로 점프한다.
추가로 **줄 단위 `goto`** 와 **`end`** 필드로 분기 이후의 흐름을 끊거나 합칠 수 있다 (14-4 참고).

### 14-1. 선택지 버튼 프리팹 만들기

1. Hierarchy 의 `Canvas` 를 **우클릭** → **UI → Button - TextMeshPro** 클릭. (TMP 가 없는 일반 Button 이 아니라 **TMP 버전**.)
2. 새 GameObject 이름을 **`ChoiceButton`** 으로 변경.
3. `ChoiceButton` 선택 → Inspector → **Rect Transform**:
   - **Width** `600`, **Height** `80`
4. **Image** 컴포넌트:
   - **Color** → 반투명 어두운 회색 (예: RGBA `30, 30, 30, 200`).
5. `ChoiceButton` 아래의 자식 **`Text (TMP)`** 선택:
   - **Font Asset** → `NotoSansKR-Regular SDF` 드래그.
   - **Font Size** → `36`
   - **Color** → 흰색.
   - **Alignment** → **Center / Middle** (가운데 정렬 가로/세로 모두).
   - Text Input 은 비우거나 `선택지 예시` 정도 임시 입력 (런타임에 라벨로 덮어쓰므로 비워도 OK).
6. Project 창에서 `Assets` 우클릭 → **Create → Folder** → 이름 `Prefabs`.
7. Hierarchy 의 `ChoiceButton` 을 끌어다 `Assets/Prefabs` 폴더 위에 놓는다 → 프리팹화. (파란색 큐브 아이콘으로 바뀐다.)
8. Hierarchy 의 `ChoiceButton` 은 이제 **삭제**한다. 런타임에 코드가 복제해서 띄울 것이므로 씬에 있으면 안 된다.

### 14-2. 선택지 컨테이너 만들기

버튼들이 자동으로 세로로 쌓일 자리를 미리 만들어 둔다.

1. Hierarchy 의 `Canvas` 를 **우클릭** → **Create Empty** → 이름 **`ChoicesContainer`**.
2. Inspector → **Rect Transform**:
   - 앵커 프리셋 → **center / middle** 클릭.
   - **Pos X** `0`, **Pos Y** `120` (대사창 위쪽).
   - **Width** `700`, **Height** `400`
3. **Add Component** → `Vertical Layout Group` 검색해 추가:
   - **Spacing** `10`
   - **Child Alignment** `Middle Center`
   - **Control Child Size** : Width ✅, Height ❌
   - **Use Child Scale** : 둘 다 ❌
   - **Child Force Expand** : Width ✅, Height ❌
4. (선택) **Add Component** → `Content Size Fitter`:
   - **Vertical Fit** → `Preferred Size` (버튼 개수에 따라 자동 크기 조절)

> 💡 `ChoicesContainer` 자체에 Image 컴포넌트는 굳이 안 붙여도 된다. 빈 컨테이너 그대로 쓴다.

### 14-3. DialogueManager 에 연결

1. Hierarchy 의 `DialogueManager` 선택.
2. Inspector 의 **Choices** 섹션:
   - **Choice Button Prefab** ← Project 의 `Assets/Prefabs/ChoiceButton` 프리팹 드래그.
   - **Choices Container** ← Hierarchy 의 `ChoicesContainer` 드래그.

### 14-4. JSON 작성 예시 (분기 + 흐름 제어)

`Assets/StreamingAssets/dialogues.json` 의 줄에 다음 필드를 더할 수 있다. **모두 선택**이고, 없으면 자연 진행.

| 필드 | 의미 | 값 |
|---|---|---|
| `choices` | 선택지 배열. 타자가 끝나면 버튼들이 뜨고 화면 클릭으로는 진행이 멈춘다. | `[{ "label": ..., "goto": ... }, ...]` |
| `choices[].label` | 버튼에 표시될 텍스트 | 문자열 |
| `choices[].goto` | 그 버튼 선택 시 점프할 `lines` 인덱스 | 0부터의 정수 |
| **`goto`** (줄 단위) | `choices` 없는 줄에서 클릭으로 진행할 때, 다음 줄 대신 이 인덱스로 점프 | 0부터의 정수 |
| **`end`** | `true` 면 이 줄 클릭 후 대화 종료 (`— 끝 —`) | bool |

우선순위: `end` > 줄 단위 `goto` > 자연 진행. (`choices` 가 있으면 그게 먼저 처리되어 클릭-진행 자체가 멈춘다.)

#### 예시 A — 분기 후 각자 끝 (`end: true` 사용)

```json
{
  "lines": [
    { "speaker": "민지", "text": "주말에 뭐 할래?",
      "choices": [
        { "label": "영화 보자",   "goto": 1 },
        { "label": "집에서 쉬자", "goto": 2 }
      ]
    },
    { "speaker": "지훈", "text": "콜! 7시에 보자.", "end": true },
    { "speaker": "지훈", "text": "그래, 푹 쉬자.", "end": true }
  ]
}
```

흐름:
1. 0번 → 민지가 묻는다. 두 버튼 표시.
2. **"영화 보자"** → 1번 (`콜! 7시에 보자.`) → 클릭 → `end: true` 라 `— 끝 —`.
3. **"집에서 쉬자"** → 2번 (`그래, 푹 쉬자.`) → 클릭 → `end: true` 라 `— 끝 —`.

두 갈래가 서로 흘러 들지 않는다.

#### 예시 B — 분기 후 공통 결말로 합류 (줄 단위 `goto` 사용)

```json
{
  "lines": [
    { "speaker": "민지", "text": "주말에 뭐 할래?",
      "choices": [
        { "label": "영화 보자",   "goto": 1 },
        { "label": "집에서 쉬자", "goto": 2 }
      ]
    },
    { "speaker": "지훈", "text": "콜!",  "goto": 3 },
    { "speaker": "지훈", "text": "그래." },
    { "speaker": "나레이션", "text": "그렇게 주말이 정해졌다.", "end": true }
  ]
}
```

흐름:
1. 0번 → 두 버튼.
2. **"영화 보자"** → 1번 (`콜!`) → 클릭 → 줄 단위 `goto: 3` → 3번 (`그렇게 주말이 정해졌다.`) → 클릭 → `end`.
3. **"집에서 쉬자"** → 2번 (`그래.`) → 클릭 → 줄 단위 `goto` 없음 → 자연 진행 → 3번 (`그렇게 주말이 정해졌다.`) → 클릭 → `end`.

두 갈래가 결말 3번에서 합쳐진다.

> 💡 합류 흐름에서 1번에 굳이 `goto: 3` 을 쓰는 이유: 그게 없으면 1번 (`콜!`) 다음에 자연 진행으로 2번 (`그래.`) 이 흘러들어 가서, "영화 보자" 를 고른 사람이 "집에서 쉬자" 의 대사까지 보게 된다. 줄 단위 `goto` 가 그 누수를 끊어 준다.

#### 흐름 제어 시 자주 나는 실수

| 증상 | 원인 |
|---|---|
| 분기 한쪽이 다른 쪽 대사까지 흐름 | 분기 끝에 `end: true` 도 줄 단위 `goto` 도 안 붙임. 둘 중 하나 필수. |
| `"goto": 0` 으로 처음으로 점프 안 됨 | 이건 정상 동작이어야 한다. 안 된다면 `LoadDialogue` 의 JSON 스캔이 그 줄을 못 찾은 것 — JSON 문법 오류(쉼표 누락 등) 가능성. Console 확인. |
| `end` 가 무시됨 | `"end": true` 가 아니라 `"end": "true"` (문자열) 로 적었다. JSON 의 bool 은 따옴표 없이. |
| 무한 루프 | 줄 A 의 `goto` 가 자기 자신/이전 줄을 가리키는데 다른 탈출구가 없다. 의도적이면 OK, 아니면 인덱스 재확인. |

### 14-5. 버튼 UI 자주 나는 실수

| 증상 | 원인 |
|---|---|
| 버튼은 뜨는데 라벨이 비어 있음 | 프리팹 자식에 `TMP_Text` 가 없다. **UI → Button - TextMeshPro** 로 만들었는지 확인. |
| 버튼 라벨에 한글이 □ | 자식 TMP 의 **Font Asset** 이 LiberationSans 그대로. `NotoSansKR-Regular SDF` 로 바꿔라. |
| 버튼 눌러도 반응 없음 | 씬에 `EventSystem` 없음, 또는 ChoicesContainer 의 자식 버튼 Raycast Target 이 꺼져 있음. |
| 화면 클릭으로도 다음 줄로 넘어가 버림 | `goto` 인덱스가 잘못됨. lines 의 0-base 인덱스인지 다시 세어 봐라. 또는 OnClick 이 버튼 위에서도 동작 → EventSystem 검사 미작동(에디터 재시작). |
| Console 에 `goto=… 가 lines 범위 밖이다` | `goto` 가 음수이거나 `lines.Length` 이상. |
| 선택지가 안 사라지고 다음 줄에도 남아 있음 | `ChoicesContainer` 가 인스펙터에 안 연결돼 있다. DialogueManager → Choices Container 칸 확인. |

---

## 15. 변수 시스템 (호감도 등)

선택지나 줄에 `effect` 필드를 더하면, 캐릭터별 호감도 같은 정수 변수가 바뀐다.
값은 `Dictionary<string, int>` 에 저장되고 **게임 시작 시 모든 변수는 자동으로 0** (없는 키 조회 시 0 반환).

### 15-1. `effect` 작성법

형식은 단순히 **`"이름+숫자"`** 또는 **`"이름-숫자"`** 한 줄짜리 문자열.

| effect 문자열 | 의미 |
|---|---|
| `"요르+10"` | `요르` 변수에 +10 |
| `"민지-5"`  | `민지` 변수에 −5 |
| `"우정+1"`  | `우정` 변수에 +1 |

- 이름엔 한글/영문/숫자 다 가능. **공백은 trim 됨** (`" 요르 + 10"` 도 OK).
- `+` / `-` 한 글자만 인정. `++`, `*=` 같은 건 미지원.
- effect 가 없거나 빈 문자열이면 변화 없음 (선택 필드).

### 15-2. 어디에 붙일 수 있나

1. **선택지에 붙이기** (`choices[].effect`) — 그 버튼을 누르면 변수 변경 후 `goto` 로 점프.
2. **줄에 붙이기** (line-level `effect`) — 그 줄이 등장하는 순간(타자 시작 직전) 변수 변경. 이벤트성 변화에 쓴다.

선택지 effect 는 **선택할 때**, 줄 effect 는 **줄이 시작될 때** 적용된다.

### 15-3. JSON 예시 — 선택지 호감도 분기

```json
{
  "lines": [
    { "speaker": "요르", "text": "같이 씻을래?",
      "choices": [
        { "label": "같이 씻는다", "goto": 1, "effect": "요르+10" },
        { "label": "도망간다",     "goto": 2, "effect": "요르-5" }
      ]
    },
    { "speaker": "요르", "text": "후훗, 좋아.", "end": true },
    { "speaker": "요르", "text": "…이상한 사람.", "end": true }
  ]
}
```

흐름:
1. 0번 → 두 버튼.
2. "같이 씻는다" 클릭 → 변수 `요르` 가 0 → 10 으로 변경 → 1번 줄 출력 → `end`.
3. "도망간다" 클릭 → 변수 `요르` 가 0 → -5 로 변경 → 2번 줄 출력 → `end`.

### 15-4. JSON 예시 — 줄 단위 effect (이벤트 호감도)

선택지가 아니라 특정 이벤트 줄 자체가 호감도를 올리는 케이스.

```json
{
  "lines": [
    { "speaker": "나레이션", "text": "당신은 요르의 가방을 들어 주었다.", "effect": "요르+3" },
    { "speaker": "요르", "text": "고마워." }
  ]
}
```

0번 줄이 화면에 뜨는 순간 `요르` 변수가 +3 된다. 클릭으로 진행하면 1번 줄.

### 15-5. 디버그 토글로 변수 상태 보기

개발 중에 "지금 요르 호감도 얼만지" 바로 확인하고 싶을 때.

#### 15-5-1. Console 로그만 켜기 (간단)

1. Hierarchy 의 `DialogueManager` 선택.
2. Inspector 의 **Debug** 섹션:
   - **Debug Show Variables** → 체크.
3. ▶ Play. effect 가 적용될 때마다 Console 에:
   ```
   [DialogueManager] (선택지) 요르: 0 → 10 (+10)
   [DialogueManager] (줄 0) 요르: 10 → 13 (+3)
   ```

#### 15-5-2. 화면 한쪽에 변수 표시 (선택)

1. Hierarchy 의 `Canvas` 를 **우클릭** → **UI → Text - TextMeshPro** → 이름 `DebugVariablesText`.
2. **Rect Transform**:
   - 앵커 프리셋 → **top-right** 클릭.
   - **Pos X** `-20`, **Pos Y** `-20`
   - **Width** `300`, **Height** `200`
3. **TextMeshPro - Text (UI)**:
   - **Font Asset** → `NotoSansKR-Regular SDF`
   - **Font Size** → `24`
   - **Color** → 노란빛 (예: RGBA `255, 230, 100, 255`)
   - **Alignment** → **Top Right**
   - **Extra Settings → Raycast Target** → 체크 해제 (클릭 가로채기 방지).
4. `DialogueManager` 선택 → Inspector → **Debug** 섹션:
   - **Debug Variables Text** ← Hierarchy 의 `DebugVariablesText` 드래그.
   - **Debug Show Variables** → 체크.
5. ▶ Play. 화면 오른쪽 위에 실시간으로:
   ```
   요르: 10
   민지: -3
   ```
   가 표시되고, effect 가 적용될 때마다 즉시 갱신.

> 💡 **Debug Show Variables** 가 꺼져 있으면 `DebugVariablesText` 는 자동으로 숨겨진다. 출시 빌드 때 토글만 끄면 됨.

### 15-6. 변수를 코드에서 읽기

다른 스크립트에서 호감도를 참조하고 싶다면:

```csharp
int 요르호감도 = dialogueManager.GetVariable("요르");
// 없는 키면 0 반환.
```

호감도 값에 따라 자동으로 갈래를 나누고 싶으면 다음 16장의 `branches` 를 쓴다.

### 15-7. 자주 나는 실수

| 증상 | 원인 |
|---|---|
| Console 에 `effect 파싱 실패: '...'` | 형식이 `이름+숫자` / `이름-숫자` 가 아니다. `"요르 10"` (연산자 없음), `"+10"` (이름 없음), `"요르+a"` (숫자 아님) 다 실패. |
| 변수가 안 바뀐다 | `Debug Show Variables` 가 꺼져 있어서 로그가 안 보일 뿐 실제로는 바뀌고 있다. 한 번 켜고 확인. |
| 같은 이름인데 다른 변수로 인식 | 공백/대소문자/유사 글자(예: `요르` vs `요르 `) 차이. effect 양쪽에서 같은 표기 쓰는지 확인. |
| 화면 표시가 안 보임 | `Debug Variables Text` 슬롯이 비어 있거나 `Debug Show Variables` 가 꺼져 있다. 둘 다 확인. |

---

## 16. 호감도(변수) 조건 분기 (`branches`)

선택지(`choices`)는 **플레이어가 버튼으로** 갈래를 고르지만, `branches` 는 **그동안 쌓인 변수 값**(호감도 등)에 따라 코드가 **자동으로** 갈래를 고른다. 호감도 엔딩 분기에 딱 맞는다.

### 16-1. 동작 방식

- 줄에 `branches` 배열이 있으면, 그 줄의 대사를 평소처럼 출력한다.
- 대사 출력이 끝난 뒤 **화면을 클릭하면**, `branches` 를 **위에서부터** 검사해서 **처음으로 참인 항목**의 `goto` 인덱스로 점프한다.
- `branches` 가 없는 줄은 지금까지처럼 자연 진행(또는 줄 단위 `goto`/`end`).

### 16-2. 작성법

각 분기는 `{ "condition": "비교식", "goto": 인덱스 }` 형태다.

| 키 | 의미 | 값 |
|---|---|---|
| `condition` | 비교식. 좌변은 **변수 이름**(없으면 0), 우변은 **정수**. | `"요르>=50"` 처럼 |
| `goto` | 이 분기가 참일 때 점프할 `lines` 인덱스 | 0부터의 정수 |

지원 연산자: **`>=`, `<=`, `>`, `<`, `==`** (다섯 가지).

규칙:
- **위에서부터 검사**하고, 처음으로 참인 곳으로 점프한 뒤 멈춘다. (그 아래 항목은 검사 안 함.)
- `condition` 이 **없는 항목은 항상 참**(기본 분기)이다. 그래서 **맨 아래**에 둔다. 어떤 조건에도 안 걸렸을 때 떨어지는 곳.
- 좌변 변수가 한 번도 변한 적 없으면 값은 **0**으로 친다.
- 공백은 무시된다(`" 요르 >= 50 "` 도 OK).
- 우선순위: `choices` (있으면 버튼이 떠서 클릭-진행이 멈춤) > **`branches`** > 줄 단위 `end` / `goto` > 자연 진행.

> ⚠️ 참인 분기가 하나도 없으면(기본 분기도 안 둠) 경고 로그를 찍고 그냥 다음 줄로 진행한다. 의도치 않은 누수를 막으려면 **항상 맨 아래에 `condition` 없는 기본 분기**를 둬라.

### 16-3. JSON 예시 — 호감도 엔딩 분기

요르 호감도를 이야기 곳곳에서 `effect` 로 올려 두었다가, 마지막 정산 줄에서 그 값으로 엔딩을 가른다. 각 엔딩 묶음은 **대사 + `── … 엔딩 ──`(`end: true`)** 두 줄로, `end` 가 흐름을 끊어 옆 엔딩으로 새지 않는다.

```json
{
  "lines": [
    { "speaker": "나레이션", "text": "당신은 요르의 가방을 들어 주었다.", "effect": "요르+30" },
    { "speaker": "요르",     "text": "고마워. 너 은근 다정하네." },
    { "speaker": "나레이션", "text": "함께 우산을 썼다.", "effect": "요르+25" },

    { "speaker": "나레이션", "text": "운명의 날이 밝았다.",
      "branches": [
        { "condition": "요르>=50", "goto": 4 },
        { "condition": "요르>=20", "goto": 6 },
        { "goto": 8 }
      ]
    },

    { "speaker": "요르",     "text": "…사실 너 좋아했어. 우리 사귀자." },
    { "speaker": "나레이션", "text": "── 연인 엔딩 ──", "end": true },

    { "speaker": "요르",     "text": "너랑 있으면 편해. 좋은 친구야." },
    { "speaker": "나레이션", "text": "── 친구 엔딩 ──", "end": true },

    { "speaker": "요르",     "text": "음… 우리 그렇게 친하진 않잖아?" },
    { "speaker": "나레이션", "text": "── 남남 엔딩 ──", "end": true }
  ]
}
```

흐름 (위 예시에서 요르 = 30 + 25 = **55**):
1. 0~2번 줄에서 `요르` 호감도가 0 → 30 → 55 로 쌓인다. (1번 줄엔 effect 가 없어 변화 없음.)
2. 3번 줄 `"운명의 날이 밝았다."` 가 출력되고, 클릭하면 위에서부터 `branches` 검사:
   - `요르>=50` → **참**(55 ≥ 50) → `goto: 4` 로 점프하고 멈춘다. 아래 항목은 검사 안 함.
3. 4번 (`…사실 너 좋아했어.`) → 클릭 → 자연 진행 → 5번 (`── 연인 엔딩 ──`) → `end` 로 종료.

만약 호감도가 **20~49** 였다면 첫 항목은 거짓이고 `요르>=20` 이 참이 되어 **6번(친구 엔딩)** 으로, **20 미만**이면 둘 다 거짓이라 맨 아래 기본 분기 `goto: 8`(남남 엔딩)로 떨어진다.

> 💡 6번·8번 엔딩 묶음은 오직 `branches` 의 점프로만 도달한다. 5번 엔딩이 `end: true` 로 흐름을 끊기 때문에, 연인 엔딩을 본 사람이 친구·남남 엔딩 대사까지 이어 보는 일이 없다. (이 `end` 끊기 원리는 14-4 와 같다.)

### 16-4. `choices` 와 무엇이 다른가

| | `choices` | `branches` |
|---|---|---|
| 누가 고르나 | **플레이어**(버튼 클릭) | **변수 값**(자동) |
| 화면 | 버튼이 뜸 | 버튼 없음, 그냥 클릭하면 점프 |
| 쓰는 곳 | 선택지 제시 | 호감도 엔딩, 조건부 이벤트 |

둘을 **한 줄에 같이 쓰지는 말 것**. 같이 있으면 `choices` 가 우선이라 `branches` 는 무시된다. 보통 호감도를 `choices`+`effect` 로 쌓아 두고, 마지막 정산 줄에서 `branches` 로 가른다.

### 16-5. 자주 나는 실수

| 증상 | 원인 |
|---|---|
| Console 에 `조건 파싱 실패: '...'` | 연산자가 없거나(`"요르 50"`), 이름이 없거나(`">=50"`), 우변이 정수가 아님(`"요르>=가"`). 지원 연산자는 `>=, <=, >, <, ==`. |
| 항상 마지막(기본) 분기로만 감 | 변수가 안 쌓였다. `effect` 가 실제로 적용됐는지 **Debug Show Variables** 켜고 확인(15-5). 변수 이름 표기(공백/한글)도 양쪽이 같은지. |
| Console 에 `branches 에 참인 분기가 없다` | `condition` 없는 기본 분기를 안 뒀고, 어떤 조건도 안 맞았다. 맨 아래에 `{ "goto": N }`(condition 없이) 하나 추가. |
| `==` 가 안 먹힘 | JSON 에 `"="` 한 개만 적었다. 같음 비교는 `==` 두 개. |
| 한 갈래가 옆 엔딩 대사까지 흐름 | 분기 도착 줄 묶음 끝에 `end: true` 나 `goto` 로 안 끊었다. 14-4 의 흐름 제어와 같은 원리. |
| 분기는 점프되는데 호감도가 틀림 | 점프 직전까지의 `effect` 합을 다시 계산. 줄 effect 는 **줄이 뜰 때**, 선택지 effect 는 **고를 때** 적용된다(15-2). |

---

## 17. 다음에 해 볼 만한 확장

이 시스템을 토대로 다음을 붙여 볼 수 있다.

- 배경 이미지 전환 (`background` 필드)
- 표정 변화 (한 캐릭터에 여러 스프라이트)
- 효과음 / BGM
- Skip / Auto 모드
- 세이브/로드 (지금은 변수도 `private Dictionary` 라 휘발성)
- 페이드인/아웃 트랜지션
- 조건부 **선택지 표시** (`requires` 필드 — "요르>=10 이면 이 선택지 버튼만 노출"). 줄 단위 자동 분기는 16장 `branches` 로 이미 가능.
- 변수 직접 설정 (`set` 필드 — `+/-` 가 아니라 절댓값으로)

지금 단계에선 위 0~16 까지가 정상 동작하는지가 우선이다. 잘 돌아가면 그때부터 한 가지씩 늘려 가면 된다.
