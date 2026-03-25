using MassTransit;
using MassTransit.RabbitMqTransport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MT.Saga.OrderProcessing.Saga;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Configuration;

/// <summary>
/// Explicit Saga configuration for order orchestration.
/// Defines: State machine, persistence (PostgreSQL), event topology.
/// </summary>
public static class OrderSagaConfiguration
{
    /// <summary>
    /// Registers the order saga state machine with durable persistence.
    ///
    /// Configuration details:
    /// - Storage: PostgreSQL via Entity Framework Core
    /// - Concurrency: Optimistic (uses xmin column for row versioning)
    /// - State: Persisted as string (CurrentState property)
    /// - Receives: order-saga endpoint listening for saga events
    /// </summary>
    public static IRegistrationConfigurator AddOrderSagaStateMachine(
        this IRegistrationConfigurator cfg,
        IConfiguration configuration)
    {
        cfg.AddSagaStateMachine<OrderStateMachine, OrderState>()
            .EntityFrameworkRepository(r =>
            {
                // PostgreSQL optimistic concurrency using xmin system column
                r.ConcurrencyMode = ConcurrencyMode.Optimistic;

                // Use existing DbContext (already registered separately)
                r.ExistingDbContext<OrderSagaDbContext>();

                // Enable PostgreSQL-specific xmin row versioning
                r.UsePostgres();
            });

        return cfg;
    }

    /// <summary>
    /// Configures the saga receive endpoint with:
    /// - Common resilience policies (retry, outbox, kill switch)
    /// - Event binding (OrderCreated, PaymentProcessed, etc.)
    /// - Saga behavior configuration
    /// </summary>
    public static void ConfigureOrderSagaReceiveEndpoint(
        this IRabbitMqBusFactoryConfigurator busConfigurator,
        IRegistrationContext context,
        CommonMassTransitPoliciesConfiguration.MessagingPoliciesOptions policyOptions)
    {
        busConfigurator.ReceiveEndpoint("order-saga", endpoint =>
        {
            // Apply common resilience policies
            endpoint.ConfigureCommonReceiveEndpointPolicies(context, policyOptions);

            // Configure saga event consumption from events exchange
            endpoint.ConfigureOrderEventsConsumption(OrderMessagingTopology.ExchangeName);

            // Register saga with endpoint
            endpoint.ConfigureSaga<OrderState>(context);
        });
    }
}
