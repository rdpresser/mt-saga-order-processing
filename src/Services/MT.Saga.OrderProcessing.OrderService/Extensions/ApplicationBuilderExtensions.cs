using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi;
using MT.Saga.OrderProcessing.OrderService.Features.Orders.CreateOrder;
using MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrderById;
using MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrders;

namespace MT.Saga.OrderProcessing.OrderService.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseOrderService(this WebApplication app)
    {
        var forwardedHeadersOptions = BuildForwardedHeadersOptions(app.Configuration);
        if (forwardedHeadersOptions is not null)
        {
            app.UseForwardedHeaders(forwardedHeadersOptions);
        }

        app.UseIngressPathBase(forwardedHeadersOptions);

        var swaggerEnabled = app.Configuration.GetValue("Swagger:Enabled", true);
        if (swaggerEnabled)
        {
            var configuredPathBase = ResolveConfiguredPathBase(app.Configuration);

            app.UseSwagger(options =>
            {
                options.PreSerializeFilters.Add((document, request) =>
                {
                    var requestPathBase = request.PathBase.ToString();
                    var serverUrl = requestPathBase;
                    if (string.IsNullOrWhiteSpace(serverUrl))
                    {
                        serverUrl = string.IsNullOrWhiteSpace(configuredPathBase)
                            ? "/"
                            : configuredPathBase;
                    }

                    document.Servers =
                    [
                        new OpenApiServer { Url = ResolveSwaggerServerUrl(serverUrl, configuredPathBase) }
                    ];
                });
            });

            app.UseSwaggerUI(options =>
            {
                options.DocumentTitle = app.Configuration["Swagger:DocumentTitle"] ?? "MT.Saga Order Service Swagger";
                options.RoutePrefix = app.Configuration["Swagger:RoutePrefix"] ?? "swagger";
                options.DisplayRequestDuration();
                options.EnableDeepLinking();

                var swaggerJsonPath = string.IsNullOrWhiteSpace(configuredPathBase)
                    ? "/swagger/v1/swagger.json"
                    : $"{configuredPathBase}/swagger/v1/swagger.json";

                options.SwaggerEndpoint(swaggerJsonPath, "MT.Saga Order Service API v1");
            });
        }

        app.UseExceptionHandler();
        app.UseHttpsRedirection();

        CreateOrderEndpoint.Map(app);
        GetOrdersEndpoint.Map(app);
        GetOrderByIdEndpoint.Map(app);

        return app;
    }

    private static IApplicationBuilder UseIngressPathBase(
        this IApplicationBuilder app,
        ForwardedHeadersOptions? forwardedHeadersOptions)
    {
        var configuredBasePath = ResolveConfiguredPathBase(app.ApplicationServices.GetRequiredService<IConfiguration>());
        if (!string.IsNullOrWhiteSpace(configuredBasePath))
        {
            app.UsePathBase(configuredBasePath);
        }

        app.Use(async (context, next) =>
        {
            if (IsTrustedForwarder(context.Connection.RemoteIpAddress, forwardedHeadersOptions))
            {
                if (context.Request.Headers.TryGetValue("X-Forwarded-Prefix", out var forwardedPrefixValues))
                {
                    var forwardedPrefix = forwardedPrefixValues.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(forwardedPrefix))
                    {
                        context.Request.PathBase = new PathString(NormalizePathBase(forwardedPrefix));
                    }
                }
                else if (context.Request.Headers.TryGetValue("X-Original-URI", out var originalUriValues))
                {
                    var originalUri = originalUriValues.FirstOrDefault() ?? string.Empty;
                    var firstSegment = originalUri.TrimStart('/').Split('/').FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(firstSegment)
                        && !string.Equals(firstSegment, "swagger", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(firstSegment, "orders", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Request.PathBase = new PathString(NormalizePathBase(firstSegment));
                    }
                }
            }

            await next().ConfigureAwait(false);
        });

        return app;
    }

    private static string ResolveConfiguredPathBase(IConfiguration configuration)
    {
        var rawPathBase = Environment.GetEnvironmentVariable("ASPNETCORE_PATHBASE")
            ?? configuration["ASPNETCORE_PATHBASE"]
            ?? Environment.GetEnvironmentVariable("PATH_BASE")
            ?? configuration["PATH_BASE"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_APPL_PATH")
            ?? configuration["ASPNETCORE_APPL_PATH"]
            ?? configuration["PathBase"]
            ?? string.Empty;

        return NormalizePathBase(rawPathBase);
    }

    private static ForwardedHeadersOptions? BuildForwardedHeadersOptions(IConfiguration configuration)
    {
        var forwardedHeadersEnabled = configuration.GetValue<bool?>("Networking:ForwardedHeaders:Enabled")
            ?? configuration.GetValue<bool?>("ASPNETCORE_FORWARDEDHEADERS_ENABLED")
            ?? false;

        if (!forwardedHeadersEnabled)
        {
            return null;
        }

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto
        };

        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        options.KnownProxies.Add(IPAddress.Loopback);
        options.KnownProxies.Add(IPAddress.IPv6Loopback);

        foreach (var knownProxy in configuration.GetSection("Networking:ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        {
            if (IPAddress.TryParse(knownProxy, out var proxyAddress))
            {
                options.KnownProxies.Add(proxyAddress);
            }
        }

        foreach (var knownNetwork in configuration.GetSection("Networking:ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
        {
            if (TryParseKnownNetwork(knownNetwork, out var network))
            {
                options.KnownIPNetworks.Add(network);
            }
        }

        return options;
    }

    private static bool IsTrustedForwarder(IPAddress? remoteIpAddress, ForwardedHeadersOptions? forwardedHeadersOptions)
    {
        if (remoteIpAddress is null || forwardedHeadersOptions is null)
        {
            return false;
        }

        if (forwardedHeadersOptions.KnownProxies.Contains(remoteIpAddress))
        {
            return true;
        }

        return forwardedHeadersOptions.KnownIPNetworks.Any(network => network.Contains(remoteIpAddress));
    }

    private static bool TryParseKnownNetwork(string value, out System.Net.IPNetwork network)
    {
        network = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var prefixAddress) || !int.TryParse(parts[1], out var prefixLength))
        {
            return false;
        }

        network = new System.Net.IPNetwork(prefixAddress, prefixLength);
        return true;
    }

    private static string ResolveSwaggerServerUrl(string? requestPathBase, string? configuredPathBase)
    {
        var normalizedRequestPathBase = NormalizePathBase(requestPathBase);
        if (!string.IsNullOrWhiteSpace(normalizedRequestPathBase))
        {
            return normalizedRequestPathBase;
        }

        var normalizedConfiguredPathBase = NormalizePathBase(configuredPathBase);
        return string.IsNullOrWhiteSpace(normalizedConfiguredPathBase)
            ? "/"
            : normalizedConfiguredPathBase;
    }

    private static string NormalizePathBase(string? pathBase)
    {
        if (string.IsNullOrWhiteSpace(pathBase))
        {
            return string.Empty;
        }

        var trimmed = pathBase.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        return $"/{trimmed}";
    }
}
