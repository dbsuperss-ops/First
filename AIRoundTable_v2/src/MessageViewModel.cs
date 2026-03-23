using System.Windows.Media;
using AIRoundTable.Models;

namespace AIRoundTable;

/// <summary>
/// Message에 WPF 스타일 속성(색상 브러시 등)을 부여한 뷰모델
/// </summary>
public class MessageViewModel
{
    // ── 발언자별 색상 테이블 ────────────────────────────────────────────────
    private static readonly Dictionary<string, (Brush Avatar, Brush Card, Brush Border)> Colors = new()
    {
        ["나"]     = (Brush("#7C3AED"), Brush("#FFFFFF"), Brush("#E5E7EB")),
        ["제미나이"] = (Brush("#10B981"), Brush("#F0FDF4"), Brush("#D1FAE5")),
        ["코파일럿"] = (Brush("#3B82F6"), Brush("#EFF6FF"), Brush("#DBEAFE")),
        ["클로드"]   = (Brush("#F59E0B"), Brush("#FFFBEB"), Brush("#FEF3C7")),
    };

    private static readonly Dictionary<string, string> Initials = new()
    {
        ["나"] = "나", ["제미나이"] = "G", ["코파일럿"] = "C", ["클로드"] = "A",
    };

    public string Sender        { get; }
    public string Content       { get; }
    public string TimestampText { get; }
    public string AvatarInitial { get; }
    public Brush  AvatarBrush   { get; }
    public Brush  CardBackground  { get; }
    public Brush  CardBorderBrush { get; }

    public MessageViewModel(Message msg)
    {
        Sender        = msg.Sender;
        Content       = msg.Content;
        TimestampText = msg.Timestamp.ToString("yyyy-MM-dd HH:mm");
        AvatarInitial = Initials.GetValueOrDefault(msg.Sender, msg.Sender.Length > 0 ? msg.Sender[0].ToString() : "?");

        if (Colors.TryGetValue(msg.Sender, out var c))
        {
            AvatarBrush     = c.Avatar;
            CardBackground  = c.Card;
            CardBorderBrush = c.Border;
        }
        else
        {
            AvatarBrush     = Brush("#6B7280");
            CardBackground  = Brush("#FFFFFF");
            CardBorderBrush = Brush("#E5E7EB");
        }
    }

    private static SolidColorBrush Brush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}
