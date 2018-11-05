using Amazon.Lambda.Core;
// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
//[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AlarmParser.Models
{
    public class AlarmSummary
    {
        public string Name { get; set; }
        public string StateChangeReason { get; set; }
        public string State { get; set; }

    }
}
