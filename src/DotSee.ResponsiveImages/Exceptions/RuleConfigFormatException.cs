using System;

namespace DotSee.ResponsiveImages.Exceptions
{
    [Serializable]
    public class RuleConfigFormatException : Exception
    {
        public RuleConfigFormatException() { }
        public RuleConfigFormatException(string message) : base(message) { }
        public RuleConfigFormatException(string message, Exception inner) : base(message, inner) { }
        protected RuleConfigFormatException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
