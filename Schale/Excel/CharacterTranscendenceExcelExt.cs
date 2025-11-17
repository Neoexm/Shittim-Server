using Schale.FlatData;

namespace Schale.Excel
{
    public static class CharacterTranscendenceExcelExt
    {
        public static CharacterTranscendenceExcelT GetCharacterTranscendenceExcelByCharacterId(
            this List<CharacterTranscendenceExcelT> transcendences, long characterId) =>
            transcendences.First(t => t.CharacterId == characterId);
    }
}


