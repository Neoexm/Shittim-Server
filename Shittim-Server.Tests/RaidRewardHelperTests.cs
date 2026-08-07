using Schale.FlatData;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

public class RaidRewardHelperTests
{
    [Fact]
    public void ClaimableRewardsFollowTheGauge()
    {
        var rewardIds = new List<long> { 10, 20, 30 };
        var thresholds = new List<long> { 100, 500, 1000 };

        Assert.Equal([10, 20], RaidService.ClaimableSeasonRewardIds(rewardIds, thresholds, 600, []));
        Assert.Equal([20], RaidService.ClaimableSeasonRewardIds(rewardIds, thresholds, 600, [10]));
        Assert.Empty(RaidService.ClaimableSeasonRewardIds(rewardIds, thresholds, 50, []));
        Assert.Equal([10, 20, 30], RaidService.ClaimableSeasonRewardIds(rewardIds, thresholds, 1000, []));
    }

    [Fact]
    public void ClaimableRewardsAreBoundedByTheShorterColumn()
    {
        var rewardIds = new List<long> { 10, 20, 30 };
        var thresholds = new List<long> { 100 };

        Assert.Equal([10], RaidService.ClaimableSeasonRewardIds(rewardIds, thresholds, 9999, []));
        Assert.Empty(RaidService.ClaimableSeasonRewardIds(null, thresholds, 9999, []));
        Assert.Empty(RaidService.ClaimableSeasonRewardIds(rewardIds, null, 9999, []));
    }

    [Fact]
    public void ParcelColumnsZipWhenAligned()
    {
        var parcels = RaidService.ZipParcelColumns(
            [ParcelType.Currency, ParcelType.Item],
            [1, 2],
            [100, 200]);

        Assert.Equal(2, parcels.Count);
        Assert.Equal(ParcelType.Currency, parcels[0].Type);
        Assert.Equal(1, parcels[0].Id);
        Assert.Equal(100, parcels[0].Amount);
        Assert.Equal(ParcelType.Item, parcels[1].Type);
        Assert.Equal(200, parcels[1].Amount);
    }

    [Fact]
    public void RaggedParcelColumnsAreRejected()
    {
        Assert.Throws<WebAPIException>(() =>
            RaidService.ZipParcelColumns([ParcelType.Currency, ParcelType.Item], [1], [100, 200]));
    }

    [Fact]
    public void StageRewardRollKeepsGuaranteedRowsAndTheirParcels()
    {
        var rows = new List<RaidStageRewardExcelT>
        {
            new() { ClearStageRewardProb = 0, ClearStageRewardParcelType = ParcelType.Currency, ClearStageRewardParcelUniqueID = 5, ClearStageRewardAmount = 50 },
            new() { ClearStageRewardProb = 10000, ClearStageRewardParcelType = ParcelType.Item, ClearStageRewardParcelUniqueID = 7, ClearStageRewardAmount = 3 },
        };

        var rolled = RaidService.RollStageRewards(rows);

        Assert.Equal(2, rolled.Count);
        Assert.Equal(ParcelType.Currency, rolled[0].Type);
        Assert.Equal(5, rolled[0].Id);
        Assert.Equal(50, rolled[0].Amount);
        Assert.Equal(ParcelType.Item, rolled[1].Type);
    }
}
