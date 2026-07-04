using System.Collections.Generic;
using System.Globalization;

namespace VNEngine
{
    // Recursive-descent parser. Grammar (low → high precedence):
    //   or   := and ("or" and)*
    //   and  := cmp ("and" cmp)*
    //   cmp  := add (("=="|"!="|">="|"<="|">"|"<") add)*
    //   add  := mul (("+"|"-") mul)*
    //   mul  := unary (("*"|"/"|"%") unary)*
    //   unary:= ("-"|"not") unary | primary
    //   primary := INT | "true" | "false" | "random" "(" or "," or ")" | IDENT | "(" or ")"
    public static class ExprParser
    {
        public static Expr Parse(string src)
        {
            var tokens = Tokenize(src);
            int pos = 0;
            var expr = ParseOr(tokens, ref pos);
            if (pos != tokens.Count)
                throw new VnParseException($"Unexpected token '{tokens[pos].Text}' in expression: {src}");
            return expr;
        }

        private enum TT { Int, Ident, Op, LParen, RParen, Comma }
        private struct Tok { public TT Type; public string Text; }

        private static List<Tok> Tokenize(string s)
        {
            var toks = new List<Tok>();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (char.IsDigit(c))
                {
                    int start = i;
                    while (i < s.Length && char.IsDigit(s[i])) i++;
                    toks.Add(new Tok { Type = TT.Int, Text = s.Substring(start, i - start) });
                    continue;
                }
                if (c == '(') { toks.Add(new Tok { Type = TT.LParen, Text = "(" }); i++; continue; }
                if (c == ')') { toks.Add(new Tok { Type = TT.RParen, Text = ")" }); i++; continue; }
                if (c == ',') { toks.Add(new Tok { Type = TT.Comma, Text = "," }); i++; continue; }
                // two-char operators
                if (i + 1 < s.Length)
                {
                    string two = s.Substring(i, 2);
                    if (two == ">=" || two == "<=" || two == "==" || two == "!=")
                    { toks.Add(new Tok { Type = TT.Op, Text = two }); i += 2; continue; }
                }
                if (c == '>' || c == '<' || c == '+' || c == '-' || c == '*' || c == '/' || c == '%')
                { toks.Add(new Tok { Type = TT.Op, Text = c.ToString() }); i++; continue; }
                // identifier: letters/digits/underscore/unicode, not starting with digit (handled above)
                if (IsIdentChar(c))
                {
                    int start = i;
                    while (i < s.Length && IsIdentChar(s[i])) i++;
                    string word = s.Substring(start, i - start);
                    if (word == "and" || word == "or" || word == "not")
                        toks.Add(new Tok { Type = TT.Op, Text = word });
                    else
                        toks.Add(new Tok { Type = TT.Ident, Text = word });
                    continue;
                }
                throw new VnParseException($"Unexpected character '{c}' in expression: {s}");
            }
            return toks;
        }

        private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        private static Tok? Peek(List<Tok> t, int pos) => pos < t.Count ? t[pos] : (Tok?)null;

        private static Expr ParseOr(List<Tok> t, ref int pos)
        {
            var left = ParseAnd(t, ref pos);
            while (Peek(t, pos) is Tok tk && tk.Type == TT.Op && tk.Text == "or")
            { pos++; var right = ParseAnd(t, ref pos); left = new BinaryExpr { Op = "or", Left = left, Right = right }; }
            return left;
        }

        private static Expr ParseAnd(List<Tok> t, ref int pos)
        {
            var left = ParseCmp(t, ref pos);
            while (Peek(t, pos) is Tok tk && tk.Type == TT.Op && tk.Text == "and")
            { pos++; var right = ParseCmp(t, ref pos); left = new BinaryExpr { Op = "and", Left = left, Right = right }; }
            return left;
        }

        private static Expr ParseCmp(List<Tok> t, ref int pos)
        {
            var left = ParseAdd(t, ref pos);
            while (Peek(t, pos) is Tok tk && tk.Type == TT.Op && IsCmp(tk.Text))
            { pos++; var right = ParseAdd(t, ref pos); left = new BinaryExpr { Op = tk.Text, Left = left, Right = right }; }
            return left;
        }

        private static bool IsCmp(string o) => o == "==" || o == "!=" || o == ">=" || o == "<=" || o == ">" || o == "<";

        private static Expr ParseAdd(List<Tok> t, ref int pos)
        {
            var left = ParseMul(t, ref pos);
            while (Peek(t, pos) is Tok tk && tk.Type == TT.Op && (tk.Text == "+" || tk.Text == "-"))
            { pos++; var right = ParseMul(t, ref pos); left = new BinaryExpr { Op = tk.Text, Left = left, Right = right }; }
            return left;
        }

        private static Expr ParseMul(List<Tok> t, ref int pos)
        {
            var left = ParseUnary(t, ref pos);
            while (Peek(t, pos) is Tok tk && tk.Type == TT.Op && (tk.Text == "*" || tk.Text == "/" || tk.Text == "%"))
            { pos++; var right = ParseUnary(t, ref pos); left = new BinaryExpr { Op = tk.Text, Left = left, Right = right }; }
            return left;
        }

        private static Expr ParseUnary(List<Tok> t, ref int pos)
        {
            if (Peek(t, pos) is Tok tk && tk.Type == TT.Op && (tk.Text == "-" || tk.Text == "not"))
            { pos++; var operand = ParseUnary(t, ref pos); return new UnaryExpr { Op = tk.Text, Operand = operand }; }
            return ParsePrimary(t, ref pos);
        }

        private static Expr ParsePrimary(List<Tok> t, ref int pos)
        {
            if (!(Peek(t, pos) is Tok tk))
                throw new VnParseException("Unexpected end of expression");

            if (tk.Type == TT.Int)
            {
                pos++;
                return new LitExpr { Value = VnValue.Int(int.Parse(tk.Text, CultureInfo.InvariantCulture)) };
            }
            if (tk.Type == TT.Ident)
            {
                if (tk.Text == "true") { pos++; return new LitExpr { Value = VnValue.Bool(true) }; }
                if (tk.Text == "false") { pos++; return new LitExpr { Value = VnValue.Bool(false) }; }
                if (tk.Text == "random")
                {
                    pos++;
                    Expect(t, ref pos, TT.LParen, "(");
                    var lo = ParseOr(t, ref pos);
                    ExpectComma(t, ref pos);
                    var hi = ParseOr(t, ref pos);
                    Expect(t, ref pos, TT.RParen, ")");
                    return new RandomExpr { Lo = lo, Hi = hi };
                }
                pos++;
                return new VarExpr { Name = tk.Text };
            }
            if (tk.Type == TT.LParen)
            {
                pos++;
                var inner = ParseOr(t, ref pos);
                Expect(t, ref pos, TT.RParen, ")");
                return inner;
            }
            throw new VnParseException($"Unexpected token '{tk.Text}' in expression");
        }

        private static void Expect(List<Tok> t, ref int pos, TT type, string what)
        {
            if (!(Peek(t, pos) is Tok tk) || tk.Type != type)
                throw new VnParseException($"Expected '{what}' in expression");
            pos++;
        }

        private static void ExpectComma(List<Tok> t, ref int pos)
        {
            if (!(Peek(t, pos) is Tok tk) || tk.Type != TT.Comma)
                throw new VnParseException("Expected ',' in random(...)");
            pos++;
        }
    }
}
