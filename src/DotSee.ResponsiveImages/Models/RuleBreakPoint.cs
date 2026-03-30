namespace DotSee.ResponsiveImages
{
    public class RuleBreakPoint
    {
        public int BreakPointWidth { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public int DefinedImageWidth 
        { 
            get
            {
                return (Width > 0) ? Width : BreakPointWidth;

            } 
        }
    }
}
