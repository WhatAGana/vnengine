using NUnit.Framework;

namespace VNEngine.Tests
{
    public class SaveCaptureTests
    {
        // Build an interpreter over src (a "label start:" is prepended) and
        // tick until it is waiting for input or finished.
        private static Interpreter Build(string src, out GameState state,
                                         out FakeDialogueView dlg, out FakeStageView stage,
                                         params int[] answers)
        {
            var program = Compiler.Compile(Parser.Parse(LineReader.Read("label start:\n" + src, "t.vns")));
            state = new GameState(new SeededRandom(1));
            dlg = new FakeDialogueView(answers);
            stage = new FakeStageView();
            var interp = new Interpreter(program, state, dlg, stage);
            interp.Start("start");
            return interp;
        }

        private static void TickToWait(Interpreter interp)
        {
            int guard = 0;
            while (!interp.IsWaiting && !interp.IsFinished)
            {
                interp.Tick();
                if (++guard > 100000) Assert.Fail("did not reach a wait point");
            }
        }

        [Test]
        public void CaptureAtLineHoldsRenderedText()
        {
            var interp = Build("$ x = 5\n요르 \"hi [x]\"", out _, out _, out _);
            TickToWait(interp);
            var data = interp.CaptureSave("H");
            Assert.AreEqual(SaveData.SaveFormatVersion, data.version);
            Assert.AreEqual("H", data.programHash);
            Assert.AreEqual(1, data.pending); // Line
            Assert.AreEqual("요르", data.lineSpeaker);
            Assert.AreEqual("hi 5", data.lineText);
        }

        [Test]
        public void CaptureIncludesVars()
        {
            var interp = Build("$ gold = 42\n$ met = true\n요르 \"x\"", out _, out _, out _);
            TickToWait(interp);
            var data = interp.CaptureSave("H");
            int gold = 0, metKind = -1, metVal = -1;
            foreach (var v in data.vars)
            {
                if (v.name == "gold") gold = v.value;
                if (v.name == "met") { metKind = v.kind; metVal = v.value; }
            }
            Assert.AreEqual(42, gold);
            Assert.AreEqual(1, metKind);   // Bool
            Assert.AreEqual(1, metVal);    // true
        }

        [Test]
        public void CaptureIncludesStage()
        {
            var interp = Build("bg 공원\nshow 요르 left\n요르 \"x\"", out _, out _, out _);
            TickToWait(interp);
            var data = interp.CaptureSave("H");
            Assert.AreEqual("공원", data.background);
            Assert.AreEqual(1, data.stage.Count);
            Assert.AreEqual("left", data.stage[0].position);
            Assert.AreEqual("요르", data.stage[0].character);
        }

        [Test]
        public void CaptureAtChoiceHoldsLabelsAndTargets()
        {
            var src =
                "menu:\n" +
                "    \"a\":\n" +
                "        jump end\n" +
                "    \"b\":\n" +
                "        jump end\n" +
                "label end:\n" +
                "요르 \"done\"";
            var interp = Build(src, out _, out _, out _, 0);
            TickToWait(interp);
            var data = interp.CaptureSave("H");
            Assert.AreEqual(2, data.pending); // Choice
            Assert.AreEqual(2, data.choiceLabels.Count);
            Assert.AreEqual("a", data.choiceLabels[0]);
            Assert.AreEqual("b", data.choiceLabels[1]);
            Assert.AreEqual(2, data.choiceTargets.Count);
        }

        [Test]
        public void CaptureWhenNotWaitingThrows()
        {
            var interp = Build("return", out _, out _, out _);
            TickToWait(interp); // finishes immediately, not waiting
            Assert.IsTrue(interp.IsFinished);
            Assert.Throws<VnRuntimeException>(() => interp.CaptureSave("H"));
        }
    }
}
