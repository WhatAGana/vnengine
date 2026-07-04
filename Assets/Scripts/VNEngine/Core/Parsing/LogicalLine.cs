namespace VNEngine
{
    public readonly struct LogicalLine
    {
        public readonly int Indent;
        public readonly string Text;
        public readonly int LineNumber;
        public readonly string File;

        public LogicalLine(int indent, string text, int lineNumber, string file)
        {
            Indent = indent; Text = text; LineNumber = lineNumber; File = file;
        }
    }
}
