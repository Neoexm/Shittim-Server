using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;

namespace Shittim_Server.Services;

public class EchelonService
{
    public static EchelonDBServer GetEchelon(SchaleDataContext context, EchelonDBServer echelonData)
    {
        return context.Echelons.FirstOrDefault(e =>
            e.AccountServerId == echelonData.AccountServerId &&
            e.EchelonType == echelonData.EchelonType &&
            e.EchelonNumber == echelonData.EchelonNumber &&
            e.ExtensionType == echelonData.ExtensionType
        );
    }

    public static EchelonPresetDBServer GetEchelonPreset(SchaleDataContext context, long accountId, EchelonPresetDBServer echelonData)
    {
        return context.EchelonPresets.FirstOrDefault(e =>
            e.AccountServerId == accountId &&
            e.ExtensionType == echelonData.ExtensionType &&
            e.Index == echelonData.Index && 
            e.GroupIndex == echelonData.GroupIndex
        );
    }
    
    public static EchelonPresetGroupDBServer GetEchelonPresetGroup(SchaleDataContext context, long accountId, EchelonPresetDBServer echelonData)
    {
        return context.EchelonPresetGroups.FirstOrDefault(e =>
            e.AccountServerId == accountId &&
            e.ExtensionType == echelonData.ExtensionType &&
            e.GroupIndex == echelonData.GroupIndex
        );
    }

    public static async Task<EchelonDBServer?> GetConcentratedCampaignEchelon(
        SchaleDataContext context, 
        long accountId, 
        long echelonNum)
    {
        return await context.Echelons
            .FirstOrDefaultAsync(e =>
                e.AccountServerId == accountId &&
                e.EchelonType == EchelonType.Adventure &&
                e.EchelonNumber == echelonNum &&
                e.ExtensionType == EchelonExtensionType.Base
            );
    }

    public static EchelonDBServer GetArenaAttackEchelon(SchaleDataContext context, long accountId)
    {
        return context.Echelons.OrderBy(e => e.ServerId).LastOrDefault(e =>
            e.AccountServerId == accountId &&
            e.EchelonType == EchelonType.ArenaAttack &&
            e.EchelonNumber == 1 &&
            e.ExtensionType == EchelonExtensionType.Base
        );
    }
    
    public static EchelonDBServer GetArenaDefenseEchelon(SchaleDataContext context, long accountId)
    {
        return context.Echelons.OrderBy(x => x.ServerId).LastOrDefault(x =>
            x.AccountServerId == accountId &&
            x.EchelonType == EchelonType.ArenaDefence &&
            x.EchelonNumber == 1 &&
            x.ExtensionType == EchelonExtensionType.Base
        );
    }
}
