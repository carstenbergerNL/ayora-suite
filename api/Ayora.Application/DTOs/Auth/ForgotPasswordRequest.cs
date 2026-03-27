using System.ComponentModel.DataAnnotations;

namespace Ayora.Application.DTOs.Auth;

public sealed record ForgotPasswordRequest(
    [Required, EmailAddress, MaxLength(320)] string Email
);

