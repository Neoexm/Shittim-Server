using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Shittim_Server.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.MX.GameLogic.DBModel;

namespace Shittim_Server.Managers
{
    public class EchelonManager(IMapper _mapper)
    {
        private readonly IMapper mapper = _mapper;

        public async Task SaveEchelon(SchaleDataContext context, AccountDBServer account, EchelonDB echelonData)
        {
            var echelon = mapper.Map<EchelonDBServer>(echelonData);
            var existing = EchelonService.GetEchelon(context, echelon);
            if (existing != null)
                context.Echelons.Remove(existing);
            context.AddEchelons(account.ServerId, [echelon]);

            await context.SaveChangesAsync();
        }

        public async Task SaveEchelonPreset(SchaleDataContext context, AccountDBServer account, EchelonPresetDB presetData)
        {
            var presetGroup = mapper.Map<EchelonPresetDBServer>(presetData);
            var existing = EchelonService.GetEchelonPreset(context, account.ServerId, presetGroup);

            if (existing == null)
            {
                // Echelon_PresetSave is also what the client calls when adding a preset, and on that
                // path the row does not exist yet. Treating it as an update dereferenced null.
                presetGroup.AccountServerId = account.ServerId;
                context.EchelonPresets.Add(presetGroup);
            }
            else
            {
                existing.LeaderUniqueId = presetGroup.LeaderUniqueId;
                existing.TSSInteractionUniqueId = 0;
                existing.StrikerUniqueIds = presetGroup.StrikerUniqueIds;
                existing.SpecialUniqueIds = presetGroup.SpecialUniqueIds;
                existing.CombatStyleIndex = presetGroup.CombatStyleIndex;
                existing.MulliganUniqueIds = presetGroup.MulliganUniqueIds;
            }

            await context.SaveChangesAsync();

            var echelonPresetGroup = EchelonService.GetEchelonPresetGroup(context, account.ServerId, presetGroup);
            if (echelonPresetGroup == null)
            {
                // A preset can be saved into a group the account has never persisted; without the
                // group row the dictionary below would dereference null as well.
                echelonPresetGroup = new EchelonPresetGroupDBServer
                {
                    GroupIndex = presetGroup.GroupIndex,
                    ExtensionType = presetGroup.ExtensionType,
                };
                context.AddEchelonPresetGroups(account.ServerId, echelonPresetGroup);
                await context.SaveChangesAsync();
            }

            echelonPresetGroup.PresetDBs = await context.GetAccountEchelonPresets(account.ServerId)
                .Where(x => x.GroupIndex == presetGroup.GroupIndex && x.ExtensionType == presetGroup.ExtensionType)
                .GroupBy(x => x.Index).Select(g => g.First())
                .ToDictionaryAsync(x => x.Index, x => x);

            await context.SaveChangesAsync();
        }
    }
}
