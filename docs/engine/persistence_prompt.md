# 클로드 코드 인계 프롬프트 — 영속화 & Regress 내용 로직 슬라이스

아래를 그대로 클로드 코드에 붙여서 착수시키면 됩니다.

---

## 프롬프트 본문

```
06 슬라이스(RunState/MetaState/CampaignState 2층 컨테이너 + LoopEngine 파사드)가 main에 머지된 상태에서,
다음 슬라이스로 "영속화 + Regress 내용 로직"을 subagent-driven-development로 완주해줘.

기존 두 원칙을 반드시 지킬 것:
① 순수 코어(Core/** 는 UnityEngine·System.IO 절대 미참조)
② 불변 상태(모든 전이는 새 인스턴스 반환, 입력 불변)

## 작업 0 — 선결: 방어적 복사 (최종 리뷰 opus 권고)
06 최종 리뷰에서 "영속화 착수 시 RunState.Resources 방어적 복사 재검토" 권고가 있었음.
지금 인메모리 단일 실행에선 안 터지지만, 세이브→로드 왕복이나 되감기를 넣으면
새 CampaignState가 옛 것과 내부 컬렉션(딕셔너리 등)을 참조 공유해 "이전 회차 상태가
현재 회차에 새어드는" 버그가 됨.
→ RunState/MetaState의 모든 컬렉션 필드가 전이 시 깊은 복사(또는 불변 컬렉션)로
   넘어가는지 점검하고, 공유되는 곳을 방어적 복사로 교정. 이걸 먼저 처리하고 나머지 착수.

## 작업 1 — Regress 내용 로직 채우기
현재 Regress(StartNewLoop)가 "회차 +1, 런 리셋" 껍데기만 있음. 내용을 채운다.
Regress(CampaignState s, RegressionInput input) 순수 함수가 다음을 수행한 새 상태 반환:

  1. 계승 처리: input.MonsterToInherit → Meta.InheritedMonster 기록. 나머지 Run.Summoned 폐기.
  2. 메타 갱신:
     - Meta.LoopCount += 1
     - Meta.LetterLines 에 새 회차 글귀 추가 (데이터 테이블 letters[LoopCount] 참조)
     - Meta.DungeonLevel / Meta.HeroStrMax 등 누적 지표는 회차 성과 반영해 갱신(리셋 아님)
     - 새로 밝혀진 TruthFlags / MigalStage 전이 반영
  3. 런 재생성: Run = CreateInitialRun(Meta). 계승 몹 있으면 초기 소환풀/배치에 반영. Day=1.

  RegressionInput: { MonsterToInherit(nullable), 특수플래그(미갈이_코어를_깬_회차 등) }

  데이터 주도 유지: letters 글귀, truthflag 이름, migal 단계 전이는 전부 데이터(SO/JSON)로 주입.
  커널은 "LoopCount에 따라 어느 데이터를 활성화하나" 규칙만 실행. 테마 문자열 하드코딩 금지.

## 작업 2 — 디스크 세이브 (04 패턴 위에)
04 세이브(VN VM: PC+콜스택+GameState)는 그대로 두고, CampaignState 저장을 추가.
  - CampaignState를 04의 리스트+원시타입 평면 직렬화 패턴으로 SaveData화(딕셔너리 금지, JsonUtility 호환).
  - 한 세이브 슬롯 = { 캠페인(Meta+Run) + VN VM 상태 } 를 함께 찍고, 로드 시 둘 다 복원.
  - programHash 호환성 가드(04 §5) 그대로 적용. 대본 바뀌면 로드 거부.
  - 디스크 IO는 Unity 레이어(SaveSystem)에만. Core에는 순수 모델까지만.

## 작업 3 — 메타→VN 변수 투영 (읽기 전용)
Meta.LoopCount / TruthFlags / MigalStage 등을 VN GameState 변수로 읽기 전용 투영하는 함수.
  - 예: LoopCount → 변수 "회차", TruthFlags{knows_emperor} → 변수 "황제진실"(0/1),
        MigalStage → 변수 "미갈단계".
  - 이러면 .vns에서 if 회차 >= 2:  /  if 황제진실:  같은 서사 분기가 기존 문법으로 가능.
  - 방향은 커널→VN 단방향(읽기 전용). VN이 이 변수를 직접 쓰지 않음.

## 검증(EditMode 테스트 필수)
  - Regress 후 Meta.LoopCount 정확히 +1
  - Regress가 입력 CampaignState를 변형하지 않음(불변)
  - 계승 몹이 다음 회차 새 런에 넘어감
  - LoopCount에 따라 편지 글귀가 정확히 누적(1회차 1줄 → 2회차 2줄 …)
  - 회귀 왕복 후 메타 지표(DungeonLevel/HeroStrMax) 유지
  - Run의 어떤 필드도 회귀 후 새 런에 새어나가지 않음(계승 몹은 Meta 경유 예외)
  - 세이브→로드 왕복 후 CampaignState 완전 일치 (방어적 복사 검증 포함)
  - 방어적 복사: 세이브 후 원본 Run 수정이 로드된 상태에 영향 없음

## 진행 방식
subagent-driven-development로: 스펙 작성 → 구현 → sonnet 리뷰 → 검증 → whole-branch 리뷰(opus) →
테스트 전건 통과 확인 → main에 --no-ff 머지. 문서 06을 이번 구현 반영해 갱신.
무관 워킹트리 변경(NotoSansKR SDF.asset, gemini-code-*.md)은 기존대로 무시.
```

---

## 이 프롬프트 설계 노트 (당신용, 클로드 코드에 안 넘겨도 됨)

- **작업 0을 맨 앞에 둔 이유**: opus가 짚은 방어적 복사는 "지금은 안 터지는" 종류라 나중에 미루면
  세이브 붙인 뒤에야 버그로 나타나 원인 추적이 어려워짐. 영속화의 전제 조건이라 선결로 뺐음.
- **Regress 내용 로직과 세이브를 같은 슬라이스로 묶은 이유**: 회귀가 "메타는 남기고 런은 버린다"인데,
  세이브/로드가 바로 이 "무엇이 남고 무엇이 버려지나"를 왕복 검증하는 최고의 테스트임. 둘을 같이 하면
  회귀 로직의 정확성이 세이브 왕복 테스트로 자동 검증됨.
- **디펜스(07)를 여기 안 넣은 이유**: RunState에 디펜스 필드(방/몹/웨이브)가 붙기 전에 회귀 내용 로직을
  먼저 완성해야, 나중에 디펜스 필드가 추가돼도 "얜 런이라 회귀 때 버림"이 자동 적용됨. 순서가 중요.
- **투영을 읽기 전용 단방향으로 못박은 이유**: VN이 메타를 직접 쓰면 "누가 진실의 소스인가"가 흐려짐.
  커널이 유일한 소스, VN은 읽어서 분기만. 이게 세이브 일관성을 지킴.
```
