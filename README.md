# dotnet-rabbitmq-client-instrumentation-app-insights
A RabbitMQ client for ASP .NET Core 7 with Open Telemetry instrumentation so that events are properly traced and compatible with any distributed tracing system.

![Instrumented RabbitMQ in Jaeger](https://i.imgur.com/7TyJBVR.png)

## How to use

1. Start RabbitMQ in Docker:
```bash
docker run -d --hostname rabbitmq --name rabbitmq -p 15672:15672 -p 5672:5672 rabbitmq:3-management
```

2. Start Jaeger in Docker:
```bash
docker run -d --name jaeger -e COLLECTOR_ZIPKIN_HOST_PORT=9411 -e COLLECTOR_OTLP_ENABLED=true -p 4317:4317 -p 4318:4318 -p 5775:5775/udp -p 5778:5778 -p 6831:6831/udp -p 6832:6832/udp -p 14250:14250 -p 14268:14268 -p 14269:14269 -p 16686:16686 -p 9411:9411 jaegertracing/all-in-one:latest
```

3. Run the `InstrumentedRabbitMqDotNetClient.TestApplication`

4. Execut the GET method in the `api.http` file.

5. Open Jaeger: http://localhost:16686

6. In Service, select `RabbitMQTestApplication`

7. Click on `Find traces`.

8. Click on the trace `RabbitMQTestApplication: /publish`

### Register it in Program
1. In the `Program` class, add `AddRabbitMqInstrumentation()` to your existing OpenTelemetry code:

```csharp
builder.Services.AddOpenTelemetryTracing(tracerProviderBuilder =>
{
    tracerProviderBuilder
        .AddOtlpExporter(opt =>
        {
            opt.Protocol = OtlpExportProtocol.HttpProtobuf;
        })
        .AddSource(serviceName)
        .SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService(serviceName, serviceVersion))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation()
        .AddRabbitMqInstrumentation(); // <- This line
});
```

2. Afterwards, add the following line:
```csharp
builder.Services.AddRabbitMQSubscriberHostedService(serviceName);
```

### Create an event
1. Declare a `record` that inherits from `IEvent`.
```csharp
public record MyEvent : IEvent
{
    public string EventName => "my.event";
}
```
### Publish an event
1. Inject `IEventPublisher` in:

    1. A minimal api method:
        ```csharp
        string Publish([FromServices] IEventPublisher eventPublisher)
        {
            eventPublisher.Publish(new TestEvent());
            return $"Event Published at {DateTime.Now.ToLongDateString()} - {DateTime.Now.ToLongTimeString()}";
        }

        app.MapGet("/publish", Publish);
        ```
    2. a class:
        ```csharp
        public class MyClass
        {
            private readonly IEventPublisher _eventPublisher;

            public MyClass(IEventPublisher eventPublisher)
            {
                _eventPublisher = eventPublisher;
            }

            public void DoSomething()
            {
                this._eventPublisher.Publish(new MyEvent())
            }
        }
        ```

### Subscribe to an event
1. Create a class to inherit from `IEventSubscription<MyEvent>`:
```csharp
public class MyEventSubscription : IEventSubscription<MyEvent>
{
    public Task HandleEventAsync(MyEvent receivedEvent, string operationId)
    {
        // Your logic here.
    }
}
```

## Instrumentation
The aporoach is based on my  repo [dotnet-rabbitmq-client-instrumentation-app-insights](https://github.com/JoanComasFdz/dotnet-rabbitmq-client-instrumentation-app-insights), where App Insights has been removed and Open Telemtry has been added.

Ultimately, it uses the same structure with all parts of the library calling the `RabbitMQDiagnosticSource` class. Is this class that has been updated to use ActivitySource and Activity in the correct way.

The work in this class is based on:
- https://www.mytechramblings.com/posts/getting-started-with-opentelemetry-and-dotnet-core/
- https://github.com/karlospn/opentelemetry-tracing-demo

### How it works
A [DiagnosticSource](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Diagnostics.DiagnosticSource/src/DiagnosticSourceUsersGuide.md) is available in the `Instrumentation` folder and contains all the necessary code to instrument the calls to and from RabbitMQ.

The `EventPublisher` uses it to start the activity.

The `RabbitMQSubscriberHostedService` uses it to start processing the event and to to signal that the event processing has finished.

## Further work
Looks like MassTransit is using a different approach, so its worth exploring:
- https://masstransit-project.com/advanced/monitoring/diagnostic-source.html
- https://github.com/alexeyzimarev/ndc-2020-talk-tracetrace/
- [![OpenMetrics, OpenTracing, OpenTelemetry - are we there yet? - Alexey Zimarev - NDC Oslo 2020](https://img.youtube.com/vi/0vl-4OhPyQY/0.jpg)](https://www.youtube.com/watch?v=0vl-4OhPyQY)
