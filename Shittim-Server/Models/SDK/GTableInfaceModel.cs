using System;
using System.Text.Json.Serialization;

namespace Shittim_Server.Models.SDK
{
    public class GTableInfaceResponse
    {
        [JsonPropertyName("toy_service_id")]
        public int ToyServiceId { get; set; }

        [JsonPropertyName("arena_product_id")]
        public int ArenaProductId { get; set; }

        [JsonPropertyName("game_client_id")]
        public object GameClientId { get; set; }

        [JsonPropertyName("portal_game_code")]
        public string PortalGameCode { get; set; }

        [JsonPropertyName("krpc_game_code")]
        public int KrpcGameCode { get; set; }

        [JsonPropertyName("jppc_game_code")]
        public object JppcGameCode { get; set; }

        [JsonPropertyName("na_service_id")]
        public long NaServiceId { get; set; }

        [JsonPropertyName("na_region_host")]
        public object NaRegionHost { get; set; }

        [JsonPropertyName("krpc_service_code")]
        public object KrpcServiceCode { get; set; }

        [JsonPropertyName("eve_gameinfo_id")]
        public object EveGameinfoId { get; set; }

        [JsonPropertyName("twitch_game_id")]
        public object TwitchGameId { get; set; }

        [JsonPropertyName("chzzk_game_id")]
        public object ChzzkGameId { get; set; }

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; }

        [JsonPropertyName("guss_service_code")]
        public object GussServiceCode { get; set; }

        [JsonPropertyName("guid")]
        public string Guid { get; set; }

        [JsonPropertyName("world_id")]
        public object WorldId { get; set; }

        [JsonPropertyName("gcid")]
        public object Gcid { get; set; }

        [JsonPropertyName("krpc_member_access_code")]
        public object KrpcMemberAccessCode { get; set; }

        [JsonPropertyName("jppc_gm")]
        public object JppcGm { get; set; }

        [JsonPropertyName("google_oauth_billing_client_redirect_uri")]
        public object GoogleOauthBillingClientRedirectUri { get; set; }

        [JsonPropertyName("krpc_product_type")]
        public object KrpcProductType { get; set; }

        [JsonPropertyName("jppc_product_type")]
        public object JppcProductType { get; set; }

        [JsonPropertyName("coin_type")]
        public object CoinType { get; set; }

        [JsonPropertyName("alltem_code")]
        public object AlltemCode { get; set; }

        [JsonPropertyName("google_oauth_billing_client_id")]
        public object GoogleOauthBillingClientId { get; set; }

        [JsonPropertyName("google_oauth_billing_client_secret")]
        public object GoogleOauthBillingClientSecret { get; set; }

        [JsonPropertyName("arena_service_code")]
        public object ArenaServiceCode { get; set; }

        [JsonPropertyName("str_env_type")]
        public string StrEnvType { get; set; }

        [JsonPropertyName("game_release_status")]
        public string GameReleaseStatus { get; set; }

        [JsonPropertyName("game_name_ko")]
        public string GameNameKo { get; set; }

        [JsonPropertyName("game_name_en")]
        public string GameNameEn { get; set; }

        [JsonPropertyName("gid")]
        public string Gid { get; set; }

        [JsonPropertyName("last_modified")]
        public AuditInfo LastModified { get; set; }

        [JsonPropertyName("krpc_alltem_code")]
        public object KrpcAlltemCode { get; set; }

        [JsonPropertyName("created")]
        public AuditInfo Created { get; set; }
    }

    public class AuditInfo
    {
        [JsonPropertyName("modify_date")]
        public DateTime? ModifyDate { get; set; }

        [JsonPropertyName("create_date")]
        public DateTime? CreateDate { get; set; }

        [JsonPropertyName("admin_no")]
        public int AdminNo { get; set; }
    }
}
