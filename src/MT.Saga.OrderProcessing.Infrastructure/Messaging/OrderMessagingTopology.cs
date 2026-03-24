namespace MT.Saga.OrderProcessing.Infrastructure.Messaging;

public static class OrderMessagingTopology
{
    public const string ExchangeName = "orders.events-exchange";

    public const string SourceService = "orders";
    public const string EntityName = "order";

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
