using Schale.Data.GameModel;
using Schale.FlatData;

namespace BlueArchiveAPI.Services
{
    public static class AcademyService
    {
        public static Dictionary<long, List<VisitingCharacterDBServer>> CreateRandomVisitor(
            List<CharacterDBServer> characters, List<AcademyZoneExcelT> academyZoneExcels, List<long> characterIds)
        {
            var characterLookup = characters.ToDictionary(c => c.UniqueId, c => c);
            var random = Random.Shared;
            
            return academyZoneExcels.ToDictionary(
                x => x.Id,
                x =>
                {
                    var availableCharacterIds = new List<long>(characterIds);
                    var visitingCharacters = new List<VisitingCharacterDBServer>();
                    var studentVisitProbs = x.StudentVisitProb;
                    
                    for (int i = 0; i < studentVisitProbs.Count; i++)
                    {
                        if (!availableCharacterIds.Any())
                            break;

                        var probability = studentVisitProbs[i];
                        if (random.Next(10000) < probability)
                        {
                            var randomCharacterIndex = random.Next(availableCharacterIds.Count);
                            var characterId = availableCharacterIds[randomCharacterIndex];
                            availableCharacterIds.RemoveAt(randomCharacterIndex);
                            
                            var visitingCharacter = new VisitingCharacterDBServer
                            {
                                UniqueId = characterId,
                                ServerId = characterLookup.TryGetValue(characterId, out var existingChar)
                                    ? existingChar.ServerId
                                    : 0
                            };
                            visitingCharacters.Add(visitingCharacter);
                        }
                    }
                    return visitingCharacters;
                }
            );
        }
    }
}
