namespace DotSee.ResponsiveImages
{
    public class ImageBreakPointModel
    {
        public string ImageUrl { get; set; }
        public bool IsFirst { get; set; }
        public int BreakPointWidth { get; set; }
        public int NextBreakPointWidth { get; set; }
        public RuleBreakPoint BreakPoint { get; set; }
    }
}