using NUnit.Framework;

namespace VNEngine.Tests
{
    public class LineReaderTests
    {
        [Test] public void SkipsBlankAndCommentLines()
        {
            var src = "\n# a comment\n요르 \"hi\"\n\n";
            var lines = LineReader.Read(src, "f.vns");
            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual("요르 \"hi\"", lines[0].Text);
            Assert.AreEqual(0, lines[0].Indent);
            Assert.AreEqual(3, lines[0].LineNumber);
        }

        [Test] public void CountsIndentSpaces()
        {
            var src = "menu:\n    \"a\":\n        jump x";
            var lines = LineReader.Read(src, "f.vns");
            Assert.AreEqual(0, lines[0].Indent);
            Assert.AreEqual(4, lines[1].Indent);
            Assert.AreEqual(8, lines[2].Indent);
        }

        [Test] public void StripsTrailingCarriageReturn()
        {
            var lines = LineReader.Read("label a:\r\n    return\r\n", "f.vns");
            Assert.AreEqual("label a:", lines[0].Text);
            Assert.AreEqual("return", lines[1].Text);
        }

        [Test] public void InlineCommentStripped()
        {
            var lines = LineReader.Read("jump x  # go", "f.vns");
            Assert.AreEqual("jump x", lines[0].Text);
        }

        [Test] public void HashInsideQuotesKept()
        {
            var lines = LineReader.Read("나 \"# 1등이야\"", "f.vns");
            Assert.AreEqual("나 \"# 1등이야\"", lines[0].Text);
        }

        [Test] public void TabIndentThrows()
        {
            Assert.Throws<VnParseException>(() => LineReader.Read("\t요르 \"x\"", "f.vns"));
        }

        [Test] public void CarriesFileName()
        {
            var lines = LineReader.Read("return", "chap1.vns");
            Assert.AreEqual("chap1.vns", lines[0].File);
        }
    }
}
