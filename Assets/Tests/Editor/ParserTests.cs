using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class ParserTests
    {
        private static List<Command> Parse(string src) =>
            Parser.Parse(LineReader.Read(src, "t.vns"));

        [Test] public void Narration()
        {
            var c = (SayCommand)Parse("\"…정적이 흘렀다.\"")[0];
            Assert.IsNull(c.SpeakerRef);
            Assert.AreEqual("…정적이 흘렀다.", c.Text);
        }

        [Test] public void SpeakerLine()
        {
            var c = (SayCommand)Parse("요르 \"주말에 뭐 할래?\"")[0];
            Assert.AreEqual("요르", c.SpeakerRef);
            Assert.AreEqual("주말에 뭐 할래?", c.Text);
        }

        [Test] public void CharacterDef()
        {
            var c = (CharacterDefCommand)Parse("character 요르 name:\"요르 (숲의 요정)\" color:\"#8fd3ff\"")[0];
            Assert.AreEqual("요르", c.Id);
            Assert.AreEqual("요르 (숲의 요정)", c.DisplayName);
            Assert.AreEqual("#8fd3ff", c.Color);
        }

        [Test] public void CharacterDefNoColor()
        {
            var c = (CharacterDefCommand)Parse("character 나 name:\"나\"")[0];
            Assert.AreEqual("나", c.Id);
            Assert.AreEqual("나", c.DisplayName);
            Assert.IsNull(c.Color);
        }

        [Test] public void LabelStripsColon()
        {
            var c = (LabelCommand)Parse("label 데이트:")[0];
            Assert.AreEqual("데이트", c.Name);
        }

        [Test] public void ShowWithPosition()
        {
            var c = (ShowCommand)Parse("show 요르 left")[0];
            Assert.AreEqual("요르", c.Character);
            Assert.AreEqual("left", c.Position);
        }

        [Test] public void ShowDefaultsToCenter()
        {
            var c = (ShowCommand)Parse("show 민지")[0];
            Assert.AreEqual("center", c.Position);
        }

        [Test] public void ShowBadPositionThrows()
            => Assert.Throws<VnParseException>(() => Parse("show 요르 up"));

        [Test] public void HideAndBg()
        {
            Assert.AreEqual("민지", ((HideCommand)Parse("hide 민지")[0]).Character);
            Assert.AreEqual("공원", ((BgCommand)Parse("bg 공원")[0]).Name);
        }

        [Test] public void JumpCallReturn()
        {
            Assert.AreEqual("데이트", ((JumpCommand)Parse("jump 데이트")[0]).Label);
            Assert.AreEqual("인트로", ((CallCommand)Parse("call 인트로")[0]).Label);
            Assert.IsInstanceOf<ReturnCommand>(Parse("return")[0]);
        }

        [Test] public void SimpleAssignment()
        {
            var c = (SetCommand)Parse("$ 요르 = 10")[0];
            Assert.AreEqual("요르", c.Var);
            Assert.AreEqual(VnValue.Int(10), ExprEval.Eval(c.Value, new GameState(new SeededRandom(1))));
        }

        [Test] public void CompoundAssignmentExpands()
        {
            // $ gold += 5  →  gold + 5
            var s = new GameState(new SeededRandom(1));
            s.Set("gold", VnValue.Int(20));
            var c = (SetCommand)Parse("$ gold += 5")[0];
            Assert.AreEqual(VnValue.Int(25), ExprEval.Eval(c.Value, s));
        }

        [Test] public void IfElifElse()
        {
            var src =
                "if 요르 >= 50:\n" +
                "    요르 \"사귈래?\"\n" +
                "elif 요르 >= 0:\n" +
                "    요르 \"친구로.\"\n" +
                "else:\n" +
                "    요르 \"안 맞아.\"\n";
            var c = (IfCommand)Parse(src)[0];
            Assert.AreEqual(3, c.Branches.Count);
            Assert.IsNotNull(c.Branches[0].Condition);
            Assert.IsNotNull(c.Branches[1].Condition);
            Assert.IsNull(c.Branches[2].Condition); // else
            Assert.AreEqual("사귈래?", ((SayCommand)c.Branches[0].Body[0]).Text);
        }

        [Test] public void WhileBlock()
        {
            var src =
                "while 남은턴 > 0:\n" +
                "    $ 남은턴 -= 1\n";
            var c = (WhileCommand)Parse(src)[0];
            Assert.IsNotNull(c.Condition);
            Assert.AreEqual(1, c.Body.Count);
            Assert.IsInstanceOf<SetCommand>(c.Body[0]);
        }

        [Test] public void MenuWithConditionalChoice()
        {
            var src =
                "menu:\n" +
                "    \"같이 영화 보자\":\n" +
                "        jump 데이트\n" +
                "    \"금화를 준다\" if 골드 >= 10:\n" +
                "        $ 골드 -= 10\n" +
                "        jump 뇌물\n";
            var c = (MenuCommand)Parse(src)[0];
            Assert.AreEqual(2, c.Choices.Count);
            Assert.AreEqual("같이 영화 보자", c.Choices[0].Label);
            Assert.IsNull(c.Choices[0].Condition);
            Assert.AreEqual("금화를 준다", c.Choices[1].Label);
            Assert.IsNotNull(c.Choices[1].Condition);
            Assert.AreEqual(2, c.Choices[1].Body.Count);
        }

        [Test] public void ElseWithoutIfThrows()
            => Assert.Throws<VnParseException>(() => Parse("else:\n    return"));

        [Test] public void PreservesLineNumbers()
        {
            var c = Parse("\n\n요르 \"hi\"");
            Assert.AreEqual(3, c[0].Line);
        }
    }
}
