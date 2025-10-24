using Plana.FlatData;

namespace Plana.Excel
{
    public static class WorldRaidSeasonManageExcelExt
    {
        public static WorldRaidSeasonManageExcelT GetWorldRaidSeasonById(this List<WorldRaidSeasonManageExcelT> worldRaidSeasonManageExcels, long seasonId)
        {
            return worldRaidSeasonManageExcels.First(x => x.SeasonId == seasonId);
        }
    }
}