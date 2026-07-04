using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class InterpreterTests
    {
        private FakeDialogueView _dlg;
        private FakeStageView _stage;
        private GameState _state;

        private void Run(string src, params int[] answers)
        {
            var program = Compiler.Compile(Parser.Parse(LineReader.Read("label start:\n" + src, "t.vns")));
            _state = new GameState(new SeededRandom(1));
            _dlg = new FakeDialogueView(answers);
            _stage = new FakeStageView();
            var interp = new Interpreter(program, _state, _dlg, _stage);
            interp.Start("start");
            int guard = 0;
            while (!interp.IsFinished)
            {
                interp.Tick();
                if (++guard > 100000) Assert.Fail("interpreter did not finish");
            }
        }

        private List<string> Texts()
        {
            var list = new List<string>();
            foreach (var s in _dlg.Lines) list.Add(s.Text);
            return list;
        }

        [Test] public void LinearOrder()
        {
            Run("요르 \"a\"\n요르 \"b\"");
            Assert.AreEqual(new[] { "a", "b" }, Texts().ToArray());
            Assert.AreEqual("요르", _dlg.Lines[0].Speaker);
        }

        [Test] public void NarrationHasNullSpeaker()
        {
            Run("\"…\"");
            Assert.IsNull(_dlg.Lines[0].Speaker);
        }

        [Test] public void SetAndBranchTrue()
        {
            Run("$ x = 100\nif x >= 50:\n    요르 \"high\"\nelse:\n    요르 \"low\"");
            Assert.AreEqual(new[] { "high" }, Texts().ToArray());
        }

        [Test] public void BranchFalseTakesElse()
        {
            Run("$ x = 10\nif x >= 50:\n    요르 \"high\"\nelse:\n    요르 \"low\"");
            Assert.AreEqual(new[] { "low" }, Texts().ToArray());
        }

        [Test] public void ElifChain()
        {
            Run("$ x = 20\nif x >= 50:\n    요르 \"a\"\nelif x >= 10:\n    요르 \"b\"\nelse:\n    요르 \"c\"");
            Assert.AreEqual(new[] { "b" }, Texts().ToArray());
        }

        [Test] public void WhileAccumulates()
        {
            Run("$ n = 3\n$ gold = 0\nwhile n > 0:\n    $ gold = gold + 10\n    $ n -= 1");
            Assert.AreEqual(VnValue.Int(30), _state.Get("gold"));
            Assert.AreEqual(VnValue.Int(0), _state.Get("n"));
        }

        [Test] public void JumpSkips()
        {
            Run("jump skip\n요르 \"skipped\"\nlabel skip:\n요르 \"here\"");
            Assert.AreEqual(new[] { "here" }, Texts().ToArray());
        }

        [Test] public void CallReturns()
        {
            Run("call sub\n요르 \"after\"\nreturn\nlabel sub:\n요르 \"in-sub\"\nreturn");
            Assert.AreEqual(new[] { "in-sub", "after" }, Texts().ToArray());
        }

        [Test] public void StageCommandsLogged()
        {
            Run("bg 공원\nshow 요르 left\nhide 요르");
            Assert.AreEqual(new[] { "bg:공원", "show:요르:left", "hide:요르" }, _stage.Log.ToArray());
        }

        [Test] public void MenuSelectsSecond()
        {
            var src =
                "menu:\n" +
                "    \"a\":\n" +
                "        요르 \"picked-a\"\n" +
                "        jump end\n" +
                "    \"b\":\n" +
                "        요르 \"picked-b\"\n" +
                "        jump end\n" +
                "label end:\n" +
                "요르 \"done\"";
            Run(src, 1); // choose index 1 => "b"
            Assert.AreEqual(new[] { "picked-b", "done" }, Texts().ToArray());
        }

        [Test] public void MenuEffectAppliesThenJumps()
        {
            var src =
                "menu:\n" +
                "    \"love\":\n" +
                "        $ affinity += 30\n" +
                "        jump end\n" +
                "label end:\n" +
                "요르 \"ok\"";
            Run(src, 0);
            Assert.AreEqual(VnValue.Int(30), _state.Get("affinity"));
        }

        [Test] public void ConditionalChoiceHiddenWhenFalse()
        {
            var src =
                "$ gold = 0\n" +
                "menu:\n" +
                "    \"always\":\n" +
                "        요르 \"a\"\n" +
                "        jump end\n" +
                "    \"bribe\" if gold >= 10:\n" +
                "        요르 \"b\"\n" +
                "        jump end\n" +
                "label end:\n" +
                "요르 \"done\"";
            Run(src, 0);
            // only one choice was eligible
            Assert.AreEqual(1, _dlg.ChoiceSets[0].Count);
            Assert.AreEqual("always", _dlg.ChoiceSets[0][0]);
            Assert.AreEqual(new[] { "a", "done" }, Texts().ToArray());
        }

        [Test] public void ConditionalChoiceShownWhenTrue()
        {
            var src =
                "$ gold = 50\n" +
                "menu:\n" +
                "    \"always\":\n" +
                "        jump end\n" +
                "    \"bribe\" if gold >= 10:\n" +
                "        요르 \"b\"\n" +
                "        jump end\n" +
                "label end:\n" +
                "요르 \"done\"";
            Run(src, 1); // pick the (now visible) bribe option
            Assert.AreEqual(2, _dlg.ChoiceSets[0].Count);
            Assert.AreEqual(new[] { "b", "done" }, Texts().ToArray());
        }

        [Test] public void InfiniteLoopGuardThrows()
        {
            var program = Compiler.Compile(Parser.Parse(LineReader.Read(
                "label start:\nwhile true:\n    $ x = 1", "t.vns")));
            var interp = new Interpreter(program, new GameState(new SeededRandom(1)),
                new FakeDialogueView(), new FakeStageView()) { MaxStepsPerTick = 5000 };
            interp.Start("start");
            Assert.Throws<VnRuntimeException>(() => interp.Tick());
        }

        [Test] public void UnknownEntryLabelThrows()
        {
            var program = Compiler.Compile(Parser.Parse(LineReader.Read("label start:\nreturn", "t.vns")));
            var interp = new Interpreter(program, new GameState(new SeededRandom(1)),
                new FakeDialogueView(), new FakeStageView());
            Assert.Throws<VnRuntimeException>(() => interp.Start("nope"));
        }
    }
}
