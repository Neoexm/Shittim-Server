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
            existing.LeaderUniqueId = presetGroup.LeaderUniqueId;
            existing.TSSInteractionUniqueId = 0;
            existing.StrikerUniqueIds = presetGroup.StrikerUniqueIds;
            existing.SpecialUniqueIds = presetGroup.SpecialUniqueIds;
            existing.CombatStyleIndex = presetGroup.CombatStyleIndex;
            existing.MulliganUniqueIds = presetGroup.MulliganUniqueIds;

            await context.SaveChangesAsync();

            var echelonPresetGroup = EchelonService.GetEchelonPresetGroup(context, account.ServerId, presetGroup);
            echelonPresetGroup.PresetDBs = await context.GetAccountEchelonPresets(account.ServerId)
                .Where(x => x.GroupIndex == presetGroup.GroupIndex && x.ExtensionType == presetGroup.ExtensionType)
                .GroupBy(x => x.Index).Select(g => g.First())
                .ToDictionaryAsync(x => x.Index, x => x);

            await context.SaveChangesAsync();
        }
    }
}
