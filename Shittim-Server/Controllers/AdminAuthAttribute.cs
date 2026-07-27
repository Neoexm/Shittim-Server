using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using BlueArchiveAPI.Configuration;

namespace Shittim_Server.Controllers;

/// <summary>
/// Access gate for the <c>/api/admin</c> surface.
/// </summary>
/// <remarks>
/// These endpoints send mail, set currency to arbitrary values, run console commands and delete
/// accounts, and every one of them was reachable unauthenticated by anything that could open a
/// socket to the API port. <c>app.UseAuthorization()</c> was already in the pipeline but did nothing:
/// no authentication scheme is registered and no action carries <c>[Authorize]</c>.
///
/// The rule is:
/// <list type="bullet">
/// <item>a correct <c>X-Admin-Key</c> header is accepted from any address, so remote administration
/// works once a key is configured;</item>
/// <item>otherwise loopback is accepted, which is how the Shittim Control Center talks to the server
/// (it hardcodes <c>http://127.0.0.1</c>), so the default setup keeps working with no configuration;</item>
/// <item>everything else is refused.</item>
/// </list>
/// Binding to a LAN or public interface therefore no longer exposes the admin API by default. Note
/// that loopback acceptance leans on CORS staying restrictive — the policy in GameServer allows only
/// <c>localhost:3000</c> and <c>tauri.localhost</c>, which is what stops a random web page from
/// preflighting a JSON request at the local port. Widening that policy would need this revisited.
/// </remarks>
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

    /// <summary>Returns the configured key, or null when none is set.</summary>
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

        // Compared without an early-exit on the first differing byte so the timing of a rejection
        // doesn't reveal how much of the key was correct.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented),
            Encoding.UTF8.GetBytes(configured));
    }

    private static bool IsLoopback(IPAddress? address)
    {
        if (address == null)
            return false;

        // A v4 client arriving on a dual-stack socket shows up as ::ffff:127.0.0.1, which
        // IPAddress.IsLoopback does not recognise on its own.
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return IPAddress.IsLoopback(address);
    }
}
