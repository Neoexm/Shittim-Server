using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using BlueArchiveAPI.Models;
using BlueArchiveAPI;
using Schale.Crypto;

namespace Shittim_Server.Controllers.SDK
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
                errorCode = -2,
                result = new { },
                errorText = "Missing request parameters or incorrect format.",
                errorDetail = ""
            };
            Response.Headers["errorcode"] = "-2";
            Response.Headers["access-control-allow-origin"] = "*";
            var encryptedBytes = Utils.PreGatewayAesEncrypt(JsonSerializer.Serialize(res));
            return Results.Bytes(encryptedBytes, contentType: "text/html; charset=UTF-8");
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
            var localHost = Request.Host.HasValue ? Request.Host.Value : "127.0.0.1:5000";
            var gtableUrl = $"http://{localHost}/gid/2079.json";

            var res = new
            {
                errorCode = 0,
                errorText = "Success",
                errorDetail = "",
                result = new
                {
                    gtable_url = gtableUrl,
                    platform_sdk = "WINDOWS",
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
            // Field set derived from NPA.InfaceSDK.NXPToySignInWithTicketResponse.FillJsonBody
            // (0x18943DDD0) — the SDK's own serializer, i.e. the authoritative shape of this response.
            // The deserializer matches JSON keys to these field names. The post-sign-in managed path
            // (NXPAccountLinkBase._LoginWithTicket_b__0 @0x1893D6850) reads guid, umKey, sessionToken
            // and termsAgree, builds the NXPUpdatedUser (SetSessionToken) and calls AgreeTermsWithTicket
            // -> GetGameToken. Omitting sessionToken/npToken left the user half-populated and the
            // post-sign-in chain stalled before issuing the game-token request. Provide the full shape:
            var res = new
            {
                errorCode = 0,
                errorDetail = "",
                errorText = "Success",
                result = new
                {
                    npSN = "76561198260711461",
                    guid = "20790000041274554",
                    umKey = "109:1120300221",
                    npaCode = "0E032VW034F",
                    npToken = $"shittim-nptoken:{Guid.NewGuid():N}",
                    sessionToken = $"shittim-session:{Guid.NewGuid():N}",
                    isNewUser = 0,
                    loginResultType = 1,
                    withdrawExpiresIn = 0,
                    isSwap = false,
                    // termsAgree empty -> NXPToyTermsManager.IsAgree (0x1893F98D0) short-circuits to
                    // "agreed" (empty/null list), so AgreeTermsWithTicket proceeds straight to
                    // GetGameToken instead of popping the offline-incompatible NXPTermsDialog.
                    termsAgree = Array.Empty<object>(),
                    terms = Array.Empty<object>()
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
                    // Empty terms so the merged terms list the SDK evaluates (NXPToyTermsManager.
                    // IsAgree) is empty -> treated as agreed -> no NXPTermsDialog. See signInWithTicket.
                    terms = Array.Empty<object>()
                }
            };
            return Results.Json(res);
        }

        // The AES "npsn" the toy SDK uses for NPSN-crypt Bolt traffic is NOT the npSN field —
        // NXPAuthRequestCredential..ctor(NXPToySession) (GameAssembly 0x18937EC20) sets
        //   _Npsn = long.Parse(session.guid)
        // i.e. it is the account GUID parsed as a long. session.guid comes from the guid we
        // return in signInWithTicket, so this MUST equal that guid (= IasController
        // DefaultSteamGuid "20790000041274554"). Using the npSN here derived the wrong AES key
        // and the SDK decrypted getPolicyList into garbage -> JSON parse 10001.
        private const long DefaultNpsn = 20790000041274554L;

        // getPolicyList/getUserInfo/getTermsList/logoutSVC are MANAGED toy Bolt requests
        // (NXPToyBoltRequestManager over BestHTTP -> visible via mitm). Decompiling
        // NXPToyNetworkUtil.MakeSuccessResult (GameAssembly 0x1893839A0) proved the SDK
        // DECRYPTS the response body before JSON-parsing it:
        //   raw = response.RawBytes; hex = BytesToHexString(raw);
        //   text = NXPCrypto.Decrypt(req.DecryptType, hex, AuthRequestCredential.Npsn);
        //   JSON.Parse(text)   // on failure -> errorCode 10001, tag
        //                      // "gs_error_network_response_json_parsing"
        // NXPToyGetPolicyListRequest sets EncryptType/DecryptType = Npsn (the
        // 0x200000002 backing field), so the cipher is AES-128-ECB keyed by the NPSN-derived
        // key (NXCrypto.GenerateNpsnAes128Key), NOT the shared PreGatewayAes key that
        // getCountry/getPromotion use (those are pre-login COMMON-crypt calls). Returning plain
        // JSON (or shared-key ciphertext) made the SDK decrypt garbage -> JSON.Parse threw ->
        // "Request failed (10001)" at TAP TO START, blocking the game-server handoff (no
        // Queuing_GetTicket). Emit raw NPSN-encrypted bytes; the SDK hex-encodes them itself.
        [HttpPost("getPolicyList.nx")]
        [HttpPost("getUserInfo.nx")]
        [HttpPost("getTermsList.nx")]
        [HttpPost("logoutSVC.nx")]
        public IResult Any()
        {
            var res = new
            {
                errorCode = 0,
                errorText = "Success",
                errorDetail = "",
                result = new
                {
                    policyList = Array.Empty<object>(),
                    policy = Array.Empty<object>(),
                    terms = Array.Empty<object>(),
                    list = Array.Empty<object>()
                }
            };
            var plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(res));
            var encrypted = new NXCrypto().PostGatewayAesEncrypt(plain, NXCrypto.NXToyCryptoType.NPSN, DefaultNpsn)
                            ?? plain;
            return Results.Bytes(encrypted, contentType: "text/html; charset=UTF-8");
        }
    }
}
