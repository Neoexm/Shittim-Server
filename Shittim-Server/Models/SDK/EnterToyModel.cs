using System.Text.Json.Serialization;

namespace Shittim.Models.SDK
{
    public class EnterToyRequest
    {
        // To be implemented
    }

    public class EnterToyResponse
    {
        [JsonPropertyName("errorCode")]
        public int ErrorCode { get; set; }

        [JsonPropertyName("errorText")]
        public string ErrorText { get; set; }

        [JsonPropertyName("errorDetail")]
        public string ErrorDetail { get; set; }

        [JsonPropertyName("result")]
        public EnterToyResult Result { get; set; }
    }

    public class EnterToyResult
    {
        [JsonPropertyName("service")]
        public Service Service { get; set; }

        [JsonPropertyName("endBanner")]
        public Dictionary<string, object> EndBanner { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("idfa")]
        public Idfa Idfa { get; set; }

        [JsonPropertyName("useLocalPolicy")]
        public List<string> UseLocalPolicy { get; set; }

        [JsonPropertyName("enableLogging")]
        public bool EnableLogging { get; set; }

        [JsonPropertyName("enablePlexLogging")]
        public bool EnablePlexLogging { get; set; }

        [JsonPropertyName("enableForcePingLogging")]
        public bool EnableForcePingLogging { get; set; }

        [JsonPropertyName("userArenaRegion")]
        public int UserArenaRegion { get; set; }

        [JsonPropertyName("offerwall")]
        public Offerwall Offerwall { get; set; }

        [JsonPropertyName("useYoutubeRewardEvent")]
        public bool UseYoutubeRewardEvent { get; set; }

        [JsonPropertyName("gpgCycle")]
        public int GpgCycle { get; set; }

        [JsonPropertyName("eve")]
        public Eve Eve { get; set; }

        [JsonPropertyName("insign")]
        public Insign Insign { get; set; }
    }

    public class Service
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("buildVer")]
        public string BuildVer { get; set; }

        [JsonPropertyName("policyApiVer")]
        public string PolicyApiVer { get; set; }

        [JsonPropertyName("termsApiVer")]
        public string TermsApiVer { get; set; }

        [JsonPropertyName("useTPA")]
        public int UseTPA { get; set; }

        [JsonPropertyName("useGbNpsn")]
        public int UseGbNpsn { get; set; }

        [JsonPropertyName("useGbKrpc")]
        public int UseGbKrpc { get; set; }

        [JsonPropertyName("useGbArena")]
        public int UseGbArena { get; set; }

        [JsonPropertyName("useGbJppc")]
        public int UseGbJppc { get; set; }

        [JsonPropertyName("useGamania")]
        public int UseGamania { get; set; }

        [JsonPropertyName("useToyBanDialog")]
        public int UseToyBanDialog { get; set; }

        [JsonPropertyName("grbRating")]
        public string GrbRating { get; set; }

        [JsonPropertyName("networkCheckSampleRate")]
        public string NetworkCheckSampleRate { get; set; }

        [JsonPropertyName("nkMemberAccessCode")]
        public string NkMemberAccessCode { get; set; }

        [JsonPropertyName("useIdfaCollection")]
        public int UseIdfaCollection { get; set; }

        [JsonPropertyName("useIdfaDialog")]
        public int UseIdfaDialog { get; set; }

        [JsonPropertyName("useIdfaDialogNTest")]
        public int UseIdfaDialogNTest { get; set; }

        [JsonPropertyName("useNexonOTP")]
        public int UseNexonOTP { get; set; }

        [JsonPropertyName("useRegionLock")]
        public int UseRegionLock { get; set; }

        [JsonPropertyName("usePcDirectRun")]
        public int UsePcDirectRun { get; set; }

        [JsonPropertyName("useArenaCSByRegion")]
        public int UseArenaCSByRegion { get; set; }

        [JsonPropertyName("usePlayNow")]
        public int UsePlayNow { get; set; }

        [JsonPropertyName("methinksUsage")]
        public MethinksUsage MethinksUsage { get; set; }

        [JsonPropertyName("livestreamUsage")]
        public LivestreamUsage LivestreamUsage { get; set; }

        [JsonPropertyName("useExactAlarmActivation")]
        public int UseExactAlarmActivation { get; set; }

        [JsonPropertyName("useCollectUserActivity")]
        public int UseCollectUserActivity { get; set; }

        [JsonPropertyName("userActivityDataPushNotification")]
        public UserActivityDataPushNotification UserActivityDataPushNotification { get; set; }

        [JsonPropertyName("appAppAuthLoginIconUrl")]
        public string AppAppAuthLoginIconUrl { get; set; }

        [JsonPropertyName("useGuidCreationBlk")]
        public int UseGuidCreationBlk { get; set; }

        [JsonPropertyName("guidCreationBlkWlCo")]
        public List<object> GuidCreationBlkWlCo { get; set; }

        [JsonPropertyName("useArena2FA")]
        public int UseArena2FA { get; set; }

        [JsonPropertyName("usePrimary")]
        public int UsePrimary { get; set; }

        [JsonPropertyName("loginUIType")]
        public string LoginUIType { get; set; }

        [JsonPropertyName("clientId")]
        public string ClientId { get; set; }

        [JsonPropertyName("useMemberships")]
        public List<int> UseMemberships { get; set; }

        [JsonPropertyName("useMembershipsInfo")]
        public UseMembershipsInfo UseMembershipsInfo { get; set; }
    }

    public class MethinksUsage
    {
        [JsonPropertyName("useAlwaysOnRecording")]
        public int UseAlwaysOnRecording { get; set; }

        [JsonPropertyName("useScreenshot")]
        public int UseScreenshot { get; set; }

        [JsonPropertyName("useStreaming")]
        public int UseStreaming { get; set; }

        [JsonPropertyName("useSurvey")]
        public int UseSurvey { get; set; }
    }

    public class LivestreamUsage
    {
        [JsonPropertyName("useIM")]
        public int UseIM { get; set; }
    }

    public class UserActivityDataPushNotification
    {
        [JsonPropertyName("changePoints")]
        public List<object> ChangePoints { get; set; }

        [JsonPropertyName("notificationType")]
        public string NotificationType { get; set; }
    }

    public class Idfa
    {
        [JsonPropertyName("dialog")]
        public List<object> Dialog { get; set; }

        [JsonPropertyName("imgUrl")]
        public string ImgUrl { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; }
    }

    public class Offerwall
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }
    }

    public class Eve
    {
        [JsonPropertyName("domain")]
        public string Domain { get; set; }

        [JsonPropertyName("g_api")]
        public string GApi { get; set; }
    }

    public class Insign
    {
        [JsonPropertyName("useSimpleSignup")]
        public int UseSimpleSignup { get; set; }

        [JsonPropertyName("useKrpcSimpleSignup")]
        public int UseKrpcSimpleSignup { get; set; }

        [JsonPropertyName("useArenaSimpleSignup")]
        public int UseArenaSimpleSignup { get; set; }
    }

    public class UseMembershipsInfo
    {
        [JsonPropertyName("nexonNetSecretKey")]
        public string NexonNetSecretKey { get; set; }

        [JsonPropertyName("nexonNetProductId")]
        public string NexonNetProductId { get; set; }

        [JsonPropertyName("nexonNetRedirectUri")]
        public string NexonNetRedirectUri { get; set; }
    }
}
