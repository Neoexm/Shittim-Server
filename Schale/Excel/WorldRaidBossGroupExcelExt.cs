using Schale.FlatData;

namespace Schale.Excel
{
    public static class WorldRaidBossGroupExcelExt
    {
        public static WorldRaidBossGroupExcelT GetWorldRaidBossGroupById(
            this List<WorldRaidBossGroupExcelT> bossGroups, long groupId) =>
            bossGroups.First(group => group.WorldRaidBossGroupId == groupId);
    }
}


