﻿using System.Collections.Generic;
using System.Text;
using InstrumentedRabbitMqDotNetClient.Connection;
using InstrumentedRabbitMqDotNetClient.Contracts;
using InstrumentedRabbitMqDotNetClient.Instrumentation;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace InstrumentedRabbitMqDotNetClient.Publishing
{
    internal class EventPublisher : IEventPublisher
    {
        private readonly RabbitMQConfiguration _configuration;
        private readonly IPublishingChannel _publishingChannel;
        private readonly ILogger<EventPublisher> _logger;
        private readonly IBasicProperties _basicProperties;
        private readonly IRabbitMQDiagnosticSource _rabbitMQDiagnosticSource;

        public EventPublisher(
            ILoggerFactory loggerFactory,
            RabbitMQConfiguration configuration,
            IPublishingChannel channelProvider,
            IRabbitMQDiagnosticSource rabbitMQDiagnosticSource)
        {
            _configuration = configuration;
            _publishingChannel = channelProvider;
            _rabbitMQDiagnosticSource = rabbitMQDiagnosticSource;
            _logger = loggerFactory.CreateLogger<EventPublisher>();

            _basicProperties = _publishingChannel.Channel.CreateBasicProperties();
            _basicProperties.Headers = new Dictionary<string, object>();
        }

        public void Publish(IEvent theEvent)
        {
            var payload = GetPayload(theEvent);

            PublishEvent(theEvent, payload);
        }

        private static string GetPayload(IEvent theEvent)
        {
            var payload = JsonConvert.SerializeObject(theEvent, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            return payload;
        }

        private void PublishEvent(IEvent theEvent, string payload)
        {
            var activity = _rabbitMQDiagnosticSource.StartSend(_basicProperties, theEvent.EventName, payload);
            var failure = false;

            try
            {
                _publishingChannel
                    .Channel
                    .BasicPublish(
                        _configuration.Exchange,
                        theEvent.EventName,
                        _basicProperties,
                        Encoding.UTF8.GetBytes(payload));

                _logger.LogDebug(
                    "Published event {@PublishedEvent} to exchange {ExchangeName} with RootActivityId {RootActivityId}, ActivityId {ActivityId}, ActivityParentId {ActivityParentId}.",
                    payload,
                    _configuration.Exchange,
                    activity.RootId,
                    activity.Id,
                    activity.ParentId);
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Error when publishing event {eventName} with payload {eventPayload}: {@exception}", theEvent.EventName, payload, ex);
                failure = true;
                throw;
            }
            finally
            {
                _rabbitMQDiagnosticSource.Stop(activity, failure);
            }
        }
    }
}