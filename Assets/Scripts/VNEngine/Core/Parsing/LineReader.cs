using System.Collections.Generic;

namespace VNEngine
{
    public static class LineReader
    {
        public static List<LogicalLine> Read(string source, string file)
        {
            var result = new List<LogicalLine>();
            if (source == null) return result;

            string[] raw = source.Split('\n');
            for (int i = 0; i < raw.Length; i++)
            {
                string line = raw[i];
                if (line.EndsWith("\r")) line = line.Substring(0, line.Length - 1);

                // measure indentation, rejecting tabs
                int indent = 0;
                while (indent < line.Length && line[indent] == ' ') indent++;
                for (int k = 0; k < indent + 1 && k < line.Length; k++)
                {
                    if (line[k] == '\t')
                        throw new VnParseException($"{file}:{i + 1}: tab used in indentation (use spaces)");
                }

                string body = StripInlineComment(line.Substring(indent));
                body = body.TrimEnd();

                if (body.Length == 0) continue;              // blank
                if (body[0] == '#') continue;                // full-line comment

                result.Add(new LogicalLine(indent, body, i + 1, file));
            }
            return result;
        }

        // Removes a trailing " # comment" that is not inside double quotes.
        private static string StripInlineComment(string s)
        {
            bool inQuote = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"') inQuote = !inQuote;
                else if (c == '#' && !inQuote && i > 0 && s[i - 1] == ' ')
                    return s.Substring(0, i);
            }
            return s;
        }
    }
}
