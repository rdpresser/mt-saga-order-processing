using MT.Saga.OrderProcessing.Contracts.Messaging;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging;

public static class OrderMessagingTopology
{
    public const string DefaultEventsExchangeName = "orders.events-exchange";
    public const string DefaultEventsExchangeType = "topic";

    public const string ExchangeName = DefaultEventsExchangeName;

    // Delegated to Contracts so the Saga project (which cannot reference Infrastructure) shares the same values.
    public const string SourceService = OrderTopologyConstants.SourceService;
    public const string EntityName = OrderTopologyConstants.EntityName;

    /// <summary>
    /// Canonical queue names for all receive endpoints.
    /// Pattern: {domain}.{purpose}-queue
    /// Suffix "-queue" avoids name collisions with exchanges in the same broker.
    /// </summary>
    public static class Queues
    {
        public const string Saga = OrderQueueNames.Saga;
        public const string ReadModel = OrderQueueNames.ReadModel;
        public const string ProcessPayment = OrderQueueNames.ProcessPayment;
        public const string RefundPayment = OrderQueueNames.RefundPayment;
        public const string ReserveInventory = OrderQueueNames.ReserveInventory;
    }

    /// <summary>
    /// Action labels for domain events — delegated to Contracts so the Saga can also use them
    /// without referencing Infrastructure.
    /// </summary>
    public static class Actions
    {
        public const string Created = OrderTopologyConstants.EventActions.Created;
        public const string PaymentProcessed = OrderTopologyConstants.EventActions.PaymentProcessed;
        public const string PaymentFailed = OrderTopologyConstants.EventActions.PaymentFailed;
        public const string InventoryReserved = OrderTopologyConstants.EventActions.InventoryReserved;
        public const string InventoryFailed = OrderTopologyConstants.EventActions.InventoryFailed;
        public const string Confirmed = OrderTopologyConstants.EventActions.Confirmed;
        public const string Cancelled = OrderTopologyConstants.EventActions.Cancelled;
    }

    /// <summary>
    /// Action labels used when the Saga forwards commands to worker queues.
    /// These are metadata-only values in the EventContext envelope — they are NOT
    /// routing key segments, since commands are sent directly to queue URIs.
    /// </summary>
    public static class CommandActions
    {
        public const string ProcessPayment = OrderTopologyConstants.CommandActions.ProcessPayment;
        public const string ReserveInventory = OrderTopologyConstants.CommandActions.ReserveInventory;
        public const string RefundPayment = OrderTopologyConstants.CommandActions.RefundPayment;
    }
}
