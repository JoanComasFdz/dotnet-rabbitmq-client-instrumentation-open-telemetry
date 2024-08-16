using System;
using System.Collections.Generic;

namespace InstrumentedRabbitMqDotNetClient.Subscribing;

internal interface IEventSubscriptionFactory
{
    IEnumerable<string> EventNames { get; }

    EventSubscriptionWrapper CreateEventBusSubscription(IServiceProvider serviceProvider, string eventName);

    Type GetEventType(string eventName);
}