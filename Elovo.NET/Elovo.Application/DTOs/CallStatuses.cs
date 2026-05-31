namespace Elovo.Application.DTOs;

public static class CallStatuses
{
    public const string Answered = "answered";
    public const string Rejected = "rejected";
    public const string Missed = "missed";

    public static bool IsSupported(string status)
    {
        return status is Answered or Rejected or Missed;
    }
}
