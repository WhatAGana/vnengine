using System.Text;

namespace VNEngine
{
    public static class TextInterpolator
    {
        public static string Interpolate(string text, GameState state)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('[') < 0) return text ?? "";

            var sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '[')
                {
                    if (i + 1 < text.Length && text[i + 1] == '[') { sb.Append('['); i += 2; continue; }
                    int close = text.IndexOf(']', i + 1);
                    if (close < 0) { sb.Append(text.Substring(i)); break; }
                    string name = text.Substring(i + 1, close - i - 1).Trim();
                    sb.Append(state.Get(name).ToString());
                    i = close + 1;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }
    }
}
