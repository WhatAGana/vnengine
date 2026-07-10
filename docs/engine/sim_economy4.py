"""
4차(튜닝 마무리) — 3차에서 병목은 풀렸고 이제 후반 골드잉여만 잡으면 됨
  - 상시 지출을 수입 스케일에 연동 (유지비 10%→30~40%대로)
      · 여관 유지비를 던전레벨에 비례 (성장할수록 유지비도 큼 = 상대화)
  - 후반 골드잉여 흡수용 sink 추가: 몹 강화 비용 (계승몹 파워업에 골드 소모)
  - 목표: 수입배율 2x이하 유지 + 유지비율 30~50% + 레벨업 꾸준 + 말골드 안정
"""
import math, random, statistics as st
def isqrt(n): return int(math.isqrt(int(max(0,n))))
P = {'w_hero':2,'w_loop':8,'w_placed':1,'w_dungeon':3,'threat_base':20,
     'gold_base':5,'gold_threat_k':3,'levelup_base':120,'levelup_exp':1.32,
     'inn_upkeep_base':15,'inn_upkeep_per_lv':18,'prison_upkeep_per':5,
     'gem_price':40,'gacha_base_cost':2}
def rooms_cap(dl): return 3+dl*2
def threat_base(h,l,ap,dl): return P['w_hero']*h+P['w_loop']*(l-1)+P['w_placed']*ap+P['w_dungeon']*dl+P['threat_base']
def loot_gold(t,cap,rng):
    raw=P['gold_base']+isqrt(t)*P['gold_threat_k']+rng.randint(-3,3); raw=max(1,raw)
    return raw//2 if cap else raw
def levelup_cost(dl): return int(P['levelup_base']*(dl**P['levelup_exp']))
def gacha_cost(p): return P['gacha_base_cost']+p//3
def mob_upgrade_cost(dl): return 200+dl*80  # 후반 골드잉여 흡수 sink

def simulate_loop(loop,hero_lv,dungeon_lv,inherited,rng):
    gold=100; gems=0; pulls=0
    rooms=min(3+(dungeon_lv-1),rooms_cap(dungeon_lv)); PRISON_CAP=5; prisoners=0
    income=upkeep=sink=released=0; placed_avg=3+inherited//20
    for w in range(9):
        threat=threat_base(hero_lv,loop,placed_avg,dungeon_lv)
        wsize=min(60,max(5,8+loop*3+dungeon_lv*3+rng.randint(-4,6)))
        cap_budget=rooms*3; combat=cap_budget*12+inherited
        handled=min(wsize,max(1,combat//max(1,threat//4)))
        captures=min(handled//2,PRISON_CAP-prisoners); kills=handled-captures
        for _ in range(kills): income+=loot_gold(threat,False,rng)
        for _ in range(captures): income+=loot_gold(threat,True,rng); prisoners+=1
        # 여관 유지비를 던전레벨 연동 (상대화) + 감옥은 실제 수감인원
        upkeep+=P['inn_upkeep_base']+dungeon_lv*P['inn_upkeep_per_lv']+prisoners*P['prison_upkeep_per']
        if prisoners>=PRISON_CAP: released+=prisoners; prisoners=0
    released+=prisoners; gold+=income-upkeep
    # 지출 우선순위: 1)레벨업 2)몹강화(잉여흡수) 3)마석/가챠
    lvcost=levelup_cost(dungeon_lv); leveled=False
    if gold>=lvcost and released>=dungeon_lv*2:
        gold-=lvcost; sink+=lvcost; leveled=True
    # 몹 강화: 골드 여유 있으면 계승몹 파워업에 투자 (상한까지)
    mob_up=0
    if inherited<200:
        muc=mob_upgrade_cost(dungeon_lv)
        while gold>=muc and inherited+mob_up<200:
            gold-=muc; sink+=muc; mob_up+=8
            if mob_up>=24: break  # 회차당 강화 상한 (급성장 방지)
    gfg=int(gold*0.6); gb=gfg//P['gem_price']; gold-=gb*P['gem_price']; sink+=gb*P['gem_price']; gems+=gb
    while gems>0:
        c=gacha_cost(pulls)
        if gems<c: break
        gems-=c; pulls+=1
    return dict(loop=loop,income=income,upkeep=upkeep,sink=sink,end_gold=gold,
                threat=threat_base(hero_lv,loop,placed_avg,dungeon_lv),pulls=pulls,gc=gacha_cost(pulls),
                released=released,leveled=leveled,dlv=dungeon_lv,ur=upkeep/max(1,income),mob_up=mob_up,inh=inherited)

rng=random.Random(42)
print("="*116)
print("4차 시뮬 (튜닝 마무리) - 20회차")
print("="*116)
print(f"{'회차':>3} {'Lv':>3} {'계승몹':>5} {'수입':>7} {'유지비':>6} {'sink':>6} {'말골드':>7} {'Threat':>7} {'가챠비':>6} {'유지%':>6} {'렙업':>4} {'몹강화':>5}")
print("-"*116)
hero_lv=5; dungeon_lv=1; inherited=30
for loop in range(1,21):
    r=simulate_loop(loop,hero_lv,dungeon_lv,inherited,rng)
    print(f"{r['loop']:>3} {r['dlv']:>3} {r['inh']:>5} {r['income']:>7} {r['upkeep']:>6} {r['sink']:>6} {r['end_gold']:>7} "
          f"{r['threat']:>7} {r['gc']:>6} {r['ur']:>5.0%} {'O' if r['leveled'] else '-':>4} {r['mob_up']:>5}")
    hero_lv+=2
    if r['leveled']: dungeon_lv+=1
    inherited=min(200,inherited+r['mob_up']+4)  # 몹강화분 + 자연성장 소폭
print("-"*116)
# 재집계
rng=random.Random(42); incomes=[]; ratios=[]; golds=[]; levels=[]
hero_lv=5; dungeon_lv=1; inherited=30
for loop in range(1,21):
    r=simulate_loop(loop,hero_lv,dungeon_lv,inherited,rng)
    incomes.append(r['income']); ratios.append(r['ur']); golds.append(r['end_gold']); levels.append(r['dlv'])
    hero_lv+=2
    if r['leveled']: dungeon_lv+=1
    inherited=min(200,inherited+r['mob_up']+4)
print(f"\n[인플레]  수입 {min(incomes)}~{max(incomes)}, 배율 {max(incomes)/min(incomes):.2f}x  (2x이하 양호)")
print(f"[상시지출] 유지비율 평균 {st.mean(ratios):.0%}, 범위 {min(ratios):.0%}~{max(ratios):.0%}  (30~50% 목표)")
print(f"[골드잉여] 말골드 평균 {int(st.mean(golds))}, 최대 {max(golds)}  (폭증 없어야)")
print(f"[성장]     던전레벨 1→{max(levels)}  (꾸준히 오르되 상한)")
print(f"[계승몹]   30→{inherited} (상한 200 도달여부 = 하드캡 작동)")
