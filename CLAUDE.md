# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A RabbitMQ client library for ASP.NET Core 8 with OpenTelemetry instrumentation for distributed tracing. This library wraps RabbitMQ.Client and provides automatic activity/span creation that propagates trace context through message headers, enabling end-to-end tracing in distributed systems (compatible with Jaeger, Zipkin, etc.).

## Build and Test Commands

Build the solution:
```bash
dotnet build InstrumentedRabbitMqDotNetClient.sln
```

Build and generate NuGet package:
```bash
dotnet build InstrumentedRabbitMqDotNetClient/InstrumentedRabbitMqDotNetClient.csproj
# Package is auto-generated to bin/Debug or bin/Release
```

Run the test application:
```bash
dotnet run --project InstrumentedRabbitMqDotNetClient.TestApplication/InstrumentedRabbitMqDotNetClient.TestApplication.csproj
```

## Testing with Docker

Start RabbitMQ:
```bash
docker run -d --hostname rabbitmq --name rabbitmq -p 15672:15672 -p 5672:5672 rabbitmq:3-management
```

Start Jaeger for tracing:
```bash
docker run -d --name jaeger -e COLLECTOR_ZIPKIN_HOST_PORT=9411 -e COLLECTOR_OTLP_ENABLED=true -p 4317:4317 -p 4318:4318 -p 5775:5775/udp -p 5778:5778 -p 6831:6831/udp -p 6832:6832/udp -p 14250:14250 -p 14268:14268 -p 14269:14269 -p 16686:16686 -p 9411:9411 jaegertracing/all-in-one:latest
```

Access Jaeger UI: http://localhost:16686
Access RabbitMQ Management: http://localhost:15672 (guest/guest)

## Architecture

### Core Components

1. **Connection Management** (`Connection/`)
   - `ConnectionManager`: Singleton that manages a single RabbitMQ connection for the entire application
   - `FluentConnector`: Retry logic wrapper (3 minutes timeout, 10 second intervals) for establishing connections
   - A single connection is shared by both publishing and subscribing

2. **Channel Separation**
   - `PublishingChannel`: Dedicated channel for publishing events (singleton)
   - `SubscribingChannel`: Dedicated channel for consuming events (singleton)
   - Both channels are created from the same connection during initialization

3. **Initialization** (`RabbitMQInitializerHostedService`)
   - Background service that runs on application startup
   - Establishes the connection, creates channels, declares exchange and queue
   - Must complete before publishing/subscribing can occur

4. **Publishing** (`Publishing/`)
   - `EventPublisher`: Scoped service injected where events need to be published
   - Serializes events to JSON, creates OpenTelemetry activity (Producer), injects trace context into message headers
   - Uses BasicPublish with the event's `EventName` as the routing key

5. **Subscribing** (`Subscribing/`)
   - `RabbitMQSubscriberHostedService`: Background service that consumes from the queue
   - Automatically discovers and registers all classes implementing `IEventSubscription<TEvent>` via reflection
   - Binds queue to exchange for each discovered event name (routing key)
   - Extracts trace context from headers and creates OpenTelemetry activity (Consumer)
   - Routes each message to the appropriate handler based on event name

6. **Instrumentation** (`Instrumentation/`)
   - `RabbitMQDiagnosticSource`: Central class for OpenTelemetry integration
   - Uses `ActivitySource` (name: "RabbitMqClient") to create activities
   - `StartSend()`: Creates Producer activity, injects trace context into message headers
   - `StartProcess()`: Extracts trace context from headers, creates Consumer activity as child of Producer
   - `Stop()`: Completes activities with success/error status

7. **Contracts** (`InstrumentedRabbitMqDotNetClient.Interfaces/`)
   - `IEvent`: Interface for events, requires `EventName` property (used as routing key)
   - `IEventSubscription<TEvent>`: Interface for event handlers, requires `HandleEventAsync()` method
   - A new handler instance is created for each message (using scoped DI)

### Key Design Patterns

- **Automatic Discovery**: Event subscriptions are discovered at startup via reflection (`EventSubscriptionSearcher`)
- **Trace Propagation**: OpenTelemetry context is injected into RabbitMQ headers using `TextMapPropagator` and extracted on the consumer side
- **Single Connection**: One connection is shared, but separate channels for pub/sub to avoid threading issues
- **Hosted Services**: Background services handle connection initialization and message consumption lifecycle

### Configuration

Required environment variables:
- `RABBITMQ_HOST`: RabbitMQ server URL
- `RABBITMQ_EXCHANGE`: Exchange name for publishing/subscribing
- `RABBITMQ_USER`: Authentication username
- `RABBITMQ_PASSWORD`: Authentication password

Queue name is provided via `AddRabbitMQ(queueName)` call.

### Registration Pattern

In `Program.cs`:
1. Add OpenTelemetry with RabbitMQ instrumentation:
```csharp
builder.Services.AddOpenTelemetry().WithTracing(builder => builder
    .AddRabbitMqInstrumentation()  // Adds "RabbitMqClient" as activity source
    // ... other instrumentation
);
```

2. Register RabbitMQ services:
```csharp
builder.Services.AddRabbitMQ(serviceName);  // serviceName becomes queue name
```

### Publishing Events

1. Create a record/class implementing `IEvent`
2. Inject `IEventPublisher` (scoped)
3. Call `eventPublisher.Publish(new MyEvent())`
4. Event is serialized to JSON, routing key = `EventName`, trace context injected into headers

### Subscribing to Events

1. Create a class implementing `IEventSubscription<TEvent>`
2. Implement `HandleEventAsync(TEvent receivedEvent, string operationId)`
3. Handler is automatically discovered and registered at startup
4. Queue is automatically bound to the exchange with event name as routing key
5. Dependencies can be injected into handler constructor (new instance per message)

## Important Notes

- The `RabbitMQDiagnosticSource` class is the core of the OpenTelemetry integration - all changes to tracing behavior should go through this class
- The approach is based on https://www.mytechramblings.com/posts/getting-started-with-opentelemetry-and-dotnet-core/
- Event handlers receive `operationId` (trace ID) for logging correlation
- The library uses Newtonsoft.Json for serialization (not System.Text.Json)
- All RabbitMQ operations use topic exchange type with event names as routing keys
- Connection retry logic: 3 minutes timeout with 10-second intervals between attempts
