namespace InvoiceBuilder.Modules.Users;

/// <summary>
/// Gatekeeper for external sign-in: only emails on the configured allow-list
/// (exact address or domain) may provision a local account. Set
/// "Authorization:AllowAnyUser" to true to let any authenticated Microsoft
/// account through (the account must still surface an email claim).
/// </summary>
public class AllowedUserPolicy
{
    private readonly bool _allowAnyUser;
    private readonly HashSet<string> _allowedEmails;
    private readonly HashSet<string> _allowedDomains;

    public AllowedUserPolicy(IConfiguration configuration)
    {
        _allowAnyUser = configuration.GetValue<bool>("Authorization:AllowAnyUser");

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

        if (_allowAnyUser)
        {
            return true;
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
