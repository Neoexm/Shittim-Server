using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schale.FlatData;
using Schale.MX.Core.Math;
using Schale.MX.Data;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Controllers.Api;
using Xunit;

namespace Shittim_Server.Tests;

// Packet-serialization rules taken off live captures of the official server (captures/NetworkLog_official.txt and "NetworkLog non truncated official.txt", client 1.90.443855). Every assertion here matches a shape seen on the wire; when they drift the client drops the session with "A request that cannot be processed".
public class OfficialWireContractTests
{
    private static JObject Serialize(object packet) =>
        JObject.Parse(JsonConvert.SerializeObject(packet, GatewayController.OfficialPacketJsonSettings));

    private static string Raw(object packet) =>
        JsonConvert.SerializeObject(packet, GatewayController.OfficialPacketJsonSettings);

    [Fact]
    public void ZeroAndFalseMembersAreOmitted()
    {
        // no mail means Protocol + ServerTimeTicks only; official drops defaults.
        var json = Serialize(new MailCheckResponse { CommonMailCount = 0 });

        Assert.False(json.ContainsKey("CommonMailCount"));
        Assert.False(json.ContainsKey("ServerNotification"));
        Assert.False(json.ContainsKey("SessionKey"));
        Assert.False(json.ContainsKey("AccountId"));
        Assert.Equal(new[] { "Protocol", "ServerTimeTicks" }, json.Properties().Select(p => p.Name).Order());
    }

    [Fact]
    public void NonDefaultScalarsArePresent()
    {
        var json = Serialize(new MailCheckResponse
        {
            CommonMailCount = 4,
            ServerNotification = ServerNotificationFlag.HasUnreadMail,
        });

        Assert.Equal(4, (int)json["CommonMailCount"]!);
        Assert.Equal(8, (int)json["ServerNotification"]!);
    }

    [Fact]
    public void AccountIdIsDerivedFromSessionKeyAndNotShadowed()
    {
        // a `new SessionKey` shadow on a response hides BasePacket.SessionKey, which leaves AccountId at 0 and gets it dropped as a default.
        var json = Serialize(new AccountCheckNexonResponse
        {
            SessionKey = new SessionKey { AccountServerId = 3218642, MxToken = "token" },
        });

        Assert.Equal(3218642L, (long)json["AccountId"]!);
        Assert.Equal(3218642L, (long)json["SessionKey"]!["AccountServerId"]!);
    }

    [Fact]
    public void DateTimesUseSecondPrecisionWithoutOffset()
    {
        var json = Raw(new MailListResponse
        {
            MailDBs = [new MailDB { SendDate = new DateTime(2026, 7, 20, 12, 34, 56, 789) }],
        });

        Assert.Contains("\"SendDate\":\"2026-07-20T12:34:56\"", json);
        Assert.DoesNotContain("+", json);
        Assert.DoesNotContain(".789", json);
    }

    [Fact]
    public void NullableDateTimesAreTrimmedToo()
    {
        // JsonConverter<DateTime> seals CanConvert to an exact-type check, so a converter declared that way silently skips DateTime? and lets Newtonsoft emit sub-second precision.
        var json = Raw(new MailListResponse
        {
            MailDBs = [new MailDB { ExpireDate = new DateTime(2026, 8, 3, 4, 19, 59, 123) }],
        });

        Assert.Contains("\"ExpireDate\":\"2026-08-03T04:19:59\"", json);
        Assert.DoesNotContain(".123", json);
    }

    [Fact]
    public void UnassignedNullableDateTimesAreStillOmitted()
    {
        // guard the converter against swallowing NullValueHandling: official omits ReceiptDate on unclaimed mail rather than sending null.
        var json = Serialize(new MailDB { ServerId = 1, ReceiptDate = null });

        Assert.False(json.ContainsKey("ReceiptDate"));
    }

    [Fact]
    public void MaxValueDateKeepsFullPrecision()
    {
        // The one exception official sends with a fractional part.
        var json = Raw(new MailListResponse
        {
            MailDBs = [new MailDB { SendDate = DateTime.MaxValue }],
        });

        Assert.Contains("9999-12-31T23:59:59.9999999", json);
    }

    [Fact]
    public void ServerTimeTicksAreWholeSeconds()
    {
        var response = new MailCheckResponse();
        Assert.Equal(0, response.ServerTimeTicks % TimeSpan.TicksPerSecond);
    }

    [Fact]
    public void EmptyCollectionsOfficialAssignsAreStillEmitted()
    {
        // DefaultValueHandling never drops [] / {}, and official sends these two empty.
        var json = Serialize(new MailReceiveResponse { MailServerIds = [1], BattlePassInfoDBs = [] });

        Assert.True(json.ContainsKey("BattlePassInfoDBs"));
        Assert.Empty((JArray)json["BattlePassInfoDBs"]!);
    }

    [Fact]
    public void OmitWhenEmptyMembersAreDroppedWhenEmpty()
    {
        // Official's post-claim Mail_List is bare: no MailDBs, no Count.
        var response = new MailListResponse { MailDBs = [], Count = 0 };
        var json = Serialize(response);

        Assert.False(json.ContainsKey("MailDBs"));
        Assert.False(json.ContainsKey("Count"));

        // the member is serializable; OmitWhenEmpty is what drops it while empty.
        Assert.Contains("MailDBs", JsonConvert.SerializeObject(response));
    }

    [Fact]
    public void OmitWhenEmptyMembersSurviveWhenPopulated()
    {
        var json = Serialize(new MailListResponse
        {
            MailDBs = [new MailDB { ServerId = 70493846 }],
            Count = 1,
        });

        Assert.Single((JArray)json["MailDBs"]!);
        Assert.Equal(1, (int)json["Count"]!);
    }

    [Fact]
    public void MissionListOmitsEmptyProgressButKeepsHistory()
    {
        // both captures: the event-scoped Mission_List calls carry MissionHistoryUniqueIds and DailySuddenMissionInfo but no ProgressDBs.
        var json = Serialize(new MissionListResponse
        {
            MissionHistoryUniqueIds = [1, 2],
            ProgressDBs = [],
            DailySuddenMissionInfo = new { },
        });

        Assert.False(json.ContainsKey("ProgressDBs"));
        Assert.True(json.ContainsKey("MissionHistoryUniqueIds"));
        Assert.True(json.ContainsKey("DailySuddenMissionInfo"));
    }

    [Fact]
    public void ClearedOriginalMissionIdsAreOmittedOutsideRerunEvents()
    {
        // official omits the key entirely for the account-wide call and for normal events (both captures: EventContentId null -> absent, 856 -> absent). MissionHandler signals that by leaving the member null, which only works because it carries no OmitWhenEmpty.
        var json = Serialize(new MissionListResponse { ClearedOrignalMissionIds = null });

        Assert.False(json.ContainsKey("ClearedOrignalMissionIds"));
    }

    [Fact]
    public void ClearedOriginalMissionIdsSurviveEmptyForRerunEvents()
    {
        // for the rerun (10842) official sends the key as [], and the distinction between absent and present-but-empty is the contract, so [] must survive rather than being dropped as a default.
        var json = Serialize(new MissionListResponse { ClearedOrignalMissionIds = [] });

        Assert.True(json.ContainsKey("ClearedOrignalMissionIds"));
        Assert.Empty((JArray)json["ClearedOrignalMissionIds"]!);
    }

    [Fact]
    public void ClanCheckCarriesNothingButTheNotificationFlag()
    {
        // ClanCheckResponse declares no members, so the flag is the entire payload.
        var json = Serialize(new ClanCheckResponse
        {
            ServerNotification = ServerNotificationFlag.CanReceiveClanAttendanceReward,
        });

        Assert.Equal(2048, (int)json["ServerNotification"]!);
        Assert.Equal(
            new[] { "Protocol", "ServerNotification", "ServerTimeTicks" },
            json.Properties().Select(p => p.Name).Order());
    }

    [Fact]
    public void ClanAttendanceFlagCombinesWithTheMailboxBaseline()
    {
        // official's Clan_Check reads 2056 when mail is also waiting: the gateway ORs the handler's own bits into the mailbox baseline rather than overwriting either, and 2056 only comes out while the two enum members keep their numbering.
        var json = Serialize(new ClanCheckResponse
        {
            ServerNotification = ServerNotificationFlag.CanReceiveClanAttendanceReward
                | ServerNotificationFlag.HasUnreadMail,
        });

        Assert.Equal(2056, (int)json["ServerNotification"]!);
    }

    [Fact]
    public void NewMailArrivedCombinesWithTheMailboxBaseline()
    {
        // official's first Mail_Check after an in-session mail delivery reads 12 (8|4); the combination only survives while both enum members keep their numbering.
        var json = Serialize(new MailCheckResponse
        {
            CommonMailCount = 1,
            ServerNotification = ServerNotificationFlag.NewMailArrived
                | ServerNotificationFlag.HasUnreadMail,
        });

        Assert.Equal(12, (int)json["ServerNotification"]!);
    }

    [Fact]
    public void ErrorPacketsCarryOnlyCodeAndTicks()
    {
        // official's captured AP-cap rejection is exactly {"Protocol":-1,"ErrorCode":10006,"ServerTimeTicks":...} - no reason text ever goes to the client.
        var raw = Raw(new ErrorPacket
        {
            Reason = "internal diagnostics that must never reach the wire",
            ErrorCode = WebAPIErrorCode.ShopCannotPurchaseActionPointLimitOver,
        });
        var json = JObject.Parse(raw);

        Assert.DoesNotContain("Reason", raw);
        Assert.Equal(-1, (int)json["Protocol"]!);
        Assert.Equal(10006, (int)json["ErrorCode"]!);
        Assert.Equal(new[] { "ErrorCode", "Protocol", "ServerTimeTicks" },
            json.Properties().Select(p => p.Name).Order());
    }

    [Fact]
    public void AttendanceMailMatchesTheCapturedShape()
    {
        // the recovered post-claim Mail_List: type 1 (Attendance), UniqueId = the attendance book's id, localisation keys, expiry exactly send + 7 days.
        var sent = new DateTime(2026, 7, 27, 4, 13, 3);
        var mail = new MailDB
        {
            ServerId = 1,
            Type = MailType.Attendance,
            UniqueId = 90027,
            Sender = "UI_MAILBOX_POST_SENDER_ARONA",
            Comment = "UI_MAILBOX_ATTENDANCE_REWARD_MESSAGE_NORMAL",
            SendDate = sent,
            ExpireDate = sent.AddDays(7),
            ParcelInfos =
            [
                new ParcelInfo
                {
                    Key = new ParcelKeyPair { Type = ParcelType.Item, Id = 11 },
                    Amount = 100,
                    Multiplier = BasisPoint.One,
                    Probability = BasisPoint.One,
                },
            ],
            RemainParcelInfos = null,
        };

        var json = Serialize(mail);
        var raw = Raw(mail);

        Assert.Equal(1, (int)json["Type"]!);
        Assert.Equal(90027L, (long)json["UniqueId"]!);
        Assert.Contains("\"ExpireDate\":\"2026-08-03T04:13:03\"", raw);
        Assert.False(json.ContainsKey("RemainParcelInfos"));
    }

    [Fact]
    public void AttendanceBooksCarryTheirTargetGroup()
    {
        // official emits TargetGroup on every book (1 basic, 3 event).
        var json = Serialize(new AttendanceBookReward { UniqueId = 90027, TargetGroup = 3 });

        Assert.Equal(3L, (long)json["TargetGroup"]!);
    }

    [Fact]
    public void MissionClaimHistoryCarriesOnlyIdAndTime()
    {
        // official's AddedHistoryDB(s) hold exactly MissionUniqueId + CompleteTime; ServerId and AccountServerId are internal and stay off the wire (the local-session contrast in the detailed capture shows the difference).
        var json = Serialize(new MissionHistoryDB
        {
            MissionUniqueId = 1513,
            CompleteTime = new DateTime(2026, 7, 27, 4, 13, 26),
        });

        Assert.Equal(new[] { "CompleteTime", "MissionUniqueId" },
            json.Properties().Select(p => p.Name).Order());
    }

    [Fact]
    public void CraftBaseNodeOmitsTierAndId()
    {
        // the captured base node is {NodeLevel, NodeRandomSeed, LeafNodeIds} - tier 0 and id 0 stay off the wire as defaults.
        var json = Serialize(new CraftNodeDB
        {
            NodeLevel = 1,
            NodeRandomSeed = 1112457065,
            LeafNodeIds = [7, 10, 3, 9, 2],
        });

        Assert.Equal(new[] { "LeafNodeIds", "NodeLevel", "NodeRandomSeed" },
            json.Properties().Select(p => p.Name).Order());
    }

    [Fact]
    public void FreshlySelectedCraftNodeKeepsItsEmptyLeafList()
    {
        // official's SelectedNodeDB is {NodeTier, NodeId, LeafNodeIds: []} - the empty list is emitted, unlike most empty collections on this wire.
        var raw = Raw(new CraftNodeDB
        {
            NodeTier = CraftNodeTier.Node01,
            NodeId = 10,
            LeafNodeIds = [],
        });

        Assert.Contains("\"LeafNodeIds\":[]", raw);
        Assert.Contains("\"NodeTier\":1", raw);
    }

    [Fact]
    public void ClanAttendanceMailMatchesTheCapturedShape()
    {
        // modelled on the delivered mail in the non-truncated capture's Mail_List: type 11 with UniqueId -1 (it comes from no DefaultMailExcel row), localisation keys rather than rendered text, one parcel, and no RemainParcelInfos or ReceiptDate.
        var sent = new DateTime(2026, 7, 27, 4, 19, 59);
        var mail = new MailDB
        {
            ServerId = 1,
            Type = MailType.ClanAttendance,
            UniqueId = -1,
            Sender = "UI_MAILBOX_POST_SENDER_ARONA",
            Comment = "UI_MAILBOX_CLAN_ATTENDANCE_REWARD_MESSAGE_NORMAL",
            SendDate = sent,
            ExpireDate = sent.AddDays(7),
            ParcelInfos =
            [
                new ParcelInfo
                {
                    Key = new ParcelKeyPair { Type = ParcelType.Currency, Id = 5 },
                    Amount = 10,
                    Multiplier = BasisPoint.One,
                    Probability = BasisPoint.One,
                },
            ],
            RemainParcelInfos = null,
        };

        var json = Serialize(mail);
        var raw = Raw(mail);

        Assert.Equal(11, (int)json["Type"]!);
        Assert.Equal(-1L, (long)json["UniqueId"]!);
        // checked against the raw text because JObject.Parse retypes these as JTokenType.Date and loses the format.
        Assert.Contains("\"SendDate\":\"2026-07-27T04:19:59\"", raw);
        Assert.Contains("\"ExpireDate\":\"2026-08-03T04:19:59\"", raw);
        Assert.False(json.ContainsKey("ReceiptDate"));
        Assert.False(json.ContainsKey("RemainParcelInfos"));

        // official carries both basis points at 1x on this parcel; at the struct default of 0 they are dropped as defaults and the client multiplies the reward down to nothing.
        var parcel = (JObject)((JArray)json["ParcelInfos"]!).Single();
        Assert.Equal(10000L, (long)parcel["Multiplier"]!["rawValue"]!);
        Assert.Equal(10000L, (long)parcel["Probability"]!["rawValue"]!);
    }

    [Fact]
    public void ParcelResultOnlyCarriesTouchedCollections()
    {
        // across every captured reward claim, ParcelResultDB held at most AccountCurrencyDB + ItemDBs + DisplaySequence + ParcelForMission; the other ~20 collections were absent.
        var json = Serialize(new MissionMultipleRewardResponse
        {
            AddedHistoryDBs = [],
            ParcelResultDB = new ParcelResultDB { ItemDBs = new() { [1] = new ItemDB { ServerId = 1 } } },
        });

        var parcel = (JObject)json["ParcelResultDB"]!;
        Assert.True(parcel.ContainsKey("ItemDBs"));
        Assert.False(parcel.ContainsKey("EquipmentDBs"));
        Assert.False(parcel.ContainsKey("CharacterDBs"));
        Assert.False(parcel.ContainsKey("FurnitureDBs"));
    }

    [Fact]
    public void ParcelResultOmitsAccountDBUntilTheAccountRowActuallyChanges()
    {
        // no captured ParcelResultDB carries AccountDB, so it stays null rather than being eagerly constructed: OmitWhenEmpty only drops ICollection { Count: 0 }, so an initialized POCO survives as the bare object "{}".
        var parcel = (JObject)Serialize(new ParcelResultDB
        {
            AccountCurrencyDB = new AccountCurrencyDB(),
        }).Root;

        Assert.False(parcel.ContainsKey("AccountDB"));
    }

    [Fact]
    public void ParcelResultCarriesAccountDBOnceAssigned()
    {
        // ParcelResolver does assign this when the reward mutated the account row, so a stray JsonIgnore would leave the test above passing for the wrong reason.
        var parcel = Serialize(new ParcelResultDB { AccountDB = new AccountDB { ServerId = 2 } });

        Assert.True(parcel.ContainsKey("AccountDB"));
        Assert.Equal(2L, (long)parcel["AccountDB"]!["ServerId"]!);
    }

    [Fact]
    public void ParcelInfoNeverSerializesTheClientComputedMultipliedAmount()
    {
        // MultipliedAmount is a client-side derived getter; official ParcelInfo on the wire is Key + Amount (+ Multiplier/Probability when non-default).
        var json = Raw(new ParcelInfo
        {
            Key = new ParcelKeyPair { Type = ParcelType.Currency, Id = 3 },
            Amount = 1200,
        });

        Assert.DoesNotContain("MultipliedAmount", json);
        Assert.Contains("\"Amount\":1200", json);
    }

    [Fact]
    public void AcademyZoneScheduleRecordsAreDroppedWhenEmpty()
    {
        var json = Serialize(new AcademyDB { ZoneScheduleGroupRecords = new() });
        Assert.False(json.ContainsKey("ZoneScheduleGroupRecords"));
    }

    [Fact]
    public void ScenarioGroupHistoryUsesOfficialWireName()
    {
        // the client sends and official returns "ScenarioGroupUniqueId" while the C# member keeps the upstream typo. getting it wrong makes the client replay cleared story.
        var json = Serialize(new ScenarioGroupHistoryDB
        {
            ScenarioGroupUqniueId = 10001,
            AccountServerId = 2,
        });

        Assert.Equal(10001L, (long)json["ScenarioGroupUniqueId"]!);
        Assert.False(json.ContainsKey("ScenarioGroupUqniueId"));
        Assert.False(json.ContainsKey("AccountServerId"));
    }

    [Fact]
    public void ServerInternalCharacterAndEchelonMembersNeverSerialize()
    {
        // Never observed on the wire across 59 official characters / 26 echelons.
        var character = Serialize(new CharacterDB
        {
            ServerId = 1,
            EquipmentSlotAndDBIds = new() { [0] = 5 },
        });
        Assert.False(character.ContainsKey("EquipmentSlotAndDBIds"));

        var echelon = Serialize(new EchelonDB { MainSlotServerIds = [1, 2, 3, 4] });
        foreach (var internalMember in new[]
                 {
                     "AllCharacterServerIds", "AllCharacterWithoutTSSServerIds",
                     "AllCharacterWithEmptyServerIds", "BattleCharacterServerIds",
                 })
            Assert.False(echelon.ContainsKey(internalMember));
    }

    [Fact]
    public void WholeFloatsKeepNewtonsoftFormatting()
    {
        // Official sends 270.0, not 270 - no FloatConverter on the packet settings.
        var json = Raw(new { Value = 270.0f });
        Assert.Contains("270.0", json);
    }
}
