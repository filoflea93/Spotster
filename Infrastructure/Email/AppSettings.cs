namespace Spotster.Infrastructure.Email;

public class AppSettings
{
    public const string SectionName = "App";

    public string PublicBaseUrl { get; set; } = "http://localhost:5124";
}
