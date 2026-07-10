# 클로드 코드 인계 프롬프트 — 07-C: 경제 배선 (통합 슬라이스)

06 + A1 + A2 + B + D 위에 얹는 **07 코어 마지막 통합 슬라이스**. 지금까지 만든 순수함수 조각들
(전투결과·포획·여관수급)을 골드/인과율로 **배선**하고, 소비(스탯강화·레벨업·골드소모)를 연결한다.
이게 서면 게임 루프가 실제로 돈다(+ 이후 대시보드가 NRE 없이 얹힌다).

---

## 프롬프트 본문

```
06 + 07-A1 + 07-A2 + 07-B + 07-D가 main에 있는 상태에서, 07-C "경제 배선"을
subagent-driven-development로 완주해줘.

기존 원칙 필수: ① 순수 코어(UnityEngine·System.IO 미참조) ② 불변 상태 ③ 데이터 주도(계수·비용을 데이터로).

이 슬라이스의 성격 = 통합 배선. 새 시스템보다 기존 조각을 잇는 게 핵심.
지금까지: 전투(A2 CombatResult), 포획(B CaptiveLedger/CaptureRule), 여관수급(D InnIncomeRule) 이 전부
순수함수 단독으로 존재하나 턴/루프에 미배선. C가 이걸 자원 흐름으로 연결한다.

## 작업 1 — 자원 상태 + 턴 루프 골격
- 자원: 골드/마석/인과율(karma bank). 인과율저금·마석·골드가 어느 층인지 06 분류 준수:
  · 인과율저금 bank, 주인공스탯 = 메타 / 골드·마석·가챠pull카운터 = 런
- 턴 진행 함수: 하루/웨이브 진행 시 (전투 → 여관수급 → 자원 누적)을 한 번에 처리하는 루프.
  ⚠️ D 주의: 여관 게이트 판정(Decor>0)을 Decor Decay 前에 수행(순서 중요).

## 작업 2 — 수급 배선 (조각 → 자원)
- 전투결과(CombatResult: Killed/Captured) → 골드 환산:
    처치 = loot_gold 100%, 포획 = loot_gold 50% + 인과율. (07 §6.2 제곱근 약탈골드, isqrt 재사용)
- 포획(B) → CaptiveLedger를 리졸버/턴루프에 실제 배선(B defer 항목). Captives 누적 → 감옥.
    감옥 방면 시 인과율 수급(released → karma bank).
- 여관(D InnIncomeRule) → 턴루프에 배선. innGold → 골드, innKarma → 인과율 bank 누적.

## 작업 3 — 소비 배선
- 인과율 소비 (2소비처): 
    · 주인공 스탯강화 = A1 StatUpgrade(순수함수) 호출. karma bank에서 차감.
    · 던전 레벨업 = 골드 + 인과율 요구(07 §5.2 지수비용). 
- 골드 소비: 방 건설 / 여관 수리·직원고용·메뉴개발(D 비용측) / 마석 구매.
- 마석 → 가챠 pull (07 §6.3): gacha_cost = base + pullsThisLoop/3. pull카운터는 런(회차리셋!).
- 지출 우선순위(07 §6.4): 내구도수리 > 레벨업 > 여관투자 > 몹강화 > 마석/가챠. (자동아니라 유저선택 여지 두되 기본 안전판)

## 작업 4 — B/A1 defer 항목 정리 (이 슬라이스에서 반드시)
- ★ hero-placement gap(B defer): CombatResolver는 경로 마지막방을 요격지점 가정, PlacementValidator는 UI게이트.
    → 턴루프 배선 시 caller가 build(전투) 前 PlacementValidator.validate 필수 호출. 안 하면 발산.
- ★ A1 opus 권고 2: StatUpgrade 부재스탯 seed-and-grow 경로(0에서 시작해 키우는) 테스트 추가.
- ★ A1 opus 권고 3: CreateInitialCampaign에서 FromDefs(StatCatalog.Default())로 주인공 8스탯 라이브 시딩.
    그 경계에서 StatId null/empty 검증.
- ★ MetaProjection: 06에서 심만 놓은 실제 배선은 아직 VN서사 슬라이스 몫 — C에선 자원/스탯 투영 대상만 추가,
    매턴 호출 배선은 하지 않음(기존 방침 유지).

## 작업 5 — 문서(07 §5·6·7 갱신, 배선 반영)

## 검증 (EditMode)
  - 전투 처치→골드100%, 포획→골드50%+인과율 정확
  - 감옥 방면 → 인과율 bank 누적
  - 여관 innGold/innKarma가 턴루프에서 자원에 누적
  - 인과율로 주인공 스탯강화 (StatUpgrade 배선, bank 차감)
  - 인과율+골드로 던전 레벨업
  - 가챠 pull카운터 회차리셋(런 귀속) — 회귀 후 0으로
  - hero-placement: validate 없이 build하면 막힘(가드), validate 후 정상
  - seed-and-grow: 부재스탯 0→성장 경로 정상
  - 라이브 시딩: CreateInitialCampaign 후 주인공 8스탯 실재 + StatId null/empty 거부
  - 여관 게이트가 Decay 前 판정
  - 정수결정론·불변·Core순수성·세이브 왕복(방어적복사)

## 진행: subagent-driven(plan→구현→sonnet→검증→opus브랜치→전건통과→main --no-ff). 무관 워킹트리 미커밋.
※ 통합 슬라이스라 태스크가 많음 — 필요시 배치로 나눠 진행. defer 항목(작업4)은 빠뜨리지 말 것(원장에 체크).
```

---

## 설계 노트 (당신용)

- **C는 07의 종착점**: 지금까지 순수함수로 만든 조각(전투·포획·여관)이 여기서 자원 흐름으로 이어져 게임 루프
  완성. C 끝나면 대시보드가 NRE 없이 얹힘(앞서 실패한 원인 = C 배선 부재였음).
- **작업4가 이 슬라이스의 함정**: B defer 2건(hero-placement, CaptiveLedger) + A1 권고 2건(seed-and-grow,
  시딩) + D 주의(게이트 순서)를 여기서 다 정리. 놓치면 나중에 배선 구멍으로 재등장. 원장 체크 필수.
- **hero-placement가 특히 중요**: 이게 앞서 대시보드 NRE의 뿌리 중 하나. validate 배선 안 하면 전투가
  빈 요격지점 만나 발산. caller가 build 전 validate 강제하는 걸 못박아야 함.
- **가챠 pull 회차리셋**: 시뮬로 검증한 핵심(메타에 두면 엔드게임 가챠 막힘). 런 귀속 반드시.
- **MetaProjection 실제 배선은 여전히 뒤로**: C도 대상 추가만. 매턴 호출은 VN서사 슬라이스. 06 방침 유지.
- **다음**: C 끝나면 (1) 대시보드(읽기전용→전투배선 완성본), (2) 08 계승·스킬증여·가챠소환, (3) VN 오프닝.
```
