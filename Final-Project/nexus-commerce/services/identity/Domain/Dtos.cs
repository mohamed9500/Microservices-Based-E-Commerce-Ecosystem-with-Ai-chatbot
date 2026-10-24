using System.ComponentModel.DataAnnotations;

namespace Nexus.Identity.Domain;

public sealed record RegisterRequest
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; init; } = "";
    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; init; } = "";
    [Required, StringLength(150, MinimumLength = 2)] public string FullName { get; init; } = "";
}

public sealed record LoginRequest
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; init; } = "";
    [Required, StringLength(100)] public string Password { get; init; } = "";
}

public sealed record UpdateProfileRequest
{
    [Required, StringLength(150, MinimumLength = 2)] public string FullName { get; init; } = "";
    [StringLength(30)] public string? Phone { get; init; }
    [StringLength(200)] public string? AddressLine { get; init; }
    [StringLength(100)] public string? City { get; init; }
    [StringLength(20)] public string? PostalCode { get; init; }
    [StringLength(100)] public string? Country { get; init; }
}

public sealed record ChangePasswordRequest
{
    [Required, StringLength(100)] public string CurrentPassword { get; init; } = "";
    [Required, StringLength(100, MinimumLength = 8)] public string NewPassword { get; init; } = "";
}

public sealed record SetActiveRequest { public bool IsActive { get; init; } }

public sealed record UserDto(Guid Id, string Email, string FullName, string Role, string? Phone, string? AddressLine,
    string? City, string? PostalCode, string? Country, bool IsActive, DateTime CreatedAtUtc, DateTime? LastLoginAtUtc);

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserDto User);

public static class UserMapping
{
    public static UserDto ToDto(this User u) => new(u.Id, u.Email, u.FullName, u.Role, u.Phone, u.AddressLine,
        u.City, u.PostalCode, u.Country, u.IsActive, u.CreatedAtUtc, u.LastLoginAtUtc);
}
