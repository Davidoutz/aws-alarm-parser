using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
//[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AlarmParser.Models
{
    public class Dimension
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
    public static class Extensions
    {
        public static Dimension[] ToDimensions(this List<Amazon.CloudWatch.Model.Dimension> dimensions)
        {
            List<Dimension> dims = new List<Dimension>();
            foreach (var item in dimensions)
            {
                dims.Add(new Dimension() { Name = item.Name, Value = item.Value });
            }
            return dims.ToArray();
        }
    }
}
