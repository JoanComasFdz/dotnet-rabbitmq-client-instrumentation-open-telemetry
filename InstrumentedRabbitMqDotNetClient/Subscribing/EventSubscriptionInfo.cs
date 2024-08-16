using System;

namespace InstrumentedRabbitMqDotNetClient.Subscribing;

internal record EventSubscriptionInfo
{
    public Type EventSubscriptionType { get; init; }

    public Type EventType { get; init; }
}