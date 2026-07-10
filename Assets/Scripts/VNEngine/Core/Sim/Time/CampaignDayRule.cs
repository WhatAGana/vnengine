namespace VNEngine
{
    // 하루 전이(순수). 속도/스킵을 전혀 모른다.
    // 웨이브날 = CampaignWaveRule.ResolveWave, 정비날 = MaintenanceRule.ApplyInnTick.
    // Day>90 이면 처리 없이 회귀신호만 반환(실제 Regress는 caller가 LoopEngine.StartNewLoop).
    public static class CampaignDayRule
    {
        public static AdvanceResult AdvanceDay(CampaignState campaign, DayContext ctx, IRandom rng)
        {
            if (campaign == null) throw new System.ArgumentNullException(nameof(campaign));
            if (ctx == null) throw new System.ArgumentNullException(nameof(ctx));
            if (rng == null) throw new System.ArgumentNullException(nameof(rng));

            int newDay = campaign.Run.Day + 1;
            if (newDay > TimeQuery.MaxDay)
                return new AdvanceResult(campaign, TimeQuery.GetPhase(campaign.Run.Day),
                                         regressPending: true, waveResolved: false, default);

            var run = campaign.Run;
            var bumped = new RunState(newDay, run.Resources, run.Captives, run.PullsThisLoop);
            var atNewDay = new CampaignState(campaign.Meta, bumped);

            if (TimeQuery.GetPhase(newDay) == DayPhase.Wave)
            {
                var outcome = CampaignWaveRule.ResolveWave(
                    atNewDay, ctx.Plan, ctx.WaveForDay(newDay), ctx.BaseGraph, ctx.MonsterCatalog,
                    atNewDay.Meta.Heroes, ctx.StatWeights, ctx.ThreatWeights, ctx.ClassCatalog, ctx.Matchup,
                    ctx.CaptureRule, atNewDay.Meta.DungeonLevel, ctx.GoldResourceId, rng);
                return new AdvanceResult(outcome.Campaign, DayPhase.Wave, false, true, outcome);
            }

            var tick = MaintenanceRule.ApplyInnTick(atNewDay, ctx.GoldResourceId);
            return new AdvanceResult(tick.Campaign, DayPhase.Maintenance, false, false, default);
        }
    }
}
