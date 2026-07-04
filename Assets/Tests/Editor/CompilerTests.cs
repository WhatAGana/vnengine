using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class CompilerTests
    {
        private static VnProgram Compile(string src) =>
            Compiler.Compile(Parser.Parse(LineReader.Read(src, "t.vns")));

        [Test] public void SayThenHalt()
        {
            var p = Compile("요르 \"hi\"");
            Assert.AreEqual(Op.Say, p.Code[0].Op);
            Assert.AreEqual("요르", p.Code[0].StrA);
            Assert.AreEqual("hi", p.Code[0].StrB);
            Assert.AreEqual(Op.Halt, p.Code[p.Code.Length - 1].Op);
        }

        [Test] public void NarrationHasNullSpeaker()
        {
            var p = Compile("\"…\"");
            Assert.IsNull(p.Code[0].StrA);
        }

        [Test] public void LabelResolvesToNextInstruction()
        {
            var p = Compile("label a:\n요르 \"x\"\njump a");
            Assert.IsTrue(p.Labels.ContainsKey("a"));
            int aIndex = p.Labels["a"];
            Assert.AreEqual(Op.Say, p.Code[aIndex].Op); // label points at the say
            var jump = FindOp(p, Op.Jump);
            Assert.AreEqual(aIndex, jump.Target);
        }

        [Test] public void DuplicateLabelThrows()
            => Assert.Throws<VnParseException>(() => Compile("label a:\nlabel a:"));

        [Test] public void UnknownJumpLabelThrows()
            => Assert.Throws<VnParseException>(() => Compile("jump nowhere"));

        [Test] public void IfLowersToJumpIfFalse()
        {
            var p = Compile("if x >= 1:\n    요르 \"y\"\n요르 \"z\"");
            var jif = FindOp(p, Op.JumpIfFalse);
            Assert.IsNotNull(jif.ExprA);
            // false-target must be a valid instruction index
            Assert.IsTrue(jif.Target >= 0 && jif.Target < p.Code.Length);
        }

        [Test] public void WhileHasBackwardJump()
        {
            var p = Compile("while n > 0:\n    $ n -= 1");
            // there must be a Jump whose target is <= its own index (loop back)
            bool foundBackward = false;
            for (int i = 0; i < p.Code.Length; i++)
                if (p.Code[i].Op == Op.Jump && p.Code[i].Target <= i) foundBackward = true;
            Assert.IsTrue(foundBackward, "expected a backward jump for while loop");
        }

        [Test] public void MenuOptionsTargetBodies()
        {
            var src =
                "menu:\n" +
                "    \"a\":\n" +
                "        jump end\n" +
                "    \"b\" if g >= 1:\n" +
                "        jump end\n" +
                "label end:\n" +
                "요르 \"done\"";
            var p = Compile(src);
            var menu = FindOp(p, Op.Menu);
            Assert.AreEqual(2, menu.Menu.Count);
            Assert.AreEqual("a", menu.Menu[0].Label);
            Assert.IsNull(menu.Menu[0].Condition);
            Assert.IsNotNull(menu.Menu[1].Condition);
            foreach (var opt in menu.Menu)
                Assert.IsTrue(opt.Target >= 0 && opt.Target < p.Code.Length);
        }

        [Test] public void CharacterDefsCollected()
        {
            var p = Compile("character 요르 name:\"요르\" color:\"#fff\"\n요르 \"hi\"");
            Assert.IsTrue(p.Characters.ContainsKey("요르"));
            Assert.AreEqual("요르", p.Characters["요르"].DisplayName);
            Assert.AreEqual("#fff", p.Characters["요르"].Color);
            // character def emits no instruction; first code is the say
            Assert.AreEqual(Op.Say, p.Code[0].Op);
        }

        [Test] public void DuplicateCharacterThrows()
            => Assert.Throws<VnParseException>(() =>
                Compile("character a name:\"A\"\ncharacter a name:\"B\""));

        [Test] public void MultiFileGlobalLabels()
        {
            var f1 = Parser.Parse(LineReader.Read("jump other", "a.vns"));
            var f2 = Parser.Parse(LineReader.Read("label other:\n요르 \"hi\"", "b.vns"));
            var p = Compiler.Compile(new List<List<Command>> { f1, f2 });
            Assert.IsTrue(p.Labels.ContainsKey("other"));
            var jump = FindOp(p, Op.Jump);
            Assert.AreEqual(p.Labels["other"], jump.Target);
        }

        private static Instruction FindOp(VnProgram p, Op op)
        {
            foreach (var ins in p.Code) if (ins.Op == op) return ins;
            Assert.Fail($"no instruction with op {op}");
            return null;
        }
    }
}
