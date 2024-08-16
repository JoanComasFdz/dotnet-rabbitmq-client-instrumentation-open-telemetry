﻿using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InstrumentedRabbitMqDotNetClient.Connection;
using InstrumentedRabbitMqDotNetClient.Contracts;
using InstrumentedRabbitMqDotNetClient.Instrumentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace InstrumentedRabbitMqDotNetClient.Subscription
{
    internal class RabbitMQSubscriberHostedService : BackgroundService
    {
        private readonly RabbitMQConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventSubscriptionFactory _eventSubscriptionFactory;
        private readonly IConnectionManager _connectionManager;
        private readonly IRabbitMQDiagnosticSource _rabbitMQDiagnosticSource;

        private IModel _subscriptionChannel;
        private EventingBasicConsumer _consumer;

        public RabbitMQSubscriberHostedService(
            RabbitMQConfiguration configuration,
            ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider,
            IEventSubscriptionFactory eventSubscriptionFactory,
            IConnectionManager connectionManager,
            IRabbitMQDiagnosticSource rabbitMQDiagnosticSource)
        {
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger<RabbitMQSubscriberHostedService>();
            _serviceProvider = serviceProvider;
            _eventSubscriptionFactory = eventSubscriptionFactory;
            _connectionManager = connectionManager;
            _rabbitMQDiagnosticSource = rabbitMQDiagnosticSource;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            Connect();

            ConfigureConsumer();

            AddSubscriptions();

            return Task.CompletedTask;
        }

        private void Connect()
        {
            _logger.LogDebug("Connecting to RabbitMQ...");

            _subscriptionChannel = _connectionManager.Connection.CreateModel();
            _subscriptionChannel.ExchangeDeclare(exchange: _configuration.Exchange, type: ExchangeType.Topic, true);
            _subscriptionChannel.QueueDeclare(_configuration.QueueName, true, false);
            _subscriptionChannel.BasicQos(0, 1, false);
            _connectionManager.Connection.ConnectionShutdown += RabbitMQ_ConnectionShutdown;

            _logger.LogInformation("Connected to RabbitQM at '{Host}' exchange '{Exchange}' queue '{QueueName}'.",
                _configuration.Host,
                _configuration.Exchange,
                _configuration.QueueName);
        }

        private void ConfigureConsumer()
        {
            _logger.LogDebug("Configuring consumer...");
            _consumer = new EventingBasicConsumer(_subscriptionChannel);
            _consumer.Received += ReceivedEventHandler;
            _consumer.Shutdown += OnConsumerShutdown;
            _consumer.Registered += OnConsumerRegistered;
            _consumer.Unregistered += OnConsumerUnregistered;
            _consumer.ConsumerCancelled += OnConsumerConsumerCancelled;

            _subscriptionChannel.BasicConsume(_configuration.QueueName, false, _consumer);

            _logger.LogDebug("Consumer configured.");
        }

        private void AddSubscriptions()
        {
            foreach (var eventName in _eventSubscriptionFactory.EventNames)
            {
                _logger.LogDebug("Binding queue '{QueueName}' on exchange '{Exchange}' to event '{EventName}'...",
                    _configuration.QueueName,
                    _configuration.Exchange,
                    eventName);
                _subscriptionChannel.QueueBind(queue: _configuration.QueueName, exchange: _configuration.Exchange, routingKey: eventName);
            }

            _logger.LogInformation("All event subscriptions bound!");
        }

        private void OnConsumerConsumerCancelled(object sender, ConsumerEventArgs e) { }
        private void OnConsumerUnregistered(object sender, ConsumerEventArgs e) { }
        private void OnConsumerRegistered(object sender, ConsumerEventArgs e) { }
        private void OnConsumerShutdown(object sender, ShutdownEventArgs e) { }
        private void RabbitMQ_ConnectionShutdown(object sender, ShutdownEventArgs e) { }

        private void ReceivedEventHandler(object model, BasicDeliverEventArgs ea)
        {
            var eventName = ea.RoutingKey;
            var activity = _rabbitMQDiagnosticSource.StartProcess(
                ea.BasicProperties,
                eventName);
            var operationId = activity.TraceId.ToString();
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());

            _logger.LogDebug("Received {eventName} with Operation Id {RequestId}. Processing...",
                eventName,
                operationId);

            HandleMessage(operationId, eventName, message)
                .ContinueWith(a =>
                {
                    _subscriptionChannel.BasicAck(ea.DeliveryTag, false);
                    _rabbitMQDiagnosticSource.Stop(activity, a.IsFaulted);
                    _logger.LogDebug("Finished processing {eventName} on Operation Id {OperationId} and Parent Operation Id.", eventName, operationId);
                });
        }

        private async Task HandleMessage(string requestId, string eventName, string content)
        {
            using var scope = _serviceProvider.CreateScope();
            using var loggerSCope = _logger.BeginScope("{@RequestId}", requestId);
            EventSubscriptionWrapper wrapper = default;
            try
            {
                var eventType = _eventSubscriptionFactory.GetEventType(eventName);
                var parsedEvent = (IEvent) JsonConvert.DeserializeObject(content, eventType);
                wrapper = _eventSubscriptionFactory.CreateEventBusSubscription(scope.ServiceProvider, eventName);

                await wrapper.HandleEventAsync(parsedEvent, requestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "Error executing event handler '{Handler}' for message '{Content}': {ExceptionType}:{ExceptionMessage}.\r\nStackTrace:\r\n{LibEventBusMessageSubscriptionStackTrace}",
                    wrapper.SubscriptionType.Name,
                    content,
                    ex.GetType(),
                    ex.Message,
                    ex.StackTrace);
                throw;
            }
        }

        public override void Dispose()
        {
            _logger.LogInformation("InstrumentedRabbitMqDotNetClient is disposing...");
            _consumer.Received -= ReceivedEventHandler;
            _consumer.Shutdown -= OnConsumerShutdown;
            _consumer.Registered -= OnConsumerRegistered;
            _consumer.Unregistered -= OnConsumerUnregistered;
            _consumer.ConsumerCancelled -= OnConsumerConsumerCancelled;

            _connectionManager.Connection.ConnectionShutdown -= RabbitMQ_ConnectionShutdown;

            _subscriptionChannel.Close();
            _connectionManager.Connection.Close();

            _logger.LogInformation("InstrumentedRabbitMqDotNetClient is disposed.");
            base.Dispose();
        }
    }
}