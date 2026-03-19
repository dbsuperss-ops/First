namespace PptxMerger.Models;

public class FormatConfig
{
    public string FontName      { get; set; } = "맑은 고딕";
    public double FontSizePt    { get; set; } = 11.0;
    public bool   Bold          { get; set; } = false;
    public string ColorHex      { get; set; } = "#1F1F1F";
    public string Align         { get; set; } = "left";
    public double LineSpacing   { get; set; } = 1.2;
    public double CharSpacing   { get; set; } = 0.0;
}
