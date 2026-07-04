using NUnit.Framework;

namespace VNEngine.Tests
{
    public class ExprParserTests
    {
        [Test] public void IntLiteral()
        {
            var e = (LitExpr)ExprParser.Parse("42");
            Assert.AreEqual(VnValue.Int(42), e.Value);
        }

        [Test] public void TrueFalseLiterals()
        {
            Assert.AreEqual(VnValue.Bool(true), ((LitExpr)ExprParser.Parse("true")).Value);
            Assert.AreEqual(VnValue.Bool(false), ((LitExpr)ExprParser.Parse("false")).Value);
        }

        [Test] public void Variable()
        {
            var e = (VarExpr)ExprParser.Parse("gold");
            Assert.AreEqual("gold", e.Name);
        }

        [Test] public void UnicodeVariable()
        {
            var e = (VarExpr)ExprParser.Parse("요르");
            Assert.AreEqual("요르", e.Name);
        }

        [Test] public void AdditionParsesAsBinary()
        {
            var e = (BinaryExpr)ExprParser.Parse("a + 2");
            Assert.AreEqual("+", e.Op);
            Assert.AreEqual("a", ((VarExpr)e.Left).Name);
            Assert.AreEqual(VnValue.Int(2), ((LitExpr)e.Right).Value);
        }

        [Test] public void PrecedenceMulOverAdd()
        {
            // a + b * 2  ->  (+ a (* b 2))
            var e = (BinaryExpr)ExprParser.Parse("a + b * 2");
            Assert.AreEqual("+", e.Op);
            var right = (BinaryExpr)e.Right;
            Assert.AreEqual("*", right.Op);
        }

        [Test] public void ParenOverridesPrecedence()
        {
            // (a + b) * 2 -> (* (+ a b) 2)
            var e = (BinaryExpr)ExprParser.Parse("(a + b) * 2");
            Assert.AreEqual("*", e.Op);
            Assert.AreEqual("+", ((BinaryExpr)e.Left).Op);
        }

        [Test] public void ComparisonBelowArithmetic()
        {
            // a + 1 >= 5 -> (>= (+ a 1) 5)
            var e = (BinaryExpr)ExprParser.Parse("a + 1 >= 5");
            Assert.AreEqual(">=", e.Op);
            Assert.AreEqual("+", ((BinaryExpr)e.Left).Op);
        }

        [Test] public void AndOrPrecedence()
        {
            // a or b and c -> (or a (and b c))
            var e = (BinaryExpr)ExprParser.Parse("a or b and c");
            Assert.AreEqual("or", e.Op);
            Assert.AreEqual("and", ((BinaryExpr)e.Right).Op);
        }

        [Test] public void NotUnary()
        {
            var e = (UnaryExpr)ExprParser.Parse("not met");
            Assert.AreEqual("not", e.Op);
            Assert.AreEqual("met", ((VarExpr)e.Operand).Name);
        }

        [Test] public void NegativeNumber()
        {
            var e = (UnaryExpr)ExprParser.Parse("-3");
            Assert.AreEqual("-", e.Op);
            Assert.AreEqual(VnValue.Int(3), ((LitExpr)e.Operand).Value);
        }

        [Test] public void RandomCall()
        {
            var e = (RandomExpr)ExprParser.Parse("random(1, 6)");
            Assert.AreEqual(VnValue.Int(1), ((LitExpr)e.Lo).Value);
            Assert.AreEqual(VnValue.Int(6), ((LitExpr)e.Hi).Value);
        }

        [Test] public void NotEqualOperator()
        {
            var e = (BinaryExpr)ExprParser.Parse("a != 2");
            Assert.AreEqual("!=", e.Op);
        }

        [Test] public void UnbalancedParenThrows()
        {
            Assert.Throws<VnParseException>(() => ExprParser.Parse("(a + 2"));
        }

        [Test] public void TrailingGarbageThrows()
        {
            Assert.Throws<VnParseException>(() => ExprParser.Parse("a 2"));
        }

        [Test] public void EmptyThrows()
        {
            Assert.Throws<VnParseException>(() => ExprParser.Parse("   "));
        }
    }
}
