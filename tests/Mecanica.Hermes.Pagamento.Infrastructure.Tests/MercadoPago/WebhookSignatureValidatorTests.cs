using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Mecanica.Hermes.Pagamento.Infrastructure.MercadoPago;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Tests.MercadoPago;

public class WebhookSignatureValidatorTests
{
    private const string Secret = "dev-secret";
    private const string DataId = "123456789";
    private const string RequestId = "req-abc";
    private const string Ts = "1700000000";

    private static string ComputeSignature(string secret, string dataId, string requestId, string ts)
    {
        var manifest = $"id:{dataId};request-id:{requestId};ts:{ts};";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private static WebhookSignatureValidator CreateValidator(string? secret = Secret)
    {
        var options = Options.Create(new MercadoPagoOptions { WebhookSecret = secret ?? string.Empty });
        return new WebhookSignatureValidator(options);
    }

    [Fact]
    public void Validate_ValidSignature_ReturnsTrue()
    {
        var v1 = ComputeSignature(Secret, DataId, RequestId, Ts);
        var header = $"ts={Ts},v1={v1}";

        var validator = CreateValidator();

        validator.Validate(header, RequestId, DataId).Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidSignatureUppercase_ReturnsTrueDueToCaseInsensitiveCompare()
    {
        var v1 = ComputeSignature(Secret, DataId, RequestId, Ts).ToUpperInvariant();
        var header = $"ts={Ts},v1={v1}";

        var validator = CreateValidator();

        validator.Validate(header, RequestId, DataId).Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidSignature_ReturnsFalse()
    {
        var header = $"ts={Ts},v1=deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";

        var validator = CreateValidator();

        validator.Validate(header, RequestId, DataId).Should().BeFalse();
    }

    [Fact]
    public void Validate_MissingSignatureHeader_ReturnsFalse()
    {
        var validator = CreateValidator();

        validator.Validate(null, RequestId, DataId).Should().BeFalse();
        validator.Validate("", RequestId, DataId).Should().BeFalse();
        validator.Validate("   ", RequestId, DataId).Should().BeFalse();
    }

    [Fact]
    public void Validate_MissingSecret_ReturnsFalse()
    {
        var v1 = ComputeSignature("any", DataId, RequestId, Ts);
        var header = $"ts={Ts},v1={v1}";

        var validator = CreateValidator(secret: string.Empty);

        validator.Validate(header, RequestId, DataId).Should().BeFalse();
    }

    [Fact]
    public void Validate_MissingDataId_ReturnsFalse()
    {
        var v1 = ComputeSignature(Secret, DataId, RequestId, Ts);
        var header = $"ts={Ts},v1={v1}";

        var validator = CreateValidator();

        validator.Validate(header, RequestId, string.Empty).Should().BeFalse();
    }

    [Fact]
    public void Validate_HeaderMissingTs_ReturnsFalse()
    {
        var v1 = ComputeSignature(Secret, DataId, RequestId, Ts);
        var header = $"v1={v1}";

        var validator = CreateValidator();

        validator.Validate(header, RequestId, DataId).Should().BeFalse();
    }

    [Fact]
    public void Validate_HeaderMissingV1_ReturnsFalse()
    {
        var header = $"ts={Ts}";

        var validator = CreateValidator();

        validator.Validate(header, RequestId, DataId).Should().BeFalse();
    }

    [Fact]
    public void Validate_HeaderWithMalformedPart_ReturnsFalse()
    {
        // "=ts" tem '=' no índice 0, idx <= 0 → ignorado; e não há ts/v1, retorna false.
        var header = "=ts,nopairs";

        var validator = CreateValidator();

        validator.Validate(header, RequestId, DataId).Should().BeFalse();
    }

    [Fact]
    public void Validate_NullRequestId_TreatsRequestIdAsEmpty()
    {
        // Manifest building: id:DATA;request-id:;ts:TS;
        var v1 = ComputeSignature(Secret, DataId, string.Empty, Ts);
        var header = $"ts={Ts},v1={v1}";

        var validator = CreateValidator();

        validator.Validate(header, requestIdHeader: null, DataId).Should().BeTrue();
    }

    [Fact]
    public void Validate_DifferentRequestId_ReturnsFalse()
    {
        var v1 = ComputeSignature(Secret, DataId, RequestId, Ts);
        var header = $"ts={Ts},v1={v1}";

        var validator = CreateValidator();

        validator.Validate(header, "other-request-id", DataId).Should().BeFalse();
    }

    [Fact]
    public void Validate_HeaderWithExtraWhitespace_ReturnsTrue()
    {
        var v1 = ComputeSignature(Secret, DataId, RequestId, Ts);
        var header = $"  ts = {Ts} ,  v1 = {v1}  ";

        var validator = CreateValidator();

        validator.Validate(header, RequestId, DataId).Should().BeTrue();
    }
}
