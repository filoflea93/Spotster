namespace Spotster.Services;

public static class ChatPhotoMessage
{
    public const string Prefix = "__SPOTPHOTO__:";

    public static bool IsPhotoMessage(string? content) =>
        !string.IsNullOrWhiteSpace(content) && content.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Format(string photoUrl) => $"{Prefix}{photoUrl}";

    public static bool TryParse(string? content, out string photoUrl)
    {
        photoUrl = string.Empty;
        if (!IsPhotoMessage(content))
        {
            return false;
        }

        photoUrl = content![Prefix.Length..].Trim();
        return photoUrl.StartsWith("/uploads/chat/", StringComparison.Ordinal)
            && !photoUrl.Contains("..", StringComparison.Ordinal);
    }
}
