using System.Collections.Generic;

namespace VNEngine
{
    // CampaignState <-> CampaignSaveData 순수 캡처/복원. IO 없음(Core).
    public static class CampaignSave
    {
        public static CampaignSaveData Capture(CampaignState c)
        {
            if (c == null) throw new System.ArgumentNullException(nameof(c));
            var data = new CampaignSaveData
            {
                version = CampaignSaveData.CampaignSaveVersion,
                loopCount = c.Meta.LoopCount,
                day = c.Run.Day,
                innStaff = c.Meta.Inn.Staff,
                innDecor = c.Meta.Inn.Decor,
                innMenuLevel = c.Meta.Inn.MenuLevel,
                karmaBank = c.Meta.KarmaBank,
                pullsThisLoop = c.Run.PullsThisLoop,
            };
            foreach (var kv in c.Run.Resources)
                data.resources.Add(new ResEntry { id = kv.Key, value = kv.Value });
            foreach (var kv in c.Meta.Heroes.Values)
                data.stats.Add(new StatEntry { id = kv.Key.Value, value = kv.Value });
            foreach (var captive in c.Run.Captives)
                data.captives.Add(new CaptiveEntry { classId = captive.ClassId.Value, isNamed = captive.IsNamed, resetPolicy = (int)captive.ResetPolicy });
            return data;
        }

        public static CampaignState Restore(CampaignSaveData data)
        {
            if (data == null) throw new System.ArgumentNullException(nameof(data));
            if (data.version != CampaignSaveData.CampaignSaveVersion)
                throw new VnRuntimeException(
                    $"Incompatible campaign save version: {data.version} (expected {CampaignSaveData.CampaignSaveVersion})");

            var res = new Dictionary<string, int>(data.resources.Count);
            foreach (var e in data.resources)
                res[e.id] = e.value;

            var statDict = new Dictionary<StatId, int>(data.stats != null ? data.stats.Count : 0);
            if (data.stats != null)
                foreach (var e in data.stats)
                    statDict[new StatId(e.id)] = e.value;
            var heroes = new HeroStats(statDict);
            var inn = new InnState(data.innStaff, data.innDecor, data.innMenuLevel);

            var captives = new List<Captive>(data.captives != null ? data.captives.Count : 0);
            if (data.captives != null)
                foreach (var e in data.captives)
                    captives.Add(new Captive(new UnitClassId(e.classId), e.isNamed, (ResetPolicy)e.resetPolicy));

            // RunState/HeroStats/InnState 생성자가 각각 방어적 복사 → 세이브데이터와 참조 분리.
            return new CampaignState(
                new MetaState(data.loopCount, heroes, inn, data.karmaBank),
                new RunState(data.day, res, captives, data.pullsThisLoop));
        }
    }
}
