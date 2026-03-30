namespace MT.Saga.OrderProcessing.Contracts.Messaging;

public static class OrderQueueNames
{
    public const string Saga = "orders.saga-queue";
    public const string ReadModel = "orders.read-model-queue";
    public const string ProcessPayment = "orders.process-payment-queue";
    public const string RefundPayment = "orders.refund-payment-queue";
    public const string ReserveInventory = "orders.reserve-inventory-queue";
}
