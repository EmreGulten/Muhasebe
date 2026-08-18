namespace Accounting.Application.Common;

/// <summary>Uygulama katmanı taban exception'u; API katmanı ProblemDetails'e çevirir.</summary>
public class AppException : Exception
{
    public int StatusCode { get; }

    public string Title { get; }

    public AppException(string message, int statusCode = 400, string title = "İstek gerçekleştirilemedi")
        : base(message)
    {
        StatusCode = statusCode;
        Title = title;
    }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message = "Kayıt bulunamadı.")
        : base(message, 404, "Bulunamadı")
    {
    }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string message)
        : base(message, 409, "Çakışma")
    {
    }
}

public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Kimlik doğrulama başarısız.")
        : base(message, 401, "Yetkisiz")
    {
    }
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Bu işlem için yetkiniz yok.")
        : base(message, 403, "Erişim engellendi")
    {
    }
}
