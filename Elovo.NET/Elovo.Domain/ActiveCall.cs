namespace Elovo.Domain;

public class ActiveCall
{
    public Guid Id { get; set; }
    public Guid CallerId { get; set; }
    public Guid ReceiverId { get; set; }
    public string CallerName { get; set; } = string.Empty;
    public string CallerAvatar { get; set; } = string.Empty;
    public string? OfferSdp { get; set; }
    public bool IsRejected { get; set; }
    public DateTime StartedAt { get; set; }
}
