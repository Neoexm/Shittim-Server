using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using BlueArchiveAPI.Models;

namespace BlueArchiveAPI.Controllers
{
    [ApiController]
    [Route("/toy/sdk/")]
    public class ToySDKController : ControllerBase
    {
        [HttpPost("getCountry.nx")]
        public IResult GetCountry()
        {
            var res = new
            {
                errorCode = 0,
                errorText = "Success",
                errorDetail = "",
                result = new { country = "PH" }
            };
            var encryptedBytes = Utils.PreGatewayAesEncrypt(JsonSerializer.Serialize(res));
            return Results.Bytes(encryptedBytes, contentType: "text/html");
        }

        [HttpPost("getPromotion.nx")]
        public IResult GetPromotion()
        {
            var res = new
            {
                errorCode = 0,
                errorDetail = "",
                errorText = "Success",
                result = new
                {
                    bannerList = new List<object>(),
                    portraitBannerList = new List<object>()
                }
            };
            var encryptedBytes = Utils.PreGatewayAesEncrypt(JsonSerializer.Serialize(res));
            return Results.Bytes(encryptedBytes, contentType: "text/html");
        }

        [HttpPost("enterToy.nx")]
        public IResult EnterToy()
        {
            var res = new
            {
                errorCode = 0,
                errorText = "Success",
                errorDetail = "",
                result = new
                {
                    service = new
                    {
                        title = "Blue Archive",
                        buildVer = "2",
                        policyApiVer = "2",
                        termsApiVer = "2",
                        useTPA = 0,
                        useGbNpsn = 1,
                        useGbKrpc = 1,
                        useGbArena = 1,
                        useGbJppc = 0,
                        useGamania = 0,
                        useToyBanDialog = 0,
                        grbRating = "",
                        networkCheckSampleRate = "3",
                        nkMemberAccessCode = "0",
                        useIdfaCollection = 0,
                        useIdfaDialog = 0,
                        useIdfaDialogNTest = 0,
                        useNexonOTP = 0,
                        useRegionLock = 0,
                        usePcDirectRun = 0,
                        useArenaCSByRegion = 0,
                        usePlayNow = 0,
                        methinksUsage = new
                        {
                            useAlwaysOnRecording = 0,
                            useScreenshot = 0,
                            useStreaming = 0,
                            useSurvey = 0
                        },
                        livestreamUsage = new { useIM = 0 },
                        useExactAlarmActivation = 0,
                        useCollectUserActivity = 0,
                        userActivityDataPushNotification = new
                        {
                            changePoints = new List<object>(),
                            notificationType = ""
                        },
                        appAppAuthLoginIconUrl = "",
                        useGuidCreationBlk = 0,
                        guidCreationBlkWlCo = new List<object>(),
                        useArena2FA = 0,
                        usePrimary = 1,
                        loginUIType = "1",
                        clientId = "MjcwOA",
                        useMemberships = new List<int> { 101, 103, 110, 107, 9999 },
                        useMembershipsInfo = new
                        {
                            nexonNetSecretKey = "",
                            nexonNetProductId = "",
                            nexonNetRedirectUri = ""
                        }
                    },
                    endBanner = new Dictionary<string, object>(),
                    country = "PH",
                    idfa = new
                    {
                        dialog = new List<object>(),
                        imgUrl = "",
                        language = ""
                    },
                    useLocalPolicy = new List<string> { "0", "0" },
                    enableLogging = false,
                    enablePlexLogging = false,
                    enableForcePingLogging = false,
                    userArenaRegion = 5,
                    offerwall = new { id = 0, title = "" },
                    useYoutubeRewardEvent = false,
                    gpgCycle = 0,
                    eve = new
                    {
                        domain = "https://eve.nexon.com",
                        gApi = "https://g-eve-apis.nexon.com"
                    },
                    insign = new
                    {
                        useSimpleSignup = 0,
                        useKrpcSimpleSignup = 0,
                        useArenaSimpleSignup = 0
                    }
                }
            };
            return Results.Json(res, contentType: "text/html");
        }

        [HttpPost("signInWithTicket.nx")]
        public IResult SignInWithTicket()
        {
            var res = new
            {
                errorCode = 0,
                errorDetail = "",
                errorText = "Success",
                result = new
                {
                    npSN = "1",
                    guid = "1",
                    umKey = "109:1120300221",
                    npaCode = "0E032VW034F",
                    isSwap = false,
                    terms = new[]
                    {
                        new
                        {
                            termID = 304,
                            type = new List<object>(),
                            optional = 0,
                            exposureType = "NORMAL",
                            title = "TOS & EULA (Shorten)",
                            titleReplacements = new List<object>(),
                            isAgree = 0,
                            isUpdate = 1
                        },
                        new
                        {
                            termID = 305,
                            type = new List<object>(),
                            optional = 0,
                            exposureType = "NORMAL",
                            title = "Privacy Policy",
                            titleReplacements = new List<object>(),
                            isAgree = 0,
                            isUpdate = 1
                        }
                    }
                }
            };
            return Results.Json(res);
        }

        [HttpPost("terms.nx")]
        public IResult Terms()
        {
            var res = new
            {
                errorCode = 0,
                errorDetail = "",
                errorText = "Success",
                result = new
                {
                    terms = new[]
                    {
                        new
                        {
                            termID = 304,
                            type = new List<object>(),
                            optional = 0,
                            exposureType = "NORMAL",
                            title = "TOS & EULA (Shorten)",
                            titleReplacements = new List<object>()
                        },
                        new
                        {
                            termID = 305,
                            type = new List<object>(),
                            optional = 0,
                            exposureType = "NORMAL",
                            title = "Privacy Policy",
                            titleReplacements = new List<object>()
                        }
                    }
                }
            };
            return Results.Json(res);
        }

        [HttpPost("getPolicyList.nx")]
        [HttpPost("getUserInfo.nx")]
        [HttpPost("getTermsList.nx")]
        [HttpPost("logoutSVC.nx")]
        public IResult Any() => Results.Ok();
    }
}
