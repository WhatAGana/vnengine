using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace VNEngine.Tests
{
    // Migration parity: the real Assets/Resources/scripts/intro.vns, loaded through the
    // real parse+compile pipeline and driven by the interpreter, must reproduce the
    // outcomes of the original StreamingAssets/dialogues.json scenario exactly.
    //   Path A (choose "같이 영화 보자"): 요르 += 30 -> 요르>=30 -> confession line.
    //   Path B (choose "귀찮은데 그냥 집에"): 요르 -= 10 -> else branch -> breakup line.
    public class IntroScenarioTests
    {
        // Mirrors VnScriptLoader.LoadAndCompile using Core-only types (the test assembly
        // references VNEngine.Core, not VNEngine.Unity), against the same Resources folder.
        private static VnProgram LoadIntro()
        {
            var assets = Resources.LoadAll<TextAsset>("scripts");
            Assert.IsNotNull(assets, "Resources.LoadAll returned null");
            Assert.Greater(assets.Length, 0, "no .vns TextAssets found under Resources/scripts");
            System.Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));

            var parsed = new List<List<Command>>();
            foreach (var ta in assets)
                parsed.Add(Parser.Parse(LineReader.Read(ta.text, ta.name)));
            return Compiler.Compile(parsed);
        }

        private static FakeDialogueView Run(VnProgram prog, params int[] answers)
        {
            var dlg = new FakeDialogueView(answers);
            var interp = new Interpreter(prog, new GameState(new SeededRandom(12345)), dlg, new FakeStageView());
            interp.Start("start");
            int guard = 0;
            while (!interp.IsFinished)
            {
                interp.Tick();
                if (++guard > 100000) Assert.Fail("interpreter did not finish");
            }
            return dlg;
        }

        [Test]
        public void OpeningShowsYoreLeftAndBothChoices()
        {
            var stage = new FakeStageView();
            var dlg = new FakeDialogueView(0);
            var interp = new Interpreter(LoadIntro(), new GameState(new SeededRandom(12345)), dlg, stage);
            interp.Start("start");
            for (int i = 0; i < 100 && dlg.ChoiceSets.Count == 0; i++) interp.Tick();

            Assert.Contains("show:요르:left", stage.Log);
            Assert.AreEqual("요르", dlg.Lines[0].Speaker);
            Assert.AreEqual("주말에 뭐 할래?", dlg.Lines[0].Text);
            Assert.AreEqual(1, dlg.ChoiceSets.Count);
            CollectionAssert.AreEqual(
                new[] { "같이 영화 보자", "귀찮은데 그냥 집에" }, dlg.ChoiceSets[0]);
        }

        [Test]
        public void PathA_AffinityUp_LeadsToConfession()
        {
            var dlg = Run(LoadIntro(), 0); // "같이 영화 보자" -> 요르 += 30
            var last = dlg.Lines[dlg.Lines.Count - 1];
            Assert.AreEqual("요르", last.Speaker);
            Assert.AreEqual("너랑 있으면 즐거워. 우리 사귈래?", last.Text);
        }

        [Test]
        public void PathB_AffinityDown_LeadsToBreakup()
        {
            var dlg = Run(LoadIntro(), 1); // "귀찮은데 그냥 집에" -> 요르 -= 10
            var last = dlg.Lines[dlg.Lines.Count - 1];
            Assert.AreEqual("요르", last.Speaker);
            Assert.AreEqual("...우리 안 맞는 것 같아.", last.Text);
        }

        [Test]
        public void NarrationLineAppearsBeforeBranch()
        {
            var dlg = Run(LoadIntro(), 0);
            bool sawNarration = dlg.Lines.Exists(
                l => l.Speaker == "나레이션" && l.Text == "...그렇게 시간이 흘렀다.");
            Assert.IsTrue(sawNarration, "narration line missing from playthrough");
        }
    }
}
