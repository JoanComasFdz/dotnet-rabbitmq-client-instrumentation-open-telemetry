using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;

namespace InstrumentedRabbitMqDotNetClient.Connection;

internal class ConnectionManager : IConnectionManager, IDisposable
{
    private readonly ILogger<ConnectionManager> _logger;
    private readonly IFluentConnector _fluentConnector;
    private IConnection _connection;
    private readonly object _lock = new();

    public ConnectionManager(ILogger<ConnectionManager> logger, IFluentConnector fluentConnector)
    {
        _logger = logger;
        _fluentConnector = fluentConnector;
    }

    public IConnection Connection
    {
        get
        {
            lock (_lock)
            {
                _logger.LogDebug("Connecting to RabbitMQ...");

                _connection ??= _fluentConnector
                        .TryFor(TimeSpan.FromMinutes(3))
                        .RetryEvery(TimeSpan.FromSeconds(10))
                        .Connect();

                //_connection.ConnectionShutdown += RabbitMQ_ConnectionShutdown;

                _logger.LogInformation("Connected to RabbitQM at '{Host}'.", _connection.Endpoint.HostName);
            }

            return _connection;
        }
    }

    public void Dispose()
    {
        //_connection.ConnectionShutdown -= RabbitMQ_ConnectionShutdown;
        _connection?.Close();
    }

    //private void RabbitMQ_ConnectionShutdown(object sender, ShutdownEventArgs e) { }
}
