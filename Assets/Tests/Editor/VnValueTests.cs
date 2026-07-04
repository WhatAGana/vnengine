using NUnit.Framework;

namespace VNEngine.Tests
{
    public class VnValueTests
    {
        [Test] public void IntStoresValue() => Assert.AreEqual(42, VnValue.Int(42).AsInt);
        [Test] public void IntKind() => Assert.AreEqual(VnKind.Int, VnValue.Int(1).Kind);
        [Test] public void BoolKind() => Assert.AreEqual(VnKind.Bool, VnValue.Bool(true).Kind);
        [Test] public void BoolTrueAsBool() => Assert.IsTrue(VnValue.Bool(true).AsBool);
        [Test] public void BoolFalseAsBool() => Assert.IsFalse(VnValue.Bool(false).AsBool);

        [Test] public void ZeroIntIsFalsy() => Assert.IsFalse(VnValue.Int(0).Truthy);
        [Test] public void NonZeroIntIsTruthy() => Assert.IsTrue(VnValue.Int(3).Truthy);
        [Test] public void NegativeIntIsTruthy() => Assert.IsTrue(VnValue.Int(-1).Truthy);
        [Test] public void BoolTrueTruthy() => Assert.IsTrue(VnValue.Bool(true).Truthy);

        [Test] public void IntToString() => Assert.AreEqual("7", VnValue.Int(7).ToString());
        [Test] public void BoolTrueToString() => Assert.AreEqual("true", VnValue.Bool(true).ToString());
        [Test] public void BoolFalseToString() => Assert.AreEqual("false", VnValue.Bool(false).ToString());

        [Test] public void EqualityByKindAndValue()
        {
            Assert.AreEqual(VnValue.Int(5), VnValue.Int(5));
            Assert.AreNotEqual(VnValue.Int(1), VnValue.Bool(true));
            Assert.AreNotEqual(VnValue.Int(0), VnValue.Bool(false));
        }
    }
}
