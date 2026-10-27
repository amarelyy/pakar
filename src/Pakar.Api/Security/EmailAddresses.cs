namespace Pakar.Api.Security;

/// <summary>
/// Emails are stored and compared in one canonical form so "A@x.com" and "a@x.com" are the same
/// account, independent of the database collation.
/// </summary>
public static class EmailAddresses
{
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
