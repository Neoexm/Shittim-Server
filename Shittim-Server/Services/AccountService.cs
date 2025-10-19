using BlueArchiveAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Numerics;

namespace BlueArchiveAPI.Services
{
    public class AccountService
    {
        private readonly BAContext _context;
        private readonly ILogger<AccountService> _logger;

        public AccountService(BAContext context, ILogger<AccountService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public string ExtractSteamIdFromPlatformToken(string? token)
        {
            if (string.IsNullOrEmpty(token))
                return "76561198260711461";

            try
            {
                // Token is a hex string - convert to bytes first
                byte[] tokenBytes;
                try
                {
                    tokenBytes = Convert.FromHexString(token);
                }
                catch
                {
                    // Not a valid hex string, try parsing as-is
                    tokenBytes = Encoding.UTF8.GetBytes(token);
                }

                // Convert bytes to string to search for Steam ID
                var tokenString = Encoding.UTF8.GetString(tokenBytes);
                
                // Steam IDs are 17-digit numbers starting with 765611
                var steamIdPattern = @"765611\d{11}";
                var match = Regex.Match(tokenString, steamIdPattern);
                
                if (match.Success)
                {
                    var steamId = match.Value;
                    _logger.LogInformation($"Extracted Steam ID from token: {steamId}");
                    return steamId;
                }

                // Also try searching in the hex string directly
                match = Regex.Match(token, steamIdPattern);
                if (match.Success)
                {
                    var steamId = match.Value;
                    _logger.LogInformation($"Extracted Steam ID from hex token: {steamId}");
                    return steamId;
                }

                _logger.LogWarning("Could not parse Steam ID from token, using default");
                return "76561198260711461";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting Steam ID");
                return "76561198260711461";
            }
        }

        public string ExtractUserIdFromTicket(string? ticket, string gid)
        {
            var steamId = ExtractSteamIdFromTicket(ticket);
            if (!string.IsNullOrEmpty(steamId))
                return $"steam:{steamId}";

            if (!string.IsNullOrEmpty(ticket))
            {
                var ticketHash = ComputeSha256Hash(ticket).Substring(0, 16);
                return $"ticket:{ticketHash}";
            }

            var randomId = Guid.NewGuid().ToString().Substring(0, 8);
            return $"anon:{randomId}";
        }

        public string? ExtractSteamIdFromTicket(string? ticket)
        {
            if (string.IsNullOrEmpty(ticket))
                return null;

            try
            {
                var steamIdPattern = @"765611\d{11}";
                var match = Regex.Match(ticket, steamIdPattern);
                
                if (match.Success)
                    return match.Value;
            }
            catch { }

            return null;
        }

        public (string platformUserId, string guid, string user64) DeriveIdsFromToken(string tokenOrKey, string gid)
        {
            var hash = ComputeSha256Hash(tokenOrKey + gid);
            
            var platformUserId = BigInteger.Parse("0" + hash.Substring(0, 15), System.Globalization.NumberStyles.HexNumber).ToString();
            var guid = BigInteger.Parse("0" + hash.Substring(16, 15), System.Globalization.NumberStyles.HexNumber).ToString();
            var user64 = BigInteger.Parse("0" + hash.Substring(32, 15), System.Globalization.NumberStyles.HexNumber).ToString();
            
            return (platformUserId, guid, user64);
        }

        public async Task<(User account, string userKey, bool isNew)> GetOrCreateAccount(string? ticket, string gid = "2079")
        {
            var userKey = ExtractUserIdFromTicket(ticket, gid);
            
            var account = await _context.Users.FirstOrDefaultAsync(u => u.UserKey == userKey);
            
            if (account != null)
            {
                account.LastLogin = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                account.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Existing account login: {account.Nickname} (ID: {userKey})");
                return (account, userKey, false);
            }
            else
            {
                var (platformUserId, guid, user64) = DeriveIdsFromToken(ticket ?? userKey, gid);
                var steamId = ExtractSteamIdFromTicket(ticket);
                
                var newAccount = new User
                {
                    UserKey = userKey,
                    Gid = gid,
                    Guid = guid,
                    NpSN = guid,
                    UmKey = $"107:{platformUserId}",
                    PlatformType = userKey.Contains("steam") ? "STEAM" : "ARENA",
                    PlatformUserId = platformUserId,
                    SteamId = steamId,
                    PublisherAccountId = steamId ?? platformUserId,
                    Nickname = $"User{platformUserId.Substring(Math.Max(0, platformUserId.Length - 6))}",
                    Level = 1,
                    Attribute = "[]",
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    LastLogin = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    IsNew = true,
                    IsGuest = false,
                    NeedsNameSetup = false,
                    ExtraData = "{}"
                };
                
                _context.Users.Add(newAccount);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"NEW account created: {newAccount.Nickname} (ID: {userKey})");
                return (newAccount, userKey, true);
            }
        }

        public async Task<User?> GetAccountByUserKey(string userKey)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.UserKey == userKey);
        }

        public async Task<bool> AccountExists(string userKey)
        {
            return await _context.Users.AnyAsync(u => u.UserKey == userKey);
        }

        public async Task<User> CreateGuestAccount(string steamId, string platform, string platformToken)
        {
            var userKey = $"steam:{steamId}";
            var (platformUserId, guid, user64) = DeriveIdsFromToken(platformToken ?? userKey, "2079");
            
            var guestAccount = new User
            {
                UserKey = userKey,
                Gid = "2079",
                Guid = guid,
                NpSN = guid,
                UmKey = $"107:{platformUserId}",
                PlatformType = platform,
                PlatformUserId = platformUserId,
                SteamId = steamId,
                PublisherAccountId = steamId,
                Nickname = $"Guest{platformUserId.Substring(Math.Max(0, platformUserId.Length - 6))}",
                Level = 1,
                Attribute = "[]",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                LastLogin = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                IsNew = true,
                IsGuest = true,
                NeedsNameSetup = true,
                ExtraData = "{}"
            };
            
            _context.Users.Add(guestAccount);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"Guest account created: {guestAccount.Nickname} (Steam ID: {steamId})");
            return guestAccount;
        }

        private string ComputeSha256Hash(string rawData)
        {
            using var sha256Hash = SHA256.Create();
            var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            var builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }
}
