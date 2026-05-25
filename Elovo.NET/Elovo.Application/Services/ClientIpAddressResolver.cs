using System.Net;
using Microsoft.AspNetCore.Http;

namespace Elovo.Application.Services;

public static class ClientIpAddressResolver
{
    public static string? Resolve(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return null;
        }

        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var forwardedIp = ResolveForwardedFor(forwardedFor);
        if (forwardedIp is not null)
        {
            return forwardedIp;
        }

        var realIp = ResolveSingleAddress(httpContext.Request.Headers["X-Real-IP"].FirstOrDefault());
        if (realIp is not null)
        {
            return realIp;
        }

        return NormalizeIpAddress(httpContext.Connection.RemoteIpAddress);
    }

    private static string? ResolveForwardedFor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var ip = ResolveSingleAddress(item);
            if (ip is not null)
            {
                return ip;
            }
        }

        return null;
    }

    private static string? ResolveSingleAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.Contains(']'))
        {
            trimmed = trimmed[1..trimmed.IndexOf(']', StringComparison.Ordinal)];
        }
        else if (trimmed.Count(x => x == ':') == 1 && trimmed.LastIndexOf(':') > trimmed.LastIndexOf('.'))
        {
            trimmed = trimmed[..trimmed.LastIndexOf(':')];
        }

        return IPAddress.TryParse(trimmed, out var address)
            ? NormalizeIpAddress(address)
            : null;
    }

    private static string? NormalizeIpAddress(IPAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.ToString();
    }
}
