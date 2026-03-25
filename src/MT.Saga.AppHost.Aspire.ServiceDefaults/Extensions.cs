using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.With<TraceContextEnricher>()
                .Enrich.WithProperty("service.name", builder.Environment.ApplicationName)
                .Enrich.WithProperty("deployment.environment", builder.Environment.EnvironmentName)
                .WriteTo.Console();
        });

        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;

            var endpoint = ResolveOtlpEndpoint(builder.Configuration);
            if (endpoint is not null)
            {
                logging.AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = endpoint;
                    otlp.Protocol = ResolveOtlpProtocol(builder.Configuration);
                    otlp.Headers = builder.Configuration["OTEL_EXPORTER_OTLP_HEADERS"];
                });
            }
        });

        var openTelemetryBuilder = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: builder.Environment.ApplicationName,
                    serviceVersion: typeof(Extensions).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = builder.Environment.EnvironmentName,
                    ["host.name"] = Environment.MachineName
                }))
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddSource("MassTransit")
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters(openTelemetryBuilder);

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Always map health checks for integration tests and monitoring
        app.MapHealthChecks(HealthEndpointPath);
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live")
        });

        return app;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder, OpenTelemetryBuilder openTelemetryBuilder) where TBuilder : IHostApplicationBuilder
    {
        var endpoint = ResolveOtlpEndpoint(builder.Configuration);
        if (endpoint is not null)
        {
            var protocol = ResolveOtlpProtocol(builder.Configuration);
            var headers = builder.Configuration["OTEL_EXPORTER_OTLP_HEADERS"];

            openTelemetryBuilder.WithTracing(tracing =>
            {
                tracing.AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = endpoint;
                    otlp.Protocol = protocol;
                    otlp.Headers = headers;
                });
            });

            openTelemetryBuilder.WithMetrics(metrics =>
            {
                metrics.AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = endpoint;
                    otlp.Protocol = protocol;
                    otlp.Headers = headers;
                });
            });

        }

        return builder;
    }

    private static Uri? ResolveOtlpEndpoint(IConfiguration configuration)
    {
        var endpointValue = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        return Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint)
            ? endpoint
            : null;
    }

    private static OtlpExportProtocol ResolveOtlpProtocol(IConfiguration configuration)
    {
        var protocol = configuration["OTEL_EXPORTER_OTLP_PROTOCOL"];
        return string.Equals(protocol, "grpc", StringComparison.OrdinalIgnoreCase)
            ? OtlpExportProtocol.Grpc
            : OtlpExportProtocol.HttpProtobuf;
    }
}
