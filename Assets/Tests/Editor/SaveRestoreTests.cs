using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class SaveRestoreTests
    {
        private static Interpreter Build(string src, GameState state,
                                         FakeDialogueView dlg, FakeStageView stage)
        {
            var program = Compiler.Compile(Parser.Parse(LineReader.Read("label start:\n" + src, "t.vns")));
            var interp = new Interpreter(program, state, dlg, stage);
            interp.Start("start");
            return interp;
        }

        private static void TickToWait(Interpreter interp)
        {
            int g = 0;
            while (!interp.IsWaiting && !interp.IsFinished)
                if (++g > 100000) { Assert.Fail("no wait"); } else interp.Tick();
        }

        private static void RunToEnd(Interpreter interp)
        {
            int g = 0;
            while (!interp.IsFinished)
                if (++g > 100000) { Assert.Fail("no finish"); } else interp.Tick();
        }

        private static List<string> Texts(FakeDialogueView d)
        {
            var l = new List<string>();
            foreach (var s in d.Lines) l.Add(s.Text);
            return l;
        }

        [Test]
        public void RestoredInterpreterResumesIdentically()
        {
            const string src =
                "요르 \"one\"\n" +   // wait A here
                "$ gold = 10\n" +
                "요르 \"two\"\n" +
                "요르 \"three\"";

            // A: run to first wait, capture, then finish.
            var sa = new GameState(new SeededRandom(1));
            var da = new FakeDialogueView();
            var interpA = Build(src, sa, da, new FakeStageView());
            TickToWait(interpA);
            var data = interpA.CaptureSave("H");
            RunToEnd(interpA);

            // B: fresh, restore from the capture, then finish.
            var sb = new GameState(new SeededRandom(1));
            var db = new FakeDialogueView();
            var interpB = Build(src, sb, db, new FakeStageView());
            interpB.RestoreSave(data);
            RunToEnd(interpB);

            // A showed one,two,three ; B (restored right after one) re-shows one then two,three.
            Assert.AreEqual(new[] { "one", "two", "three" }, Texts(da).ToArray());
            Assert.AreEqual(new[] { "one", "two", "three" }, Texts(db).ToArray());
            Assert.AreEqual(VnValue.Int(10), sb.Get("gold"));
        }

        [Test]
        public void RandomIsDeterministicAcrossSaveLoad()
        {
            const string src =
                "요르 \"pause\"\n" +
                "$ r = random(1, 1000000)\n" +
                "요르 \"[r]\"";

            var sa = new GameState(new SeededRandom(12345));
            var da = new FakeDialogueView();
            var interpA = Build(src, sa, da, new FakeStageView());
            TickToWait(interpA);                 // at "pause"
            var data = interpA.CaptureSave("H");
            RunToEnd(interpA);
            int ra = sa.Get("r").AsInt;

            var sb = new GameState(new SeededRandom(999)); // different seed
            var db = new FakeDialogueView();
            var interpB = Build(src, sb, db, new FakeStageView());
            interpB.RestoreSave(data);           // restores RNG state
            RunToEnd(interpB);
            int rb = sb.Get("r").AsInt;

            Assert.AreEqual(ra, rb);
        }

        [Test]
        public void StageIsClearedAndReapplied()
        {
            const string src = "bg 공원\nshow 요르 left\n요르 \"hi\"";
            var sa = new GameState(new SeededRandom(1));
            var interpA = Build(src, sa, new FakeDialogueView(), new FakeStageView());
            TickToWait(interpA);
            var data = interpA.CaptureSave("H");

            var stageB = new FakeStageView();
            var interpB = Build(src, new GameState(new SeededRandom(1)), new FakeDialogueView(), stageB);
            interpB.RestoreSave(data);

            Assert.Contains("clear", stageB.Log);
            Assert.Contains("bg:공원", stageB.Log);
            Assert.Contains("show:요르:left", stageB.Log);
        }

        [Test]
        public void NarrationNullSpeakerSurvivesEmptyString()
        {
            // Simulate the JsonUtility null->"" round trip: RestoreSave must
            // treat "" speaker/color/background as null.
            const string src = "\"just narration\"";
            var interpA = Build(src, new GameState(new SeededRandom(1)),
                                new FakeDialogueView(), new FakeStageView());
            TickToWait(interpA);
            var data = interpA.CaptureSave("H");
            data.lineSpeaker = "";   // as JsonUtility would produce
            data.lineColor = "";
            data.background = "";

            var db = new FakeDialogueView();
            var interpB = Build(src, new GameState(new SeededRandom(1)), db, new FakeStageView());
            interpB.RestoreSave(data);

            Assert.IsNull(db.Lines[0].Speaker);
        }

        [Test]
        public void CallStackSurvivesSaveLoad()
        {
            // Save while inside a called subroutine; the return address must survive
            // restore so execution returns to the caller afterwards.
            const string src =
                "call sub\n" +
                "요르 \"after-return\"\n" +
                "return\n" +
                "label sub:\n" +
                "요르 \"in-sub\"\n" +    // wait here: call stack holds 1 return frame
                "요르 \"still-sub\"\n" +
                "return";

            var da = new FakeDialogueView();
            var interpA = Build(src, new GameState(new SeededRandom(1)), da, new FakeStageView());
            TickToWait(interpA);            // stops at "in-sub", inside sub
            var data = interpA.CaptureSave("H");
            RunToEnd(interpA);

            var db = new FakeDialogueView();
            var interpB = Build(src, new GameState(new SeededRandom(1)), db, new FakeStageView());
            interpB.RestoreSave(data);
            RunToEnd(interpB);

            var expected = new[] { "in-sub", "still-sub", "after-return" };
            Assert.AreEqual(expected, Texts(da).ToArray());
            Assert.AreEqual(expected, Texts(db).ToArray());
        }
    }
}
