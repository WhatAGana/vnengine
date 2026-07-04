namespace VNEngine
{
    public static class ExprEval
    {
        public static VnValue Eval(Expr e, GameState state)
        {
            switch (e)
            {
                case LitExpr lit: return lit.Value;
                case VarExpr v: return state.Get(v.Name);
                case UnaryExpr u: return EvalUnary(u, state);
                case BinaryExpr b: return EvalBinary(b, state);
                case RandomExpr r: return EvalRandom(r, state);
                default: throw new VnRuntimeException($"Unknown expression node: {e?.GetType().Name}");
            }
        }

        private static VnValue EvalUnary(UnaryExpr u, GameState s)
        {
            var v = Eval(u.Operand, s);
            if (u.Op == "-")
            {
                if (v.Kind != VnKind.Int) throw new VnRuntimeException("Unary '-' requires an integer");
                return VnValue.Int(-v.AsInt);
            }
            if (u.Op == "not") return VnValue.Bool(!v.Truthy);
            throw new VnRuntimeException($"Unknown unary operator '{u.Op}'");
        }

        private static VnValue EvalRandom(RandomExpr r, GameState s)
        {
            var lo = Eval(r.Lo, s);
            var hi = Eval(r.Hi, s);
            if (lo.Kind != VnKind.Int || hi.Kind != VnKind.Int)
                throw new VnRuntimeException("random(a, b) requires integer bounds");
            return VnValue.Int(s.Random.Range(lo.AsInt, hi.AsInt));
        }

        private static VnValue EvalBinary(BinaryExpr b, GameState s)
        {
            // Short-circuit logical operators.
            if (b.Op == "and")
            {
                var l = Eval(b.Left, s);
                if (!l.Truthy) return VnValue.Bool(false);
                return VnValue.Bool(Eval(b.Right, s).Truthy);
            }
            if (b.Op == "or")
            {
                var l = Eval(b.Left, s);
                if (l.Truthy) return VnValue.Bool(true);
                return VnValue.Bool(Eval(b.Right, s).Truthy);
            }

            var left = Eval(b.Left, s);
            var right = Eval(b.Right, s);

            switch (b.Op)
            {
                case "+": return VnValue.Int(Int(left, "+") + Int(right, "+"));
                case "-": return VnValue.Int(Int(left, "-") - Int(right, "-"));
                case "*": return VnValue.Int(Int(left, "*") * Int(right, "*"));
                case "/":
                    { int d = Int(right, "/"); if (d == 0) throw new VnRuntimeException("Division by zero"); return VnValue.Int(Int(left, "/") / d); }
                case "%":
                    { int d = Int(right, "%"); if (d == 0) throw new VnRuntimeException("Modulo by zero"); return VnValue.Int(Int(left, "%") % d); }
                case "==": return VnValue.Bool(left.Equals(right));
                case "!=": return VnValue.Bool(!left.Equals(right));
                case ">": return VnValue.Bool(Int(left, ">") > Int(right, ">"));
                case "<": return VnValue.Bool(Int(left, "<") < Int(right, "<"));
                case ">=": return VnValue.Bool(Int(left, ">=") >= Int(right, ">="));
                case "<=": return VnValue.Bool(Int(left, "<=") <= Int(right, "<="));
                default: throw new VnRuntimeException($"Unknown operator '{b.Op}'");
            }
        }

        private static int Int(VnValue v, string op)
        {
            if (v.Kind != VnKind.Int)
                throw new VnRuntimeException($"Operator '{op}' requires integer operands");
            return v.AsInt;
        }
    }
}
