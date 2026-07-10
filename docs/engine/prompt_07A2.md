# 클로드 코드 인계 프롬프트 — 07-A2: 전투 코어

06(루프+영속화) + 07-A1(8스탯) 위에 얹는 슬라이스. A1의 HeroStats를 읽어 **실제 전투**를 만든다.
데미지 공식·병종·상극·웨이브 해결까지. 배치 예산(07-B)·경제(07-C)·여관(07-D)은 다음.

---

## 프롬프트 본문

```
06 + 07-A1이 main에 있는 상태에서, 07-A2 "전투 코어"를 subagent-driven-development로 완주해줘.

기존 원칙 필수: ① 순수 코어(UnityEngine·System.IO 미참조) ② 불변 상태 ③ 데이터 주도(공식·병종·상극·가중치를 데이터로).

스코프: ThreatBase 산출 + 다스탯 데미지공식 + 병종/상극 + 웨이브 전투해결(CombatResolver) + 처치/포획 분기.
방 배치(예산제)는 07-B, 골드/인과율 수급은 07-C. 여기선 "웨이브가 와서 방의 몹/주인공이 요격하고 결과를 낸다"까지.
방 구조는 A1까지 없으니, 이 슬라이스에선 06/07의 선형(1→2→3→4→5코어)을 최소 가정으로 쓴다.

## ⚠️ A1 opus 리뷰 권고 반영 (필수)
A1의 ProjectHeroTotal은 raw 스탯 합이라 시작값 HP50+MP30이 전투력을 지배함(STR5 올려도 묻힘).
→ 07-A2는 "가중 전투력"으로 간다. 각 스탯의 전투 기여를 가중치로 분리:
   물리공격 ← STR 기여, 마법공격 ← INT 기여, 방어 ← DEF 기여, 명중/치명 ← DEX(+LUK),
   회피/속도 ← AGI, 체력 ← HP, 스킬자원 ← MP.
   raw 합이 아니라 "역할별 파생치"로 전투력을 산출. 가중치는 전부 데이터(하드코딩 금지).

## 작업 1 — 병종/상극 (데이터)
Core/Sim/Combat/UnitClassDef.cs: { ClassId, DisplayName, HpMul, AtkMul, DefMul, CanBeCaptured }
Core/Sim/Combat/ClassMatchup.cs: 정수% 상극테이블 int[atk][def] (예 150=×1.5). 데이터 주입.
  1편 병종: 탱커/마법사/성기사/궁수/성직 (07 §2.2). 수치는 데이터.

## 작업 2 — ThreatBase 산출 (데이터 가중치)
Core/Sim/Combat/ThreatFormula.cs:
  ThreatBase = w_hero*HeroLevel + w_loop*(loop-1) + w_placed*avgPlacedMonLevel + w_dungeon*dungeonLevel + baseOffset
  (초기추정 w_hero=2,w_loop=8,w_placed=1,w_dungeon=3,base=20 — 전부 데이터/튜닝대상)
  ※ HeroLevel은 A1의 HeroStats에서 "가중 전투력"으로 환산해 투입(위 권고). raw 합 쓰지 말 것.

## 작업 3 — 개체 능력치 생성
Core/Sim/Combat/Attacker.cs (struct) + 생성기:
  unit.HP/ATK/DEF = clamp(1, round(ThreatBase * class.mul) + random(-5,5))
  random은 A1/02의 시드결정론 PRNG(재현성). 규모는 07-B/C에서 웨이브정의로, 여기선 개체생성 단위만.

## 작업 4 — 데미지 공식 (분기형, 다스탯 반영)
Core/Sim/Combat/DamageFormula.cs:
  물리: atkStat = STR기여 전투력, 마법: atkStat = INT기여. (공격자가 주인공이면 스탯파생, 몹이면 몹ATK)
  if atk >= def: dmg = atk*2 - def
  else:          dmg = atk*atk / def        // 정수나눗셈
  dmg = dmg * matchupMul / 100              // 상극(정수%)
  dmg = max(1, dmg)
  DEX 명중/치명, AGI 회피는 명중판정·치명배수로 반영(데이터 계수). 전부 정수연산(02 Int전용).

## 작업 5 — 웨이브 전투 해결 (순수함수)
Core/Sim/Combat/CombatResolver.cs:
  ResolveWave(RunState, WaveDef, HeroStats, IRandom) → CombatResult
  - 침입자가 선형 방경로를 통과, 각 방의 몹/함정이 요격(데미지공식). 주인공은 코어앞1칸에서 요격.
  - HP≤0 처치. 특정 병종/조건은 포획분기(CanBeCaptured + 조건) → Captured에.
  - 코어 도달 → CoreHit.
  CombatResult { CoreHit, Killed[], Captured[] }  // 골드/인과율 환산은 07-C, 여기선 전투결과만.
  순수함수: 입력 RunState/HeroStats 불변, 결과 반환. 같은시드+같은배치 → 같은결과(결정론).

## 검증 (EditMode)
  - 동일시드 → 동일 전투결과(결정론)
  - ATK=DEF일 때 dmg>0 (분기형)
  - 상극 배수 정확 (150이면 1.5배)
  - 가중 전투력: STR만 올리면 물리공격 오르고 HP는 물리공격에 기여 안 함(raw합 아님 검증) ← A1권고 핵심
  - 주인공 코어앞1칸 배치 요격 동작
  - 포획분기: CanBeCaptured=true + 조건충족 시 Killed 아닌 Captured
  - 코어 도달 시 CoreHit
  - 개체능력치 ±5편차 + min1 클램프

## 진행: subagent-driven-development(스펙→구현→sonnet→검증→opus브랜치→전건통과→main --no-ff). 07문서 §2·3·13 갱신. 무관파일 무시.
```

---

## 설계 노트 (당신용)

- **A2의 핵심 난이도 = 가중 전투력**: A1 raw합 문제를 여기서 푼다. 8스탯이 "역할별"로 전투에 들어가야
  스탯 빌드(물리/마법)가 의미를 가짐. 이걸 안 잡으면 "스탯 올려도 안 세짐" 버그.
- **방은 최소 가정(선형)**: 배치 예산제(07-B)가 아직이라, A2는 선형 방에 몹이 있다고 가정하고 전투만.
  B에서 방 그래프·예산이 서면 A2의 CombatResolver가 그걸 읽게 확장. 지금은 전투 규칙 자체에 집중.
- **골드/인과율 환산 제외**: CombatResult는 "누가 죽고 잡혔나"까지. 그게 얼마 골드/인과율인지는 07-C.
  전투 결과와 경제 환산을 분리해 슬라이스를 얇게.
- **07-C 메모(A1 권고 2·3)**: seed-and-grow 경로 테스트 + CreateInitialCampaign 시딩 배선 + StatId null/empty
  경계검증. 07-C 프롬프트에 반영할 것.
- **다음(07-B)**: 방 그래프 + 배치 예산제(현재방×3, 코스트=레어도, 던전레벨=방상한). A2 CombatResolver가
  선형가정에서 실제 방배치를 읽도록 확장.
```
