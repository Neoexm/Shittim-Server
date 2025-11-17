using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Configuration;
using Schale.MX.NetworkProtocol;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.Models;
using Protocol = Schale.MX.NetworkProtocol.Protocol;

namespace BlueArchiveAPI.Services
{
    public class SessionKeyService : ISessionKeyService
    {
        private readonly ConcurrentDictionary<long, ActiveSession> _activeSessions;
        private readonly IDbContextFactory<SchaleDataContext> _dbFactory;
        
        private static readonly HashSet<Protocol> _publicProtocols = new()
        {
            Protocol.Queuing_GetTicket,
            Protocol.Queuing_GetTicketGL,
            Protocol.Account_CheckYostar,
            Protocol.Account_CheckNexon,
            Protocol.Account_Auth,
            Protocol.Account_Create,
            Protocol.ProofToken_RequestQuestion
        };

        public SessionKeyService(IDbContextFactory<SchaleDataContext> dbFactory)
        {
            _dbFactory = dbFactory;
            _activeSessions = new ConcurrentDictionary<long, ActiveSession>();
        }

        public async Task<SessionKey?> GenerateSession(long publisherAccountId, string? customToken = null)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            
            var account = await db.Accounts
                .FirstOrDefaultAsync(a => a.PublisherAccountId == publisherAccountId);

            if (account == null)
                return null;

            var tokenGuid = string.IsNullOrEmpty(customToken) 
                ? Guid.NewGuid() 
                : Guid.Parse(customToken);

            var session = new ActiveSession
            {
                UserId = account.ServerId,
                Token = tokenGuid,
                CreatedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow
            };

            _activeSessions.AddOrUpdate(account.ServerId, session, (_, _) => session);

            return new SessionKey
            {
                AccountServerId = account.ServerId,
                MxToken = tokenGuid.ToString()
            };
        }

        public bool ValidateRequest(RequestPacket request)
        {
            if (_publicProtocols.Contains(request.Protocol))
                return true;

            if (request.SessionKey == null)
                return false;

            if (Config.Instance.ServerConfiguration.BypassAuthentication)
            {
                var devSession = new ActiveSession
                {
                    UserId = request.AccountId,
                    Token = Guid.Parse(request.SessionKey.MxToken),
                    CreatedAt = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow
                };
                _activeSessions.TryAdd(request.AccountId, devSession);
                return true;
            }

            if (_activeSessions.TryGetValue(request.AccountId, out var session))
            {
                if (session.Token.ToString().Equals(request.SessionKey.MxToken, StringComparison.OrdinalIgnoreCase))
                {
                    session.LastActivity = DateTime.UtcNow;
                    return true;
                }
            }

            return false;
        }

        public async Task<AccountDBServer> GetAuthenticatedUser(SchaleDataContext context, SessionKey? sessionKey)
        {
            if (sessionKey == null)
                throw new UnauthorizedAccessException("Session key is required");

            if (!_activeSessions.TryGetValue(sessionKey.AccountServerId, out var session))
                throw new UnauthorizedAccessException("Session expired or invalid");

            if (!session.Token.ToString().Equals(sessionKey.MxToken, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Session token mismatch");

            session.LastActivity = DateTime.UtcNow;

            var account = await context.Accounts
                .FirstOrDefaultAsync(a => a.ServerId == session.UserId);

            return account ?? throw new UnauthorizedAccessException("User not found");
        }

        public void RevokeSession(long userId)
        {
            _activeSessions.TryRemove(userId, out _);
        }

        public int PurgeExpiredSessions(TimeSpan maxInactivity)
        {
            var cutoff = DateTime.UtcNow - maxInactivity;
            var expiredKeys = _activeSessions
                .Where(kvp => kvp.Value.LastActivity < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            var removed = 0;
            foreach (var key in expiredKeys)
            {
                if (_activeSessions.TryRemove(key, out _))
                    removed++;
            }

            return removed;
        }

        private class ActiveSession
        {
            public required long UserId { get; init; }
            public required Guid Token { get; init; }
            public required DateTime CreatedAt { get; init; }
            public DateTime LastActivity { get; set; }
        }
    }
}
