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

        public async Task<EchelonPresetDBServer> SaveEchelonPreset(SchaleDataContext context, AccountDBServer account, EchelonPresetDB presetData)
        {
            var presetGroup = mapper.Map<EchelonPresetDBServer>(presetData);

            // The client sends GroupIndex/-1 and Index/-1 while it has no slot selected - that is
            // what the "add preset" and import flows send. Persisting those sentinels puts the row
            // in a phantom group no list ever reads back, so resolve them to a real slot first.
            if (presetGroup.GroupIndex < 0)
            {
                var firstGroup = context.EchelonPresetGroups
                    .Where(g => g.AccountServerId == account.ServerId && g.ExtensionType == presetGroup.ExtensionType)
                    .OrderBy(g => g.GroupIndex)
                    .Select(g => g.GroupIndex)
                    .FirstOrDefault();
                presetGroup.GroupIndex = firstGroup ?? 0;
            }

            if (presetGroup.Index < 0)
            {
                // The client sends Index/-1 together with the Label of the slot it is currently
                // showing ("Preset 1" and so on), so the label - not a free-slot search - is the
                // identifier for "the preset I am editing". Searching for a free slot instead wrote
                // the import into an unrelated preset and left the card the player was looking at
                // untouched.
                var labelledIndex = context.EchelonPresets
                    .Where(p => p.AccountServerId == account.ServerId
                        && p.ExtensionType == presetGroup.ExtensionType
                        && p.GroupIndex == presetGroup.GroupIndex
                        && p.Label == presetGroup.Label)
                    .Select(p => (int?)p.Index)
                    .FirstOrDefault();

                if (labelledIndex != null)
                {
                    presetGroup.Index = labelledIndex.Value;
                }
                else
                {
                    var used = context.EchelonPresets
                        .Where(p => p.AccountServerId == account.ServerId
                            && p.ExtensionType == presetGroup.ExtensionType
                            && p.GroupIndex == presetGroup.GroupIndex)
                        .Select(p => p.Index)
                        .ToList();

                    var nextIndex = 0;
                    while (used.Contains(nextIndex) && nextIndex < MaxPresetIndex)
                        nextIndex++;

                    presetGroup.Index = nextIndex <= MaxPresetIndex ? nextIndex : 0;
                }
            }

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
                // Label carries a rename: there is no preset-rename protocol, so the client sends
                // Echelon_PresetSave with the new name and the slot it belongs to.
                existing.Label = presetGroup.Label;
                existing.LeaderUniqueId = presetGroup.LeaderUniqueId;
                existing.TSSInteractionUniqueId = 0;
                existing.StrikerUniqueIds = presetGroup.StrikerUniqueIds;
                existing.SpecialUniqueIds = presetGroup.SpecialUniqueIds;
                existing.CombatStyleIndex = presetGroup.CombatStyleIndex;
                existing.MulliganUniqueIds = presetGroup.MulliganUniqueIds;
            }

            await context.SaveChangesAsync();

            var saved = existing ?? presetGroup;
            saved.AccountServerId = account.ServerId;

            var echelonPresetGroup = EchelonService.GetEchelonPresetGroup(context, account.ServerId, saved);
            if (echelonPresetGroup == null)
            {
                // A preset can be saved into a group the account has never persisted; without the
                // group row the dictionary below would dereference null as well.
                echelonPresetGroup = new EchelonPresetGroupDBServer
                {
                    GroupIndex = saved.GroupIndex,
                    ExtensionType = saved.ExtensionType,
                };
                context.AddEchelonPresetGroups(account.ServerId, echelonPresetGroup);
                await context.SaveChangesAsync();
            }

            echelonPresetGroup.PresetDBs = await context.GetAccountEchelonPresets(account.ServerId)
                .Where(x => x.GroupIndex == saved.GroupIndex && x.ExtensionType == saved.ExtensionType)
                .GroupBy(x => x.Index).Select(g => g.First())
                .ToDictionaryAsync(x => x.Index, x => x);

            await context.SaveChangesAsync();

            return saved;
        }

        // Presets are created per group by AccountService as indices 0..4.
        private const int MaxPresetIndex = 4;
    }
}
