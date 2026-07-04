using System.Collections.Generic;

namespace VNEngine
{
    public static class Compiler
    {
        public static VnProgram Compile(List<Command> commands)
            => Compile(new List<List<Command>> { commands });

        public static VnProgram Compile(IReadOnlyList<List<Command>> files)
        {
            var code = new List<Instruction>();
            var labels = new Dictionary<string, int>();
            var characters = new Dictionary<string, CharacterDef>();
            int synth = 0;

            string NewLabel() => "@L" + (synth++);

            void DefineHere(string name) { labels[name] = code.Count; }

            void Emit(Instruction ins) => code.Add(ins);

            void EmitBlock(List<Command> cmds)
            {
                foreach (var c in cmds) EmitCommand(c);
            }

            void EmitCommand(Command c)
            {
                switch (c)
                {
                    case CharacterDefCommand cd:
                        if (characters.ContainsKey(cd.Id))
                            throw new VnParseException($"{c.File}:{c.Line}: duplicate character '{cd.Id}'");
                        characters[cd.Id] = new CharacterDef { Id = cd.Id, DisplayName = cd.DisplayName, Color = cd.Color };
                        break;

                    case LabelCommand lc:
                        if (labels.ContainsKey(lc.Name))
                            throw new VnParseException($"{c.File}:{c.Line}: duplicate label '{lc.Name}'");
                        labels[lc.Name] = code.Count;
                        break;

                    case SayCommand say:
                        Emit(new Instruction { Op = Op.Say, StrA = say.SpeakerRef, StrB = say.Text, Line = c.Line, File = c.File });
                        break;

                    case BgCommand bg:
                        Emit(new Instruction { Op = Op.Bg, StrA = bg.Name, Line = c.Line, File = c.File });
                        break;

                    case ShowCommand sh:
                        Emit(new Instruction { Op = Op.Show, StrA = sh.Character, StrB = sh.Position, Line = c.Line, File = c.File });
                        break;

                    case HideCommand hd:
                        Emit(new Instruction { Op = Op.Hide, StrA = hd.Character, Line = c.Line, File = c.File });
                        break;

                    case SetCommand set:
                        Emit(new Instruction { Op = Op.Set, StrA = set.Var, ExprA = set.Value, Line = c.Line, File = c.File });
                        break;

                    case JumpCommand jp:
                        Emit(new Instruction { Op = Op.Jump, TargetLabel = jp.Label, Line = c.Line, File = c.File });
                        break;

                    case CallCommand cl:
                        Emit(new Instruction { Op = Op.Call, TargetLabel = cl.Label, Line = c.Line, File = c.File });
                        break;

                    case ReturnCommand rc:
                        Emit(new Instruction { Op = Op.Return, Line = c.Line, File = c.File });
                        break;

                    case IfCommand ifc:
                        EmitIf(ifc);
                        break;

                    case WhileCommand wc:
                        EmitWhile(wc);
                        break;

                    case MenuCommand mc:
                        EmitMenu(mc);
                        break;

                    default:
                        throw new VnParseException($"{c.File}:{c.Line}: cannot compile {c.GetType().Name}");
                }
            }

            void EmitIf(IfCommand ifc)
            {
                string endLabel = NewLabel();
                foreach (var branch in ifc.Branches)
                {
                    if (branch.Condition != null)
                    {
                        string nextLabel = NewLabel();
                        Emit(new Instruction { Op = Op.JumpIfFalse, ExprA = branch.Condition, TargetLabel = nextLabel, Line = ifc.Line, File = ifc.File });
                        EmitBlock(branch.Body);
                        Emit(new Instruction { Op = Op.Jump, TargetLabel = endLabel, Line = ifc.Line, File = ifc.File });
                        DefineHere(nextLabel);
                    }
                    else
                    {
                        EmitBlock(branch.Body); // else
                    }
                }
                DefineHere(endLabel);
            }

            void EmitWhile(WhileCommand wc)
            {
                string startLabel = NewLabel();
                string endLabel = NewLabel();
                DefineHere(startLabel);
                Emit(new Instruction { Op = Op.JumpIfFalse, ExprA = wc.Condition, TargetLabel = endLabel, Line = wc.Line, File = wc.File });
                EmitBlock(wc.Body);
                Emit(new Instruction { Op = Op.Jump, TargetLabel = startLabel, Line = wc.Line, File = wc.File });
                DefineHere(endLabel);
            }

            void EmitMenu(MenuCommand mc)
            {
                string afterLabel = NewLabel();
                var options = new List<MenuOption>();
                var bodyLabels = new List<string>();
                foreach (var choice in mc.Choices)
                {
                    string bodyLabel = NewLabel();
                    bodyLabels.Add(bodyLabel);
                    options.Add(new MenuOption { Label = choice.Label, Condition = choice.Condition, TargetLabel = bodyLabel });
                }
                Emit(new Instruction { Op = Op.Menu, Menu = options, Line = mc.Line, File = mc.File });
                for (int i = 0; i < mc.Choices.Count; i++)
                {
                    DefineHere(bodyLabels[i]);
                    EmitBlock(mc.Choices[i].Body);
                    Emit(new Instruction { Op = Op.Jump, TargetLabel = afterLabel, Line = mc.Line, File = mc.File });
                }
                DefineHere(afterLabel);
            }

            // Emit every file sequentially into the shared code list.
            foreach (var file in files)
                EmitBlock(file);

            Emit(new Instruction { Op = Op.Halt });

            // Resolve all symbolic targets.
            foreach (var ins in code)
            {
                if (ins.TargetLabel != null)
                    ins.Target = ResolveLabel(labels, ins.TargetLabel, ins);
                if (ins.Menu != null)
                    foreach (var opt in ins.Menu)
                        opt.Target = ResolveLabel(labels, opt.TargetLabel, ins);
            }

            return new VnProgram { Code = code.ToArray(), Labels = labels, Characters = characters };
        }

        private static int ResolveLabel(Dictionary<string, int> labels, string name, Instruction origin)
        {
            if (!labels.TryGetValue(name, out int idx))
                throw new VnParseException($"{Where(origin)}: unknown label '{name}'");
            return idx;
        }

        private static string Where(Instruction ins)
            => ins?.File != null ? $"{ins.File}:{ins.Line}" : "<compiled>";
    }
}
