using Schale.FlatData;

namespace Schale.Excel
{
    public static class AcademyMessangerExcelExt
    {
        public static List<AcademyMessangerExcelT> GetMessangerByCharacterId(this List<AcademyMessangerExcelT> messengers, long characterId)
        {
            var filtered = messengers.Where(msg => msg.CharacterId == characterId);
            return filtered.ToList();
        }
    }
}


