using System.Windows;
using System.Windows.Media;
using AIRoundTable.Models;

namespace AIRoundTable;

public class MessageViewModel
{
    private record ModelStyle(
        string Avatar, string CardBg, string CardBorder, bool RightAlign);

    private static readonly Dictionary<string, ModelStyle> Styles = new()
    {
        ["나"]     = new("#3B82F6", "#0D1929", "#1E3A6E", true),
        ["제미나이"] = new("#10B981", "#081F15", "#103D28", false),
        ["코파일럿"] = new("#0078D4", "#081526", "#0E2545", false),
        ["클로드"]   = new("#D97706", "#1A1005", "#3D2A0A", false),
    };

    private static readonly Dictionary<string, string> Initials = new()
    {
        ["나"] = "나", ["제미나이"] = "G", ["코파일럿"] = "C", ["클로드"] = "A",
    };

    public Message    Source          { get; }
    public string     Sender          { get; }
    public string     Content         { get; }
    public string     TimestampText   { get; }
    public string     AvatarInitial   { get; }
    public Brush      AvatarBrush     { get; }
    public Brush      CardBackground  { get; }
    public Brush      CardBorderBrush { get; }
    public Visibility LeftVisibility  { get; }
    public Visibility RightVisibility { get; }

    public MessageViewModel(Message msg)
    {
        Source        = msg;
        Sender        = msg.Sender;
        Content       = msg.Content;
        TimestampText = msg.Timestamp.ToString("HH:mm");
        AvatarInitial = Initials.GetValueOrDefault(msg.Sender,
                            msg.Sender.Length > 0 ? msg.Sender[0].ToString() : "?");

        if (Styles.TryGetValue(msg.Sender, out var s))
        {
            AvatarBrush     = Hex(s.Avatar);
            CardBackground  = Hex(s.CardBg);
            CardBorderBrush = Hex(s.CardBorder);
            LeftVisibility  = s.RightAlign ? Visibility.Collapsed : Visibility.Visible;
            RightVisibility = s.RightAlign ? Visibility.Visible   : Visibility.Collapsed;
        }
        else
        {
            AvatarBrush     = Hex("#64748B");
            CardBackground  = Hex("#141B2D");
            CardBorderBrush = Hex("#2A3550");
            LeftVisibility  = Visibility.Visible;
            RightVisibility = Visibility.Collapsed;
        }
    }

    private static SolidColorBrush Hex(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}
