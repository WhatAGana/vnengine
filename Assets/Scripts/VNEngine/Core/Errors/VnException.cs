namespace VNEngine
{
    public class VnException : System.Exception
    {
        public VnException(string message) : base(message) { }
    }
    public sealed class VnParseException : VnException
    {
        public VnParseException(string message) : base(message) { }
    }
    public sealed class VnRuntimeException : VnException
    {
        public VnRuntimeException(string message) : base(message) { }
    }
}
