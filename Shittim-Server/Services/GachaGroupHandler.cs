using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;
using Shittim_Server.Services;

namespace Shittim_Server.Services;

public class GachaGroupHandler
{
    private readonly List<GachaElementExcelT> _gachaElementExcels;

    public GachaGroupHandler(List<GachaElementExcelT> gachaElementExcels)
    {
        _gachaElementExcels = gachaElementExcels;
    }

    public List<ParcelResult> CreateGachaGroupParcel(List<ParcelResult> gachaParcels)
    {
        var parcelResults = new List<ParcelResult>();
        foreach (var parcel in gachaParcels)
        {
            var gachaElementList = _gachaElementExcels.GetGachaElementsByGroupId(parcel.Id);
            if (gachaElementList.Count == 0) continue;

            int totalWeight = gachaElementList.Sum(e => e.Prob);
            for (int i = 0; i < parcel.Amount; i++)
            {
                int randomNumber = Random.Shared.Next(1, totalWeight + 1);

                int currentWeight = 0;
                foreach (var element in gachaElementList)
                {
                    currentWeight += element.Prob;
                    if (randomNumber <= currentWeight)
                    {
                        var amount = element.ParcelAmountMin == element.ParcelAmountMax
                            ? element.ParcelAmountMax
                            : Random.Shared.Next(element.ParcelAmountMin, element.ParcelAmountMax + 1);
                        parcelResults.Add(new ParcelResult(element.ParcelType, element.ParcelID, amount));
                        break;
                    }
                }
            }
        }
        return parcelResults;
    }
}
