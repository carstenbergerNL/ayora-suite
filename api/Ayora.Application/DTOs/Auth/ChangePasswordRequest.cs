using System.ComponentModel.DataAnnotations;

namespace Ayora.Application.DTOs.Auth;

public sealed record ChangePasswordRequest(
    [property: Required, MinLength(8), MaxLength(128)] string CurrentPassword,
    [property: Required, MinLength(8), MaxLength(128)] string NewPassword
);

