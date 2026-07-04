namespace VNEngine
{
    public abstract class Expr { }

    public sealed class LitExpr : Expr { public VnValue Value; }
    public sealed class VarExpr : Expr { public string Name; }
    public sealed class UnaryExpr : Expr { public string Op; public Expr Operand; }
    public sealed class BinaryExpr : Expr { public string Op; public Expr Left; public Expr Right; }
    public sealed class RandomExpr : Expr { public Expr Lo; public Expr Hi; }
}
