using Schale.FlatData;

namespace Shittim.Services;

public static class MathService
{
    public static (int, long) CalculateLevelExp(int currentLevel, long currentExp, long addedExp, List<ExpLevelData> expLevelDatas)
    {
        int maxLevel = expLevelDatas.Max(d => d.Level);

        if (currentLevel >= maxLevel)
            return (maxLevel, 0);

        long totalExp = currentExp + addedExp;
        int newLevel = currentLevel;
        long newExp = totalExp;

        while (newLevel < maxLevel && newExp >= expLevelDatas.First(d => d.Level == newLevel).Exp)
        {
            newExp -= expLevelDatas.First(d => d.Level == newLevel).Exp;
            newLevel++;
        }

        if (newLevel >= maxLevel)
            return (maxLevel, 0);

        return (newLevel, newExp);
    }
    
    public static (int, long) CalculateLevelExpWithoutReset(int currentLevel, long currentExp, long addedExp, List<ExpLevelData> expLevelDatas)
    {
        long totalExp = currentExp + addedExp;
        int newLevel = currentLevel;

        while (newLevel < expLevelDatas.Max(d => d.Level) && totalExp >= expLevelDatas.First(d => d.Level == newLevel).Exp)
        {
            totalExp -= expLevelDatas.First(d => d.Level == newLevel).Exp;
            newLevel++;
        }

        if (newLevel >= expLevelDatas.Max(d => d.Level))
            return (expLevelDatas.Max(d => d.Level), totalExp);

        return (newLevel, totalExp);
    }

    public static (int, long, long, long) CalculateAccountExp(int level, long exp, long addedExp, List<AccountLevelExcelT> expLevelDatas)
    {
        var expLevelMap = new ExpLevelData().ConvertExpLevelData(expLevelDatas);
        var currentLevelData = expLevelMap.FirstOrDefault(d => d.Level == level);

        if (currentLevelData == null || addedExp <= 0)
            return (level, exp, 0, 0);

        long bonusExpTotal = 0;

        if (level <= currentLevelData.CloseInterval && currentLevelData.NewbieExpRatio > 0)
        {
            double ratio = currentLevelData.NewbieExpRatio / 10000.0;
            bonusExpTotal = (long)(addedExp * ratio);
        }

        long addedBonusExp = addedExp + bonusExpTotal;
        var (finalLevel, finalTotalExp) = CalculateLevelExp(level, exp, addedBonusExp, expLevelMap);

        if (finalLevel > level)
        {
            long totalAPBonus = 0;
            for (int i = level + 1; i <= finalLevel; i++)
            {
                var levelData = expLevelMap.FirstOrDefault(d => d.Level == i);
                if (levelData != null)
                    totalAPBonus += levelData.APAutoChargeMax;
            }
            return (finalLevel, finalTotalExp, bonusExpTotal, totalAPBonus);
        }

        return (finalLevel, finalTotalExp, bonusExpTotal, 0);
    }

    public static long CalculateTimeScore(float duration, long multiplier)
    {
        return (long)((3600f - duration) * (multiplier / 10));
    }

    public static long CalculateTADScore(float duration, TimeAttackDungeonGeasExcelT taDungeonData)
    {
        long totalBattleDuration = taDungeonData.BattleDuration / 1000;
        long timeWeightConst = taDungeonData.TimeWeightConst / 10000;

        return (long)Math.Floor(taDungeonData.ClearTimeWeightPoint * (1 - duration / (totalBattleDuration + (timeWeightConst * duration))));
    }

    public static List<T> GetRandomList<T>(List<T> list, int limit = 0)
    {
        int n = list.Count;

        while (n > 1)
        {
            n--;
            int k = Random.Shared.Next(n + 1);
            (list[n], list[k]) = (list[k], list[n]);
        }
        if (limit <= 0) return list;

        return list.GetRange(0, Math.Min(limit, list.Count));
    }

    public static bool GenerateProbability(long probability)
    {
        if (probability == 0) return true;
        return Random.Shared.Next(10000) < probability;
    }

    public static IEnumerable<long> NumberPadding(IEnumerable<long> src, int limit)
        => src.Concat(Enumerable.Repeat(0L, limit)).Take(limit);
}

public class ExpLevelData
{
    public int Level { get; set; }
    public long TotalExp { get; set; }
    public long Exp { get; set; }

    public long NewbieExpRatio { get; set; }
    public long CloseInterval { get; set; }
    public long APAutoChargeMax { get; set; }

    public List<ExpLevelData> ConvertExpLevelData(List<AccountLevelExcelT> accountLevelExcel)
    {
        long totalExp = 0;
        return accountLevelExcel
            .Select(x =>
            {
                totalExp += x.Exp;
                return new ExpLevelData
                {
                    Level = (int)x.Level,
                    Exp = x.Exp,
                    TotalExp = totalExp,
                    NewbieExpRatio = x.NewbieExpRatio,
                    CloseInterval = x.CloseInterval,
                    APAutoChargeMax = x.APAutoChargeMax
                };
            }).ToList();
    }

    public List<ExpLevelData> ConvertExpLevelData(List<CharacterLevelExcelT> characterLevelExcel)
    {
        return characterLevelExcel
            .Select(x =>
                new ExpLevelData
                {
                    Level = x.Level,
                    Exp = x.Exp,
                    TotalExp = x.TotalExp
                })
            .ToList();
    }

    public List<ExpLevelData> ConvertExpLevelData(List<CharacterWeaponLevelExcelT> characterWeaponLevelExcel)
    {
        return characterWeaponLevelExcel
            .Select(x =>
                new ExpLevelData
                {
                    Level = x.Level,
                    Exp = x.Exp,
                    TotalExp = x.TotalExp
                })
            .ToList();
    }

    public List<ExpLevelData> ConvertExpLevelData(List<FavorLevelExcelT> favorLevelExcel)
    {
        long totalExp = 0;
        return favorLevelExcel
            .Select(x =>
            {
                totalExp += x.ExpType[0];
                return new ExpLevelData
                {
                    Level = (int)x.Level,
                    Exp = x.ExpType[0],
                    TotalExp = totalExp
                };
            })
            .ToList();
    }

    public List<ExpLevelData> ConvertExpLevelData(List<AcademyLocationRankExcelT> academyLocationRankExcel)
    {
        return academyLocationRankExcel
            .Select(x =>
                new ExpLevelData
                {
                    Level = (int)x.Rank,
                    Exp = x.RankExp,
                    TotalExp = x.TotalExp,
                })
            .ToList();
    }

    public List<ExpLevelData> ConvertExpLevelData(List<EquipmentLevelExcelT> equipmentLevelExcels)
    {
        return equipmentLevelExcels
            .Select(x =>
            {
                return new ExpLevelData
                {
                    Level = x.Level,
                    Exp = x.TierLevelExp.Last(),
                    TotalExp = x.TotalExp.Last()
                };
            })
            .ToList();
    }
}
