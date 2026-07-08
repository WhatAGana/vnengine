using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class InnPersistenceTests
    {
        [Test]
        public void MetaStateDefaultsInnToEmpty()
        {
            Assert.AreEqual(0, new MetaState(1).Inn.Decor, "1-arg 생성자 → Inn=Empty");
            Assert.AreEqual(0, new MetaState(1, HeroStats.Empty).Inn.Staff, "2-arg 생성자 → Inn=Empty");
        }

        [Test]
        public void MetaStateThreeArgCarriesInn()
        {
            var meta = new MetaState(2, HeroStats.Empty, new InnState(4, 7, 3));
            Assert.AreEqual(4, meta.Inn.Staff);
            Assert.AreEqual(7, meta.Inn.Decor);
            Assert.AreEqual(3, meta.Inn.MenuLevel);
        }

        [Test]
        public void CaptureRestoreRoundTripsInn()
        {
            var state = new CampaignState(
                new MetaState(5, HeroStats.Empty, new InnState(6, 9, 2)),
                new RunState(3, new Dictionary<string, int>()));
            var restored = CampaignSave.Restore(CampaignSave.Capture(state));
            Assert.AreEqual(6, restored.Meta.Inn.Staff);
            Assert.AreEqual(9, restored.Meta.Inn.Decor);
            Assert.AreEqual(2, restored.Meta.Inn.MenuLevel);
        }

        [Test]
        public void RestoreOfSaveDataWithDefaultInnFieldsYieldsClosedGate()
        {
            // 구세이브 모사: inn 필드 미기록 → JsonUtility 기본 0 → Decor=0(게이트 닫힘).
            var data = new CampaignSaveData
            {
                version = CampaignSaveData.CampaignSaveVersion,
                loopCount = 1,
                day = 1,
            };
            var restored = CampaignSave.Restore(data);
            Assert.AreEqual(0, restored.Meta.Inn.Decor, "inn 미기록 세이브는 Decor=0으로 복원");
        }
    }
}
