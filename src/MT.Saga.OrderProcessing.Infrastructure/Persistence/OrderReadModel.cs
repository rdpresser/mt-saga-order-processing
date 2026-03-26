namespace MT.Saga.OrderProcessing.Infrastructure.Persistence;

public sealed class OrderReadModel
{
    public Guid OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    // PostgreSQL optimistic concurrency token mapped to hidden xmin column.
    public uint RowVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
