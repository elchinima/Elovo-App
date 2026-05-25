namespace Elovo.Application.DTOs;

public class ImageDownloadResultDto
{
    public byte[] Bytes { get; set; } = [];
    public string ContentType { get; set; } = "application/octet-stream";
}
