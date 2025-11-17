using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.FlatData;

namespace Schale.Data.GameModel
{
    public class SchoolDungeonStageHistoryDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [JsonIgnore]
        [Key]
        public long ServerId { get; set; }

        public long StageUniqueId { get; set; }
        public long BestStarRecord { get; set; }
        public bool[] StarFlags { get; set; }
        public bool Star1Flag { get; set; }
        public bool Star2Flag { get; set; }
        public bool Star3Flag { get; set; }
        public bool IsClearedEver { get; set; }

        public SchoolDungeonStageHistoryDBServer() { }

        public SchoolDungeonStageHistoryDBServer(long accountId, SchoolDungeonStageExcelT excel)
        {
            AccountServerId = accountId;
            StageUniqueId = excel.StageId;
            StarFlags = [false, false, false];
            Star1Flag = false;
            Star2Flag = false;
            Star3Flag = false;
            IsClearedEver = false;
        }
    }

    public static class SchoolDungeonStageHistoryDBServerExtension
    {
        public static IQueryable<SchoolDungeonStageHistoryDBServer> GetAccountSchoolDungeonStageHistories(this SchaleDataContext context, long accountId)
        {
            return context.SchoolDungeonStageHistories.Where(x => x.AccountServerId == accountId);
        }
    }
}


