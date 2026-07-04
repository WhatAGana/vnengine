using NUnit.Framework;

namespace VNEngine.Tests
{
    public class SmokeTest
    {
        [Test]
        public void CoreAssemblyIsReferencable()
        {
            Assert.AreEqual("VNEngine.Core", VNEngine.VnEngineInfo.Name);
        }
    }
}
