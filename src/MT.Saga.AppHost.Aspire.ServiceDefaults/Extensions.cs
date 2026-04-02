using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useCompactJsonConsole = builder.Configuration.GetValue<bool?>("Logging:Console:UseCompactJson")
            ?? !builder.Environment.IsDevelopment();
        var hasConfiguredSerilogSinks = builder.Configuration
            .GetSection("Serilog:WriteTo")
            .GetChildren()
            .Any();

        builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.With<TraceContextEnricher>()
                .Enrich.WithProperty("service.name", builder.Environment.ApplicationName)
                .Enrich.WithProperty("deployment.environment", builder.Environment.EnvironmentName);

            // Add a default console sink only when no sink is configured in appsettings.
            // This prevents duplicate log events when users configure Serilog:WriteTo (for example, Console + OTLP).
            if (!hasConfiguredSerilogSinks)
            {
                if (useCompactJsonConsole)
                {
                    loggerConfiguration.WriteTo.Console(new RenderedCompactJsonFormatter());
                }
                else
                {
                    loggerConfiguration.WriteTo.Console();
                }
            }
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

        // When running under Aspire, OTLP endpoint can be exposed through dashboard-specific env vars.
        // Use them as fallback to avoid hard dependencies on manual OTEL_* configuration.
        if (string.IsNullOrWhiteSpace(endpointValue))
        {
            endpointValue = configuration["ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL"]
                ?? configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"];
        }

        endpointValue = NormalizeOtlpBaseEndpoint(endpointValue);

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

    private static string? NormalizeOtlpBaseEndpoint(string? endpointValue)
    {
        if (string.IsNullOrWhiteSpace(endpointValue))
        {
            return endpointValue;
        }

        var normalized = endpointValue.Trim();

        // OTLP SDK expects a base endpoint for multi-signal exporters.
        // If a signal-specific path is provided, trim to base to keep logs/traces/metrics working together.
        var suffixes = new[] { "/v1/logs", "/v1/traces", "/v1/metrics" };
        var suffixIndex = Array.FindIndex(
            suffixes,
            suffix => normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        if (suffixIndex >= 0)
        {
            var suffix = suffixes[suffixIndex];
            normalized = normalized[..^suffix.Length];
        }

        return normalized.TrimEnd('/');
    }
}
