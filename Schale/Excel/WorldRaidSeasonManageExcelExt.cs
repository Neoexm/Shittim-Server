using Schale.FlatData;

namespace Schale.Excel
{
    public static class WorldRaidSeasonManageExcelExt
    {
        public static WorldRaidSeasonManageExcelT GetWorldRaidSeasonById(
            this List<WorldRaidSeasonManageExcelT> seasons, long seasonId) =>
            seasons.First(season => season.SeasonId == seasonId);
    }
}


