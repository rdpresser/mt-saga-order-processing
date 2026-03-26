using MassTransit;

namespace MT.Saga.OrderProcessing.Saga;

public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    // PostgreSQL optimistic concurrency token mapped to hidden xmin column.
    public uint RowVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
