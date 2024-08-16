using RabbitMQ.Client;

namespace InstrumentedRabbitMqDotNetClient.Connection
{
    internal interface IConnectionManager
    {
        IConnection Connection { get; }
    }
}