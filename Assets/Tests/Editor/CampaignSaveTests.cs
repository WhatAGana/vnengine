using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine; // JsonUtility

namespace VNEngine.Tests
{
    public class CampaignSaveTests
    {
        private static CampaignState Sample() =>
            new CampaignState(
                new MetaState(3),
                new RunState(5, new Dictionary<string, int> { { "money", 150 }, { "magic", 30 } }));

        [Test]
        public void CaptureThenRestoreRoundTrips()
        {
            var restored = CampaignSave.Restore(CampaignSave.Capture(Sample()));
            Assert.AreEqual(3, restored.Meta.LoopCount);
            Assert.AreEqual(5, restored.Run.Day);
            Assert.AreEqual(150, restored.Run.Resources["money"]);
            Assert.AreEqual(30, restored.Run.Resources["magic"]);
            Assert.AreEqual(2, restored.Run.Resources.Count);
        }

        [Test]
        public void JsonUtilityRoundTripThroughSaveData()
        {
            var data = CampaignSave.Capture(Sample());
            string json = JsonUtility.ToJson(data);
            var back = JsonUtility.FromJson<CampaignSaveData>(json);
            var restored = CampaignSave.Restore(back);
            Assert.AreEqual(3, restored.Meta.LoopCount);
            Assert.AreEqual(5, restored.Run.Day);
            Assert.AreEqual(150, restored.Run.Resources["money"]);
        }

        [Test]
        public void CapturedVersionIsCurrent()
        {
            Assert.AreEqual(CampaignSaveData.CampaignSaveVersion, CampaignSave.Capture(Sample()).version);
        }

        [Test]
        public void RestoreRejectsIncompatibleVersion()
        {
            var data = CampaignSave.Capture(Sample());
            data.version = 999;
            Assert.Throws<VnRuntimeException>(() => CampaignSave.Restore(data));
        }

        [Test]
        public void RestoreDoesNotAliasSaveDataList()
        {
            var data = CampaignSave.Capture(Sample());
            var restored = CampaignSave.Restore(data);
            data.resources[0].value = 999;                 // 복원 후 원본 세이브데이터 수정
            data.resources.Add(new ResEntry { id = "x", value = 1 });
            Assert.AreEqual(150, restored.Run.Resources["money"], "복원 상태가 세이브데이터 리스트를 참조 공유하면 안 됨");
            Assert.AreEqual(2, restored.Run.Resources.Count);
        }

        private static CampaignState SampleWithHeroes()
        {
            var heroes = HeroStats.FromDefs(StatCatalog.Default())
                .WithStat(StatIds.STR, 120)
                .WithStat(StatIds.LUK, 7);
            return new CampaignState(
                new MetaState(4, heroes),
                new RunState(2, new System.Collections.Generic.Dictionary<string, int> { { "money", 10 } }));
        }

        [Test]
        public void HeroStatsRoundTripThroughCapture()
        {
            var restored = CampaignSave.Restore(CampaignSave.Capture(SampleWithHeroes()));
            Assert.AreEqual(120, restored.Meta.Heroes.Get(StatIds.STR));
            Assert.AreEqual(7, restored.Meta.Heroes.Get(StatIds.LUK));
            Assert.AreEqual(50, restored.Meta.Heroes.Get(StatIds.HP), "손대지 않은 스탯도 유지");
            Assert.AreEqual(8, restored.Meta.Heroes.Values.Count);
        }

        [Test]
        public void HeroStatsRoundTripThroughJsonUtility()
        {
            var data = CampaignSave.Capture(SampleWithHeroes());
            string json = JsonUtility.ToJson(data);
            var back = JsonUtility.FromJson<CampaignSaveData>(json);
            var restored = CampaignSave.Restore(back);
            Assert.AreEqual(120, restored.Meta.Heroes.Get(StatIds.STR));
            Assert.AreEqual(8, restored.Meta.Heroes.Values.Count);
        }

        [Test]
        public void RestoreDoesNotAliasStatsList()
        {
            var data = CampaignSave.Capture(SampleWithHeroes());
            var restored = CampaignSave.Restore(data);
            data.stats[0].value = 999;                          // 복원 후 원본 세이브데이터 수정
            data.stats.Add(new StatEntry { id = "GHOST", value = 1 });
            Assert.IsFalse(restored.Meta.Heroes.Has(new StatId("GHOST")), "복원 상태가 세이브 리스트를 참조 공유하면 안 됨");
            Assert.AreEqual(120, restored.Meta.Heroes.Get(StatIds.STR));
        }

        [Test]
        public void EmptyHeroesRoundTripsToEmpty()
        {
            var restored = CampaignSave.Restore(CampaignSave.Capture(Sample())); // Sample()=스탯 없음
            Assert.AreEqual(0, restored.Meta.Heroes.Values.Count);
        }

        private static CampaignState SampleWithKarmaAndPulls()
        {
            return new CampaignState(
                new MetaState(3, HeroStats.Empty, InnState.Empty, 25),
                new RunState(5, new Dictionary<string, int> { { "money", 150 } }, System.Array.Empty<Captive>(), 9));
        }

        [Test]
        public void KarmaBankAndPullsThisLoopRoundTripThroughCapture()
        {
            var restored = CampaignSave.Restore(CampaignSave.Capture(SampleWithKarmaAndPulls()));
            Assert.AreEqual(25, restored.Meta.KarmaBank);
            Assert.AreEqual(9, restored.Run.PullsThisLoop);
        }

        [Test]
        public void KarmaBankAndPullsThisLoopRoundTripThroughJsonUtility()
        {
            var data = CampaignSave.Capture(SampleWithKarmaAndPulls());
            string json = JsonUtility.ToJson(data);
            var back = JsonUtility.FromJson<CampaignSaveData>(json);
            var restored = CampaignSave.Restore(back);
            Assert.AreEqual(25, restored.Meta.KarmaBank);
            Assert.AreEqual(9, restored.Run.PullsThisLoop);
        }

        [Test]
        public void OldSaveMissingKarmaAndPullsFieldsRestoresToZeroWithoutException()
        {
            // 구세이브 시뮬레이션: karmaBank/pullsThisLoop 필드가 없던 시절의 JSON(누락 → 기본값 0).
            var data = CampaignSave.Capture(Sample());
            data.karmaBank = 0;
            data.pullsThisLoop = 0;

            CampaignState restored = null;
            Assert.DoesNotThrow(() => restored = CampaignSave.Restore(data));
            Assert.AreEqual(0, restored.Meta.KarmaBank);
            Assert.AreEqual(0, restored.Run.PullsThisLoop);
        }

        private static CampaignState SampleWithCaptives()
        {
            var captives = new List<Captive>
            {
                new Captive(new UnitClassId("Succubus"), isNamed: true, ResetPolicy.PersistAcrossLoops),
                new Captive(new UnitClassId("Imp"), isNamed: false, ResetPolicy.Unspecified),
            };
            return new CampaignState(
                new MetaState(3),
                new RunState(5, new Dictionary<string, int> { { "money", 150 } }, captives));
        }

        [Test]
        public void CaptivesRoundTripThroughCapture()
        {
            var restored = CampaignSave.Restore(CampaignSave.Capture(SampleWithCaptives()));
            Assert.AreEqual(2, restored.Run.Captives.Count);
            Assert.AreEqual(new UnitClassId("Succubus"), restored.Run.Captives[0].ClassId);
            Assert.IsTrue(restored.Run.Captives[0].IsNamed);
            Assert.AreEqual(ResetPolicy.PersistAcrossLoops, restored.Run.Captives[0].ResetPolicy);
            Assert.AreEqual(new UnitClassId("Imp"), restored.Run.Captives[1].ClassId);
            Assert.IsFalse(restored.Run.Captives[1].IsNamed);
            Assert.AreEqual(ResetPolicy.Unspecified, restored.Run.Captives[1].ResetPolicy);
        }

        [Test]
        public void CaptivesRoundTripThroughJsonUtility()
        {
            var data = CampaignSave.Capture(SampleWithCaptives());
            string json = JsonUtility.ToJson(data);
            var back = JsonUtility.FromJson<CampaignSaveData>(json);
            var restored = CampaignSave.Restore(back);
            Assert.AreEqual(2, restored.Run.Captives.Count);
            Assert.AreEqual(new UnitClassId("Succubus"), restored.Run.Captives[0].ClassId);
            Assert.AreEqual(ResetPolicy.PersistAcrossLoops, restored.Run.Captives[0].ResetPolicy);
        }

        [Test]
        public void OldSaveMissingCaptivesFieldRestoresToEmptyWithoutException()
        {
            // 구세이브 시뮬레이션: captives 필드가 없던 시절의 JSON(누락 리스트 → JsonUtility가 null로 역직렬화).
            var data = CampaignSave.Capture(Sample());
            data.captives = null;

            CampaignState restored = null;
            Assert.DoesNotThrow(() => restored = CampaignSave.Restore(data));
            Assert.AreEqual(0, restored.Run.Captives.Count);
        }

        private static CampaignState SampleWithDungeonLevel(int dungeonLevel) =>
            new CampaignState(
                new MetaState(3, HeroStats.Empty, InnState.Empty, 0, dungeonLevel),
                new RunState(5, new Dictionary<string, int> { { "money", 150 } }));

        [Test]
        public void DungeonLevelRoundTripsThroughCapture()
        {
            var restored = CampaignSave.Restore(CampaignSave.Capture(SampleWithDungeonLevel(5)));
            Assert.AreEqual(5, restored.Meta.DungeonLevel);
        }

        [Test]
        public void DungeonLevelRoundTripsThroughJsonUtility()
        {
            var data = CampaignSave.Capture(SampleWithDungeonLevel(5));
            string json = JsonUtility.ToJson(data);
            var back = JsonUtility.FromJson<CampaignSaveData>(json);
            var restored = CampaignSave.Restore(back);
            Assert.AreEqual(5, restored.Meta.DungeonLevel);
        }

        [Test]
        public void OldSaveMissingDungeonLevelFieldRestoresToOneWithoutException()
        {
            // 구세이브 시뮬레이션: dungeonLevel 필드가 없던 시절의 JSON(누락 → JsonUtility 기본 int 0).
            // DungeonLevelRule 은 dl<1 을 예외로 다루므로 0이 아니라 1로 복원돼야 한다.
            var data = CampaignSave.Capture(Sample());
            data.dungeonLevel = 0;

            CampaignState restored = null;
            Assert.DoesNotThrow(() => restored = CampaignSave.Restore(data));
            Assert.AreEqual(1, restored.Meta.DungeonLevel);
        }
    }
}
