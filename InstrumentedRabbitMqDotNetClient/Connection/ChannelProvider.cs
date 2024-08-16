using RabbitMQ.Client;

namespace InstrumentedRabbitMqDotNetClient.Connection
{
    internal class ChannelProvider : IChannelProvider
    {
        private readonly IModel _channel;

        public ChannelProvider(RabbitMQConfiguration configuration, IConnectionManager connectionManager)
        {
            _channel = connectionManager.Connection.CreateModel();
            _channel.ExchangeDeclare(exchange: configuration.Exchange, type: ExchangeType.Topic, true);
        }

        public IModel GetChannel() => _channel;
    }
}