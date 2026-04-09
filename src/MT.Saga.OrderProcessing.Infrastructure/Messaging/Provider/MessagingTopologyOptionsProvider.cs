using Microsoft.Extensions.Options;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Provider;

public sealed class MessagingTopologyOptionsProvider(IOptions<MessagingTopologyOptions> options)
    : IMessagingTopologyOptionsProvider
{
    private readonly MessagingTopologyOptions _options = options.Value;

    public MessagingTopologyOptions Current => _options;
}
