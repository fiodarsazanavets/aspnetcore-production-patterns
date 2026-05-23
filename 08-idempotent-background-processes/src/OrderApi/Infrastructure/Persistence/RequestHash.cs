using OrderApi.Contracts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OrderApi.Infrastructure.Persistence;

public static class RequestHash
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static string Create(CreateOrderRequest request)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));

        return Convert.ToHexString(bytes);
    }
}
