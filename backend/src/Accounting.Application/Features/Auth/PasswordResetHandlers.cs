using Accounting.Application.Abstractions;
using Accounting.Application.Common;
using Accounting.Contracts.Auth;
using Accounting.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Accounting.Application.Features.Auth;

/// <summary>
/// Parola sıfırlama talebi. Kullanıcı var olmasa da aynı yanıtı döner;
/// token yalnızca e-posta sahibine gider (MVP'de dev e-posta göndericisi log'lar).
/// </summary>
public sealed class ForgotPasswordHandler(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    IOptions<AppOptions> appOptions)
{
    public async Task<MessageResponse> HandleAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());

        if (user is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var link = $"{appOptions.Value.FrontendUrl.TrimEnd('/')}/reset-password" +
                       $"?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";

            var body = $"""
                <p>Merhaba {user.FullName},</p>
                <p>Parolanızı sıfırlamak için aşağıdaki bağlantıyı kullanabilirsiniz:</p>
                <p><a href="{link}">Parolayı sıfırla</a></p>
                <p>Bu talebi siz yapmadıysanız bu e-postayı yok sayabilirsiniz.</p>
                """;

            await emailSender.SendAsync(user.Email!, "Parola sıfırlama", body, cancellationToken);
        }

        return new MessageResponse("Parola sıfırlama bağlantısı e-posta adresinize gönderildi (eğer böyle bir hesap varsa).");
    }
}

/// <summary>Parolayı token ile sıfırlar ve kullanıcıyı tüm oturumlardan çıkarır.</summary>
public sealed class ResetPasswordHandler(
    UserManager<ApplicationUser> userManager,
    IRefreshTokenService refreshTokenService)
{
    public async Task<MessageResponse> HandleAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            throw new AppException("Parola sıfırlama bağlantısı geçersiz veya süresi dolmuş.", 400, "İşlem başarısız");
        }

        var result = await userManager.ResetPasswordAsync(user, request.ResetToken, request.NewPassword);
        if (!result.Succeeded)
        {
            var details = string.Join(" ", result.Errors.Select(e => e.Description));
            throw new AppException(details, 400, "İşlem başarısız");
        }

        // Parola değişti: tüm refresh token'ları iptal et.
        await refreshTokenService.RevokeAllForUserAsync(user.Id, "Parola sıfırlandı", cancellationToken);

        return new MessageResponse("Parolanız güncellendi. Yeni parolanızla giriş yapabilirsiniz.");
    }
}
