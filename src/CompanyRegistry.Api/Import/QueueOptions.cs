namespace CompanyRegistry.Api.Import;

public sealed class QueueOptions
{
    public const string Section = "Queue";

    /// <summary>"InMemory" (default: Render, tests, CI) or "RabbitMq".</summary>
    public string Provider { get; set; } = "InMemory";
    public string HostName { get; set; } = "localhost";
    public string QueueName { get; set; } = "company-imports";
}