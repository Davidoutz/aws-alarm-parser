using System;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using System.Net;
using System.Collections.Specialized;
using System.Text;
using AlarmParser.Models;
// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
//[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AlarmParser.Slack
{
    public class SlackClient
    {
        private readonly Uri _uri;
        private readonly Encoding _encoding = new UTF8Encoding();

        public SlackClient(string urlWithAccessToken)
        {
            _uri = new Uri(urlWithAccessToken);
        }

        //Post a message using simple strings
        public void PostMessage(AlarmMessage alarm, string text, int currentProductionAlarmsCount, string username = null, string channel = null)
        {
            var color = alarm.NewStateValue.Equals("OK") ? "good" : (alarm.NewStateValue.Equals("ALARM") ? "danger" : "warning");

            string dimensions = "";
            foreach (var item in alarm.Trigger.Dimensions)
            {
                dimensions += item.Name + " --> " + item.Value + "\n";
            }

            Payload payload = new Payload()
            {
                Channel = channel,
                Username = username,
                Text = "Hello support, \nAn alarm has been raised and needs deeper analysis.",
                Attachments = new Attachment[]
                {
                    new Attachment()
                    {
                        Text = alarm.AlarmName,
                        Author_name = "CloudWatch Alarm",
                        Fallback = "Alarm summary",
                        Color = color,
                        Fields = new Field[]
                        {
                            new Field()
                            {
                                Short = true,
                                title = "Status",
                                Value = alarm.OldStateValue + " --> " + alarm.NewStateValue
                            },
                            new Field()
                            {
                                Short = true,
                                title = "Check duration",
                                Value = Function.GenerateCheckDescription(alarm)
                            },
                            new Field()
                            {
                                Short = false,
                                title = "Metrics information",
                                Value = Function.GenerateMetricDescription(alarm)
                            },
                            new Field()
                            {
                                Short = false,
                                title = "Technical reason (from AWS)",
                                Value = alarm.NewStateReason
                            },
                            new Field()
                            {
                                Short = false,
                                title = "Extra information (optional)",
                                Value = dimensions
                            },
                            new Field()
                            {
                                Short = false,
                                title = "Total production alarms in ALARM state is " + currentProductionAlarmsCount,
                                Value = ""
                            }
                        },
                        Footer = "_This message has been sent in using the following channels: Slack/email/SMS_"
                    }
                }
            };

            PostMessage(payload);
        }

        //Post a message using a Payload object
        public void PostMessage(Payload payload)
        {
            string payloadJson = JsonConvert.SerializeObject(payload);

            using (WebClient client = new WebClient())
            {
                NameValueCollection data = new NameValueCollection();
                data["payload"] = payloadJson;

                var response = client.UploadValues(_uri, "POST", data);

                //The response text is usually "ok"
                string responseText = _encoding.GetString(response);
            }
        }
    }
}
