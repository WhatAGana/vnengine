"""
6차 — 5차에서 여관수입 폭주(4.24x) 수정.
핵심 재설계: 여관의 성격을 '골드벌이'에서 '인과율 배출구(레벨엔진)'로 이동.
  - 여관 골드수입: 제곱근 완화 + 낮은 상한 (돈은 주로 전투에서 벌게)
  - 여관 인과율: 손님만족도로 계속 배출 (레벨업의 주력 축)
  - 손님수: 던전레벨 분리, 여관규모(직원·메뉴)에만 연동 → 자동스케일 끊기
설계원칙 확립: "새 수입원엔 새 수입원과 같은 완화를 걸어라"
"""
import math, random, statistics as st
def isqrt(n): return int(math.isqrt(int(max(0,n))))
P = {'w_hero':2,'w_loop':8,'w_placed':1,'w_dungeon':3,'threat_base':20,
     'gold_base':5,'gold_threat_k':3,'levelup_base':120,'levelup_exp':1.32,
     'prison_upkeep_per':5,'gem_price':40,'gacha_base_cost':2}
def rooms_cap(dl): return 3+dl*2
def threat_base(h,l,ap,dl): return P['w_hero']*h+P['w_loop']*(l-1)+P['w_placed']*ap+P['w_dungeon']*dl+P['threat_base']
def loot_gold(t,cap,rng):
    raw=P['gold_base']+isqrt(t)*P['gold_threat_k']+rng.randint(-3,3); raw=max(1,raw)
    return raw//2 if cap else raw
def levelup_cost(dl): return int(P['levelup_base']*(dl**P['levelup_exp']))
def gacha_cost(p): return P['gacha_base_cost']+p//3

def simulate_loop(loop,hero_lv,dungeon_lv,inherited,inn,rng):
    gold=100; gems=0; pulls=0
    rooms=min(3+(dungeon_lv-1),rooms_cap(dungeon_lv)); PRISON_CAP=5; prisoners=0
    combat_income=inn_income=upkeep=sink=released=inn_karma=0
    placed_avg=3+inherited//20
    for w in range(9):
        threat=threat_base(hero_lv,loop,placed_avg,dungeon_lv)
        wsize=min(60,max(5,8+loop*3+dungeon_lv*3+rng.randint(-4,6)))
        cap_budget=rooms*3; combat=cap_budget*12+inherited
        handled=min(wsize,max(1,combat//max(1,threat//4)))
        captures=min(handled//2,PRISON_CAP-prisoners); kills=handled-captures
        for _ in range(kills): combat_income+=loot_gold(threat,False,rng)
        for _ in range(captures): combat_income+=loot_gold(threat,True,rng); prisoners+=1
        # --- 여관: 인과율 주력, 골드는 완화+상한 ---
        if inn['decor']>0:
            # 손님수: 여관규모(직원·메뉴)에만 연동, 던전레벨 무관
            guests=min(inn['staff']*2+inn['menu_lv'], 25)  # 상한 25
            # 골드수입: 제곱근 완화 + 상한 (여관은 돈벌이 주력이 아님)
            gval=isqrt(guests)*8 + inn['menu_lv']*3
            inn_income+=min(gval, 300)  # 웨이브당 여관골드 상한 300
            inn_karma+=guests            # 인과율은 손님수 그대로 (레벨엔진)
            inn['decor']-=2
        upkeep+=inn['staff']*12 + prisoners*P['prison_upkeep_per']
        if prisoners>=PRISON_CAP: released+=prisoners; prisoners=0
    released+=prisoners
    income=combat_income+inn_income
    gold+=income-upkeep
    total_karma=released+inn_karma//3

    if inn['decor']<10:
        repair=10-inn['decor']; cost=repair*15
        if gold>=cost: gold-=cost; sink+=cost; inn['decor']+=repair
    lvcost=levelup_cost(dungeon_lv); leveled=False
    if gold>=lvcost and total_karma>=dungeon_lv*2:
        gold-=lvcost; sink+=lvcost; leveled=True
    if gold>500 and inn['staff']<3+dungeon_lv:
        hire=150+inn['staff']*50
        if gold>=hire: gold-=hire; sink+=hire; inn['staff']+=1
    if gold>800 and inn['menu_lv']<10:
        mcost=300+inn['menu_lv']*120
        if gold>=mcost: gold-=mcost; sink+=mcost; inn['menu_lv']+=1
    mob_up=0
    if inherited<200:
        muc=200+dungeon_lv*80
        while gold>=muc and inherited+mob_up<200 and mob_up<24:
            gold-=muc; sink+=muc; mob_up+=8
    gfg=int(gold*0.5); gb=gfg//P['gem_price']; gold-=gb*P['gem_price']; sink+=gb*P['gem_price']; gems+=gb
    while gems>0:
        c=gacha_cost(pulls)
        if gems<c: break
        gems-=c; pulls+=1
    return dict(loop=loop,ci=combat_income,ii=inn_income,income=income,upkeep=upkeep,sink=sink,
                end_gold=gold,dlv=dungeon_lv,leveled=leveled,mob_up=mob_up,
                staff=inn['staff'],menu=inn['menu_lv'],ur=upkeep/max(1,income),inh=inherited,karma=total_karma)

rng=random.Random(42)
print("="*120)
print("6차 시뮬 (여관=인과율엔진 재설계) - 20회차")
print("="*120)
print(f"{'회차':>3} {'Lv':>3} {'전투골드':>7} {'여관골드':>7} {'유지비':>6} {'말골드':>7} {'유지%':>6} {'렙업':>4} {'직원':>4} {'메뉴':>4} {'인과율':>5} {'계승':>4}")
print("-"*120)
hero_lv=5; dungeon_lv=1; inherited=30; inn={'staff':1,'decor':20,'menu_lv':1}
for loop in range(1,21):
    r=simulate_loop(loop,hero_lv,dungeon_lv,inherited,inn,rng)
    print(f"{r['loop']:>3} {r['dlv']:>3} {r['ci']:>7} {r['ii']:>7} {r['upkeep']:>6} {r['end_gold']:>7} "
          f"{r['ur']:>5.0%} {'O' if r['leveled'] else '-':>4} {r['staff']:>4} {r['menu']:>4} {r['karma']:>5} {r['inh']:>4}")
    hero_lv+=2
    if r['leveled']: dungeon_lv+=1
    inherited=min(200,inherited+r['mob_up']+4)
print("-"*120)
rng=random.Random(42); incomes=[]; ratios=[]; golds=[]; cis=[]; iis=[]
hero_lv=5; dungeon_lv=1; inherited=30; inn={'staff':1,'decor':20,'menu_lv':1}
for loop in range(1,21):
    r=simulate_loop(loop,hero_lv,dungeon_lv,inherited,inn,rng)
    incomes.append(r['income']); ratios.append(r['ur']); golds.append(r['end_gold']); cis.append(r['ci']); iis.append(r['ii'])
    hero_lv+=2
    if r['leveled']: dungeon_lv+=1
    inherited=min(200,inherited+r['mob_up']+4)
print(f"\n[인플레]  총수입 배율 {max(incomes)/min(incomes):.2f}x  (2x이하 목표)")
print(f"          전투골드 {min(cis)}~{max(cis)} ({max(cis)/min(cis):.1f}x), 여관골드 {min(iis)}~{max(iis)} ({max(iis)/max(1,min(iis)):.1f}x)")
print(f"[유지비율] 평균 {st.mean(ratios):.0%}, 범위 {min(ratios):.0%}~{max(ratios):.0%}")
print(f"[골드잉여] 말골드 평균 {int(st.mean(golds))}, 최대 {max(golds)}")
print(f"[여관역할] 골드수입 비중 {sum(iis)/sum(incomes):.0%} (낮을수록 여관=인과율엔진, 돈은 전투에서)")
