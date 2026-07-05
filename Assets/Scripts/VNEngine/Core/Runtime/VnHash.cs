namespace VNEngine
{
    // Deterministic FNV-1a (32-bit) over a string's chars. Used to fingerprint
    // the compiled scenario so a save can detect a changed script.
    public static class VnHash
    {
        public static string Fnv1a(string s)
        {
            uint hash = 2166136261u;
            if (s != null)
                foreach (char c in s)
                    hash = unchecked((hash ^ c) * 16777619u);
            return hash.ToString("x8");
        }
    }
}
