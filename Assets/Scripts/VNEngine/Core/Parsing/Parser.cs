using System.Collections.Generic;

namespace VNEngine
{
    public static class Parser
    {
        public static List<Command> Parse(List<LogicalLine> lines)
        {
            int pos = 0;
            return ParseBlock(lines, ref pos, 0);
        }

        // Parses all lines whose indent >= minIndent, stopping when indent drops below minIndent.
        private static List<Command> ParseBlock(List<LogicalLine> lines, ref int pos, int minIndent)
        {
            var result = new List<Command>();
            while (pos < lines.Count && lines[pos].Indent >= minIndent)
            {
                var line = lines[pos];
                // 'elif'/'else' are consumed by ParseIf; encountering them here is an error.
                string first = FirstWord(line.Text);
                if (first == "elif" || first == "else")
                    throw Err(line, $"'{first}' without matching 'if'");

                result.Add(ParseStatement(lines, ref pos));
            }
            return result;
        }

        private static Command ParseStatement(List<LogicalLine> lines, ref int pos)
        {
            var line = lines[pos];
            string text = line.Text;
            string first = FirstWord(text);

            switch (first)
            {
                case "character": pos++; return ParseCharacter(line);
                case "label": pos++; return Tag(new LabelCommand { Name = RequireColonName(line, "label") }, line);
                case "bg": pos++; return Tag(new BgCommand { Name = Rest(text, "bg") }, line);
                case "show": pos++; return ParseShow(line);
                case "hide": pos++; return Tag(new HideCommand { Character = Rest(text, "hide") }, line);
                case "jump": pos++; return Tag(new JumpCommand { Label = Rest(text, "jump") }, line);
                case "call": pos++; return Tag(new CallCommand { Label = Rest(text, "call") }, line);
                case "return": pos++; return Tag(new ReturnCommand(), line);
                case "menu": return ParseMenu(lines, ref pos);
                case "if": return ParseIf(lines, ref pos);
                case "while": return ParseWhile(lines, ref pos);
            }

            if (text.StartsWith("$")) { pos++; return ParseSet(line); }
            if (text.StartsWith("\"")) { pos++; return ParseSay(line, null, text); }

            // speaker "text"
            int q = text.IndexOf('"');
            if (q < 0) throw Err(line, $"cannot parse statement: {text}");
            string speaker = text.Substring(0, q).Trim();
            if (speaker.Length == 0) throw Err(line, "missing speaker before quote");
            pos++;
            return ParseSay(line, speaker, text);
        }

        private static Command ParseCharacter(LogicalLine line)
        {
            // character <id> name:"..." [color:"#.."]
            string rest = Rest(line.Text, "character");
            int sp = rest.IndexOf(' ');
            if (sp < 0) throw Err(line, "character requires an id and name:\"...\"");
            string id = rest.Substring(0, sp).Trim();
            string tail = rest.Substring(sp + 1);
            string name = ExtractKeyed(tail, "name:");
            if (name == null) throw Err(line, "character requires name:\"...\"");
            string color = ExtractKeyed(tail, "color:");
            return Tag(new CharacterDefCommand { Id = id, DisplayName = name, Color = color }, line);
        }

        // Finds key + quoted value, e.g. name:"요르"  → 요르 ; returns null if key absent.
        private static string ExtractKeyed(string s, string key)
        {
            int k = s.IndexOf(key, System.StringComparison.Ordinal);
            if (k < 0) return null;
            int q1 = s.IndexOf('"', k + key.Length);
            if (q1 < 0) return null;
            int q2 = s.IndexOf('"', q1 + 1);
            if (q2 < 0) return null;
            return s.Substring(q1 + 1, q2 - q1 - 1);
        }

        private static Command ParseShow(LogicalLine line)
        {
            string rest = Rest(line.Text, "show");
            string[] parts = rest.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) throw Err(line, "show requires a character name");
            string ch = parts[0];
            string pos = parts.Length >= 2 ? parts[1] : "center";
            if (pos != "left" && pos != "center" && pos != "right")
                throw Err(line, $"unknown position '{pos}' (use left/center/right)");
            return Tag(new ShowCommand { Character = ch, Position = pos }, line);
        }

        private static Command ParseSay(LogicalLine line, string speaker, string text)
        {
            int q1 = text.IndexOf('"');
            int q2 = text.LastIndexOf('"');
            if (q1 < 0 || q2 <= q1) throw Err(line, $"unterminated string: {text}");
            string body = text.Substring(q1 + 1, q2 - q1 - 1);
            return Tag(new SayCommand { SpeakerRef = speaker, Text = body }, line);
        }

        private static Command ParseSet(LogicalLine line)
        {
            // $ <var> <op> <expr>
            string rest = line.Text.Substring(1).Trim();
            string op = null; int opIdx = -1;
            foreach (var cand in new[] { "+=", "-=", "*=", "/=", "=" })
            {
                int idx = rest.IndexOf(cand, System.StringComparison.Ordinal);
                if (idx >= 0) { op = cand; opIdx = idx; break; }
            }
            if (op == null) throw Err(line, $"assignment needs an operator: {line.Text}");
            string var = rest.Substring(0, opIdx).Trim();
            if (var.Length == 0) throw Err(line, "assignment missing variable name");
            string rhs = rest.Substring(opIdx + op.Length).Trim();
            Expr rhsExpr = ExprParser.Parse(rhs);
            Expr value = op == "="
                ? rhsExpr
                : new BinaryExpr { Op = op.Substring(0, 1), Left = new VarExpr { Name = var }, Right = rhsExpr };
            return Tag(new SetCommand { Var = var, Value = value }, line);
        }

        private static Command ParseMenu(List<LogicalLine> lines, ref int pos)
        {
            var header = lines[pos];
            if (header.Text.TrimEnd() != "menu:") throw Err(header, "menu header must be 'menu:'");
            int baseIndent = header.Indent;
            pos++;
            var choices = new List<MenuChoiceNode>();
            while (pos < lines.Count && lines[pos].Indent > baseIndent)
            {
                var cl = lines[pos];
                // choice header: "label" [if <cond>] :
                if (!cl.Text.StartsWith("\"")) throw Err(cl, "menu choice must start with a quoted label");
                if (!cl.Text.EndsWith(":")) throw Err(cl, "menu choice header must end with ':'");
                int q2 = cl.Text.IndexOf('"', 1);
                if (q2 < 0) throw Err(cl, "unterminated choice label");
                string label = cl.Text.Substring(1, q2 - 1);
                string between = cl.Text.Substring(q2 + 1, cl.Text.Length - (q2 + 1) - 1).Trim(); // drop trailing ':'
                Expr cond = null;
                if (between.StartsWith("if "))
                    cond = ExprParser.Parse(between.Substring(3).Trim());
                else if (between.Length != 0)
                    throw Err(cl, $"unexpected text in choice header: {between}");
                int choiceIndent = cl.Indent;
                pos++;
                var body = ParseBlock(lines, ref pos, choiceIndent + 1);
                choices.Add(new MenuChoiceNode { Label = label, Condition = cond, Body = body });
            }
            if (choices.Count == 0) throw Err(header, "menu has no choices");
            return Tag(new MenuCommand { Choices = choices }, header);
        }

        private static Command ParseIf(List<LogicalLine> lines, ref int pos)
        {
            var header = lines[pos];
            int baseIndent = header.Indent;
            var branches = new List<IfBranch>();

            // first 'if'
            branches.Add(ParseCondBranch(lines, ref pos, "if", baseIndent));
            // subsequent elif/else at same indent
            while (pos < lines.Count && lines[pos].Indent == baseIndent)
            {
                string fw = FirstWord(lines[pos].Text);
                if (fw == "elif") branches.Add(ParseCondBranch(lines, ref pos, "elif", baseIndent));
                else if (fw == "else") { branches.Add(ParseElseBranch(lines, ref pos, baseIndent)); break; }
                else break;
            }
            return Tag(new IfCommand { Branches = branches }, header);
        }

        private static IfBranch ParseCondBranch(List<LogicalLine> lines, ref int pos, string kw, int baseIndent)
        {
            var line = lines[pos];
            string cond = RequireColonName(line, kw);
            pos++;
            var body = ParseBlock(lines, ref pos, baseIndent + 1);
            return new IfBranch { Condition = ExprParser.Parse(cond), Body = body };
        }

        private static IfBranch ParseElseBranch(List<LogicalLine> lines, ref int pos, int baseIndent)
        {
            var line = lines[pos];
            if (line.Text.TrimEnd() != "else:") throw Err(line, "else header must be 'else:'");
            pos++;
            var body = ParseBlock(lines, ref pos, baseIndent + 1);
            return new IfBranch { Condition = null, Body = body };
        }

        private static Command ParseWhile(List<LogicalLine> lines, ref int pos)
        {
            var header = lines[pos];
            int baseIndent = header.Indent;
            string cond = RequireColonName(header, "while");
            pos++;
            var body = ParseBlock(lines, ref pos, baseIndent + 1);
            return Tag(new WhileCommand { Condition = ExprParser.Parse(cond), Body = body }, header);
        }

        // ---- helpers ----

        private static Command Tag(Command c, LogicalLine line) { c.Line = line.LineNumber; c.File = line.File; return c; }

        private static string FirstWord(string s)
        {
            int sp = s.IndexOf(' ');
            string w = sp < 0 ? s : s.Substring(0, sp);
            if (w.EndsWith(":")) w = w.Substring(0, w.Length - 1);
            return w;
        }

        private static string Rest(string text, string keyword)
        {
            string r = text.Substring(keyword.Length).Trim();
            return r;
        }

        // For 'label x:', 'if cond:', etc. — returns the text between keyword and trailing ':'.
        private static string RequireColonName(LogicalLine line, string keyword)
        {
            string t = line.Text.TrimEnd();
            if (!t.EndsWith(":")) throw Err(line, $"'{keyword}' header must end with ':'");
            string inner = t.Substring(keyword.Length, t.Length - keyword.Length - 1).Trim();
            if (inner.Length == 0) throw Err(line, $"'{keyword}' requires a value before ':'");
            return inner;
        }

        private static VnParseException Err(LogicalLine line, string msg)
            => new VnParseException($"{line.File}:{line.LineNumber}: {msg}");
    }
}
