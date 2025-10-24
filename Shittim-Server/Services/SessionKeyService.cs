using BlueArchiveAPI.Models;
using BlueArchiveAPI.NetworkModels;
using Microsoft.EntityFrameworkCore;

namespace BlueArchiveAPI.Services
{
    /// <summary>
    /// Service for retrieving accounts from session keys
    /// </summary>
    public class SessionKeyService
    {
        private readonly BAContext _context;

        public SessionKeyService(BAContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gets account from session key, throws if not found
        /// </summary>
        public User GetAccount(BAContext context, SessionKey sessionKey)
        {
            if (sessionKey == null)
                throw new ArgumentNullException(nameof(sessionKey));

            var account = context.Users
                .AsNoTracking()
                .FirstOrDefault(u => u.Id == sessionKey.AccountServerId);

            if (account == null)
                throw new InvalidOperationException($"Account not found for SessionKey.AccountServerId: {sessionKey.AccountServerId}");

            return account;
        }
    }

    public static class SessionKeyServiceExtensions
    {
        public static void AddSessionKeyService(this IServiceCollection services)
        {
            services.AddScoped<SessionKeyService>();
        }
    }
}