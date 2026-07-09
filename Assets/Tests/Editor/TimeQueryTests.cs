using NUnit.Framework;

namespace VNEngine.Tests
{
    public class TimeQueryTests
    {
        [Test] public void Constants_MatchNinetyDayStructure()
        {
            Assert.AreEqual(90, TimeQuery.MaxDay);
            Assert.AreEqual(10, TimeQuery.WaveInterval);
            Assert.AreEqual(9, TimeQuery.Cycles);
            Assert.AreEqual(9, TimeQuery.SaveDayInCycle);
        }

        [Test] public void GetPhase_MultiplesOfTen_AreWaveDays()
        {
            Assert.AreEqual(DayPhase.Wave, TimeQuery.GetPhase(10));
            Assert.AreEqual(DayPhase.Wave, TimeQuery.GetPhase(90));
        }

        [Test] public void GetPhase_NonMultiplesOfTen_AreMaintenance()
        {
            Assert.AreEqual(DayPhase.Maintenance, TimeQuery.GetPhase(1));
            Assert.AreEqual(DayPhase.Maintenance, TimeQuery.GetPhase(9));
            Assert.AreEqual(DayPhase.Maintenance, TimeQuery.GetPhase(11));
        }

        [Test] public void IsWaveDay_TrueOnlyForMultiplesOfTenWithinRange()
        {
            Assert.IsTrue(TimeQuery.IsWaveDay(10));
            Assert.IsTrue(TimeQuery.IsWaveDay(90));
            Assert.IsFalse(TimeQuery.IsWaveDay(9));
            Assert.IsFalse(TimeQuery.IsWaveDay(100)); // 범위 밖
        }

        [Test] public void IsSaveDay_TrueOnEveOfEachWave()
        {
            Assert.IsTrue(TimeQuery.IsSaveDay(9));
            Assert.IsTrue(TimeQuery.IsSaveDay(89));
            Assert.IsFalse(TimeQuery.IsSaveDay(10));
            Assert.IsFalse(TimeQuery.IsSaveDay(1));
        }

        [Test] public void DaysUntilNextWave_CountsToNextMultipleOfTen()
        {
            Assert.AreEqual(9, TimeQuery.DaysUntilNextWave(1));  // 1->10
            Assert.AreEqual(1, TimeQuery.DaysUntilNextWave(9));  // 9->10
            Assert.AreEqual(10, TimeQuery.DaysUntilNextWave(10)); // 10->20
            Assert.AreEqual(0, TimeQuery.DaysUntilNextWave(90));  // 마지막 웨이브 이후 없음
        }

        [Test] public void Queries_RejectNonPositiveDay()
        {
            Assert.Throws<VnRuntimeException>(() => TimeQuery.GetPhase(0));
            Assert.Throws<VnRuntimeException>(() => TimeQuery.IsWaveDay(0));
            Assert.Throws<VnRuntimeException>(() => TimeQuery.IsSaveDay(-1));
            Assert.Throws<VnRuntimeException>(() => TimeQuery.DaysUntilNextWave(0));
        }
    }
}
