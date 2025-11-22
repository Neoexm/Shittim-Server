using System.Text.Json.Serialization;

namespace Shittim.Models.SDK
{
    public class GetPromotionResponse
    {
        [JsonPropertyName("errorCode")]
        public int ErrorCode { get; set; }

        [JsonPropertyName("result")]
        public PromotionResult Result { get; set; }

        [JsonPropertyName("errorText")]
        public string ErrorText { get; set; }

        [JsonPropertyName("errorDetail")]
        public string ErrorDetail { get; set; }
    }

    public class PromotionResult
    {
        [JsonPropertyName("portraitBannerList")]
        public List<object> PortraitBannerList { get; set; }

        [JsonPropertyName("bannerList")]
        public List<Banner> BannerList { get; set; }
    }

    public class Banner
    {
        [JsonPropertyName("bannerId")]
        public int BannerId { get; set; }

        [JsonPropertyName("touchEvent")]
        public int TouchEvent { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("bannerName")]
        public string BannerName { get; set; }

        [JsonPropertyName("opacity")]
        public double Opacity { get; set; }

        [JsonPropertyName("rotation")]
        public int Rotation { get; set; }

        [JsonPropertyName("frequency")]
        public int Frequency { get; set; }

        [JsonPropertyName("maximum")]
        public int Maximum { get; set; }

        [JsonPropertyName("segmentId")]
        public int SegmentId { get; set; }

        [JsonPropertyName("segmentName")]
        public string SegmentName { get; set; }

        [JsonPropertyName("exposureRate")]
        public string ExposureRate { get; set; }

        [JsonPropertyName("imgList")]
        public List<Img> ImgList { get; set; }

        [JsonPropertyName("buttonList")]
        public List<Button> ButtonList { get; set; }

        [JsonPropertyName("optionList")]
        public List<object> OptionList { get; set; }
    }

    public class Img
    {
        [JsonPropertyName("contentId")]
        public int ContentId { get; set; }

        [JsonPropertyName("imgURL")]
        public string ImgURL { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("left")]
        public int Left { get; set; }

        [JsonPropertyName("top")]
        public int Top { get; set; }

        [JsonPropertyName("right")]
        public int Right { get; set; }

        [JsonPropertyName("bottom")]
        public int Bottom { get; set; }
    }

    public class Button
    {
        [JsonPropertyName("linkId")]
        public int LinkId { get; set; }

        [JsonPropertyName("pid")]
        public string Pid { get; set; }

        [JsonPropertyName("meta")]
        public string Meta { get; set; }

        [JsonPropertyName("urlScheme")]
        public string UrlScheme { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("left")]
        public int Left { get; set; }

        [JsonPropertyName("top")]
        public int Top { get; set; }

        [JsonPropertyName("right")]
        public int Right { get; set; }

        [JsonPropertyName("bottom")]
        public int Bottom { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("userInfoStatus")]
        public bool UserInfoStatus { get; set; }
    }
}
