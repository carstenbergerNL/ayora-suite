namespace Ayora.Application.Options.Auth;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshTokens";

    public int DaysToLive { get; init; } = 30;
}

