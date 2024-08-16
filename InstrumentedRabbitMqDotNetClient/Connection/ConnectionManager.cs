using RabbitMQ.Client;
using System;

namespace InstrumentedRabbitMqDotNetClient.Connection;

internal class ConnectionManager : IConnectionManager
{
    private readonly IFluentConnector _fluentConnector;
    private IConnection _connection;
    private readonly object _lock = new();

    public ConnectionManager(IFluentConnector fluentConnector)
    {
        _fluentConnector = fluentConnector;
    }

    public IConnection Connection
    {
        get
        {
            lock (_lock)
            {
                _connection ??= _fluentConnector
                        .TryFor(TimeSpan.FromMinutes(3))
                        .RetryEvery(TimeSpan.FromSeconds(10))
                        .Connect();
            }

            return _connection;
        }
    }
}
