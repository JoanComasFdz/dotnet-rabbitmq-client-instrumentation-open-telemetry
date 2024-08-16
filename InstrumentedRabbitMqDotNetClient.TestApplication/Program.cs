using InstrumentedRabbitMqDotNetClient;
using InstrumentedRabbitMqDotNetClient.Publishing;
using InstrumentedRabbitMqDotNetClient.TestApplication;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;

// Define some important constants to initialize tracing with
var serviceName = "RabbitMQTestApplication";
var serviceVersion = "1.0.0";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Configure important OpenTelemetry settings, the console exporter, and instrumentation library
// IMPORTANT: DO NOT USE THE NEW METHODS, THEY DO NOT WORK
builder.Services.AddOpenTelemetry().
    WithTracing(builder => builder
        .AddSource(serviceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService(serviceName, serviceVersion))
        .AddConsoleExporter() // For debugging purposes
        .AddOtlpExporter(options =>
        {
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        })
        .AddSqlClientInstrumentation()
        .AddRabbitMqInstrumentation()
        );

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRabbitMQ(serviceName);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

string Publish([FromServices] IEventPublisher eventPublisher)
{
    eventPublisher.Publish(new TestEvent());
    return $"Event Published at {DateTime.Now.ToLongDateString()} - {DateTime.Now.ToLongTimeString()}";
}

app.MapGet("/publish", Publish);


app.Run();