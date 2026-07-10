# 클로드 코드 인계 프롬프트 — 07-A1: 주인공 8스탯 시스템

아래를 그대로 클로드 코드에 붙여서 착수. 06(루프+영속화) 위에 얹는 첫 07 슬라이스.
**전투는 이 슬라이스에 없음** — 스탯 데이터 골격 + 주인공 스탯 상태 + 인과율 소비(스탯 강화)까지만.

---

## 프롬프트 본문

```
06(RunState/MetaState/CampaignState + 디스크세이브 + 메타→VN투영)이 main에 있는 상태에서,
07-A1 "주인공 8스탯 시스템"을 subagent-driven-development로 완주해줘.

기존 원칙 필수 준수:
① 순수 코어(Core/** 는 UnityEngine·System.IO 미참조)
② 불변 상태(전이는 새 인스턴스 반환, 입력 불변)
③ 데이터 주도(테마·수치·스탯목록을 하드코딩하지 말고 데이터로 주입)

이 슬라이스의 스코프: 스탯 데이터 구조 + 주인공 스탯 상태 + 인과율→스탯강화 전이.
전투/데미지공식은 07-A2(다음 슬라이스)에서. 여기선 스탯을 "값"으로만 다룬다.

## 설계 핵심: 확장 대비 데이터 주도 (이 슬라이스의 존재 이유)
장기적으로 스탯 관여도를 늘려 하드코어 빌드(확장판) + 2편 "순수 힘 던전"으로 확장한다.
그래서 스탯을 절대 하드코딩("public int STR; public int INT;")하지 말 것.
스탯은 데이터 테이블로 정의하고, 주인공 스탯은 Dictionary<StatId, int> 형태로 보관한다.
→ 확장판에서 StatDef를 추가하면 코드 수정 없이 스탯이 늘어나야 한다.

## 작업 1 — 스탯 정의 (데이터)
Core/Sim/Stats/StatDef.cs:
  StatDef { StatId Id; string DisplayName; int StartValue; int Cap; }
  - 1편 8스탯: STR, INT, DEX, AGI, HP, MP, DEF, LUK
  - Cap = 999 (데이터로, 스탯별로 다를 수 있게)
  - StartValue: STR/INT/DEX/AGI/DEF/LUK=5, HP=50, MP=30 (데이터)
  전투역할은 여기 코드에 안 넣음(07-A2에서 공식이 참조). 여기선 순수 값 정의만.

## 작업 2 — 주인공 스탯 상태 (메타에 귀속)
Core/Sim/Stats/HeroStats.cs:
  - Dictionary<StatId,int> 로 스탯 보관 (하드코딩 필드 금지)
  - 불변: 스탯 변경은 새 HeroStats 반환
  - 06 MetaState에 HeroStats 필드 추가 (주인공 성장은 회차 넘어 유지 = 메타)
  - 세이브(04/06 패턴): HeroStats를 평면 직렬화(List<StatEntry{id,value}>)로 CampaignState에 포함
  주의: 06에서 확립한 방어적 복사 — Dictionary가 전이 시 공유되지 않게 깊은복사/불변컬렉션.

## 작업 3 — 인과율 → 스탯 강화 전이 (구간지수 비용)
Core/Sim/Stats/StatUpgrade.cs (순수 함수):
  UpgradeStat(HeroStats, StatId, karmaAvailable) → (newHeroStats, karmaSpent, pointsGained)
  - 구간지수 비용(데이터 테이블 StatCostCurve):
      cur<100→1, <250→2, <450→3, <650→5, <800→9, <950→16, else→28  (전부 데이터, 튜닝대상)
  - karmaAvailable 안에서 가능한 만큼 해당 스탯 상승, Cap(999) 초과 불가
  - 비용곡선은 하드코딩 말고 StatCostCurve 데이터로 주입 (구간 경계·배수 교체 가능하게)
  인과율(karma) 자체의 수급/저금 관리는 07-C(경제)에서. 여기선 "karma를 받아 스탯을 올리는 순수함수"만.

## 작업 4 — 메타→VN 투영 확장 (06 MetaProjection 재사용)
주인공 주요 스탯을 VN GameState 변수로 읽기전용 투영(06에서 만든 심 재사용):
  예: STR→변수 "주인공_STR", 총스탯합→"주인공_전투력" 등.
  → .vns에서 if 주인공_STR >= 500: 같은 서사분기(드보라 해금조건 등) 가능.
  방향은 커널→VN 단방향 읽기전용(06 원칙 유지).
  ※ 06 리뷰 권고: MetaProjection.Project를 실제 호출지점에 배선하는 건 VN서사 슬라이스에서.
     여기선 투영 대상에 스탯을 추가만 하고, 배선은 기존 상태 유지.

## 검증 (EditMode 테스트)
  - HeroStats가 Dictionary 기반이고 스탯 추가가 데이터만으로 되는가(하드코딩 필드 없음)
  - UpgradeStat이 입력 HeroStats를 변형하지 않음(불변)
  - 구간지수 비용이 정확히 적용(cur<100이면 1, ≥950이면 28 등)
  - Cap 999 초과 불가
  - karmaAvailable 부족 시 가능한 만큼만 상승
  - 세이브→로드 왕복 후 HeroStats 완전 일치(방어적 복사 검증: 원본 수정이 로드본에 영향 없음)
  - 스탯 투영이 VN 변수로 읽히는가
  - (시뮬 대조) 인과율 일정량 투입 시 스탯 상승폭이 sim_8stat.py 구간지수와 일치

## 진행 방식
subagent-driven-development: 스펙→구현→sonnet리뷰→검증→whole-branch리뷰(opus)→테스트 전건통과→main --no-ff 머지.
07 문서(07-defense-combat.md §12)를 이번 구현 반영해 갱신. 무관 워킹트리 변경은 기존대로 무시.
```

---

## 설계 노트 (당신용, 클로드 코드에 안 넘겨도 됨)

- **왜 A1을 전투와 분리했나**: 8스탯이 들어오면서 07-A(전투코어)가 무거워짐. 스탯 데이터 골격을 먼저
  독립 슬라이스로 세우면, A2(전투)에서 그 스탯을 참조만 하면 됨. 골격과 사용을 분리해 리뷰·검증이 가벼워짐.
- **이 슬라이스의 진짜 목표는 "확장 대비 데이터 구조"**: 지금 8스탯을 Dictionary+StatDef로 짜두면,
  확장판 하드코어(3번)·2편 순수힘던전에서 스탯을 데이터로만 늘릴 수 있음. 여기서 하드코딩하면 나중에
  전투 전체 재작업. 그래서 "하드코딩 금지"를 프롬프트에 반복 강조함.
- **인과율 수급은 뺐다**: A1은 "karma를 받아 스탯 올리는 순수함수"까지만. karma가 어디서 오고(여관·감옥)
  어떻게 저금되는지는 07-C 경제 슬라이스. 소비 로직과 수급 로직을 분리해 각 슬라이스를 얇게.
- **투영은 대상 추가만, 배선은 뒤로**: 06 리뷰가 "MetaProjection.Project 실제 배선은 VN서사 슬라이스에서"라
  권고. A1은 투영 목록에 스탯만 추가하고 실제 매턴 호출 배선은 안 함(서사 붙일 때).
- **다음(07-A2)**: 이 스탯을 전투에 연결. ThreatBase + 다스탯 데미지공식(물리=STR기여, 마법=INT기여) +
  병종/상극 + CombatResolver. A1의 HeroStats를 읽어 전투력 산출.
```
