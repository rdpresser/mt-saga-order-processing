namespace MT.Saga.OrderProcessing.Infrastructure.Caching;

public sealed class RedisOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6379;
    public string Password { get; set; } = string.Empty;
    public bool Secure { get; set; }
    public string InstanceName { get; set; } = "mt-saga-order-processing";

    public string ConnectionString
    {
        get
        {
            var parts = new List<string> { $"{Host}:{Port}" };

            if (!string.IsNullOrWhiteSpace(Password))
                parts.Add($"password={Password}");

            parts.Add($"ssl={(Secure ? "true" : "false")}");
            parts.Add("abortConnect=False");

            return string.Join(',', parts);
        }
    }
}
