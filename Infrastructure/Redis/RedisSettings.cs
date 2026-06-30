namespace Spotster.Infrastructure.Redis;

public class RedisSettings
{
    public const string SectionName = "Redis";

    public string? ConnectionString { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}
