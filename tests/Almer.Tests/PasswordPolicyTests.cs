using Almer.Infrastructure;

namespace Almer.Tests;

public sealed class PasswordPolicyTests
{
    [Fact]
    public void Strong_password_is_accepted() => PasswordPolicy.Validate("Different!2026", "admin@almer.example");

    [Theory]
    [InlineData("Short!1")]
    [InlineData("alllowercase!2026")]
    [InlineData("ALLUPPERCASE!2026")]
    [InlineData("NoNumbersHere!")]
    [InlineData("NoSymbols2026")]
    [InlineData("admin-Secure!2026")]
    public void Weak_or_identity_based_password_is_rejected(string password) => Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate(password, "admin@almer.example"));
}
