using System.Security.Cryptography;
using Estacionamento.Application.Common;
using Estacionamento.Domain.Common;
using Microsoft.AspNetCore.DataProtection;

namespace Estacionamento.Infrastructure.Security;

public sealed class DataProtectionDocumentProtector : IDocumentProtector
{
    private readonly IDataProtector protector;

    public DataProtectionDocumentProtector(IDataProtectionProvider dataProtectionProvider)
    {
        protector = dataProtectionProvider.CreateProtector("Estacionamento.Document.v1");
    }

    public string Protect(string plaintext)
    {
        var normalized = (plaintext ?? string.Empty).Trim();
        if (normalized.Length is < 3 or > 40)
        {
            throw DomainException.Validation("O documento do titular deve conter entre 3 e 40 caracteres.");
        }

        return protector.Protect(normalized);
    }

    public string Unprotect(string protectedValue)
    {
        try
        {
            return protector.Unprotect(protectedValue);
        }
        catch (CryptographicException)
        {
            throw DomainException.RuleViolation(
                "O documento protegido não pôde ser lido com o key ring atual.");
        }
    }

    public string Mask(string plaintext)
    {
        var normalized = (plaintext ?? string.Empty).Trim();
        if (normalized.Length <= 2)
        {
            return "***";
        }

        return $"***{normalized[^2..]}";
    }
}
