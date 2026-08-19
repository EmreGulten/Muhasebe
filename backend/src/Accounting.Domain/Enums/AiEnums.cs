namespace Accounting.Domain.Enums;

/// <summary>AI asistan sohbet mesajının tarafı (muhasebe.md bölüm 11).</summary>
public enum AiMessageRole
{
    /// <summary>Kullanıcının sorusu.</summary>
    User = 1,

    /// <summary>Asistanın yanıtı.</summary>
    Assistant = 2,
}
