namespace Schale.MX.None
{
    public class MiniGameShootingSummary
    {
        public void AddGeas(long id) {}
        public void KillEnemy(long id) {}

        public long EventContentId;
        public long StageId;
        public long PlayerCharacterId;
        public List<long>? GeasIds;
        public long SectionCount;
        public long ArriveSection;
        public float LeftTimeSec;
        public float ProgressedTimeSec;
        public Dictionary<long, int>? KillEnemies;
        public bool IsWin;
    }

    public class MinigameRhythmSummary : IEquatable<MinigameRhythmSummary>
    {
        public MinigameRhythmSummary(string musicTitle, int patternDifficulty, bool isSpecial, int totalNoteCount, int criticalCount, int attackCount, int missCount, bool isFullCombo, int maxCombo, long finalScore, long hpBonusScore, DateTime gameStartTime, DateTime gameEndTime, float rhythmGamePlayTime, float stdDev, bool isAutoPlay, MinigameJudgeRecord[] minigameJudgeRecords)
        {
            this.MusicTitle = musicTitle;
            this.PatternDifficulty = patternDifficulty;
            this.IsSpecial = isSpecial;
            this.TotalNoteCount = totalNoteCount;
            this.CriticalCount = criticalCount;
            this.AttackCount = attackCount;
            this.MissCount = missCount;
            this.IsFullCombo = isFullCombo;
            this.MaxCombo = maxCombo;
            this.FinalScore = finalScore;
            this.HPBonusScore = hpBonusScore;
            this.GameStartTime = gameStartTime;
            this.GameEndTime = gameEndTime;
            this.RhythmGamePlayTime = rhythmGamePlayTime;
            this.StdDev = stdDev;
            this.MinigameJudgeRecords = minigameJudgeRecords;
            this.IsAutoPlay = isAutoPlay;
        }

        public bool Equals(MinigameRhythmSummary? other)
        {
            return other != null && this.MusicTitle == other.MusicTitle;
        }

        public string MusicTitle;
        public int PatternDifficulty;
        public bool IsSpecial;
        public int TotalNoteCount;
        public int CriticalCount;
        public int AttackCount;
        public int MissCount;
        public bool IsFullCombo;
        public int MaxCombo;
        public long FinalScore;
        public long HPBonusScore;
        public DateTime GameStartTime;
        public DateTime GameEndTime;
        public float RhythmGamePlayTime;
        public float StdDev;
        public MinigameJudgeRecord[] MinigameJudgeRecords;
        public bool IsAutoPlay;
    }

    public class MinigameJudgeRecord
    {
        public MinigameJudgeRecord(int noteIndex, float timingError, int currentCombo, JudgeGrade judgeGrade, bool isFeverOn)
        {
            NoteIndex = noteIndex;
            TimingError = timingError;
            CurrentCombo = currentCombo;
            JudgeGradeOfThisNote = judgeGrade;
            IsFeverOn = isFeverOn;
        }

        public int NoteIndex;
        public float TimingError;
        public int CurrentCombo;
        public JudgeGrade JudgeGradeOfThisNote;
        public bool IsFeverOn;
    }

    public enum JudgeGrade
	{
		None,
		Miss,
		Attack,
		Critical
	}
}



