using InstrumentedRabbitMqDotNetClient.Connection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using System.Threading;
using InstrumentedRabbitMqDotNetClient;
using RabbitMQ.Client;
using InstrumentedRabbitMqDotNetClient.Subscription;
using InstrumentedRabbitMqDotNetClient.Publishing;

internal class RabbitMQInitializerHostedService(
    RabbitMQConfiguration configuration,
    IConnectionManager connectionManager,
    SubscribingChannel subscribingChannel,
    PublishingChannel publishingChannel) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Establish the connection
        var connection = connectionManager.Connection;

        // Subscribing
        var subscriptionModel = connection.CreateModel();
        subscriptionModel.ExchangeDeclare(exchange: configuration.Exchange, type: ExchangeType.Topic, true);
        subscriptionModel.QueueDeclare(configuration.QueueName, true, false);
        subscriptionModel.BasicQos(0, 1, false);

        subscribingChannel.SetChannel(subscriptionModel);

        // Publishing
        var publishingModel = connection.CreateModel();
        publishingModel.ExchangeDeclare(exchange: configuration.Exchange, type: ExchangeType.Topic, true);
        publishingChannel.SetChannel(publishingModel);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // No need to close the connection, it is handled by the ConnectionManager
        return Task.CompletedTask;
    }

}
