using System;
using InstrumentedRabbitMqDotNetClient.Connection;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using InstrumentedRabbitMqDotNetClient.Contracts;
using InstrumentedRabbitMqDotNetClient.Instrumentation;
using InstrumentedRabbitMqDotNetClient.Publishing;
using InstrumentedRabbitMqDotNetClient.Subscription;
using OpenTelemetry.Trace;

namespace InstrumentedRabbitMqDotNetClient
{
    /// <summary>
    /// <para>
    /// Provides extension methods to configure RabbitMQ  in an instance of the <see cref="IServiceCollection"/> class.
    /// </para>
    /// </summary>
    public static class ServicesConfigurationExtensions
    {
        /// <summary>
        /// <para>
        /// Adds all the necessary classes to the ServiceCollection and configures RabbitMQ so it automatically binds any class implementing the <see cref="IEventSubscription{TEvent}"/> interface.
        /// </para>
        /// <para>
        /// <remarks>
        /// This method must be called in the <c>Startup.ConfigureServices()</c> method of your micro service.
        /// </remarks>
        /// </para>
        /// <para>
        /// <remarks>
        /// Requires the following environment variables:
        /// <list type="bullet">
        /// <item><term>RABBITMQ_HOST</term><description>The URL of the RabbitMQ server.</description></item>
        /// <item><term>RABBITMQ_EXCHANGE</term><description>The Exchange name to which events will be publish.</description></item>
        /// <item><term>RABBITMQ_USER</term><description></description>Username to authenticate in the RabbitMQ server when establishing a connection.</item>
        /// <item><term>RABBITMQ_PASSWORD</term><description></description>Password to authenticate in the RabbitMQ server when establishing a connection.</item>
        /// </list>
        /// </remarks>
        /// </para>
        /// </summary>
        /// <param name="services">The service collection where to register RabbitMQ.</param>
        /// <param name="queueName">The name of the queue where to read events from.</param>
        public static void AddRabbitMQSubscriberHostedService(this IServiceCollection services, string queueName)
        {
            var rabbitMQConfiguration = GetRabbitMQConfiguration(queueName);

            RegisterEventSubscriptions(services);

            services.AddSingleton(rabbitMQConfiguration);
            services.AddSingleton<IChannelProvider, ChannelProvider>();
            services.AddSingleton<IEventSubscriptionFactory, EventSubscriptionFactory>();
            services.AddSingleton<IFluentConnector, FluentConnector>();
            services.AddSingleton<IConnectionFactory>(new ConnectionFactory
            {
                HostName = rabbitMQConfiguration.Host,
                UserName = rabbitMQConfiguration.User,
                Password = rabbitMQConfiguration.Password
            });
            services.AddScoped<IEventPublisher, EventPublisher>();
            services.AddTransient<IRabbitMQDiagnosticSource, RabbitMQDiagnosticSource>();
            services.AddSingleton<IConnectionManager, ConnectionManager>();
            services.AddHostedService<RabbitMQSubscriberHostedService>();
        }

        /// <summary>
        /// Enables RabbitMQ instrumentation.
        /// <para>
        /// <remarks>
        /// This method must be called in the <c>Startup.cs<c> in the </c>builder.Services.AddOpenTelemetryTracing()</c> method of your micro service.
        /// </remarks>
        /// </para>
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static TracerProviderBuilder AddRabbitMqInstrumentation(this TracerProviderBuilder builder)
        {
            builder.AddSource(RabbitMQDiagnosticSource.RabbitMQDiagnosticSourceName);
            return builder;
        }

        private static RabbitMQConfiguration GetRabbitMQConfiguration(string queueName)
        {
            var envVars = EnvironmentVariableGetter.GetValues("RABBITMQ_HOST", "RABBITMQ_EXCHANGE", "RABBITMQ_USER", "RABBITMQ_PASSWORD");
            var rabbitMQConfiguration = new RabbitMQConfiguration
            {
                Host = envVars["RABBITMQ_HOST"],
                Exchange = envVars["RABBITMQ_EXCHANGE"],
                User = envVars["RABBITMQ_USER"],
                Password = envVars["RABBITMQ_PASSWORD"],
                QueueName = queueName
            };
            return rabbitMQConfiguration;
        }

        private static void RegisterEventSubscriptions(IServiceCollection services)
        {
            var typesToRegister = EventSubscriptionSearcher.GetEventSubscriptionTypes();

            foreach (var type in typesToRegister)
            {
                services.AddTransient(type);
            }

            Console.WriteLine($"Registered '{typesToRegister.Count}' subscriptions: '{string.Join($"{Environment.NewLine} - ", typesToRegister)}'.");
        }
    }
}