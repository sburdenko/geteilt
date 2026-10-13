using System.Security.Cryptography;
using Geteilt.Infrastructure.Encryption;
using Microsoft.Extensions.Options;

namespace Geteilt.Infrastructure.Tests;

/// <summary>An IBAN is encrypted before it reaches the database.</summary>
public sealed class IbanCipherTests
{
    private const string Iban = "DE89370400440532013000";

    private static readonly string Key = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
    private static readonly string OtherKey = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    private static IbanCipher Cipher(string key) => new(Options.Create(new IbanProtectionOptions { Key = key }));

    [Fact]
    public void AProtectedIban_ReadsBackAsItWas()
    {
        IbanCipher cipher = Cipher(Key);

        cipher.Unprotect(cipher.Protect(Iban)).Should().Be(Iban);
    }

    [Fact]
    public void TheStoredForm_DoesNotContainTheIban()
    {
        string stored = Cipher(Key).Protect(Iban);

        stored.Should().StartWith(IbanCipher.Prefix);
        stored.Should().NotContain(Iban).And.NotContain(Iban[^4..] + "\0");
    }

    [Fact]
    public void TheSameIbanTwice_IsStoredDifferently()
    {
        // A fresh nonce every time: equal ciphertexts would tell a reader of a dump who shares an
        // account with whom.
        IbanCipher cipher = Cipher(Key);

        cipher.Protect(Iban).Should().NotBe(cipher.Protect(Iban));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    [InlineData(28)]
    [InlineData(-1)]
    public void AChangedByte_IsNoticed_NotDecryptedIntoSomethingElse(int position)
    {
        // GCM authenticates: a tampered value fails loudly instead of yielding another account.
        // The byte is flipped after decoding, not as a base64 character: some characters carry only
        // padding bits that the decoder ignores, and the test would pass a value through unchanged.
        IbanCipher cipher = Cipher(Key);
        byte[] sealedValue = Convert.FromBase64String(cipher.Protect(Iban)[IbanCipher.Prefix.Length..]);
        int index = position < 0 ? sealedValue.Length + position : position;
        sealedValue[index] ^= 0x01;

        Action read = () => cipher.Unprotect(IbanCipher.Prefix + Convert.ToBase64String(sealedValue));

        read.Should().Throw<CryptographicException>();
    }

    [Theory]
    [InlineData("v1:not base64!")]
    [InlineData("v1:AAAA")]
    public void AGarbledProtectedValue_IsACryptographicFailure(string stored)
    {
        Action read = () => Cipher(Key).Unprotect(stored);

        read.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void AnIbanLongerThanAnyIbanCanBe_IsRefusedOnTheWayIn()
    {
        // Protect and Unprotect agree on the limit: nothing is written that could not be read back.
        Action write = () => Cipher(Key).Protect(new string('A', 35));

        write.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AnotherKey_CannotReadIt()
    {
        string stored = Cipher(Key).Protect(Iban);

        Action read = () => Cipher(OtherKey).Unprotect(stored);

        read.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void AnUnprotectedValue_IsRefused_RatherThanPassedThrough()
    {
        // A plaintext IBAN in the column means the backfill has not run. Reading it as if it were
        // fine would hide exactly the state this whole feature exists to prevent.
        Action read = () => Cipher(Key).Unprotect(Iban);

        read.Should().Throw<InvalidOperationException>().WithMessage("*not protected*");
    }

    [Theory]
    [InlineData(Iban, false)]
    [InlineData("v1:abc", true)]
    public void IsProtected_TellsTheTwoFormsApart(string stored, bool isProtected)
    {
        IbanCipher.IsProtected(stored).Should().Be(isProtected);
    }

    [Fact]
    public void TheLongestIban_FitsTheColumn()
    {
        string longest = new('A', 34);

        Cipher(Key).Protect(longest).Length.Should().BeLessThanOrEqualTo(IbanCipher.MaxStoredLength);
    }

    [Fact]
    public void EnsureKey_WithoutAKey_Fails_EvenWithNothingToEncrypt()
    {
        // protect-ibans on a database with nothing left to encrypt would otherwise report success
        // on a server that cannot encrypt the next IBAN.
        Action check = () => Cipher(string.Empty).EnsureKey();

        check.Should().Throw<InvalidOperationException>().WithMessage("*IbanProtection:Key*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("dG9vIHNob3J0")]
    [InlineData("not base64 at all")]
    public void AKeyThatIsNot32Bytes_IsRefused(string key)
    {
        new IbanProtectionOptions { Key = key }.HasValidKey().Should().BeFalse();
    }
}
