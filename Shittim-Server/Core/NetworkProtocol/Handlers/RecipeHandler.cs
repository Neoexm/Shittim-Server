using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class RecipeHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly ParcelHandler _parcelHandler;

    public RecipeHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        ParcelHandler parcelHandler) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _parcelHandler = parcelHandler;
    }

    [ProtocolHandler(Protocol.Recipe_Craft)]
    public async Task<RecipeCraftResponse> Craft(
        SchaleDataContext db,
        RecipeCraftRequest request,
        RecipeCraftResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var craft = _excelService.GetTable<RecipeCraftExcelT>().FirstOrDefault(x => x.Id == request.RecipeCraftUniqueId)
            ?? throw new WebAPIException(WebAPIErrorCode.RecipeCraftNoData,
                $"Recipe craft {request.RecipeCraftUniqueId} not found");

        var ingredient = _excelService.GetTable<RecipeIngredientExcelT>()
                .FirstOrDefault(x => x.Id == craft.RecipeIngredientId)
            ?? throw new WebAPIException(WebAPIErrorCode.RecipeCraftDataError,
                $"Recipe ingredient {craft.RecipeIngredientId} not found");

        if (request.RecipeIngredientUniqueId != 0 && request.RecipeIngredientUniqueId != ingredient.Id)
            throw new WebAPIException(WebAPIErrorCode.RecipeCraftDataError,
                $"Ingredient {request.RecipeIngredientUniqueId} does not belong to craft {craft.Id}");

        var costs = new List<ParcelResult>();
        for (int i = 0; i < ShopHandler.AlignedColumnCount(
                 ingredient.CostParcelType?.Count, ingredient.CostId?.Count, ingredient.CostAmount?.Count); i++)
            costs.Add(new ParcelResult(ingredient.CostParcelType![i], ingredient.CostId![i], ingredient.CostAmount![i]));
        for (int i = 0; i < ShopHandler.AlignedColumnCount(
                 ingredient.IngredientParcelType?.Count, ingredient.IngredientId?.Count, ingredient.IngredientAmount?.Count); i++)
            costs.Add(new ParcelResult(ingredient.IngredientParcelType![i], ingredient.IngredientId![i], ingredient.IngredientAmount![i]));

        if (costs.Count > 0)
            await _parcelHandler.BuildParcel(db, account, costs, isConsume: true);

        var rewards = new List<ParcelResult>();
        var rewardCount = ShopHandler.AlignedColumnCount(craft.ParcelType?.Count, craft.ParcelId?.Count, craft.ResultAmountMin?.Count);
        if (craft.ResultAmountMax?.Count != rewardCount)
            throw new WebAPIException(WebAPIErrorCode.RecipeCraftDataError,
                $"Ragged result columns on recipe craft {craft.Id}");
        for (int i = 0; i < rewardCount; i++)
        {
            var amount = Random.Shared.NextInt64(craft.ResultAmountMin![i], craft.ResultAmountMax[i] + 1);
            if (amount > 0)
                rewards.Add(new ParcelResult(craft.ParcelType![i], craft.ParcelId![i], amount));
        }

        var resolver = await _parcelHandler.BuildParcel(db, account, rewards);
        response.ParcelResultDB = resolver.ParcelResult;

        return response;
    }
}
