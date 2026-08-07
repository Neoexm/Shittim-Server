using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using BlueArchiveAPI.Configuration;

namespace Shittim_Server.Controllers;

/// <summary>
/// Access gate for <c>/api/admin</c>: a correct <c>X-Admin-Key</c> is accepted from any address, loopback is accepted without one (the Control Center hardcodes <c>http://127.0.0.1</c>), and everything else is refused.
/// <c>app.UseAuthorization()</c> alone does nothing here - no scheme is registered and no action carries <c>[Authorize]</c> - so without this the mail, currency, console-command and account-delete endpoints answer anything that can reach the port.
/// Loopback acceptance leans on the CORS policy staying narrow; widening it needs this revisited.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminAuthAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Admin-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var configuredKey = ResolveConfiguredKey();

        if (configuredKey != null)
        {
            var presented = context.HttpContext.Request.Headers[HeaderName].ToString();
            if (MatchesKey(presented, configuredKey))
                return;
        }

        if (IsLoopback(context.HttpContext.Connection.RemoteIpAddress))
            return;

        context.Result = new ObjectResult(new
        {
            error = configuredKey == null
                ? $"Admin API is restricted to loopback. Set ServerConfiguration.AdminApiKey (or SHITTIM_ADMIN_API_KEY) and send it as {HeaderName} to administer this server remotely."
                : $"Admin API requires a valid {HeaderName} header."
        })
        {
            StatusCode = (int)HttpStatusCode.Forbidden
        };
    }

    private static string? ResolveConfiguredKey()
    {
        var key = Environment.GetEnvironmentVariable("SHITTIM_ADMIN_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
            key = Config.Instance.ServerConfiguration.AdminApiKey;

        return string.IsNullOrWhiteSpace(key) ? null : key;
    }

    private static bool MatchesKey(string presented, string configured)
    {
        if (string.IsNullOrEmpty(presented))
            return false;

        // Compared without an early-exit on the first differing byte so the timing of a rejection doesn't reveal how much of the key was correct.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented),
            Encoding.UTF8.GetBytes(configured));
    }

    private static bool IsLoopback(IPAddress? address)
    {
        if (address == null)
            return false;

        // A v4 client arriving on a dual-stack socket shows up as ::ffff:127.0.0.1, which IPAddress.IsLoopback does not recognise on its own.
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return IPAddress.IsLoopback(address);
    }
}
