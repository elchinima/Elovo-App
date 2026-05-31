namespace Elovo.Application.Services;

public static class PushLocalization
{
    public static string GetText(string key, string? language)
    {
        return (key, NormalizeLanguage(language)) switch
        {
            ("Voice message", "ru") => "Голосовое сообщение",
            ("Voice message", "az") => "Səsli mesaj",
            ("Sent a file", "ru") => "Отправил файл",
            ("Sent a file", "az") => "Fayl göndərdi",
            ("Answered call", "ru") => "Отвеченный звонок",
            ("Answered call", "az") => "Cavablandırılmış zəng",
            ("Rejected call", "ru") => "Отклонённый звонок",
            ("Rejected call", "az") => "Rədd edilmiş zəng",
            ("Missed call", "ru") => "Пропущенный звонок",
            ("Missed call", "az") => "Buraxılmış zəng",
            _ => key
        };
    }

    private static string NormalizeLanguage(string? language)
    {
        return language?.Trim().ToLowerInvariant() switch
        {
            "ru" => "ru",
            "az" => "az",
            _ => "en"
        };
    }
}
