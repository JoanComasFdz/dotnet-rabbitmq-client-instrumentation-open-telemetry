using RabbitMQ.Client;

namespace InstrumentedRabbitMqDotNetClient.Publishing;

internal interface IPublishingChannel
{
    public IModel Channel { get; }
}

internal class PublishingChannel : IPublishingChannel
{
    private IModel _channel;

    public IModel Channel => _channel;

    public void SetChannel(IModel channel) => _channel = channel;
}