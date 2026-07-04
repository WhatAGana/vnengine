using System.Collections.Generic;

namespace VNEngine
{
    public enum Op { Say, Bg, Show, Hide, Set, Jump, JumpIfFalse, Menu, Call, Return, Halt }

    public sealed class MenuOption
    {
        public string Label;
        public Expr Condition;       // null = always shown
        public string TargetLabel;
        public int Target = -1;
    }

    public sealed class Instruction
    {
        public Op Op;
        public string StrA;
        public string StrB;
        public Expr ExprA;
        public string TargetLabel;   // for Jump/JumpIfFalse/Call
        public int Target = -1;
        public List<MenuOption> Menu;
        public int Line;
        public string File;
    }

    public sealed class CharacterDef
    {
        public string Id;
        public string DisplayName;
        public string Color;
    }

    public sealed class VnProgram
    {
        public Instruction[] Code;
        public Dictionary<string, int> Labels;
        public Dictionary<string, CharacterDef> Characters;
    }
}
