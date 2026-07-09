using System;
using System.Collections.Generic;

namespace VNEngine
{
    // 웨이브 1회 처리 결과 묶음. Campaign은 배치검증→전투→약탈/포획 인과율 반영이 끝난 새 상태.
    public readonly struct WaveOutcome
    {
        public CampaignState Campaign { get; }
        public CombatResult Combat { get; }
        public int GoldGained { get; }
        public int CaptureKarmaGained { get; }
        public int InnGoldGained { get; }
        public int InnKarmaGained { get; }

        public WaveOutcome(CampaignState campaign, CombatResult combat, int goldGained, int captureKarmaGained, int innGoldGained, int innKarmaGained)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
            GoldGained = goldGained;
            CaptureKarmaGained = captureKarmaGained;
            InnGoldGained = innGoldGained;
            InnKarmaGained = innKarmaGained;
        }
    }

    // 턴 오케스트레이터(순수함수), 07-C 통합 핵심: 배치검증(하드가드) -> 전투해결 -> 약탈골드/포획인과율 -> 포로/자원 누적.
    // 입력(campaign/baseGraph/...)은 절대 고치지 않는다 — 전부 새 인스턴스로 반환.
    public static class CampaignWaveRule
    {
        // CombatResolver 내부(threatBase 계산)와 동일한 중립값 — 07-B 배치몹 레벨 미도입(비스코프)이므로 0.
        private const int AvgPlacedMonsterLevelUnmodeled = 0;

        public static WaveOutcome ResolveWave(
            CampaignState campaign,
            PlacementPlan plan,
            WaveDef wave,
            RoomGraph baseGraph,
            IReadOnlyList<MonsterDef> monsterCatalog,
            HeroStats hero,
            StatCombatWeights statWeights,
            ThreatWeights threatWeights,
            IReadOnlyList<UnitClassDef> classCatalog,
            ClassMatchup matchup,
            CaptureRule captureRule,
            int dungeonLevel,
            string goldResourceId,
            IRandom rng)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (wave == null) throw new ArgumentNullException(nameof(wave));
            if (baseGraph == null) throw new ArgumentNullException(nameof(baseGraph));
            if (monsterCatalog == null) throw new ArgumentNullException(nameof(monsterCatalog));
            if (hero == null) throw new ArgumentNullException(nameof(hero));
            if (statWeights == null) throw new ArgumentNullException(nameof(statWeights));
            if (threatWeights == null) throw new ArgumentNullException(nameof(threatWeights));
            if (classCatalog == null) throw new ArgumentNullException(nameof(classCatalog));
            if (matchup == null) throw new ArgumentNullException(nameof(matchup));
            if (captureRule == null) throw new ArgumentNullException(nameof(captureRule));
            if (goldResourceId == null) throw new ArgumentNullException(nameof(goldResourceId));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            // (1) 배치 하드가드 — 미검증 플랜으로는 절대 전투에 들어가지 않는다(주인공 코어앞1칸 위반 등 즉시 예외).
            var graph = PlacementBuilder.ValidateAndApply(plan, baseGraph, monsterCatalog);

            // (2) 전투 해결 — rng 소비는 여기 한 곳뿐(결정론: 같은 시드 -> 같은 결과).
            var combat = CombatResolver.ResolveWave(
                campaign.Run, wave, graph, hero, statWeights, threatWeights,
                classCatalog, matchup, captureRule, dungeonLevel, campaign.Meta.LoopCount, rng);

            // (3) threatBase 재계산 — CombatResolver.cs:62-64 과 동일 호출(같은 loopCount/dungeonLevel/미도입 중립값)
            // 이어야 약탈 threat 이 방금 싸운 웨이브의 threat 과 일치한다.
            var heroPower = ThreatFormula.HeroPowerOf(statWeights, hero);
            var threatBase = ThreatFormula.Compute(threatWeights, heroPower, campaign.Meta.LoopCount, AvgPlacedMonsterLevelUnmodeled, dungeonLevel);

            // (4) 약탈 골드 + 포획 인과율.
            var gold = combat.Killed.Count * LootRule.LootGold(threatBase, false)
                + combat.Captured.Count * LootRule.LootGold(threatBase, true);
            var captureKarma = combat.Captured.Count * LootRule.CaptureKarma(threatBase);

            // (5) 여관 수급 — 게이트(Decor<=0 -> Zero)는 반드시 감쇠 전(pre-decay) Decor로 판정한다(07-D 규칙 유지,
            // 순서 위반 시 Decor=1에서도 수입이 0이 되어버리는 회귀가 발생한다: ResolveWave_InnGateBeforeDecay_DecorOne로 방지).
            var income = InnIncomeRule.Compute(campaign.Meta.Inn);
            var decayedInn = InnUpkeepRule.Decay(campaign.Meta.Inn);

            // (6) 포로 누적 + 골드 자원 반영(약탈골드 + 여관골드를 한 번에 얹는다). Day/PullsThisLoop는 Accumulate가
            // 보존한다(CaptiveLedgerTests.AccumulatePreservesPullsThisLoop / ResolveWave_PreservesPullsThisLoop로 회귀 방지).
            var accumulatedRun = CaptiveLedger.Accumulate(campaign.Run, combat);
            var resources = new Dictionary<string, int>(accumulatedRun.Resources);
            resources.TryGetValue(goldResourceId, out var currentGold);
            resources[goldResourceId] = currentGold + gold + income.Gold;
            var newRun = new RunState(accumulatedRun.Day, resources, accumulatedRun.Captives, accumulatedRun.PullsThisLoop);

            // (7) KarmaBank 증가(포획인과율 + 여관인과율) + 여관 감쇠 반영. LoopCount/Heroes 보존.
            var newMeta = new MetaState(campaign.Meta.LoopCount, campaign.Meta.Heroes, decayedInn, campaign.Meta.KarmaBank + captureKarma + income.Karma, campaign.Meta.DungeonLevel);

            // (8) 결과.
            return new WaveOutcome(new CampaignState(newMeta, newRun), combat, gold, captureKarma, income.Gold, income.Karma);
        }
    }
}
