using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schale.Data.GameModel;
using Schale.MappingProfiles;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Controllers.Api;
using Xunit;

namespace Shittim_Server.Tests;

public class ItemLockPersistenceTests
{
    private static JObject Wire(ResponsePacket response) =>
        JObject.Parse(JsonConvert.SerializeObject(response, GatewayController.OfficialPacketJsonSettings));

    [Fact]
    public void ALockedItemCarriesTheFlagThroughMapperAndWire()
    {
        var mapped = Mapper.Map<ItemDB>(new ItemDBServer { UniqueId = 2000, StackCount = 1, IsLocked = true });

        var json = Wire(new ItemLockResponse { ItemDB = mapped });

        Assert.True((bool)json["ItemDB"]!["IsLocked"]!);
    }

    [Fact]
    public void ALockedEquipmentCarriesTheFlagThroughMapperAndWire()
    {
        var mapped = Mapper.Map<EquipmentDB>(new EquipmentDBServer { UniqueId = 1000, StackCount = 1, IsLocked = true });

        var json = Wire(new EquipmentItemLockResponse { EquipmentDB = mapped });

        Assert.True((bool)json["EquipmentDB"]!["IsLocked"]!);
    }

    private static readonly IMapper Mapper = BuildMapper();

    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => { }, typeof(GameModelsMappingProfile).Assembly);
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }
}
