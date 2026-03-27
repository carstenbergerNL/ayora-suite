using System.ComponentModel.DataAnnotations;

namespace Ayora.Application.DTOs.Auth;

public sealed record ResetPasswordRequest(
    [Required, MinLength(32), MaxLength(2048)] string ResetToken,
    [Required, MinLength(8), MaxLength(128)] string NewPassword
);

