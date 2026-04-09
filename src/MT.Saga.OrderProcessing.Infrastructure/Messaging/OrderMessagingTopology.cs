using MT.Saga.OrderProcessing.Contracts.Messaging;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging;

public static class OrderMessagingTopology
{
    public const string DefaultEventsExchangeName = "orders.events-exchange";
    public const string DefaultEventsExchangeType = "topic";

    public const string ExchangeName = DefaultEventsExchangeName;

    public const string SourceService = "orders";
    public const string EntityName = "order";

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

    public static class Actions
    {
        public const string Created = "created";
        public const string PaymentProcessed = "payment-processed";
        public const string PaymentFailed = "payment-failed";
        public const string InventoryReserved = "inventory-reserved";
        public const string InventoryFailed = "inventory-failed";
        public const string Confirmed = "confirmed";
        public const string Cancelled = "cancelled";
    }
}
