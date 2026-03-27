using System.ComponentModel.DataAnnotations;

namespace Ayora.Application.DTOs.Auth;

public sealed record ForgotPasswordRequest(
    [property: Required, EmailAddress, MaxLength(320)] string Email
);

