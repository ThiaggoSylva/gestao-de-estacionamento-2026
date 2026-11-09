namespace Estacionamento.Application.Common;

public interface IDocumentProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
    string Mask(string plaintext);
}
