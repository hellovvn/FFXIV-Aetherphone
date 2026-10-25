using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal readonly struct ListRowSize
{
    private const float StandardHeight = 0.84f;
    private const float StandardAvatar = 0.82f;
    private const float StandardGlyph = 0.92f;
    private const float CompactHeight = 0.66f;
    private const float CompactAvatar = 0.6f;
    private const float CompactGlyph = 0.8f;
    private const float CompactLineGap = 1f;
    private const float MinimumPadY = 5f;

    public static readonly ListDensity[] Order = { ListDensity.Comfortable, ListDensity.Standard, ListDensity.Compact };

    private static readonly TextStyle CompactTitleStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle CompactSubStyle = TextStyles.Footnote;
    private static readonly TextStyle CompactMetaStyle = TextStyles.Caption1;

    public readonly ListDensity Density;
    public readonly float Height;
    public readonly float AvatarRadius;
    public readonly float Glyph;
    public readonly float LineGap;
    public readonly TextStyle TitleStyle;
    public readonly TextStyle SubStyle;
    public readonly TextStyle MetaStyle;

    private ListRowSize(ListDensity density, float height, float avatarRadius, float glyph, float lineGap,
        TextStyle titleStyle, TextStyle subStyle, TextStyle metaStyle)
    {
        Density = density;
        Height = height;
        AvatarRadius = avatarRadius;
        Glyph = glyph;
        LineGap = lineGap;
        TitleStyle = titleStyle;
        SubStyle = subStyle;
        MetaStyle = metaStyle;
    }

    public static ListRowSize For(ListDensity density, float height, float avatarRadius)
    {
        if (density == ListDensity.Comfortable)
        {
            return new ListRowSize(density, height, avatarRadius, 1f, ChatListChrome.RowLineGap,
                ChatListChrome.RowTitleStyle, ChatListChrome.RowSubStyle, ChatListChrome.RowMetaStyle);
        }

        var compact = density == ListDensity.Compact;
        var titleStyle = compact ? CompactTitleStyle : ChatListChrome.RowTitleStyle;
        var subStyle = compact ? CompactSubStyle : ChatListChrome.RowSubStyle;
        var lineGap = compact ? CompactLineGap : ChatListChrome.RowLineGap;
        var scale = UiScale.Current;
        var textBlock = (Typography.LineHeight(titleStyle) + Typography.LineHeight(subStyle)) / scale + lineGap;
        var fitted = MathF.Max(height * (compact ? CompactHeight : StandardHeight), textBlock + MinimumPadY * 2f);
        return new ListRowSize(density, fitted, avatarRadius * (compact ? CompactAvatar : StandardAvatar),
            compact ? CompactGlyph : StandardGlyph, lineGap, titleStyle, subStyle,
            compact ? CompactMetaStyle : ChatListChrome.RowMetaStyle);
    }

    public float TextTop(Rect bounds, float comfortableTop)
    {
        var scale = UiScale.Current;
        if (Density == ListDensity.Comfortable)
        {
            return bounds.Min.Y + comfortableTop * scale;
        }

        var block = Typography.LineHeight(TitleStyle) + LineGap * scale + Typography.LineHeight(SubStyle);
        return bounds.Center.Y - block * 0.5f;
    }

    public static void FillMenu(List<DropdownMenu.Item> items, ListDensity current)
    {
        items.Clear();
        for (var index = 0; index < Order.Length; index++)
        {
            items.Add(new DropdownMenu.Item(Loc.T(Label(Order[index])), string.Empty, false, Order[index] == current));
        }
    }

    public static LocString Label(ListDensity density) => density switch
    {
        ListDensity.Standard => L.Home.GridStandard,
        ListDensity.Compact => L.Home.GridCompact,
        _ => L.Home.GridComfortable,
    };
}
