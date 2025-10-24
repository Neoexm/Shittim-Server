using Plana.FlatData;

namespace Plana.Excel
{
    public static class CharacterTranscendenceExcelExt
    {
        public static CharacterTranscendenceExcelT GetCharacterTranscendenceExcelByCharacterId(this List<CharacterTranscendenceExcelT> character, long characterId)
        {
            return character.First(x => x.CharacterId == characterId);
        }
    }
}