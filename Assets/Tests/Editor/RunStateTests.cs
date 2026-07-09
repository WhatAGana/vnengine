using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class RunStateTests
    {
        [Test]
        public void ConstructorCopiesResourcesSoLaterMutationDoesNotLeak()
        {
            var src = new Dictionary<string, int> { { "money", 100 }, { "magic", 50 } };
            var run = new RunState(1, src);

            src["money"] = 999;      // 원본 딕셔너리 수정
            src["extra"] = 7;        // 키 추가

            Assert.AreEqual(100, run.Resources["money"], "원본 수정이 새어들면 안 됨");
            Assert.IsFalse(run.Resources.ContainsKey("extra"), "원본에 추가한 키가 새어들면 안 됨");
            Assert.AreEqual(2, run.Resources.Count);
        }

        [Test]
        public void ConstructorPreservesDayAndValues()
        {
            var run = new RunState(3, new Dictionary<string, int> { { "money", 40 } });
            Assert.AreEqual(3, run.Day);
            Assert.AreEqual(40, run.Resources["money"]);
        }

        [Test]
        public void CaptivesDefaultsToEmpty()
        {
            var run = new RunState(1, new Dictionary<string, int>());
            Assert.AreEqual(0, run.Captives.Count);
        }

        [Test]
        public void PullsThisLoop_DefaultsToZero()
        {
            var r = new RunState(1, new Dictionary<string, int>());
            Assert.AreEqual(0, r.PullsThisLoop);
        }

        [Test]
        public void PullsThisLoop_RoundTripsThroughFullCtor()
        {
            var r = new RunState(1, new Dictionary<string, int>(), System.Array.Empty<Captive>(), 7);
            Assert.AreEqual(7, r.PullsThisLoop);
        }
    }
}
