// File: Shittim-Server/Handlers/Echelon.cs
// Handlers: List, Save, PresetList, PresetSave, PresetGroupRename
// Models needed: Echelon, EchelonPreset, EchelonPresetGroup

using BlueArchiveAPI.NetworkModels;
using BlueArchiveAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlueArchiveAPI.Handlers
{
    public static class Echelon
    {
        /// <summary>
        /// Handler for Echelon_List protocol.
        /// Returns all echelons (team configurations) for the account.
        /// </summary>
        public class List : BaseHandler<EchelonListRequest, EchelonListResponse>
        {
            private readonly BAContext _dbContext;

            public List(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<EchelonListResponse> Handle(EchelonListRequest request)
            {
                var user = await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                var echelons = await _dbContext.Echelons
                    .AsNoTracking()
                    .Where(e => e.AccountServerId == user.Id)
                    .ToListAsync();

                return new EchelonListResponse
                {
                    EchelonDBs = echelons.Select(e => new EchelonDB
                    {
                        AccountServerId = e.AccountServerId,
                        EchelonType = (EchelonType)e.EchelonType,
                        EchelonNumber = e.EchelonNumber,
                        LeaderServerId = e.LeaderServerId,
                        MainSlotServerIds = string.IsNullOrEmpty(e.MainSlotServerIds)
                            ? new List<long>()
                            : JsonSerializer.Deserialize<List<long>>(e.MainSlotServerIds) ?? new List<long>(),
                        SupportSlotServerIds = string.IsNullOrEmpty(e.SupportSlotServerIds)
                            ? new List<long>()
                            : JsonSerializer.Deserialize<List<long>>(e.SupportSlotServerIds) ?? new List<long>(),
                        TSSInteractionServerId = e.TSSServerId,  // Map from database TSSServerId to network TSSInteractionServerId
                        UsingFlag = (EchelonStatusFlag)e.UsingFlag,
                        SkillCardMulliganCharacterIds = string.IsNullOrEmpty(e.SkillCardMulliganCharacterIds)
                            ? new List<long>()
                            : JsonSerializer.Deserialize<List<long>>(e.SkillCardMulliganCharacterIds) ?? new List<long>()
                    }).ToList(),
                    ArenaEchelonDB = new EchelonDB() // Empty arena echelon for now
                };
            }
        }

        /// <summary>
        /// Handler for Echelon_Save protocol.
        /// Saves/updates an echelon configuration.
        /// </summary>
        public class Save : BaseHandler<EchelonSaveRequest, EchelonSaveResponse>
        {
            private readonly BAContext _dbContext;

            public Save(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<EchelonSaveResponse> Handle(EchelonSaveRequest request)
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                // Find existing echelon
                var existing = await _dbContext.Echelons
                    .FirstOrDefaultAsync(e => 
                        e.AccountServerId == user.Id &&
                        e.EchelonType == (int)request.EchelonDB.EchelonType &&
                        e.EchelonNumber == request.EchelonDB.EchelonNumber);

                if (existing != null)
                {
                    // Update existing echelon
                    existing.LeaderServerId = request.EchelonDB.LeaderServerId;
                    existing.MainSlotServerIds = JsonSerializer.Serialize(request.EchelonDB.MainSlotServerIds ?? new List<long>());
                    existing.SupportSlotServerIds = JsonSerializer.Serialize(request.EchelonDB.SupportSlotServerIds ?? new List<long>());
                    existing.TSSServerId = request.EchelonDB.TSSInteractionServerId;  // Map from network TSSInteractionServerId to database TSSServerId
                    existing.UsingFlag = (int)request.EchelonDB.UsingFlag;
                    existing.SkillCardMulliganCharacterIds = JsonSerializer.Serialize(request.EchelonDB.SkillCardMulliganCharacterIds ?? new List<long>());
                    _dbContext.Echelons.Update(existing);
                }
                else
                {
                    // Create new echelon
                    var newEchelon = new Models.Echelon
                    {
                        AccountServerId = user.Id,
                        EchelonType = (int)request.EchelonDB.EchelonType,
                        EchelonNumber = request.EchelonDB.EchelonNumber,
                        LeaderServerId = request.EchelonDB.LeaderServerId,
                        MainSlotServerIds = JsonSerializer.Serialize(request.EchelonDB.MainSlotServerIds ?? new List<long>()),
                        SupportSlotServerIds = JsonSerializer.Serialize(request.EchelonDB.SupportSlotServerIds ?? new List<long>()),
                        TSSServerId = request.EchelonDB.TSSInteractionServerId,  // Map from network TSSInteractionServerId to database TSSServerId
                        UsingFlag = (int)request.EchelonDB.UsingFlag,
                        SkillCardMulliganCharacterIds = JsonSerializer.Serialize(request.EchelonDB.SkillCardMulliganCharacterIds ?? new List<long>())
                    };
                    _dbContext.Echelons.Add(newEchelon);
                }

                await _dbContext.SaveChangesAsync();

                return new EchelonSaveResponse
                {
                    EchelonDB = request.EchelonDB
                };
            }
        }

        /// <summary>
        /// Handler for Echelon_PresetList protocol.
        /// Returns all saved echelon presets for the account.
        /// </summary>
        public class PresetList : BaseHandler<EchelonPresetListRequest, EchelonPresetListResponse>
        {
            private readonly BAContext _dbContext;

            public PresetList(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<EchelonPresetListResponse> Handle(EchelonPresetListRequest request)
            {
                var user = await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                // Get preset groups for the specified extension type
                var presetGroups = await _dbContext.EchelonPresetGroups
                    .AsNoTracking()
                    .Where(g => g.AccountServerId == user.Id && g.ExtensionType == request.EchelonExtensionType)
                    .ToListAsync();

                // Get all presets for these groups
                var presets = await _dbContext.EchelonPresets
                    .AsNoTracking()
                    .Where(p => p.AccountServerId == user.Id && p.ExtensionType == request.EchelonExtensionType)
                    .ToListAsync();

                // Build preset group DBs
                var presetGroupDBs = new List<EchelonPresetGroupDB>();
                foreach (var group in presetGroups)
                {
                    var groupPresets = presets
                        .Where(p => p.GroupIndex == group.GroupIndex)
                        .ToDictionary(p => p.Index, p => new EchelonPresetDB
                        {
                            GroupIndex = p.GroupIndex,
                            Index = p.Index,
                            Label = p.Label,
                            LeaderUniqueId = p.LeaderUniqueId,
                            StrikerUniqueIds = string.IsNullOrEmpty(p.StrikerUniqueIds)
                                ? new long[] { }
                                : JsonSerializer.Deserialize<long[]>(p.StrikerUniqueIds) ?? new long[] { },
                            SpecialUniqueIds = string.IsNullOrEmpty(p.SpecialUniqueIds)
                                ? new long[] { }
                                : JsonSerializer.Deserialize<long[]>(p.SpecialUniqueIds) ?? new long[] { },
                            MulliganUniqueIds = string.IsNullOrEmpty(p.MulliganUniqueIds)
                                ? new List<long>()
                                : JsonSerializer.Deserialize<List<long>>(p.MulliganUniqueIds) ?? new List<long>()
                        });

                    presetGroupDBs.Add(new EchelonPresetGroupDB
                    {
                        GroupIndex = group.GroupIndex,
                        GroupLabel = group.GroupLabel,
                        PresetDBs = groupPresets,
                        Item = groupPresets.Values.FirstOrDefault() ?? new EchelonPresetDB()
                    });
                }

                return new EchelonPresetListResponse
                {
                    PresetGroupDBs = presetGroupDBs.ToArray()
                };
            }
        }

        /// <summary>
        /// Handler for Echelon_PresetSave protocol.
        /// Saves an echelon preset configuration.
        /// </summary>
        public class PresetSave : BaseHandler<EchelonPresetSaveRequest, EchelonPresetSaveResponse>
        {
            private readonly BAContext _dbContext;

            public PresetSave(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<EchelonPresetSaveResponse> Handle(EchelonPresetSaveRequest request)
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                // Find or create the preset group
                var presetGroup = await _dbContext.EchelonPresetGroups
                    .FirstOrDefaultAsync(g =>
                        g.AccountServerId == user.Id &&
                        g.GroupIndex == request.PresetDB.GroupIndex &&
                        g.ExtensionType == request.EchelonExtensionType);

                if (presetGroup == null)
                {
                    presetGroup = new EchelonPresetGroup
                    {
                        AccountServerId = user.Id,
                        GroupIndex = request.PresetDB.GroupIndex,
                        ExtensionType = request.EchelonExtensionType,
                        GroupLabel = $"Group {request.PresetDB.GroupIndex}"
                    };
                    _dbContext.EchelonPresetGroups.Add(presetGroup);
                    await _dbContext.SaveChangesAsync();
                }

                // Find or create the preset
                var existing = await _dbContext.EchelonPresets
                    .FirstOrDefaultAsync(p =>
                        p.AccountServerId == user.Id &&
                        p.GroupIndex == request.PresetDB.GroupIndex &&
                        p.Index == request.PresetDB.Index &&
                        p.ExtensionType == request.EchelonExtensionType);

                if (existing != null)
                {
                    // Update existing preset
                    existing.Label = request.PresetDB.Label;
                    existing.LeaderUniqueId = request.PresetDB.LeaderUniqueId;
                    existing.TSSInteractionUniqueId = 0;
                    existing.StrikerUniqueIds = JsonSerializer.Serialize(request.PresetDB.StrikerUniqueIds ?? new long[] { });
                    existing.SpecialUniqueIds = JsonSerializer.Serialize(request.PresetDB.SpecialUniqueIds ?? new long[] { });
                    existing.CombatStyleIndex = "[]"; // TODO: Add CombatStyleIndex to request if needed
                    existing.MulliganUniqueIds = JsonSerializer.Serialize(request.PresetDB.MulliganUniqueIds ?? new List<long>());
                    _dbContext.EchelonPresets.Update(existing);
                }
                else
                {
                    // Create new preset
                    var newPreset = new EchelonPreset
                    {
                        AccountServerId = user.Id,
                        GroupIndex = request.PresetDB.GroupIndex,
                        Index = request.PresetDB.Index,
                        Label = request.PresetDB.Label,
                        ExtensionType = request.EchelonExtensionType,
                        LeaderUniqueId = request.PresetDB.LeaderUniqueId,
                        TSSInteractionUniqueId = 0,
                        StrikerUniqueIds = JsonSerializer.Serialize(request.PresetDB.StrikerUniqueIds ?? new long[] { }),
                        SpecialUniqueIds = JsonSerializer.Serialize(request.PresetDB.SpecialUniqueIds ?? new long[] { }),
                        CombatStyleIndex = "[]",
                        MulliganUniqueIds = JsonSerializer.Serialize(request.PresetDB.MulliganUniqueIds ?? new List<long>())
                    };
                    _dbContext.EchelonPresets.Add(newPreset);
                }

                await _dbContext.SaveChangesAsync();

                return new EchelonPresetSaveResponse
                {
                    PresetDB = request.PresetDB
                };
            }
        }

        /// <summary>
        /// Handler for Echelon_PresetGroupRename protocol.
        /// Renames an echelon preset group.
        /// </summary>
        public class PresetGroupRename : BaseHandler<EchelonPresetGroupRenameRequest, EchelonPresetGroupRenameResponse>
        {
            private readonly BAContext _dbContext;

            public PresetGroupRename(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<EchelonPresetGroupRenameResponse> Handle(EchelonPresetGroupRenameRequest request)
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                var presetGroup = await _dbContext.EchelonPresetGroups
                    .FirstOrDefaultAsync(g =>
                        g.AccountServerId == user.Id &&
                        g.GroupIndex == request.PresetGroupIndex &&
                        g.ExtensionType == request.ExtensionType);

                if (presetGroup != null)
                {
                    presetGroup.GroupLabel = request.PresetGroupLabel;
                    _dbContext.EchelonPresetGroups.Update(presetGroup);
                    await _dbContext.SaveChangesAsync();
                }
                else
                {
                    // Create new preset group if it doesn't exist
                    presetGroup = new EchelonPresetGroup
                    {
                        AccountServerId = user.Id,
                        GroupIndex = request.PresetGroupIndex,
                        ExtensionType = request.ExtensionType,
                        GroupLabel = request.PresetGroupLabel
                    };
                    _dbContext.EchelonPresetGroups.Add(presetGroup);
                    await _dbContext.SaveChangesAsync();
                }

                // Get all presets for this group
                var presets = await _dbContext.EchelonPresets
                    .AsNoTracking()
                    .Where(p => 
                        p.AccountServerId == user.Id && 
                        p.GroupIndex == request.PresetGroupIndex &&
                        p.ExtensionType == request.ExtensionType)
                    .ToListAsync();

                var presetDBs = presets.ToDictionary(p => p.Index, p => new EchelonPresetDB
                {
                    GroupIndex = p.GroupIndex,
                    Index = p.Index,
                    Label = p.Label,
                    LeaderUniqueId = p.LeaderUniqueId,
                    StrikerUniqueIds = string.IsNullOrEmpty(p.StrikerUniqueIds)
                        ? new long[] { }
                        : JsonSerializer.Deserialize<long[]>(p.StrikerUniqueIds) ?? new long[] { },
                    SpecialUniqueIds = string.IsNullOrEmpty(p.SpecialUniqueIds)
                        ? new long[] { }
                        : JsonSerializer.Deserialize<long[]>(p.SpecialUniqueIds) ?? new long[] { },
                    MulliganUniqueIds = string.IsNullOrEmpty(p.MulliganUniqueIds)
                        ? new List<long>()
                        : JsonSerializer.Deserialize<List<long>>(p.MulliganUniqueIds) ?? new List<long>()
                });

                return new EchelonPresetGroupRenameResponse
                {
                    PresetGroupDB = new EchelonPresetGroupDB
                    {
                        GroupIndex = presetGroup.GroupIndex,
                        GroupLabel = presetGroup.GroupLabel,
                        PresetDBs = presetDBs,
                        Item = presetDBs.Values.FirstOrDefault() ?? new EchelonPresetDB()
                    }
                };
            }
        }
    }
}