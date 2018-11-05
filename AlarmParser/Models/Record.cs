using Amazon.Lambda.Core;
// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
//[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AlarmParser.Models
{
    public class Record
    {
        public string EventSource { get; set; }
        public string EventVersion { get; set; }
        public string EventSubscriptionArn { get; set; }
        public Sns Sns { get; set; }
    }
}
