using RabbitMQ.Client;

namespace InstrumentedRabbitMqDotNetClient.Subscribing;

internal interface ISubscribingChannel
{
    public IModel Channel { get; }
}

internal class SubscribingChannel : ISubscribingChannel
{
    private IModel _channel;

    public IModel Channel => _channel;

    public void SetChannel(IModel channel) => _channel = channel;
}