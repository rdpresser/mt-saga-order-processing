using Microsoft.Extensions.Options;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Provider;

public sealed class MessagingResilienceOptionsProvider(IOptions<MessagingResilienceOptions> options)
    : IMessagingResilienceOptionsProvider
{
    private readonly MessagingResilienceOptions _options = options.Value;

    public MessagingResilienceOptions Current => _options;
}
