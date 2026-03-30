using System;

namespace DotSee.ResponsiveImages.Exceptions
{
    [Serializable]
    public class RuleSetNotFoundException : Exception
    {
        public RuleSetNotFoundException() { }
        public RuleSetNotFoundException(string message) : base(message) { }
        public RuleSetNotFoundException(string message, Exception inner) : base(message, inner) { }
        protected RuleSetNotFoundException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
