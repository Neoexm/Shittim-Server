using Plana.FlatData;

namespace Plana.Excel
{
    public static class WorldRaidBossGroupExcelExt
    {
        public static WorldRaidBossGroupExcelT GetWorldRaidBossGroupById(this List<WorldRaidBossGroupExcelT> worldRaidBossGroupExcels, long groupId)
        {
            return worldRaidBossGroupExcels.First(x => x.WorldRaidBossGroupId == groupId);
        }
    }
}