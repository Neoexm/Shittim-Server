using BlueArchiveAPI.Configuration;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;

namespace Shittim_Server.Services;

// Counters the game resets on a cycle - hard-stage play purchases, strategy-map heals. Official resets them
// daily; here a counter resets by ageing out of ResetableContentResetDays.
// The values live in the account's GameSettings JSON column, so every caller has to follow a mutation with
// db.Accounts.Update(account) - EF does not see changes made inside the column.
public static class ResetableContentService
{
    public static TimeSpan ResetWindow =>
        TimeSpan.FromDays(Config.Instance.ServerConfiguration.ResetableContentResetDays);

    public static List<ResetableContentValueDB> LiveValues(AccountDBServer account, TimeSpan window)
    {
        var now = account.GameSettings.ServerDateTime();
        account.GameSettings.ResetableContents.RemoveAll(x => now - x.LastUpdateTime >= window);
        return account.GameSettings.ResetableContents;
    }

    public static long LiveValue(AccountDBServer account, ResetContentType type, long mapped, TimeSpan window)
    {
        return LiveValues(account, window)
            .FirstOrDefault(x => x.ResetableContentId.Type == type && x.ResetableContentId.Mapped == mapped)
            ?.ContentValue ?? 0;
    }

    public static long Bump(AccountDBServer account, ResetContentType type, long mapped, TimeSpan window, long delta = 1)
    {
        var live = LiveValues(account, window);
        var entry = live.FirstOrDefault(x => x.ResetableContentId.Type == type && x.ResetableContentId.Mapped == mapped);
        if (entry == null)
        {
            entry = new ResetableContentValueDB
            {
                ResetableContentId = new ResetableContentId { Type = type, Mapped = mapped }
            };
            live.Add(entry);
        }

        entry.ContentValue += delta;
        entry.LastUpdateTime = account.GameSettings.ServerDateTime();
        return entry.ContentValue;
    }
}
