using System.Collections.Generic;

namespace VNEngine
{
    [System.Serializable]
    public sealed class VarEntry
    {
        public string name;
        public int kind;   // 0 = Int, 1 = Bool
        public int value;  // Bool stored as 0/1
    }

    [System.Serializable]
    public sealed class StageChar
    {
        public string position;
        public string character;
    }

    // Plain, JsonUtility-friendly snapshot of a running interpreter.
    // Lists (not dictionaries) and primitives only.
    [System.Serializable]
    public sealed class SaveData
    {
        public const int SaveFormatVersion = 1;

        public int version;
        public string programHash;

        public List<VarEntry> vars = new List<VarEntry>();
        public int rngState;            // bit pattern of the PRNG's uint state

        public int pc;
        public List<int> callStack = new List<int>(); // top-first order

        public int pending;             // 0=None, 1=Line, 2=Choice
        // pending == Line:
        public string lineSpeaker;
        public string lineColor;
        public string lineText;
        // pending == Choice:
        public List<string> choiceLabels = new List<string>();
        public List<int> choiceTargets = new List<int>();

        // stage:
        public string background;
        public List<StageChar> stage = new List<StageChar>();
    }
}
