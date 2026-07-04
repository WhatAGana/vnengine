using System.Collections.Generic;

namespace VNEngine
{
    public abstract class Command { public int Line; public string File; }

    public sealed class CharacterDefCommand : Command { public string Id; public string DisplayName; public string Color; }
    public sealed class LabelCommand : Command { public string Name; }
    public sealed class SayCommand : Command { public string SpeakerRef; public string Text; }
    public sealed class BgCommand : Command { public string Name; }
    public sealed class ShowCommand : Command { public string Character; public string Position; }
    public sealed class HideCommand : Command { public string Character; }
    public sealed class SetCommand : Command { public string Var; public Expr Value; }
    public sealed class JumpCommand : Command { public string Label; }
    public sealed class CallCommand : Command { public string Label; }
    public sealed class ReturnCommand : Command { }

    public sealed class IfBranch { public Expr Condition; public List<Command> Body; }
    public sealed class IfCommand : Command { public List<IfBranch> Branches; }

    public sealed class WhileCommand : Command { public Expr Condition; public List<Command> Body; }

    public sealed class MenuChoiceNode { public string Label; public Expr Condition; public List<Command> Body; }
    public sealed class MenuCommand : Command { public List<MenuChoiceNode> Choices; }
}
