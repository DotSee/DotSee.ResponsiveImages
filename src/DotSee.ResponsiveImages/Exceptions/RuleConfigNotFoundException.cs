using System;

namespace DotSee.ResponsiveImages.Exceptions
{
    [Serializable]
    public class RuleConfigNotFoundException : Exception
    {
        public RuleConfigNotFoundException() { }
        public RuleConfigNotFoundException(string message) : base(message) { }
        public RuleConfigNotFoundException(string message, Exception inner) : base(message, inner) { }
        protected RuleConfigNotFoundException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
