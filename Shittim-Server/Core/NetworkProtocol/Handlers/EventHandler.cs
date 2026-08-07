using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class EventHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;

    public EventHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    // 25000 and 25001 have no caller left in the client - the lobby banner strip is fed by Management_BannerList instead. Answering them keeps a stray call from surfacing as ErrorCode 500.
    [ProtocolHandler(Protocol.Event_GetList)]
    public async Task<EventListResponse> GetList(
        SchaleDataContext db,
        EventListRequest request,
        EventListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.EventInfoDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.Event_GetImage)]
    public async Task<EventImageResponse> GetImage(
        SchaleDataContext db,
        EventImageRequest request,
        EventImageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ImageBytes = null;

        return response;
    }

    // the client only routes a serial here when it is not a Yostar CDKey (those are StartsWith("G") && Length == 15 and go to the web API), and we issue none of our own.
    [ProtocolHandler(Protocol.Event_UseCoupon)]
    public async Task<UseCouponResponse> UseCoupon(
        SchaleDataContext db,
        UseCouponRequest request,
        UseCouponResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        if (string.IsNullOrWhiteSpace(request.CouponSerial))
            throw new WebAPIException(WebAPIErrorCode.CouponIsEmpty, "coupon serial was empty");

        throw new WebAPIException(WebAPIErrorCode.UseCouponNotFoundSerials, $"no coupon issued for serial {request.CouponSerial}");
    }

    [ProtocolHandler(Protocol.Event_RewardIncrease)]
    public async Task<EventRewardIncreaseResponse> RewardIncrease(
        SchaleDataContext db,
        EventRewardIncreaseRequest request,
        EventRewardIncreaseResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.EventRewardIncreaseDBs = [];
        if (account.GameSettings.EnableMultiFloorRaid)
            response.ServerTimeTicks = MultiFloorRaidHandler.MultiFloorRaidDateTime.Ticks;

        return response;
    }
}
