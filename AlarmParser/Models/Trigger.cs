using Amazon.Lambda.Core;
// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
//[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AlarmParser.Models
{
    public class Trigger
    {
        public string MetricName { get; set; }
        public string Namespace { get; set; }
        public string Statistic { get; set; }
        public object Unit { get; set; }
        public Dimension[] Dimensions { get; set; }
        public int Period { get; set; }
        public int EvaluationPeriods { get; set; }
        public string ComparisonOperator { get; set; }
        public float Threshold { get; set; }
    }
}
