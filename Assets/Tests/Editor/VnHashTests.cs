using NUnit.Framework;

namespace VNEngine.Tests
{
    public class VnHashTests
    {
        [Test]
        public void SameInputSameHash()
            => Assert.AreEqual(VnHash.Fnv1a("label start:\n요르 \"hi\""), VnHash.Fnv1a("label start:\n요르 \"hi\""));

        [Test]
        public void DifferentInputDifferentHash()
            => Assert.AreNotEqual(VnHash.Fnv1a("a"), VnHash.Fnv1a("b"));

        [Test]
        public void EmptyIsStable()
            => Assert.AreEqual(VnHash.Fnv1a(""), VnHash.Fnv1a(""));

        [Test]
        public void HashIsEightHexChars()
            => Assert.AreEqual(8, VnHash.Fnv1a("anything").Length);
    }
}
