using Plana.FlatData;

namespace Plana.Excel
{
    public static class AcademyMessangerExcelExt
    {
        public static List<AcademyMessangerExcelT> GetMessangerByCharacterId(this List<AcademyMessangerExcelT> messanger, long characterId)
        {
            return messanger.Where(x => x.CharacterId == characterId).ToList();
        }
    }
}