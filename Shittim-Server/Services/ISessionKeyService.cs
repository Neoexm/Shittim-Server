using Schale.MX.NetworkProtocol;
using Schale.Data;
using Schale.Data.GameModel;

namespace BlueArchiveAPI.Services
{
    public interface ISessionKeyService
    {
        Task<SessionKey?> GenerateSession(long publisherAccountId, string? customToken = null);
        bool ValidateRequest(RequestPacket request);
        Task<AccountDBServer> GetAuthenticatedUser(SchaleDataContext context, SessionKey? sessionKey);
        void RevokeSession(long userId);
        int PurgeExpiredSessions(TimeSpan maxInactivity);
    }
}
