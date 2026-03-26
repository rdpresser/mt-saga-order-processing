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
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.All
        });

        app.UseIngressPathBase();

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

    private static IApplicationBuilder UseIngressPathBase(this IApplicationBuilder app)
    {
        var configuredBasePath = ResolveConfiguredPathBase(app.ApplicationServices.GetRequiredService<IConfiguration>());
        if (!string.IsNullOrWhiteSpace(configuredBasePath))
        {
            app.UsePathBase(configuredBasePath);
        }

        app.Use(async (context, next) =>
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

            await next().ConfigureAwait(false);
        });

        return app;
    }

    private static string ResolveConfiguredPathBase(IConfiguration configuration)
    {
        var rawPathBase = Environment.GetEnvironmentVariable("ASPNETCORE_APPL_PATH")
            ?? configuration["ASPNETCORE_APPL_PATH"]
            ?? configuration["PathBase"]
            ?? string.Empty;

        return NormalizePathBase(rawPathBase);
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
