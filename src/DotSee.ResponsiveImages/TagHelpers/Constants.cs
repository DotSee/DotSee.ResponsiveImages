namespace DotSee.ResponsiveImages.TagHelpers;

public static class Constants
{
    public const string SectionBlockError = "Error: No Section Block found to render! This tag works only with a BlockListItem model.";

    public const string PicElErrorImageAltError = "Error: Please provide an image alt!";

    public const string PicElErrorImageError = "Error: Please provide an image!";

    public const string PicElErrorRuleSetError = "Error: Please provide a rule set!";

    /// <summary>
    /// Shown instead of the exception's own message, which is internal detail that has no business in
    /// front of a visitor. The actual exception is logged.
    /// </summary>
    public const string RenderError = "Error: The responsive image could not be rendered. See the log for details.";
}