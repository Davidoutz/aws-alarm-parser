using System;
using Amazon.Lambda.Core;
// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
//[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AlarmParser.Models
{
    public class AlarmMessage
    {
        public string AlarmName { get; set; }
        public string AlarmDescription { get; set; }
        public string AWSAccountId { get; set; }
        public string NewStateValue { get; set; }
        public string NewStateReason { get; set; }
        public DateTime StateChangeTime { get; set; }
        public string Region { get; set; }
        public string OldStateValue { get; set; }
        public Trigger Trigger { get; set; }
    }
}
