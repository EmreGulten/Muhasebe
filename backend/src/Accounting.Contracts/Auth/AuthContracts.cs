namespace Accounting.Contracts.Auth;

/// <summary>Kayıt isteği. BusinessName verilmezse varsayılan işletme adı oluşturulur.</summary>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string? BusinessName);

public sealed record LoginRequest(string Email, string Password);

/// <summary>Refresh isteği. Body boşsa httpOnly cookie'deki token kullanılır.</summary>
public sealed record RefreshRequest(string? RefreshToken);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(
    string Email,
    string ResetToken,
    string NewPassword);

public sealed record UserDto(Guid Id, string Email, string FullName);

public sealed record TenantMembershipDto(
    Guid TenantId,
    string Name,
    string Role,
    DateTime JoinedAtUtc);

/// <summary>Başarılı kimlik doğrulama yanıtı.</summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    UserDto User,
    IReadOnlyList<TenantMembershipDto> Tenants);

/// <summary>Giriş yapmış kullanıcının kendi bilgisi.</summary>
public sealed record MeResponse(UserDto User, IReadOnlyList<TenantMembershipDto> Tenants);

/// <summary>Kullanıcıya genel bilgi mesajı dönen endpoint'ler için.</summary>
public sealed record MessageResponse(string Message);
