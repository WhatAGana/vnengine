using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace VNEngine.Tests
{
    public class SaveLoadIntegrationTests
    {
        private const string Src =
            "bg 공원\n" +
            "show 요르 left\n" +
            "$ affinity = 10\n" +
            "요르 \"주말에 뭐 할래?\"\n" +      // wait point we save at
            "menu:\n" +
            "    \"데이트\":\n" +
            "        $ affinity += 30\n" +
            "        $ luck = random(1, 1000000)\n" +
            "        요르 \"좋아! [affinity] [luck]\"\n" +
            "        jump end\n" +
            "    \"거절\":\n" +
            "        요르 \"그렇구나\"\n" +
            "        jump end\n" +
            "label end:\n" +
            "요르 \"끝\"";

        private static Interpreter Build(GameState s, FakeDialogueView d, FakeStageView g)
        {
            var program = Compiler.Compile(Parser.Parse(LineReader.Read("label start:\n" + Src, "t.vns")));
            var interp = new Interpreter(program, s, d, g);
            interp.Start("start");
            return interp;
        }

        private static void TickToWait(Interpreter i)
        { int g = 0; while (!i.IsWaiting && !i.IsFinished) if (++g > 100000) Assert.Fail(); else i.Tick(); }
        private static void RunToEnd(Interpreter i)
        { int g = 0; while (!i.IsFinished) if (++g > 100000) Assert.Fail(); else i.Tick(); }
        private static string[] Texts(FakeDialogueView d)
        { var l = new List<string>(); foreach (var s in d.Lines) l.Add(s.Text); return l.ToArray(); }

        [Test]
        public void SaveJsonLoadResumesWithParity()
        {
            string hash = VnHash.Fnv1a(Src);

            // Baseline: run straight through, choosing "데이트" (index 0).
            var sBase = new GameState(new SeededRandom(777));
            var dBase = new FakeDialogueView(0);
            var iBase = Build(sBase, dBase, new FakeStageView());
            RunToEnd(iBase);
            int baseAffinity = sBase.Get("affinity").AsInt;
            int baseLuck = sBase.Get("luck").AsInt;

            // Save/load run: save at the first line, JSON round-trip, restore, resume.
            var sA = new GameState(new SeededRandom(777));
            var iA = Build(sA, new FakeDialogueView(0), new FakeStageView());
            TickToWait(iA);
            var data = iA.CaptureSave(hash);

            // Simulate disk via the real JSON serializer.
            string json = JsonUtility.ToJson(data);
            var loaded = JsonUtility.FromJson<SaveData>(json);
            Assert.AreEqual(SaveData.SaveFormatVersion, loaded.version);
            Assert.AreEqual(hash, loaded.programHash);

            var sB = new GameState(new SeededRandom(0)); // seed irrelevant; restored
            var dB = new FakeDialogueView(0);
            var gB = new FakeStageView();
            var iB = Build(sB, dB, gB);
            iB.RestoreSave(loaded);
            RunToEnd(iB);

            // Parity: same final variables (incl. deterministic random) and ending line.
            Assert.AreEqual(baseAffinity, sB.Get("affinity").AsInt);
            Assert.AreEqual(baseLuck, sB.Get("luck").AsInt);
            CollectionAssert.Contains(Texts(dB), "끝");
            // Stage was restored (cleared + re-applied) before resuming.
            Assert.Contains("clear", gB.Log);
            Assert.Contains("show:요르:left", gB.Log);
        }

        [Test]
        public void IncompatibleHashIsRejected()
        {
            var data = new SaveData { version = SaveData.SaveFormatVersion, programHash = "real" };
            Assert.IsFalse(VNEngine.Unity.SaveSystem.IsCompatible(data, "tampered"));
        }
    }
}
