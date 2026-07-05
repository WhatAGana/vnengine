using NUnit.Framework;

namespace VNEngine.Tests
{
    public class ExprEvalTests
    {
        private GameState _s;

        [SetUp] public void Setup() => _s = new GameState(new SeededRandom(42));

        private VnValue Eval(string src) => ExprEval.Eval(ExprParser.Parse(src), _s);

        [Test] public void IntLiteral() => Assert.AreEqual(VnValue.Int(5), Eval("5"));
        [Test] public void UndefinedVarIsZero() => Assert.AreEqual(VnValue.Int(0), Eval("gold"));

        [Test] public void Addition() => Assert.AreEqual(VnValue.Int(7), Eval("3 + 4"));
        [Test] public void Subtraction() => Assert.AreEqual(VnValue.Int(-1), Eval("3 - 4"));
        [Test] public void Multiplication() => Assert.AreEqual(VnValue.Int(12), Eval("3 * 4"));
        [Test] public void IntegerDivisionTruncates() => Assert.AreEqual(VnValue.Int(3), Eval("7 / 2"));
        [Test] public void Modulo() => Assert.AreEqual(VnValue.Int(1), Eval("7 % 2"));
        [Test] public void Precedence() => Assert.AreEqual(VnValue.Int(11), Eval("3 + 4 * 2"));
        [Test] public void Parens() => Assert.AreEqual(VnValue.Int(14), Eval("(3 + 4) * 2"));
        [Test] public void NegativeUnary() => Assert.AreEqual(VnValue.Int(-3), Eval("-3"));

        [Test] public void VariableArithmetic()
        {
            _s.Set("gold", VnValue.Int(10));
            _s.Set("yield", VnValue.Int(5));
            Assert.AreEqual(VnValue.Int(20), Eval("gold + yield * 2"));
        }

        [Test] public void GreaterEqualTrue() => Assert.AreEqual(VnValue.Bool(true), Eval("5 >= 5"));
        [Test] public void LessThanFalse() => Assert.AreEqual(VnValue.Bool(false), Eval("5 < 3"));
        [Test] public void EqualityInt() => Assert.AreEqual(VnValue.Bool(true), Eval("2 == 2"));
        [Test] public void NotEqualInt() => Assert.AreEqual(VnValue.Bool(true), Eval("2 != 3"));

        // Equality is kind-strict: an Int never equals a Bool, even when their payloads
        // match (Int(1) vs Bool(true) both carry 1). Pin this so `if flag == true`
        // against an int-valued flag can't silently start matching after a refactor.
        [Test] public void CrossKindEqualityIsFalse() => Assert.AreEqual(VnValue.Bool(false), Eval("1 == true"));
        [Test] public void CrossKindInequalityIsTrue() => Assert.AreEqual(VnValue.Bool(true), Eval("1 != true"));

        [Test] public void VarVsVarComparison()
        {
            _s.Set("a", VnValue.Int(7));
            _s.Set("b", VnValue.Int(3));
            Assert.AreEqual(VnValue.Bool(true), Eval("a > b"));
        }

        [Test] public void AndOrNot()
        {
            _s.Set("met", VnValue.Bool(true));
            _s.Set("gold", VnValue.Int(100));
            Assert.AreEqual(VnValue.Bool(true), Eval("gold >= 50 and met"));
            Assert.AreEqual(VnValue.Bool(false), Eval("gold >= 50 and not met"));
            Assert.AreEqual(VnValue.Bool(true), Eval("gold < 0 or met"));
        }

        [Test] public void BoolTruthyFlag()
        {
            _s.Set("flag", VnValue.Int(1)); // int used as flag
            Assert.AreEqual(VnValue.Bool(true), Eval("flag and true"));
        }

        [Test] public void RandomWithinBounds()
        {
            for (int i = 0; i < 200; i++)
            {
                var v = Eval("random(1, 6)");
                Assert.AreEqual(VnKind.Int, v.Kind);
                Assert.IsTrue(v.AsInt >= 1 && v.AsInt <= 6);
            }
        }

        [Test] public void DivideByZeroThrows()
            => Assert.Throws<VnRuntimeException>(() => Eval("1 / 0"));

        [Test] public void ModuloByZeroThrows()
            => Assert.Throws<VnRuntimeException>(() => Eval("1 % 0"));

        [Test] public void ArithmeticOnBoolThrows()
            => Assert.Throws<VnRuntimeException>(() => Eval("true + 1"));

        [Test] public void OrderingOnBoolThrows()
            => Assert.Throws<VnRuntimeException>(() => Eval("true > false"));
    }
}
