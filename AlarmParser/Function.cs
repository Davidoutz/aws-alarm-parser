using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.CloudWatch;
using Newtonsoft.Json;
using System.Net.Mail;
using Amazon;
using Amazon.CloudWatch.Model;
using Amazon.SimpleNotificationService;
using System.IO;
using System.ComponentModel;
using AlarmParser.Models;
using AlarmParser.Slack;
using System;
using System.Text;
using System.Net.Mime;
using System.Text.RegularExpressions;
// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AlarmParser
{
    public enum ENDPOINTS
    {
        MAIL,
        MAIL_SLACK,
        MAIL_SMS,
        MAIL_SLACK_SMS,
        SLACK,
        SLACK_SMS,
        SMS
    }
    public class Target
    {
        public string Recipients { get; set; }
        public ENDPOINTS Endpoints { get; set; }

    }
    public class Function
    {
        #region constants
        public const string NOT_VALID_ALARM  = "Not a valid NESS-PROD alarm...";
        public const string UNDER_MAINTENANCE = "Service is under maintenance...";
        public const string LOG_NEW_ALARM = "Received NESS|MGMT PROD alarm - ID :{0}, STATE IS NOW {1}";
        public const string SUCCESS = "SUCCESS";
        public const string FAILURE = "FAILURE";
        public const string INVALID_JSON_ALARM = "Alarm JSON is invalid!";
        public const string DEFAULT_EMAIL = "david.*****@*****.ch";
        public const string LOGICAL_FREE_DISK_SPACE = "LogicalDisk % Free Space";
        public const string DISK_USED_PERCENT = "disk_used_percent";
        public const string BURSTBALANCE = "BurstBalance";
        public const string CPU_UTILIZATION = "CPUUtilization";
        public const string ELVIS_LOGIN_POSSIBLE = "elvis_login_possible";
        public const string ELVIS_NUMBER_OF_DATA_NODES = "elvis_number_of_data_nodes";
        public const string HEALTHY_HOST_COUNT = "HealthyHostCount";
        public const string STATUS_CHECK_FAILED_SYSTEM = "StatusCheckFailed_System";
        public const string UNHEALTHY_HOST_COUNT = "UnHealthyHostCount";

        #endregion

        /// <summary>
        /// A pretty specific Alarm Parser for Tamedia
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<string> FunctionHandler(SnsNotification input, ILambdaContext context)
        {
            var alarmMessage = input.Records[0].Sns.Message; // json string
            #region Alarm setup/validation
            AlarmMessage alarm;
            try
            {
                alarm = JsonConvert.DeserializeObject<AlarmMessage>(alarmMessage);
            }
            catch
            {
                Console.WriteLine(INVALID_JSON_ALARM);
                return FAILURE;
            }
            if (alarm == null)
            {
                return FAILURE;
            }
            if (!IsNessProdAlarm(alarm.AlarmName))
                return NOT_VALID_ALARM;

            // --> Starting from here, the alarm is a NESS-PROD-*** or a MGMT-**** --> Check for maintenance window
            if (IsAlarmInMaintenanceMode(alarm.AlarmName))
                return UNDER_MAINTENANCE;
            #endregion
            #region Alarm processing
            // No maintenance window, let's go!
            var htmlDescription = GenerateHtmlMessage(alarm);
            var htmlAlarmsLeft = await GenerateAlarmsHtmlTable();
            Console.WriteLine(String.Format(LOG_NEW_ALARM, alarm.AlarmName, alarm.NewStateValue));
            var target = GetRecipients(alarm.Trigger.MetricName);
            //target.Endpoints = ENDPOINTS.MAIL; // For forcing testing only the emails... Remove it in prod!!!
            if (alarm.NewStateValue.Equals(ALARM_STATE.OK.ToString()))
            {
                target.Endpoints = ENDPOINTS.MAIL_SLACK;
                await SendToEndpoints(target, alarm, htmlDescription,htmlAlarmsLeft);
            }
            else if (alarm.NewStateValue.Equals(ALARM_STATE.INSUFFICIANT_DATA.ToString()))
            {
                await SendToEndpoints(target, alarm, htmlDescription, htmlAlarmsLeft);
            }
            else if(alarm.NewStateValue.Equals(ALARM_STATE.ALARM.ToString())) {
                await SendToEndpoints(target, alarm, htmlDescription, htmlAlarmsLeft);
            }
            #endregion
            return SUCCESS;
        }
        public int CurrentProductionAlarmsCount { get; set; }

        #region Methods
        private async Task SendToEndpoints(Target target, AlarmMessage alarm, string htmlContent, string alarmLeft)
        {
            Console.WriteLine("DEBUG: ");
            Console.WriteLine("----------> endpoints: " + target.Endpoints.ToString());
            switch (target.Endpoints)
            {
                case ENDPOINTS.MAIL:
                    await this.SendEmail(htmlContent, alarmLeft, alarm, target.Recipients);
                    break;
                case ENDPOINTS.MAIL_SLACK:
                    await this.SendEmail(htmlContent, alarmLeft, alarm, target.Recipients);
                    this.SendSlack(alarm, CurrentProductionAlarmsCount);
                    break;
                case ENDPOINTS.MAIL_SMS:
                    await this.SendSms(alarm);
                    await this.SendEmail(htmlContent, alarmLeft, alarm, target.Recipients);
                    break;
                case ENDPOINTS.MAIL_SLACK_SMS:
                    await this.SendSms(alarm);
                    await this.SendEmail(htmlContent, alarmLeft, alarm, target.Recipients);
                    this.SendSlack(alarm, CurrentProductionAlarmsCount);
                    break;
                case ENDPOINTS.SLACK:
                    this.SendSlack(alarm, CurrentProductionAlarmsCount);
                    break;
                case ENDPOINTS.SLACK_SMS:
                    await this.SendSms(alarm);
                    this.SendSlack(alarm, CurrentProductionAlarmsCount);
                    break;
                case ENDPOINTS.SMS:
                    await this.SendSms(alarm);
                    break;
                default:
                    break;
            }
        }

        private Target GetRecipients(string metric)
        {
            Target recipients = null;
            try
            {
                switch (metric)
                {
                    case LOGICAL_FREE_DISK_SPACE: // 1
                    case DISK_USED_PERCENT: // 1
                    case ELVIS_LOGIN_POSSIBLE: // 1
                    case ELVIS_NUMBER_OF_DATA_NODES: // 1
                    case HEALTHY_HOST_COUNT: // 1
                    case STATUS_CHECK_FAILED_SYSTEM: // 1
                    case UNHEALTHY_HOST_COUNT: // 1
                        recipients = new Target() { Recipients = Environment.GetEnvironmentVariable("EmailCAS") + ";" + Environment.GetEnvironmentVariable("EmailDAI"), Endpoints = ENDPOINTS.MAIL_SLACK_SMS };
                        break;
                    case BURSTBALANCE: // 2
                    case CPU_UTILIZATION: // 2
                        recipients = new Target() { Recipients = Environment.GetEnvironmentVariable("EmailDAI"), Endpoints = ENDPOINTS.MAIL_SLACK };
                        break;
                    default:
                        recipients = new Target() { Recipients = Environment.GetEnvironmentVariable("EmailDAI"), Endpoints = ENDPOINTS.MAIL };
                        break;
                }
            }
            catch
            {
            }
            return recipients;

        } // Based on Monitoring requirements document

        private bool IsNessProdAlarm(string alarmName)
        {
            if (!alarmName.ToLower().Contains("ness"))
            {
                Console.WriteLine("WARNING: Not a NESS alarm...");
                return false;
            }
            if (!alarmName.ToLower().Contains("prod") && !alarmName.ToLower().Contains("mgmt"))
            {
                Console.WriteLine($"WARNING: {alarmName} is not a PRODUCTION/MANAGEMENT alarm...");
                return false;
            }
            Console.WriteLine($"SUCCESS: {alarmName} is a PRODUCTION/MANAGEMENT alarm...");
            return true;
        }

        private bool IsAlarmInMaintenanceMode(string alarmName)
        {
            string SERVICES_UNDER_MAINTENANCE = Environment.GetEnvironmentVariable("MaintenanceServices"); // each service is separated with a ","
            string[] tokens = SERVICES_UNDER_MAINTENANCE.Split(',');
            foreach (var item in tokens)
            {
                if (alarmName.ToLower().Contains(item.ToLower()))
                {
                    Console.WriteLine($"Service {item} is under maintenance. Alarm named {alarmName} will not be raised.");
                    return true;
                }
            }
            return false;
        }

        private void SendSlack(AlarmMessage alarm, int currentProductionAlarmsCount)
        {
            try
            {
                Console.WriteLine("DEBUG: ");
                Console.WriteLine("----------> Creating SLACK message...");
                string urlWithAccessToken = Environment.GetEnvironmentVariable("SlackHook"); // webhook urlw
                SlackClient client = new SlackClient(urlWithAccessToken);
                client.PostMessage(alarm: alarm,
                                   username: "David B",
                                   text: "Not used...",
                                   channel: "#aws-notifications",
                                   currentProductionAlarmsCount: currentProductionAlarmsCount);
                Console.WriteLine("----------> Slack message sent");
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: Slack message could not been sent! " + e.Message);
            }
        }

        private async Task SendSms(AlarmMessage alarm)
        {
            try
            {
                Console.WriteLine("DEBUG: ");
                Console.WriteLine("----------> Creating SMS...");
                AmazonSimpleNotificationServiceClient snsClient = new AmazonSimpleNotificationServiceClient();
                string msg = $"Hello DAI team, An alarm has been raised and needs deeper analysis.\nName: {alarm.AlarmName}\nOldState->NewState: {alarm.OldStateValue}->{alarm.NewStateValue}\n{GenerateMetricDescription(alarm)}\nFor more details, check your emails and log into AWS Console.";
                string nbr = Environment.GetEnvironmentVariable("SmsNumber"); // number of support team
                Amazon.SimpleNotificationService.Model.PublishRequest req = new Amazon.SimpleNotificationService.Model.PublishRequest();
                req.PhoneNumber = nbr;
                req.Message = msg;
                req.Subject = "NESS ALARM";
                await snsClient.PublishAsync(req);
                Console.WriteLine("----------> number : " + nbr);
                Console.WriteLine("----------> message: " + msg);
                Console.WriteLine("----------> SMS sent...");
            }
            catch(Exception e)
            {
                Console.WriteLine("ERROR: SMS could not been sent! "  + e.Message); 
            }
        }

        private async Task SendEmail(string htmlContent, string htmlAlarmsLeft, AlarmMessage alarm, string mailTo)
        {
                using (var stream = await GetChart(alarm))
                {
                    using (var client = new SmtpClient())
                    {
                        Console.WriteLine("DEBUG: ");
                        Console.WriteLine("----------> Creating Email...");
                        client.EnableSsl = true;
                        client.Port = 587;
                        client.DeliveryMethod = SmtpDeliveryMethod.Network;
                        client.UseDefaultCredentials = false;
                        client.Host = "smtp.gmail.com";
                        client.Credentials = new System.Net.NetworkCredential("*****@*****.ch", "***jaijrskoeboeshad***"); 
                        using (var mail = new MailMessage())
                        {
                            mail.From = new MailAddress("david.*****@*****.ch");
                            foreach (var address in mailTo.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                mail.To.Add(address);
                                Console.WriteLine("----------> target: " + address);
                            }
                            mail.Subject = "ALARM on " + alarm.AlarmName;
                            mail.IsBodyHtml = true;

                            // check if the stream contains a valid chart
                            if (stream != null)
                            { // image uploaded!
                                Console.WriteLine("----------> stream: building chart...");

                                stream.Position = 0;
                                LinkedResource res = new LinkedResource(stream, "image/png")
                                {
                                    ContentId = Guid.NewGuid().ToString()
                                };
                                mail.Body = htmlContent + @"<img src='cid:" + res.ContentId + @"'/>" + htmlAlarmsLeft;
                                AlternateView alternateView = AlternateView.CreateAlternateViewFromString(mail.Body, null, MediaTypeNames.Text.Html);
                                alternateView.LinkedResources.Add(res);
                                stream.Position = 0;
                                mail.AlternateViews.Add(alternateView);
                                try
                                {
                                    client.Send(mail);
                                    Console.WriteLine("INFO------> email : Email sent!");

                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e.Message);
                                    Console.WriteLine("ERROR: Sending 2nd time without chart...");
                                    mail.Body = htmlContent + htmlAlarmsLeft;
                                    client.Send(mail);
                                }
                            }
                            else
                            {
                                Console.WriteLine("----------> stream: ###EMPTY###");
                                mail.Body = htmlContent + htmlAlarmsLeft;
                                client.Send(mail);
                                Console.WriteLine("INFO------> email : Email sent!");
                            }
                        }
                    }
                }
        }
        #endregion

        #region async
        private async Task<MemoryStream> GetChart(AlarmMessage alarm)
        {
            string[] metricsToMonitor = { "disk_used_percent", "LogicalDisk % Free Space", "BurstBalance", "CPUUtilization" };
            bool result;
            bool GraphMonitoring = bool.TryParse(Environment.GetEnvironmentVariable("GraphMonitoring"), out result);

            if (!metricsToMonitor.Contains(alarm.Trigger.MetricName) || !result)
            {
                Console.WriteLine($"Metric {alarm.Trigger.MetricName} should not generate a chart. Exiting...");
                return null;
            }
            //var json = "{\"metrics\":[[\"{0}\", \"{1}\"{3}]], \"title\":\"Last recorded metrics\",\"stacked\": false,\"view\": \"timeSeries\"}";
            var dim = String.Empty;
            foreach (var item in alarm.Trigger.Dimensions)
            {
                if (item.Name.ToLower().Equals("instanceid")){
                    dim += $",\"{item.Name}\",\"{item.Value.Split('(')[0]}\"";
                }
                else
                {
                    dim += $",\"{item.Name}\",\"{item.Value}\"";
                }
            }
            //var jsonString = String.Format(json, alarm.Trigger.Namespace, alarm.Trigger.MetricName, dim);
            var jsonString = "{\"metrics\":[[\"" + alarm.Trigger.Namespace + "\", \"" + alarm.Trigger.MetricName + "\"" + dim + "]], \"title\":\"Last recorded metrics\",\"stacked\": false,\"view\": \"timeSeries\"}";
            Console.WriteLine($"Chart request: {jsonString}");
            //var jsonString = "{\"metrics\": [[ \"" + alarm.Trigger.Namespace + "\", \"" + alarm.Trigger.MetricName + "\"]],\"title\": \"Last recorded metrics\",\"stacked\": false,\"region\": \"eu-west-1\",\"view\": \"timeSeries\"}";

            var amazonCloudwatch = new AmazonCloudWatchClient(RegionEndpoint.EUWest1);
            GetMetricWidgetImageResponse t = null;
            try
            {
                t = await amazonCloudwatch.GetMetricWidgetImageAsync(new GetMetricWidgetImageRequest() { MetricWidget = jsonString, OutputFormat = "png" });
                Console.WriteLine("INFO -> Graph generated!");
                Console.WriteLine("Status code: " + t.HttpStatusCode);
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR -> Could not generate graph! " + e.Message);
                return null;
            }
            return t?.MetricWidgetImage;
        }
        private async Task<string> GenerateAlarmsHtmlTable()
        {
            var alarmsAlert = await GetCurrentAlarms(ALARM_STATE.ALARM);
            var htmlAlarmsLeft = "";
            if (alarmsAlert.Count > 0)
            {
                htmlAlarmsLeft = $"<h3>Current situation : <u>{alarmsAlert.Count} active alarms</u></h3><table border='2px solid #A40000' bgcolor='F58881' width='100%'><tr><th>Alarm info</th><th>State change time</th></tr>";

                foreach (var item in alarmsAlert.OrderByDescending(a => a.StateChangeTime))
                {
                    htmlAlarmsLeft += $"<tr><td><p><b>{item.AlarmName} - <i>State is: {item.NewStateValue}</i></b><br/>{GenerateMetricDescription(item)}<br/><i>{GenerateDimensionsDescription(item)}</i></p></td><td>{item.StateChangeTime}</td></tr>";
                }
                htmlAlarmsLeft += "</table>";
            }
            else
            {
                htmlAlarmsLeft = $"<h3>Current situation : <u>No more active alarms</u></h3>";
            }
            CurrentProductionAlarmsCount = alarmsAlert.Count;
            return htmlAlarmsLeft;
        }
        private async Task<List<AlarmMessage>> GetCurrentAlarms(ALARM_STATE state)
        {
            var alarmsAlert = new List<AlarmMessage>();
            using (var cloudWatch = new AmazonCloudWatchClient(RegionEndpoint.EUWest1))
            {
                var request = new DescribeAlarmsRequest();
                request.StateValue = state.ToString();
                do
                {
                    DescribeAlarmsResponse response = await cloudWatch.DescribeAlarmsAsync(request);
                    foreach (var al in response.MetricAlarms)
                    {
                        if (IsNessProdAlarm(al.AlarmName))
                        {
                            AlarmMessage am = new AlarmMessage()
                            {
                                AlarmName = al.AlarmName,
                                AlarmDescription = al.AlarmDescription,
                                NewStateReason = al.StateReason,
                                NewStateValue = al.StateValue,
                                StateChangeTime = al.StateUpdatedTimestamp,
                                Trigger = new Trigger()
                                {
                                    ComparisonOperator = al.ComparisonOperator,
                                    EvaluationPeriods = al.EvaluationPeriods,
                                    MetricName = al.MetricName,
                                    Namespace = al.Namespace,
                                    Period = al.Period,
                                    Statistic = al.Statistic,
                                    Threshold = (float)al.Threshold,
                                    Unit = al.Unit,
                                    Dimensions = al.Dimensions.ToDimensions()
                                }

                            };
                            alarmsAlert.Add(am);
                        }
                    }
                    request.NextToken = response.NextToken;
                } while (request.NextToken != null);
            }
            return alarmsAlert;
        }
        #endregion

        #region static functions
        public static string GenerateHtmlMessage(AlarmMessage alarm)
        {
            return $"<p>Hello support, <br/>An alarm has been raised and needs deeper analysis." +
                   $"<h3>Alarm name</h3><p>{alarm.AlarmName}</p>" +
                   $"<h3>Old state -> new state</h3><p>{alarm.OldStateValue} -> {alarm.NewStateValue}</p>" +
                   $"<h3>Check duration</h3><p>{GenerateCheckDescription(alarm)}</p>" +
                   $"<h3>Metrics information</h3><p>{GenerateMetricDescription(alarm)}</p>" +
                   $"<h3>Technical reason</h3><p>{alarm.NewStateReason}</p>" +
                   $"<h3>Extra information (opt)</h3><p>{GenerateDimensionsDescription(alarm)}</p>";
        }
        public static string GenerateCheckDescription(AlarmMessage alarm)
        {
            var sec = alarm.Trigger.EvaluationPeriods * alarm.Trigger.Period;
            var min = sec / 60;
            var reste = sec % 60;
            return "Check duration last " + min + " minutes" + (reste > 0 ? " and " + reste + "seconds." : ".");
        }
        public static string TranslateOperator(string comparisonOperator)
        {
            var compOperator = comparisonOperator;
            if (comparisonOperator.Equals("GreaterThanOrEqualToThreshold"))
            {
                compOperator = ">=";
            }
            else if (comparisonOperator.Equals("GreaterThanThreshold"))
            {
                compOperator = ">";
            }
            else if (comparisonOperator.Equals("LessThanThreshold"))
            {
                compOperator = "<";
            }
            else if (comparisonOperator.Equals("LessThanOrEqualToThreshold"))
            {
                compOperator = "<=";
            }
            return compOperator;
        }
        public static string ReverseOperator(string comparisonOperator)
        {
            var compOperator = comparisonOperator;
            if (comparisonOperator.Equals(">="))
            {
                compOperator = "<";
            }
            else if (comparisonOperator.Equals(">"))
            {
                compOperator = "<=";
            }
            else if (comparisonOperator.Equals("<"))
            {
                compOperator = ">=";
            }
            else if (comparisonOperator.Equals("<="))
            {
                compOperator = ">";
            }
            return compOperator;
        }
        public static string GenerateMetricDescription(AlarmMessage alarm)
        {
            var compOperator = TranslateOperator(alarm.Trigger.ComparisonOperator);
            if (alarm.NewStateValue.Equals(ALARM_STATE.INSUFFICIANT_DATA.ToString()))
            {
                return "Metric " + alarm.Trigger.MetricName + " is " + compOperator + " " + alarm.Trigger.Threshold + " --> CANNOT BE EVALUATED!";
            }
            else if (alarm.NewStateValue.Equals(ALARM_STATE.OK.ToString()))
            {
                compOperator = ReverseOperator(compOperator);
            }
            return "Metric " + alarm.Trigger.MetricName + " is " + compOperator + " " + alarm.Trigger.Threshold + ".";
        }
        public static string GenerateDimensionsDescription(AlarmMessage alarm)
        {
            string temp = "";
            foreach (var item in alarm.Trigger.Dimensions)
            {
                temp += item.Name + " = " + item.Value + "<br/>";
            }
            return temp;
        }
        public static string GenerateHtmlHeader()
        {
            string body = string.Empty;
            //using (StreamReader reader = new StreamReader(@"https://s3-eu-west-1.amazonaws.com/taits/test.html"))
            //{
            //    body = reader.ReadToEnd();
            //}
            return body;
        }
        #endregion static
    }
}
