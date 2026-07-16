namespace InvoiceBuilder.Services;

/// <summary>
/// Gatekeeper for external sign-in: only emails on the configured allow-list
/// (exact address or domain) may provision a local account.
/// </summary>
public class AllowedUserPolicy
{
    private readonly HashSet<string> _allowedEmails;
    private readonly HashSet<string> _allowedDomains;

    public AllowedUserPolicy(IConfiguration configuration)
    {
        _allowedEmails = configuration.GetSection("Authorization:AllowedEmails").Get<string[]>()
            ?.Select(e => e.Trim().ToLowerInvariant())
            .Where(e => e.Length > 0)
            .ToHashSet() ?? [];

        _allowedDomains = configuration.GetSection("Authorization:AllowedEmailDomains").Get<string[]>()
            ?.Select(d => d.Trim().TrimStart('@').ToLowerInvariant())
            .Where(d => d.Length > 0)
            .ToHashSet() ?? [];
    }

    public bool IsAllowed(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        email = email.Trim().ToLowerInvariant();

        if (_allowedEmails.Contains(email))
        {
            return true;
        }

        var atIndex = email.LastIndexOf('@');
        if (atIndex < 0 || atIndex == email.Length - 1)
        {
            return false;
        }

        var domain = email[(atIndex + 1)..];
        return _allowedDomains.Contains(domain);
    }
}
