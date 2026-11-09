using System.Security.Cryptography;

namespace Estacionamento.Application.Operations;

public sealed class PublicIdGenerator : IPublicIdGenerator
{
    public string NewPublicId()
    {
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
        return $"AT-{DateTime.UtcNow:yyyyMMdd}-{suffix}";
    }
}
