namespace MT.Saga.OrderProcessing.Contracts;

public static class OrderStatuses
{
    public const string Created = "Created";
    public const string PaymentProcessing = "PaymentProcessing";
    public const string PaymentProcessed = "PaymentProcessed";
    public const string PaymentFailed = "PaymentFailed";
    public const string InventoryReserving = "InventoryReserving";
    public const string InventoryReserved = "InventoryReserved";
    public const string InventoryFailed = "InventoryFailed";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
}
