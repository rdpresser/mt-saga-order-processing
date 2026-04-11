namespace MT.Saga.OrderProcessing.Contracts.Messaging;

/// <summary>
/// Topology-level constants shared across the Contracts boundary.
/// Accessible by the Saga project (which only references Contracts, not Infrastructure)
/// and reused by Infrastructure.OrderMessagingTopology to avoid duplication.
/// </summary>
public static class OrderTopologyConstants
{
    public const string SourceService = "orders";
    public const string EntityName = "order";

    /// <summary>
    /// Action labels for domain events published to the event exchange.
    /// Used as routing key segments and as the <c>Action</c> field in EventContext.
    /// </summary>
    public static class EventActions
    {
        public const string Created = "created";
        public const string PaymentProcessed = "payment-processed";
        public const string PaymentFailed = "payment-failed";
        public const string InventoryReserved = "inventory-reserved";
        public const string InventoryFailed = "inventory-failed";
        public const string Confirmed = "confirmed";
        public const string Cancelled = "cancelled";
    }

    /// <summary>
    /// Action labels used when the Saga forwards commands to worker queues.
    /// These are metadata-only values in the EventContext envelope — they are NOT
    /// routing key segments, since commands are sent directly to queue URIs.
    /// </summary>
    public static class CommandActions
    {
        public const string ProcessPayment = "process-payment";
        public const string ReserveInventory = "reserve-inventory";
        public const string RefundPayment = "refund-payment";
    }
}
