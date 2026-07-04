using System.Collections.Generic;

namespace VNEngine
{
    public sealed class Interpreter
    {
        private enum Pending { None, Line, Choice }

        private readonly VnProgram _program;
        private readonly GameState _state;
        private readonly IDialogueView _dialogue;
        private readonly IStageView _stage;
        private readonly Stack<int> _callStack = new Stack<int>();

        private int _pc;
        private Pending _pending = Pending.None;
        private List<MenuOption> _activeOptions;

        public bool IsFinished { get; private set; }
        public int MaxStepsPerTick = 100000;

        public Interpreter(VnProgram program, GameState state, IDialogueView dialogue, IStageView stage)
        {
            _program = program ?? throw new System.ArgumentNullException(nameof(program));
            _state = state ?? throw new System.ArgumentNullException(nameof(state));
            _dialogue = dialogue ?? throw new System.ArgumentNullException(nameof(dialogue));
            _stage = stage ?? throw new System.ArgumentNullException(nameof(stage));
        }

        public void Start(string entryLabel)
        {
            if (!_program.Labels.TryGetValue(entryLabel, out _pc))
                throw new VnRuntimeException($"entry label '{entryLabel}' not found");
            _callStack.Clear();
            _pending = Pending.None;
            _activeOptions = null;
            IsFinished = false;
        }

        public void Tick()
        {
            if (IsFinished) return;

            // Resolve any outstanding wait first.
            if (_pending == Pending.Line)
            {
                if (_dialogue.IsLineComplete) _pending = Pending.None;
                else return;
            }
            if (_pending == Pending.Choice)
            {
                if (!_dialogue.HasChoice) return;
                int idx = _dialogue.ChosenIndex;
                _dialogue.ClearChoices();
                if (_activeOptions == null || idx < 0 || idx >= _activeOptions.Count)
                    throw new VnRuntimeException($"choice index {idx} out of range");
                _pc = _activeOptions[idx].Target;
                _pending = Pending.None;
                _activeOptions = null;
            }

            int steps = 0;
            while (true)
            {
                if (++steps > MaxStepsPerTick)
                    throw new VnRuntimeException("step limit exceeded within one Tick (infinite loop?)");

                if (_pc < 0 || _pc >= _program.Code.Length) { IsFinished = true; return; }

                var ins = _program.Code[_pc];
                switch (ins.Op)
                {
                    case Op.Say:
                        {
                            string speaker, color;
                            if (ins.StrA == null) { speaker = null; color = null; }
                            else if (_program.Characters.TryGetValue(ins.StrA, out var def))
                            { speaker = def.DisplayName; color = def.Color; }
                            else { speaker = ins.StrA; color = null; }
                            _dialogue.ShowLine(speaker, color, TextInterpolator.Interpolate(ins.StrB, _state));
                        }
                        _pc++;
                        _pending = Pending.Line;
                        return;

                    case Op.Bg: _stage.SetBackground(ins.StrA); _pc++; break;
                    case Op.Show: _stage.ShowCharacter(ins.StrA, ins.StrB); _pc++; break;
                    case Op.Hide: _stage.HideCharacter(ins.StrA); _pc++; break;

                    case Op.Set:
                        _state.Set(ins.StrA, ExprEval.Eval(ins.ExprA, _state));
                        _pc++;
                        break;

                    case Op.Jump:
                        _pc = ins.Target;
                        break;

                    case Op.JumpIfFalse:
                        if (!ExprEval.Eval(ins.ExprA, _state).Truthy) _pc = ins.Target;
                        else _pc++;
                        break;

                    case Op.Call:
                        _callStack.Push(_pc + 1);
                        _pc = ins.Target;
                        break;

                    case Op.Return:
                        if (_callStack.Count == 0) { IsFinished = true; return; }
                        _pc = _callStack.Pop();
                        break;

                    case Op.Menu:
                        BuildMenu(ins);
                        _pending = Pending.Choice;
                        return;

                    case Op.Halt:
                        IsFinished = true;
                        return;

                    default:
                        throw new VnRuntimeException($"unknown opcode {ins.Op}");
                }
            }
        }

        private void BuildMenu(Instruction ins)
        {
            _activeOptions = new List<MenuOption>();
            var labels = new List<string>();
            foreach (var opt in ins.Menu)
            {
                if (opt.Condition == null || ExprEval.Eval(opt.Condition, _state).Truthy)
                {
                    _activeOptions.Add(opt);
                    labels.Add(opt.Label);
                }
            }
            if (labels.Count == 0)
                throw new VnRuntimeException($"{ins.File}:{ins.Line}: menu has no available choices");
            _dialogue.ShowChoices(labels);
        }
    }
}
