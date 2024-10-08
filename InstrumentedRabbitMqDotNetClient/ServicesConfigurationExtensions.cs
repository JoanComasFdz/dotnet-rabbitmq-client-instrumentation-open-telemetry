﻿using System;
using InstrumentedRabbitMqDotNetClient.Connection;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using InstrumentedRabbitMqDotNetClient.Contracts;
using InstrumentedRabbitMqDotNetClient.Instrumentation;
using InstrumentedRabbitMqDotNetClient.Publishing;
using InstrumentedRabbitMqDotNetClient.Subscribing;
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
        /// This method must be called in the <c>Startup.ConfigureServices()</c> method or <c>Program.cs</c> of your ASP .Net Core application.
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
        public static void AddRabbitMQ(this IServiceCollection services, string queueName)
        {
            var rabbitMQConfiguration = GetRabbitMQConfiguration(queueName);
            services.AddSingleton(rabbitMQConfiguration);

            // Tracing
            services.AddTransient<IRabbitMQDiagnosticSource, RabbitMQDiagnosticSource>();

            // Connection
            services.AddSingleton<IConnectionFactory>(new ConnectionFactory
            {
                HostName = rabbitMQConfiguration.Host,
                UserName = rabbitMQConfiguration.User,
                Password = rabbitMQConfiguration.Password
            });
            services.AddSingleton<IFluentConnector, FluentConnector>();
            services.AddSingleton<IConnectionManager, ConnectionManager>();
            services.AddHostedService<RabbitMQInitializerHostedService>();

            // Subscribing
            services.RegisterEventSubscriptions();
            services.AddSingleton<IEventSubscriptionFactory, EventSubscriptionFactory>();
            services.AddSingleton<SubscribingChannel>();
            services.AddSingleton<ISubscribingChannel>(p =>p.GetRequiredService<SubscribingChannel>());
            services.AddHostedService<RabbitMQSubscriberHostedService>();

            // Publishing
            services.AddSingleton<PublishingChannel>();
            services.AddSingleton<IPublishingChannel>(p => p.GetRequiredService<Publishing.PublishingChannel>());
            services.AddScoped<IEventPublisher, EventPublisher>();
        }

        /// <summary>
        /// Enables this RabbitMQ library to configue the traces properly.
        /// <para>
        /// <remarks>
        /// This method must be called in the <c>Startup.cs</c> or <c>Program.cs</c> of your ASP .Net Core application,
        /// in the <c>builder.Services.AddOpenTelemetry().WithTracing(builder => ...)</c> method.
        /// </remarks>
        /// </para>
        /// </summary>
        /// <param name="builder"></param>
        /// <returns>The same instance of the <see cref="TracerProviderBuilder"/> that was passed as parameter.</returns>
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

        private static void RegisterEventSubscriptions(this IServiceCollection services)
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