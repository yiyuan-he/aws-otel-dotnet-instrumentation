using System;
using Amazon.S3;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net.Http;
using Microsoft.AspNetCore.Http.Extensions;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Instrumentation;
using System.Diagnostics.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace dotnet_sample_app.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AppController : ControllerBase
    {
        private readonly AmazonS3Client s3Client = new AmazonS3Client();
        private readonly HttpClient httpClient = new HttpClient();
        private static Random rand = new Random(DateTime.Now.Millisecond);

        public static readonly ActivitySource tracer = new(
        "dotnet-sample-app");

        public AppController() {}
        
        [HttpGet]
        [Route("/outgoing-http-call")]
        public string OutgoingHttp()
        {
            using var activity = tracer.StartActivity("outgoing-http-call");
            activity?.SetTag("language", "dotnet");
            activity?.SetTag("signal", "trace");
            
            var res = httpClient.GetAsync("https://aws.amazon.com/").Result;
            string statusCode = res.StatusCode.ToString();
            
            // Request Based Metrics
            Startup.metricEmitter.emitReturnTimeMetric(MimicLatency());
            int loadSize = MimicPayLoadSize();
            Startup.metricEmitter.apiRequestSentMetric();
            Startup.metricEmitter.updateTotalBytesSentMetric(loadSize);
            
            return GetTraceId();
        }

        [HttpGet]
        [Route("/test")]
        public string AWSSDKCall()
        {
            using var activity = tracer.StartActivity("test_parent_span");
            activity?.SetTag("language", "dotnet");
            activity?.SetTag("signal", "trace");
            activity?.SetTag("test.attribute", "test_value");

            string traceId = activity?.TraceId.ToString() ?? "no-trace-available";

            return traceId;
        }

        [HttpGet]
        [Route("/")]
        public string Default()
        {
            return "Application started!";
        }

        [HttpGet]
        [Route("/outgoing-sampleapp")]
        public string OutgoingSampleApp()
        {
            using var activity = tracer.StartActivity("outgoing-sampleapp");
            activity?.SetTag("language", "dotnet");
            activity?.SetTag("signal", "trace");
            string statusCode = "";

            if (Program.cfg.SampleAppPorts.Length == 0) {
                var res = httpClient.GetAsync("https://aws.amazon.com/").Result;
                statusCode = res.StatusCode.ToString();
            }
            else {
                foreach (string port in Program.cfg.SampleAppPorts) {
                    if (!String.IsNullOrEmpty(port)) {
                        string uri = string.Format("http://127.0.0.1:{0}/outgoing-sampleapp", port);
                        var res = httpClient.GetAsync(uri).Result;
                        statusCode = res.StatusCode.ToString();
                    }
                }
            }
            
            // Request Based Metrics
            Startup.metricEmitter.emitReturnTimeMetric(MimicLatency());
            int loadSize = MimicPayLoadSize();
            Startup.metricEmitter.apiRequestSentMetric();
            Startup.metricEmitter.updateTotalBytesSentMetric(loadSize);
            
            return GetTraceId();
        }

        private string GetTraceId()
        {
            var traceId = Activity.Current.TraceId.ToHexString();
            var version = "1";
            var epoch = traceId.Substring(0, 8);
            var random = traceId.Substring(8);
            return "{" + "\"traceId\"" + ": " + "\"" + version + "-" + epoch + "-" + random + "\"" + "}";
        }

        private static int MimicPayLoadSize()
        {
            return rand.Next(101);
        }

        private static int MimicLatency()
        {
            return rand.Next(100,500);
        }
    }

}
