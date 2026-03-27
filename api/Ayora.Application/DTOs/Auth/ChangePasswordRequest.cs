using System.ComponentModel.DataAnnotations;

namespace Ayora.Application.DTOs.Auth;

public sealed record ChangePasswordRequest(
    [Required, MinLength(8), MaxLength(128)] string CurrentPassword,
    [Required, MinLength(8), MaxLength(128)] string NewPassword
);

