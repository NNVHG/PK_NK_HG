using Dental.Infrastructure.Services;
using Xunit;

namespace Dental.Tests.Infrastructure;

public sealed class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_ShouldReturnNonEmptyString()
    {
        var hash = _hasher.Hash("password123");
        Assert.False(string.IsNullOrWhiteSpace(hash));
    }

    [Fact]
    public void Hash_ShouldNotContainOriginalPassword()
    {
        var hash = _hasher.Hash("password123");
        Assert.DoesNotContain("password123", hash);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash("mySecret@456");
        Assert.True(_hasher.Verify("mySecret@456", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("mySecret@456");
        Assert.False(_hasher.Verify("wrongPassword", hash));
    }

    [Fact]
    public void TwoHashes_SamePassword_ShouldBeDifferent()
    {
        // BCrypt dùng salt ngẫu nhiên — hai hash cùng password khác nhau
        var h1 = _hasher.Hash("samePassword");
        var h2 = _hasher.Hash("samePassword");
        Assert.NotEqual(h1, h2);
        Assert.True(_hasher.Verify("samePassword", h1));
        Assert.True(_hasher.Verify("samePassword", h2));
    }
}
