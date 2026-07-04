using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class DisplayTests
    {
        // ---- TextInterpolator ----

        private static string Interp(string text, System.Action<GameState> setup = null)
        {
            var s = new GameState(new SeededRandom(1));
            setup?.Invoke(s);
            return TextInterpolator.Interpolate(text, s);
        }

        [Test] public void NoBracketsUnchanged() => Assert.AreEqual("hello", Interp("hello"));

        [Test] public void SubstitutesIntVar()
            => Assert.AreEqual("gold 5", Interp("gold [gold]", s => s.Set("gold", VnValue.Int(5))));

        [Test] public void UndefinedVarBecomesZero()
            => Assert.AreEqual("x=0", Interp("x=[x]"));

        [Test] public void SubstitutesBool()
            => Assert.AreEqual("met? true", Interp("met? [met]", s => s.Set("met", VnValue.Bool(true))));

        [Test] public void EscapedBracket() => Assert.AreEqual("a[b", Interp("a[[b"));

        [Test] public void UnmatchedBracketLiteral() => Assert.AreEqual("a[b", Interp("a[b"));

        [Test] public void MultipleVars()
            => Assert.AreEqual("1 and 2", Interp("[a] and [b]", s => { s.Set("a", VnValue.Int(1)); s.Set("b", VnValue.Int(2)); }));

        // ---- speaker resolution in the interpreter ----

        private FakeDialogueView Run(string src)
        {
            var program = Compiler.Compile(Parser.Parse(LineReader.Read("label start:\n" + src, "t.vns")));
            var dlg = new FakeDialogueView();
            var interp = new Interpreter(program, new GameState(new SeededRandom(1)), dlg, new FakeStageView());
            interp.Start("start");
            int guard = 0;
            while (!interp.IsFinished) { interp.Tick(); if (++guard > 100000) Assert.Fail("stuck"); }
            return dlg;
        }

        [Test] public void CharacterDefResolvesNameAndColor()
        {
            var dlg = Run("character 요르 name:\"요르 (숲의 요정)\" color:\"#8fd3ff\"\n요르 \"hi\"");
            Assert.AreEqual("요르 (숲의 요정)", dlg.Lines[0].Speaker);
            Assert.AreEqual("#8fd3ff", dlg.Lines[0].Color);
        }

        [Test] public void UndefinedSpeakerFallsBackToLiteral()
        {
            var dlg = Run("민지 \"hi\"");
            Assert.AreEqual("민지", dlg.Lines[0].Speaker);
            Assert.IsNull(dlg.Lines[0].Color);
        }

        [Test] public void NarrationStaysNull()
        {
            var dlg = Run("\"…\"");
            Assert.IsNull(dlg.Lines[0].Speaker);
            Assert.IsNull(dlg.Lines[0].Color);
        }

        [Test] public void SayTextIsInterpolated()
        {
            var dlg = Run("$ gold = 42\n요르 \"남은 골드는 [gold]개야.\"");
            Assert.AreEqual("남은 골드는 42개야.", dlg.Lines[0].Text);
        }
    }
}
