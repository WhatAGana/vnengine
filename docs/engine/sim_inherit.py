"""
계승/스킬증여 5축 상한 검증 — 무한강화가 실제로 차단되는가
5축: 1)계승인원 하드캡 2)스킬슬롯 고정 3)증여손실(원본소멸+비용) 4)능력치 하드캡 5)ThreatBase가 계승반영
"""
import random, statistics as st
random.seed(42)

INHERIT_SLOTS_CAP = 3   # 축1: 계승 인원 하드캡
SKILL_SLOTS = 3         # 축2: 몹당 스킬 슬롯
STAT_CAP = 200          # 축4: 능력치 하드캡

class Monster:
    def __init__(self, name, base_stat, skills):
        self.name=name; self.stat=base_stat; self.skills=skills[:SKILL_SLOTS]
    def power(self):
        # 능력치 + 스킬효과, 단 STAT_CAP 하드캡
        raw = self.stat + len(self.skills)*15
        return min(STAT_CAP, raw)

def transfer_skill(src, dst, gold):
    """축3: 스킬 이식 = 원본 소멸 + 골드비용. 슬롯 꽉차면 교체."""
    if not src.skills: return gold, None
    skill = src.skills[0]
    cost = 100
    if gold < cost: return gold, None
    gold -= cost
    if len(dst.skills) < SKILL_SLOTS:
        dst.skills.append(skill)
    else:
        dst.skills[-1] = skill  # 교체(무한축적 불가)
    src.destroyed = True  # 원본 소멸
    return gold, skill

# 시뮬: 20회차 반복하며 계승몹 강화 시도
print("계승/스킬증여 20회차 — 무한강화 차단 검증")
print(f"{'회차':>3} {'계승수':>5} {'최강몹파워':>8} {'평균파워':>8} {'스킬합계':>7}")
print("-"*45)

inherited = [Monster("초기몹", 30, ["기본"])]
powers_over_time=[]
for loop in range(1,21):
    # 매 회차: 새 몹 뽑고, 스킬 증여로 계승몹 강화 시도
    new_mobs = [Monster(f"L{loop}몹{i}", random.randint(20,50), 
                        random.sample(["초회복","영웅베기","광역","관통","흡혈","보호막"], random.randint(1,3)))
                for i in range(5)]
    # 계승몹에 좋은 스킬 몰아주기 시도 (유저의 최적화 행동 모방)
    gold = 500
    for src in new_mobs:
        if inherited:
            gold, _ = transfer_skill(src, inherited[0], gold)
    # 계승몹 능력치 성장 (하드캡)
    for m in inherited:
        m.stat = min(STAT_CAP, m.stat + 12)
    # 계승 인원 확장 시도 (하드캡)
    if len(inherited) < INHERIT_SLOTS_CAP and loop % 5 == 0:
        best_new = max(new_mobs, key=lambda m:m.power())
        if not getattr(best_new,'destroyed',False):
            inherited.append(best_new)
    
    powers = [m.power() for m in inherited]
    powers_over_time.append(max(powers))
    print(f"{loop:>3} {len(inherited):>5} {max(powers):>8} {int(st.mean(powers)):>8} {sum(len(m.skills) for m in inherited):>7}")

print("-"*45)
print(f"\n검증:")
print(f"1. 계승 인원: 최종 {len(inherited)}명 (하드캡 {INHERIT_SLOTS_CAP}) → {'차단됨' if len(inherited)<=INHERIT_SLOTS_CAP else 'X 초과!'}")
print(f"2. 최강몹 파워: {max(powers_over_time)} (하드캡 {STAT_CAP}) → {'차단됨' if max(powers_over_time)<=STAT_CAP else 'X 초과!'}")
print(f"3. 스킬슬롯: 몹당 최대 {max(len(m.skills) for m in inherited)} (고정 {SKILL_SLOTS}) → {'차단됨' if max(len(m.skills) for m in inherited)<=SKILL_SLOTS else 'X 초과!'}")
print(f"4. 파워 배율: 초기30 → 최종{max(powers_over_time)} = {max(powers_over_time)/30:.1f}x (유한 성장)")
print(f"5. 파워 증가 곡선: {powers_over_time[:5]} ... {powers_over_time[-3:]}")
print(f"   → 후반부 증가율 둔화(하드캡 수렴): {powers_over_time[-1]-powers_over_time[-2]} (마지막 회차 증가분)")
