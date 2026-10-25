namespace Aetherphone.Core.Gifs;

internal static class KlipyReportReasons
{
    public static string? For(string category)
    {
        return category switch
        {
            "explicit" => "nudity",
            "hate" => "hate_speech",
            "threats" or "selfharm" => "violence",
            "child" or "exploitation" => "illegal",
            "creators" => "copyright",
            "impersonation" => "impersonation",
            _ => null,
        };
    }
}
