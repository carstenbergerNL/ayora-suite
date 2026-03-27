using System.ComponentModel.DataAnnotations;

namespace Ayora.Application.DTOs.Auth;

public sealed record RefreshRequest(
    [property: Required, MinLength(32), MaxLength(512)] string RefreshToken
);

