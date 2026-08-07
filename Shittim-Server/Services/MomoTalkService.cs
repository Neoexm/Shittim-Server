using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;

namespace BlueArchiveAPI.Services
{
    public static class MomoTalkService
    {
        public static Dictionary<long, List<long>> GetAllFavorSchedules(List<MomoTalkOutLineDBServer> momoTalkOutLines)
        {
            Dictionary<long, List<long>> favorSchedules = new();
            foreach (var outline in momoTalkOutLines)
            {
                favorSchedules[outline.CharacterId] = outline.ScheduleIds;
            }
            return favorSchedules;
        }

        /// <summary>
        /// The group a student's MomoTalk opens on, or 0 while their first conversation is still rank-locked.
        /// AcademyMessangerExcel numbers a student's groups in reading order and only the opening one has nothing
        /// pointing at it, so the lowest id is the entry point; it carries the FavorRankUp condition that unlocks it
        /// (true for 255 of the 256 students in the live table).
        /// </summary>
        public static long OpeningGroup(
            List<AcademyMessangerExcelT> messengers, long characterId, long favorRank)
        {
            var opening = messengers
                .Where(x => x.CharacterId == characterId)
                .GroupBy(x => x.MessageGroupId)
                .OrderBy(g => g.Key)
                .FirstOrDefault();

            if (opening == null || !IsUnlocked(opening, favorRank))
                return 0;

            return opening.Key;
        }

        /// <summary>
        /// The group after <paramref name="currentGroupId"/> when it is a conversation that a rank-up has just
        /// opened, otherwise 0. Ordinary continuations are excluded on purpose: the client walks those itself
        /// through MomoTalk_Read, and pushing them here would spill a conversation the player has not read yet.
        /// </summary>
        public static long RankUnlockedGroup(
            List<AcademyMessangerExcelT> messengers, long currentGroupId, long favorRank)
        {
            var nextGroupId = messengers
                .Where(x => x.MessageGroupId == currentGroupId && x.NextGroupId > 0 && x.NextGroupId != currentGroupId)
                .Select(x => x.NextGroupId)
                .FirstOrDefault();

            if (nextGroupId == 0)
                return 0;

            var next = messengers.Where(x => x.MessageGroupId == nextGroupId).ToList();
            if (next.Count == 0)
                return 0;

            var opening = next.OrderBy(x => x.Id).First();
            if (opening.MessageCondition != AcademyMessageConditions.FavorRankUp
                || favorRank < opening.ConditionValue)
            {
                return 0;
            }

            return nextGroupId;
        }

        /// <summary>
        /// Gives every owned student the outline row the MomoTalk list renders from, and moves a student whose
        /// conversation stopped at a FavorRankUp gate onto the conversation that rank has since opened. Without this
        /// a fresh account has no rows at all, so no conversation can be started, and a conversation that ran into a
        /// gate never resumes - the client has no reason to send another MomoTalk_Read for a group it already read.
        /// The rows are added to the context but not saved - the caller's SaveChangesAsync commits them.
        /// </summary>
        public static void SyncOutlines(
            SchaleDataContext context, AccountDBServer account, List<AcademyMessangerExcelT> messengers)
        {
            // Grouped rather than keyed directly: a duplicate row from older data would throw out of ToDictionary,
            // and this runs on the login path where that would cost the account its session rather than a conversation.
            var outlinesByCharacterDbId = context.GetAccountMomoTalkOutLines(account.ServerId)
                .ToList()
                .GroupBy(x => x.CharacterDBId)
                .ToDictionary(g => g.Key, g => g.First());

            var now = account.GameSettings.ServerDateTime();

            foreach (var character in context.Characters
                         .Where(x => x.AccountServerId == account.ServerId).ToList())
            {
                if (outlinesByCharacterDbId.TryGetValue(character.ServerId, out var outline))
                {
                    var unlocked = RankUnlockedGroup(
                        messengers, outline.LatestMessageGroupId, character.FavorRank);
                    if (unlocked == 0)
                        continue;

                    outline.LatestMessageGroupId = unlocked;
                    outline.ChosenMessageId = null;
                    outline.LastUpdateDate = now;
                    continue;
                }

                var opening = OpeningGroup(messengers, character.UniqueId, character.FavorRank);
                if (opening == 0)
                    continue;

                context.MomoTalkOutLines.Add(new MomoTalkOutLineDBServer
                {
                    AccountServerId = account.ServerId,
                    CharacterDBId = character.ServerId,
                    CharacterId = character.UniqueId,
                    LatestMessageGroupId = opening,
                    LastUpdateDate = now
                });
            }
        }

        private static bool IsUnlocked(IEnumerable<AcademyMessangerExcelT> group, long favorRank)
        {
            var opening = group.OrderBy(x => x.Id).First();
            return opening.MessageCondition != AcademyMessageConditions.FavorRankUp
                || favorRank >= opening.ConditionValue;
        }
    }
}
