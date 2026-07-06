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
    }
}
