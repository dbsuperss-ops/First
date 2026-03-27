using System.Windows.Media;
using AIRoundTable.Models;

namespace AIRoundTable;

public class MessageViewModel
{
    private static readonly Dictionary<string, (Brush Avatar, Brush Card, Brush Border)> _colors = new()
    {
        ["나"] = (HexBrush("#7C3AED"), HexBrush("#FFFFFF"), HexBrush("#E5E7EB")),
    };

    /// <summary>
    /// 설정에서 AI 모델 색상을 등록합니다. UI 재구성 시 호출하십시오.
    /// </summary>
    public static void RegisterColors(IEnumerable<AiModelConfig> models)
    {
        _colors.Clear();
        _colors["나"] = (HexBrush("#7C3AED"), HexBrush("#FFFFFF"), HexBrush("#E5E7EB"));

        foreach (var m in models)
        {
            try
            {
                var c      = (Color)ColorConverter.ConvertFromString(m.Color);
                var light  = Color.FromArgb(30, c.R, c.G, c.B);
                var border = Color.FromArgb(80, c.R, c.G, c.B);
                _colors[m.Name] = (
                    Freeze(new SolidColorBrush(c)),
                    Freeze(new SolidColorBrush(light)),
                    Freeze(new SolidColorBrush(border))
                );
            }
            catch
            {
                _colors[m.Name] = (HexBrush("#6B7280"), HexBrush("#FFFFFF"), HexBrush("#E5E7EB"));
            }
        }
    }

    public Message Source          { get; }
    public string  Sender          { get; }
    public string  Content         { get; }
    public string  TimestampText   { get; }
    public string  AvatarInitial   { get; }
    public Brush   AvatarBrush     { get; }
    public Brush   CardBackground  { get; }
    public Brush   CardBorderBrush { get; }

    public MessageViewModel(Message msg)
    {
        Source        = msg;
        Sender        = msg.Sender;
        Content       = msg.Content;
        TimestampText = msg.Timestamp.ToString("HH:mm");
        AvatarInitial = msg.Sender.Length > 0 ? msg.Sender[0].ToString() : "?";

        if (_colors.TryGetValue(msg.Sender, out var c))
        {
            AvatarBrush     = c.Avatar;
            CardBackground  = c.Card;
            CardBorderBrush = c.Border;
        }
        else
        {
            AvatarBrush     = HexBrush("#6B7280");
            CardBackground  = HexBrush("#FFFFFF");
            CardBorderBrush = HexBrush("#E5E7EB");
        }
    }

    private static SolidColorBrush HexBrush(string hex)
        => Freeze(new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)));

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
}
