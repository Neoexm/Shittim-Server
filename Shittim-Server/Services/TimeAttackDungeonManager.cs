using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Logic.Battles.Summary;
using BlueArchiveAPI.Services;

namespace Shittim_Server.Services
{
    public class TimeAttackDungeonManager
    {
        private readonly ExcelTableService _excelService;
        private readonly IMapper _mapper;

        public TimeAttackDungeonManager(ExcelTableService excelService, IMapper mapper)
        {
            _excelService = excelService;
            _mapper = mapper;
        }

        public DateTime GetTADTimeTicks(AccountDBServer account)
        {
            var seasonExcel = _excelService.GetTable<TimeAttackDungeonSeasonManageExcelT>();
            var season = seasonExcel.FirstOrDefault(x => x.Id == account.ContentInfo.TimeAttackDungeonDataInfo.SeasonId);
            
            if (season != null && !string.IsNullOrEmpty(season.StartDate))
            {
                return DateTime.Parse(season.StartDate);
            }
            
            return account.GameSettings.ServerDateTime();
        }

        public TimeAttackDungeonGeasExcelT GetTADGeas(long stageId)
        {
            var geasExcel = _excelService.GetTable<TimeAttackDungeonGeasExcelT>();
            return geasExcel.FirstOrDefault(x => x.Id == stageId);
        }

        public List<TimeAttackDungeonGeasExcelT> GetTADGeas()
        {
            return _excelService.GetTable<TimeAttackDungeonGeasExcelT>();
        }

        public Dictionary<long, TimeAttackDungeonRoomDB> GetLobby(SchaleDataContext context, AccountDBServer account)
        {
            var room = context.GetAccountTimeAttackDungeonRooms(account.ServerId).FirstOrDefault();
            
            if (room != null)
            {
                return new Dictionary<long, TimeAttackDungeonRoomDB> 
                { 
                    { room.RoomId, room.ToMap(_mapper) } 
                };
            }
            
            return new Dictionary<long, TimeAttackDungeonRoomDB>();
        }

        public TimeAttackDungeonRoomDBServer GetRoom(SchaleDataContext context, AccountDBServer account)
        {
            return context.GetAccountTimeAttackDungeonRooms(account.ServerId).FirstOrDefault();
        }

        public async Task<TimeAttackDungeonRoomDBServer> GetPreviousRoom(SchaleDataContext context, AccountDBServer account)
        {
            var historyCount = context.GetAccountTimeAttackDungeonBattleHistoryDBs(account.ServerId).Count();
            
            if (historyCount == 3)
            {
                var room = GetRoom(context, account);
                if (room != null)
                {
                    room.RewardDate = GetTADTimeTicks(account);
                    context.TimeAttackDungeonRooms.Remove(room);
                    
                    var histories = context.GetAccountTimeAttackDungeonBattleHistoryDBs(account.ServerId).ToList();
                    context.TimeAttackDungeonBattleHistories.RemoveRange(histories);
                    
                    await context.SaveChangesAsync();
                    return room;
                }
            }
            
            return null;
        }

        public async Task<TimeAttackDungeonRoomDBServer> CreateBattle(SchaleDataContext context, AccountDBServer account, bool isPractice)
        {
            var room = GetRoom(context, account);
            
            if (room == null)
            {
                room = new TimeAttackDungeonRoomDBServer
                {
                    RoomId = account.ServerId,
                    AccountId = account.ServerId,
                    SeasonId = account.ContentInfo.TimeAttackDungeonDataInfo.SeasonId,
                    CreateDate = GetTADTimeTicks(account),
                    IsPractice = isPractice,
                    BattleHistoryDBs = new List<TimeAttackDungeonBattleHistoryDBServer>()
                };
                context.TimeAttackDungeonRooms.Add(room);
                await context.SaveChangesAsync();
            }
            
            return GetRoom(context, account);
        }

        public async Task<TimeAttackDungeonRoomDBServer> BattleResult(SchaleDataContext context, AccountDBServer account, BattleSummary battleSummary)
        {
            var room = GetRoom(context, account);
            if (room == null)
            {
                return null;
            }

            var targetGeas = GetTADGeas(battleSummary.StageId);
            if (targetGeas == null)
            {
                return room;
            }

            var battleHistory = CreateBattleHistory(room, targetGeas, battleSummary);
            battleHistory.AccountServerId = account.ServerId;

            context.TimeAttackDungeonBattleHistories.Add(battleHistory);
            
            if (room.BattleHistoryDBs == null)
            {
                room.BattleHistoryDBs = new List<TimeAttackDungeonBattleHistoryDBServer>();
            }
            room.BattleHistoryDBs.Add(battleHistory);

            var totalAllPoint = CalculateScoreRecord(room, GetTADGeas());
            var seasonBestRecord = account.ContentInfo.TimeAttackDungeonDataInfo.SeasonBestRecord;
            
            if (seasonBestRecord < totalAllPoint)
            {
                account.ContentInfo.TimeAttackDungeonDataInfo.SeasonBestRecord = totalAllPoint;
            }

            await context.SaveChangesAsync();
            return room;
        }

        public async Task<TimeAttackDungeonRoomDBServer> GiveUp(SchaleDataContext context, AccountDBServer account)
        {
            var tempData = GetRoom(context, account);
            if (tempData != null)
            {
                tempData.RewardDate = GetTADTimeTicks(account);
                
                context.TimeAttackDungeonRooms.RemoveRange(context.GetAccountTimeAttackDungeonRooms(account.ServerId));
                context.TimeAttackDungeonBattleHistories.RemoveRange(context.GetAccountTimeAttackDungeonBattleHistoryDBs(account.ServerId));
                
                await context.SaveChangesAsync();
            }
            
            return tempData;
        }

        private TimeAttackDungeonBattleHistoryDBServer CreateBattleHistory(
            TimeAttackDungeonRoomDBServer room,
            TimeAttackDungeonGeasExcelT targetGeas,
            BattleSummary battleSummary)
        {
            var battleHistory = new TimeAttackDungeonBattleHistoryDBServer
            {
                DungeonType = targetGeas.TimeAttackDungeonType,
                GeasId = targetGeas.Id,
                DefaultPoint = targetGeas.ClearDefaultPoint,
                ClearTimePoint = CalculateTADScore(battleSummary.EndFrame / 30f, targetGeas),
                EndFrame = battleSummary.EndFrame,
                MainCharacterDBs = ConvertHeroSummaryToCollection(battleSummary.Group01Summary?.Heroes),
                SupportCharacterDBs = ConvertHeroSummaryToCollection(battleSummary.Group01Summary?.Supporters)
            };

            battleHistory.TotalPoint = battleHistory.DefaultPoint + battleHistory.ClearTimePoint;

            return battleHistory;
        }

        private List<TimeAttackDungeonCharacterDBServer> ConvertHeroSummaryToCollection(HeroSummaryCollection heroSummary)
        {
            if (heroSummary == null) return new List<TimeAttackDungeonCharacterDBServer>();

            return heroSummary.Select(x => new TimeAttackDungeonCharacterDBServer
            {
                ServerId = x.ServerId,
                UniqueId = x.CharacterId,
                StarGrade = x.Grade,
                Level = x.Level,
                HasWeapon = x.CharacterWeapon != null,
                WeaponDB = x.CharacterWeapon == null ? null : new WeaponDBServer
                {
                    UniqueId = x.CharacterWeapon.Value.UniqueId,
                    Level = x.CharacterWeapon.Value.Level
                }
            }).ToList();
        }

        private long CalculateScoreRecord(TimeAttackDungeonRoomDBServer room, List<TimeAttackDungeonGeasExcelT> geasExcel)
        {
            if (room.BattleHistoryDBs == null) return 0;

            long totalPoint = 0;
            foreach (var history in room.BattleHistoryDBs)
            {
                var geasData = geasExcel.FirstOrDefault(x => x.Id == history.GeasId);
                if (geasData != null)
                {
                    var timePoint = CalculateTADScore(history.EndFrame / 30f, geasData);
                    totalPoint += geasData.ClearDefaultPoint + timePoint;
                }
            }

            return totalPoint;
        }

        private long CalculateTADScore(float clearTimeInSeconds, TimeAttackDungeonGeasExcelT geasData)
        {
            if (geasData == null) return 0;

            var timeLimitInSeconds = 180;
            if (timeLimitInSeconds <= 0) return 0;

            var remainingTime = timeLimitInSeconds - clearTimeInSeconds;
            if (remainingTime <= 0) return 0;

            var timeRatio = remainingTime / timeLimitInSeconds;
            var maxTimePoint = 10000;

            return (long)(timeRatio * maxTimePoint);
        }
    }
}
