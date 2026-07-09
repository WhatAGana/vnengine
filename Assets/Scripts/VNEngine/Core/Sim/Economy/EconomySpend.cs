using System;
using System.Collections.Generic;

namespace VNEngine
{
    // 던전 레벨업 결과(값 타입, 불변). 실패(골드/인과율 부족) 시 Campaign 은 입력 그대로, Leveled=false.
    public readonly struct LevelUpResult
    {
        public CampaignState Campaign { get; }
        public bool Leveled { get; }

        public LevelUpResult(CampaignState campaign, bool leveled)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            Leveled = leveled;
        }
    }

    // 인과율(karma)/골드 소비 배선. 07-C task9: 스탯 강화(karma) + 던전 레벨업(gold+karma).
    // 순수 함수 — 재사용: StatUpgrade/DungeonLevelRule 을 그대로 호출할 뿐 재구현하지 않는다.
    public static class EconomySpend
    {
        // 인과율로 주인공 스탯 강화. StatUpgrade.Upgrade 가 실제 로직 담당 — 여기선 KarmaBank 차감만 배선.
        // KarmaSpent 는 StatUpgrade 가 가용 karma 이하로만 산출하므로 KarmaBank 는 절대 음수가 되지 않는다.
        public static MetaState UpgradeHeroStat(MetaState meta, StatDef def, StatCostCurve curve)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (curve == null) throw new ArgumentNullException(nameof(curve));

            var result = StatUpgrade.Upgrade(meta.Heroes, def, curve, meta.KarmaBank);
            return new MetaState(meta.LoopCount, result.Stats, meta.Inn, meta.KarmaBank - result.KarmaSpent, meta.DungeonLevel);
        }

        // 골드+인과율로 던전 레벨을 1 올린다. 골드 비용은 DungeonLevelRule.LevelUpCost(현재 레벨).
        // 둘 다 충족해야 성공 — 하나라도 부족하면 완전 no-op(Leveled=false, 아무것도 차감되지 않음).
        public static LevelUpResult LevelUpDungeon(CampaignState campaign, string goldResourceId, int karmaCost)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (goldResourceId == null) throw new ArgumentNullException(nameof(goldResourceId));

            int goldCost = DungeonLevelRule.LevelUpCost(campaign.Meta.DungeonLevel);
            campaign.Run.Resources.TryGetValue(goldResourceId, out var currentGold);

            if (currentGold < goldCost || campaign.Meta.KarmaBank < karmaCost)
                return new LevelUpResult(campaign, false);

            var resources = new Dictionary<string, int>(campaign.Run.Resources);
            resources[goldResourceId] = currentGold - goldCost;
            var newRun = new RunState(campaign.Run.Day, resources, campaign.Run.Captives, campaign.Run.PullsThisLoop);

            var newMeta = new MetaState(
                campaign.Meta.LoopCount,
                campaign.Meta.Heroes,
                campaign.Meta.Inn,
                campaign.Meta.KarmaBank - karmaCost,
                campaign.Meta.DungeonLevel + 1);

            return new LevelUpResult(new CampaignState(newMeta, newRun), true);
        }
    }
}
