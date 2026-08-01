using System.Text;
using BlueArchiveAPI.Services;
using Schale.FlatData;
using Xunit;

namespace Shittim_Server.Tests;

public class ProbeCurrencyLimitTests
{
    [Fact]
    public void Dump()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Shittim-Server")))
            dir = Path.GetDirectoryName(dir);

        ExcelTableService.DumpedDir = new[]
        {
            Path.Combine(dir!, "Shittim-Server", "Resources", "Dumped"),
            Path.Combine(dir!, "Shittim-Server", "bin", "Debug", "net10.0", "Resources", "Dumped"),
        }.First(Directory.Exists);

        var excels = new ExcelTableService();
        var currencies = excels.GetTable<CurrencyExcelT>();

        var sb = new StringBuilder();
        foreach (var c in currencies)
            sb.AppendLine($"{c.CurrencyType} charge={c.CurrencyAdditionalChargeType}/{c.CurrencyOverChargeType} chargeLimit={c.ChargeLimit} overChargeLimit={c.OverChargeLimit}");

        var stageRewards = excels.GetTable<CampaignStageRewardExcelT>();
        var stages = excels.GetTable<CampaignStageExcelT>();
        var stage = stages.First(x => x.Name != null && x.Name.Contains("Normal_Main"));
        sb.AppendLine($"stage {stage.Id} {stage.Name} rewardGroup={stage.CampaignStageRewardId}");
        foreach (var r in stageRewards.Where(x => x.GroupId == stage.CampaignStageRewardId))
            sb.AppendLine($"  tag={r.RewardTag} type={r.StageRewardParcelType} id={r.StageRewardId} amount={r.StageRewardAmount} prob={r.StageRewardProb} displayed={r.IsDisplayed}");

        Assert.Fail(sb.ToString());
    }
}
