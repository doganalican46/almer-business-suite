namespace Almer.Infrastructure;

public static class PasswordPolicy
{
    public static void Validate(string password, string email)
    {
        if (password.Length is < 12 or > 128 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || !password.Any(x => !char.IsLetterOrDigit(x))) throw new ArgumentException("Password must be 12-128 characters and include upper-case, lower-case, number and symbol.");
        var localPart = email.Split('@')[0];
        if (localPart.Length >= 4 && password.Contains(localPart, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Password must not contain the email name.");
    }
}
