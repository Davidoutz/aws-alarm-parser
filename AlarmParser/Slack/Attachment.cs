using Amazon.Lambda.Core;
using Newtonsoft.Json;
// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
//[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AlarmParser.Slack
{
    public class Attachment
    {
        [JsonProperty("fallback")]
        public string Fallback { get; set; }

        [JsonProperty("pretext")]
        public string Pretext { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("author_name")]
        public string Author_name { get; set; }

        [JsonProperty("author_link")]
        public string Author_link { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("title_link")]
        public string Title_link { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("image_url")]
        public string Image_url { get; set; }

        [JsonProperty("thumb_url")]
        public string Thumb_url { get; set; }

        [JsonProperty("footer")]
        public string Footer { get; set; }

        [JsonProperty("footer_icon")]
        public string Footer_icon { get; set; }

        [JsonProperty("ts")]
        public string Ts { get; set; }


        [JsonProperty("fields")]
        public Field[] Fields { get; set; }


    }
}
