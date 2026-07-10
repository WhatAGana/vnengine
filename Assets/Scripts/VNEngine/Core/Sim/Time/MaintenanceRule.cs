using System.Collections.Generic;

namespace VNEngine
{
    // 정비일 자원 정산(canonical 하루 여관 틱).
    // 07-D 불변식: InnIncomeRule.Compute(게이트=Decor>0)를 반드시 InnUpkeepRule.Decay 前에 평가.
    public static class MaintenanceRule
    {
        public static InnTickOutcome ApplyInnTick(CampaignState campaign, string goldResourceId)
        {
            if (campaign == null) throw new System.ArgumentNullException(nameof(campaign));
            if (goldResourceId == null) throw new System.ArgumentNullException(nameof(goldResourceId));

            var meta = campaign.Meta;
            var run = campaign.Run;

            var income = InnIncomeRule.Compute(meta.Inn);   // 게이트: pre-decay Decor
            var decayedInn = InnUpkeepRule.Decay(meta.Inn); // 그 다음 Decay

            var res = new Dictionary<string, int>(run.Resources.Count);
            foreach (var kv in run.Resources) res[kv.Key] = kv.Value;
            res.TryGetValue(goldResourceId, out int gold);
            res[goldResourceId] = gold + income.Gold;

            var newRun = new RunState(run.Day, res, run.Captives, run.PullsThisLoop);
            var newMeta = new MetaState(meta.LoopCount, meta.Heroes, decayedInn,
                                        meta.KarmaBank + income.Karma, meta.DungeonLevel);
            return new InnTickOutcome(new CampaignState(newMeta, newRun), income.Gold, income.Karma);
        }
    }

    public readonly struct InnTickOutcome
    {
        public CampaignState Campaign { get; }
        public int Gold { get; }
        public int Karma { get; }
        public InnTickOutcome(CampaignState campaign, int gold, int karma)
        {
            Campaign = campaign;
            Gold = gold;
            Karma = karma;
        }
    }
}
