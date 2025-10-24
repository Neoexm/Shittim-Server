// Models needed: CampaignStageHistory, CampaignChapterReward, StrategyObjectHistory
// BAContext additions: DbSet<CampaignStageHistory>, DbSet<CampaignChapterReward>, DbSet<StrategyObjectHistory>
// Migrations: dotnet ef migrations add AddCampaignTables

using BlueArchiveAPI.Models;
using BlueArchiveAPI.NetworkModels;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlueArchiveAPI.Handlers
{
    public static class Campaign
    {
        /// <summary>
        /// Returns campaign progress and history
        /// Protocol: Campaign_List
        /// </summary>
        public class List : BaseHandler<CampaignListRequest, CampaignListResponse>
        {
            private readonly BAContext _dbContext;

            public List(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<CampaignListResponse> Handle(CampaignListRequest request)
            {
                var stageHistories = await _dbContext.CampaignStageHistories
                    .AsNoTracking()
                    .Where(h => h.AccountServerId == request.SessionKey.AccountServerId)
                    .ToListAsync();

                var chapterRewards = await _dbContext.CampaignChapterRewards
                    .AsNoTracking()
                    .Where(r => r.AccountServerId == request.SessionKey.AccountServerId)
                    .ToListAsync();

                var strategyObjects = await _dbContext.StrategyObjectHistories
                    .AsNoTracking()
                    .Where(s => s.AccountServerId == request.SessionKey.AccountServerId)
                    .ToListAsync();

                return new CampaignListResponse
                {
                    CampaignChapterClearRewardHistoryDBs = chapterRewards.Select(r => new CampaignChapterClearRewardHistoryDB
                    {
                        AccountServerId = r.AccountServerId,
                        ChapterUniqueId = r.ChapterUniqueId,
                        RewardType = (StageDifficulty)r.RewardType,
                        ReceiveDate = r.ReceiveDate
                    }).ToList(),
                    StageHistoryDBs = stageHistories.Select(h => new CampaignStageHistoryDB
                    {
                        AccountServerId = h.AccountServerId,
                        StoryUniqueId = h.StoryUniqueId,
                        ChapterUniqueId = h.ChapterUniqueId,
                        StageUniqueId = h.StageUniqueId,
                        TacticClearCountWithRankSRecord = h.TacticClearCountWithRankSRecord,
                        ClearTurnRecord = h.ClearTurnRecord,
                        Star1Flag = h.Star1Flag,
                        Star2Flag = h.Star2Flag,
                        Star3Flag = h.Star3Flag,
                        LastPlay = h.LastPlay,
                        TodayPlayCount = h.TodayPlayCount,
                        TodayPurchasePlayCountHardStage = h.TodayPurchasePlayCountHardStage,
                        FirstClearRewardReceive = h.FirstClearRewardReceive,
                        StarRewardReceive = h.StarRewardReceive,
                        TodayPlayCountForUI = h.TodayPlayCountForUI
                    }).ToList(),
                    StrategyObjecthistoryDBs = strategyObjects.Select(s => new StrategyObjectHistoryDB
                    {
                        AccountId = s.AccountServerId,
                        StrategyObjectId = s.StrategyObjectId
                    }).ToList(),
                    DailyResetCountDB = new DailyResetCountDB
                    {
                        AccountServerId = request.SessionKey.AccountServerId,
                        ResetCount = new Dictionary<ResetContentType, long>()
                    }
                };
            }
        }

        /// <summary>
        /// Enters a sub-stage (simple battle stage)
        /// Protocol: Campaign_EnterSubStage
        /// </summary>
        public class EnterSubStage : BaseHandler<CampaignEnterSubStageRequest, CampaignEnterSubStageResponse>
        {
            private readonly BAContext _dbContext;

            public EnterSubStage(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<CampaignEnterSubStageResponse> Handle(CampaignEnterSubStageRequest request)
            {
                // TODO: Load stage entrance cost from CampaignStageExcel
                // TODO: Consume entrance fee (AP usually)
                
                return new CampaignEnterSubStageResponse
                {
                    ParcelResultDB = new ParcelResultDB(), // TODO: Build from consumed entrance fee
                    SaveDataDB = new CampaignSubStageSaveDB
                    {
                        AccountServerId = request.SessionKey.AccountServerId,
                        CreateTime = DateTime.UtcNow,
                        StageUniqueId = request.StageUniqueId,
                        LastEnterStageEchelonNumber = request.LastEnterStageEchelonNumber,
                        StageEntranceFee = new List<ParcelInfo>() // TODO: Calculate from stage Excel
                    }
                };
            }
        }

        /// <summary>
        /// Processes sub-stage battle result
        /// Protocol: Campaign_SubStageResult
        /// </summary>
        public class SubStageResult : BaseHandler<CampaignSubStageResultRequest, CampaignSubStageResultResponse>
        {
            private readonly BAContext _dbContext;

            public SubStageResult(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<CampaignSubStageResultResponse> Handle(CampaignSubStageResultRequest request)
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                // Check if player won
                bool isVictory = request.Summary.EndType == BattleEndType.Clear;

                if (!isVictory)
                {
                    // Retreat - return 90% of entrance fee
                    return new CampaignSubStageResultResponse
                    {
                        TacticRank = 0,
                        CampaignStageHistoryDB = new CampaignStageHistoryDB(),
                        LevelUpCharacterDBs = new List<CharacterDB>(),
                        ParcelResultDB = new ParcelResultDB(), // TODO: Refund 90% of AP
                        FirstClearReward = new List<ParcelInfo>()
                    };
                }

                // Victory - record history
                var existing = await _dbContext.CampaignStageHistories
                    .FirstOrDefaultAsync(h => h.AccountServerId == user.Id 
                                           && h.StageUniqueId == request.Summary.StageId);

                bool isFirstClear = existing == null;

                if (existing == null)
                {
                    // TODO: Get ChapterUniqueId from CampaignStageExcel
                    existing = new CampaignStageHistory
                    {
                        AccountServerId = user.Id,
                        StageUniqueId = request.Summary.StageId,
                        ChapterUniqueId = 1, // TODO: Load from Excel
                        StoryUniqueId = 1, // TODO: Load from Excel
                        Star1Flag = true,
                        Star2Flag = true,
                        Star3Flag = true,
                        TacticClearCountWithRankSRecord = 1,
                        ClearTurnRecord = 1,
                        LastPlay = DateTime.UtcNow,
                        TodayPlayCount = 1,
                        TodayPlayCountForUI = 1
                    };
                    _dbContext.CampaignStageHistories.Add(existing);
                }
                else
                {
                    existing.LastPlay = DateTime.UtcNow;
                    existing.TodayPlayCount++;
                    existing.TodayPlayCountForUI++;
                }

                await _dbContext.SaveChangesAsync();

                // TODO: Load rewards from CampaignStageRewardExcel
                // TODO: Give account exp
                // TODO: Give stage clear rewards

                return new CampaignSubStageResultResponse
                {
                    TacticRank = 0,
                    CampaignStageHistoryDB = new CampaignStageHistoryDB
                    {
                        AccountServerId = existing.AccountServerId,
                        StoryUniqueId = existing.StoryUniqueId,
                        ChapterUniqueId = existing.ChapterUniqueId,
                        StageUniqueId = existing.StageUniqueId,
                        TacticClearCountWithRankSRecord = existing.TacticClearCountWithRankSRecord,
                        ClearTurnRecord = existing.ClearTurnRecord,
                        Star1Flag = existing.Star1Flag,
                        Star2Flag = existing.Star2Flag,
                        Star3Flag = existing.Star3Flag,
                        LastPlay = existing.LastPlay,
                        TodayPlayCount = existing.TodayPlayCount,
                        TodayPurchasePlayCountHardStage = existing.TodayPurchasePlayCountHardStage,
                        FirstClearRewardReceive = existing.FirstClearRewardReceive,
                        StarRewardReceive = existing.StarRewardReceive,
                        TodayPlayCountForUI = existing.TodayPlayCountForUI
                    },
                    LevelUpCharacterDBs = new List<CharacterDB>(), // TODO: Calculate leveled characters
                    ParcelResultDB = new ParcelResultDB(), // TODO: Build rewards
                    FirstClearReward = isFirstClear ? new List<ParcelInfo>() : new List<ParcelInfo>()
                };
            }
        }

        /// <summary>
        /// Enters tutorial stage
        /// Protocol: Campaign_EnterTutorialStage
        /// </summary>
        public class EnterTutorialStage : BaseHandler<CampaignEnterTutorialStageRequest, CampaignEnterTutorialStageResponse>
        {
            protected override async Task<CampaignEnterTutorialStageResponse> Handle(CampaignEnterTutorialStageRequest request)
            {
                return new CampaignEnterTutorialStageResponse
                {
                    ParcelResultDB = new ParcelResultDB(),
                    SaveDataDB = new CampaignTutorialStageSaveDB
                    {
                        AccountServerId = request.SessionKey.AccountServerId,
                        CreateTime = DateTime.UtcNow,
                        StageUniqueId = request.StageUniqueId,
                        StageEntranceFee = new List<ParcelInfo>()
                    }
                };
            }
        }

        /// <summary>
        /// Processes tutorial stage result
        /// Protocol: Campaign_TutorialStageResult
        /// </summary>
        public class TutorialStageResult : BaseHandler<CampaignTutorialStageResultRequest, CampaignTutorialStageResultResponse>
        {
            private readonly BAContext _dbContext;

            public TutorialStageResult(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<CampaignTutorialStageResultResponse> Handle(CampaignTutorialStageResultRequest request)
            {
                bool isVictory = request.Summary.EndType == BattleEndType.Clear;

                if (!isVictory)
                {
                    return new CampaignTutorialStageResultResponse
                    {
                        CampaignStageHistoryDB = new CampaignStageHistoryDB(),
                        ParcelResultDB = new ParcelResultDB(),
                        ClearReward = new List<ParcelInfo>(),
                        FirstClearReward = new List<ParcelInfo>()
                    };
                }

                // Record tutorial completion
                var existing = await _dbContext.CampaignStageHistories
                    .FirstOrDefaultAsync(h => h.AccountServerId == request.SessionKey.AccountServerId 
                                           && h.StageUniqueId == request.Summary.StageId);

                if (existing == null)
                {
                    existing = new CampaignStageHistory
                    {
                        AccountServerId = request.SessionKey.AccountServerId,
                        StageUniqueId = request.Summary.StageId,
                        ChapterUniqueId = 1,
                        StoryUniqueId = 1,
                        Star1Flag = true,
                        Star2Flag = true,
                        Star3Flag = true,
                        ClearTurnRecord = 1,
                        LastPlay = DateTime.UtcNow,
                        TodayPlayCount = 1
                    };
                    _dbContext.CampaignStageHistories.Add(existing);
                    await _dbContext.SaveChangesAsync();
                }

                return new CampaignTutorialStageResultResponse
                {
                    CampaignStageHistoryDB = new CampaignStageHistoryDB
                    {
                        AccountServerId = existing.AccountServerId,
                        StageUniqueId = existing.StageUniqueId,
                        ChapterUniqueId = existing.ChapterUniqueId,
                        Star1Flag = existing.Star1Flag,
                        Star2Flag = existing.Star2Flag,
                        Star3Flag = existing.Star3Flag,
                        ClearTurnRecord = existing.ClearTurnRecord,
                        LastPlay = existing.LastPlay
                    },
                    ParcelResultDB = new ParcelResultDB(), // TODO: Add tutorial rewards
                    ClearReward = new List<ParcelInfo>(),
                    FirstClearReward = new List<ParcelInfo>()
                };
            }
        }

        /// <summary>
        /// Claims chapter clear rewards
        /// Protocol: Campaign_ChapterClearReward
        /// </summary>
        public class ChapterClearReward : BaseHandler<CampaignChapterClearRewardRequest, CampaignChapterClearRewardResponse>
        {
            private readonly BAContext _dbContext;

            public ChapterClearReward(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<CampaignChapterClearRewardResponse> Handle(CampaignChapterClearRewardRequest request)
            {
                // Check if reward already claimed
                var existing = await _dbContext.CampaignChapterRewards
                    .FirstOrDefaultAsync(r => r.AccountServerId == request.SessionKey.AccountServerId
                                           && r.ChapterUniqueId == request.CampaignChapterUniqueId
                                           && r.RewardType == (int)request.StageDifficulty);

                if (existing != null)
                {
                    // Already claimed
                    return new CampaignChapterClearRewardResponse
                    {
                        CampaignChapterClearRewardHistoryDB = new CampaignChapterClearRewardHistoryDB
                        {
                            AccountServerId = existing.AccountServerId,
                            ChapterUniqueId = existing.ChapterUniqueId,
                            RewardType = (StageDifficulty)existing.RewardType,
                            ReceiveDate = existing.ReceiveDate
                        },
                        ParcelResultDB = new ParcelResultDB()
                    };
                }

                // Add new reward claim
                var rewardHistory = new CampaignChapterReward
                {
                    AccountServerId = request.SessionKey.AccountServerId,
                    ChapterUniqueId = request.CampaignChapterUniqueId,
                    RewardType = (int)request.StageDifficulty,
                    ReceiveDate = DateTime.UtcNow
                };

                _dbContext.CampaignChapterRewards.Add(rewardHistory);
                await _dbContext.SaveChangesAsync();

                // TODO: Load rewards from CampaignChapterRewardExcel
                // TODO: Give rewards via parcel system

                return new CampaignChapterClearRewardResponse
                {
                    CampaignChapterClearRewardHistoryDB = new CampaignChapterClearRewardHistoryDB
                    {
                        AccountServerId = rewardHistory.AccountServerId,
                        ChapterUniqueId = rewardHistory.ChapterUniqueId,
                        RewardType = (StageDifficulty)rewardHistory.RewardType,
                        ReceiveDate = rewardHistory.ReceiveDate
                    },
                    ParcelResultDB = new ParcelResultDB() // TODO: Build reward parcels
                };
            }
        }

        /// <summary>
        /// Retreats from a campaign stage (gives back 90% of entrance fee)
        /// Protocol: Campaign_Retreat
        /// </summary>
        public class Retreat : BaseHandler<CampaignRetreatRequest, CampaignRetreatResponse>
        {
            private readonly BAContext _dbContext;

            public Retreat(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<CampaignRetreatResponse> Handle(CampaignRetreatRequest request)
            {
                // TODO: Load stage entrance cost from CampaignStageExcel
                // TODO: Refund 90% of entrance cost (usually AP)
                
                return new CampaignRetreatResponse
                {
                    ReleasedEchelonNumbers = new List<long>(),
                    ParcelResultDB = new ParcelResultDB() // TODO: Refund 90% of AP
                };
            }
        }

        /// <summary>
        /// Restarts a main stage (strategy map)
        /// Protocol: Campaign_RestartMainStage
        /// </summary>
        public class RestartMainStage : BaseHandler<CampaignRestartMainStageRequest, CampaignRestartMainStageResponse>
        {
            protected override async Task<CampaignRestartMainStageResponse> Handle(CampaignRestartMainStageRequest request)
            {
                // TODO: Load stage entrance cost
                // TODO: Consume entrance fee
                
                return new CampaignRestartMainStageResponse
                {
                    ParcelResultDB = new ParcelResultDB(),
                    SaveDataDB = new CampaignMainStageSaveDB
                    {
                        AccountServerId = request.SessionKey.AccountServerId,
                        CreateTime = DateTime.UtcNow,
                        StageUniqueId = request.StageUniqueId,
                        StageEntranceFee = new List<ParcelInfo>()
                    }
                };
            }
        }
    }
}
