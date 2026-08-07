using System.Text;
using BlueArchiveAPI.Services;
using Schale.FlatData;
using Xunit;

namespace Shittim_Server.Tests;

// Started life as a dump of CurrencyExcel and one stage's reward group. The two things it was actually being read for are asserted now: AP is the only currency that auto-charges past its limit, and every stage reward group carries its FirstClear/ThreeStar rows at prob 10000 in among the Default drops, which is why rolling the whole group hands out the clear bonus on every sweep.
public class ProbeCurrencyLimitTests
{
    [Fact]
    public void ActionPointIsTheOnlyCurrencyThatChargesPastItsLimit()
    {
        var excels = Excels();
        var currencies = excels.GetTable<CurrencyExcelT>();

        var ap = currencies.Single(x => x.CurrencyType == CurrencyTypes.ActionPoint);
        Assert.Equal(CurrencyAdditionalChargeType.EnableAutoChargeOverLimit, ap.CurrencyAdditionalChargeType);
        Assert.Equal(999, ap.OverChargeLimit);

        var autoCharging = currencies.Where(x => x.CurrencyAdditionalChargeType == CurrencyAdditionalChargeType.EnableAutoChargeOverLimit).ToList();
        Assert.Equal([CurrencyTypes.ActionPoint], autoCharging.Select(x => x.CurrencyType));
    }

    [Fact]
    public void AStageRewardGroupCarriesItsClearBonusAlongsideTheDrops()
    {
        var excels = Excels();
        var stages = excels.GetTable<CampaignStageExcelT>();
        var stage = stages.First(x => x.Name != null && x.Name.Contains("Normal_Main"));
        var rows = excels.GetTable<CampaignStageRewardExcelT>().Where(x => x.GroupId == stage.CampaignStageRewardId).ToList();

        var sb = new StringBuilder();
        foreach (var r in rows)
            sb.AppendLine($"  tag={r.RewardTag} type={r.StageRewardParcelType} id={r.StageRewardId} amount={r.StageRewardAmount} prob={r.StageRewardProb} displayed={r.IsDisplayed}");

        Assert.True(rows.Any(x => x.RewardTag == RewardTag.FirstClear), $"no FirstClear row in group {stage.CampaignStageRewardId}\n{sb}");
        Assert.True(rows.Any(x => x.RewardTag == RewardTag.Default), $"no Default row in group {stage.CampaignStageRewardId}\n{sb}");

        // The bonus rows sit at certainty, so anything that rolls the group as a whole pays them out every time.
        Assert.All(rows.Where(x => x.RewardTag != RewardTag.Default), x => Assert.Equal(10000, x.StageRewardProb));

        // Undisplayed rows are the GachaGroup entries; raw excel renders them as blank cells.
        Assert.All(rows.Where(x => !x.IsDisplayed), x => Assert.Equal(ParcelType.GachaGroup, x.StageRewardParcelType));
    }

    private static ExcelTableService Excels()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Shittim-Server")))
            dir = Path.GetDirectoryName(dir);

        ExcelTableService.DumpedDir = new[]
        {
            Path.Combine(dir!, "Shittim-Server", "Resources", "Dumped"),
            Path.Combine(dir!, "Shittim-Server", "bin", "Debug", "net10.0", "Resources", "Dumped"),
            Path.Combine(dir!, "Shittim-Server", "bin", "Release", "net10.0", "Resources", "Dumped"),
        }.First(Directory.Exists);

        return new ExcelTableService();
    }
}
