using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using System.Linq;
using System.Text;

namespace InstrumentedRabbitMqDotNetClient.Instrumentation
{
    /// <summary>
    /// <para>
    /// Creates activities and tells a <see cref="DiagnosticListener"/> to start or stop them.
    /// </para>
    /// <para>
    /// Application Insights is automatically tracking events from "Microsoft.Azure.ServiceBus" and sending them correctly to Azure.
    /// To avoid creating all the necessary code to receive the events, we just mimic ServiceBus and let Application Insights to the job.
    /// </para>
    /// </summary>
    internal class RabbitMQDiagnosticSource : IRabbitMQDiagnosticSource
    {
        public const string RabbitMQDiagnosticSourceName = "RabbitMqClient";

        // Source: https://www.mytechramblings.com/posts/getting-started-with-opentelemetry-and-dotnet-core/
        private static readonly ActivitySource ActivitySource = new(RabbitMQDiagnosticSourceName);
        private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

        private static readonly Activity NoActivity = new ("");

        public Activity StartSend(IBasicProperties props, string eventName, string payload)
        {
            var sendEventName = $"{RabbitMQDiagnosticSourceName}.Send";
            var activity = ActivitySource.StartActivity(sendEventName, ActivityKind.Producer);
            if (activity is null)
            {
                return NoActivity;
            }
            activity.SetTag("MessageId", eventName);
            activity.SetTag("MessageContent", payload);
            AddActivityToHeader(activity, props);
            return activity;
        }

        private void AddActivityToHeader(Activity activity, IBasicProperties props)
        {
            Propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), props, InjectContextIntoHeader);
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination_kind", "queue");
            activity?.SetTag("messaging.rabbitmq.queue", "sample");
        }

        private void InjectContextIntoHeader(IBasicProperties props, string key, string value)
        {
            props.Headers ??= new Dictionary<string, object>();
            props.Headers[key] = value;
        }

        public Activity StartProcess(IBasicProperties props, string eventName)
        {
            var processEventName = $"{RabbitMQDiagnosticSourceName}.Process";
            var parentContext = Propagator.Extract(default, props, ExtractTraceContextFromBasicProperties);
            Baggage.Current = parentContext.Baggage;

            var activity = ActivitySource.StartActivity(processEventName, ActivityKind.Consumer, parentContext.ActivityContext);
            if (activity is null)
            {
                return NoActivity;
            }
            AddActivityTags(activity);
            return activity;
        }

        private static IEnumerable<string> ExtractTraceContextFromBasicProperties(IBasicProperties props, string key)
        {
            if (props.Headers == null)
            {
                return Enumerable.Empty<string>();
            }

            if (!props.Headers.TryGetValue(key, out var value))
            {
                return Enumerable.Empty<string>();
            }

            var bytes = value as byte[];
            return new[] { Encoding.UTF8.GetString(bytes) };
        }

        private static void AddActivityTags(Activity activity)
        {
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination_kind", "queue");
            activity?.SetTag("messaging.rabbitmq.queue", "sample");
        }

        public void Stop(Activity activity, bool failure = false)
        {
            if (activity == NoActivity)
            {
                return;
            }
            activity.SetStatus(failure? ActivityStatusCode.Error : ActivityStatusCode.Ok);
            activity.Stop();
        }
    }
}