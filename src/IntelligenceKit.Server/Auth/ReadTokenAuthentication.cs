using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace IntelligenceKit.Server.Auth;

public static class ReadTokenDefaults
{
    public const string Scheme = "ReadToken";

    /// <summary>Config key holding the shared admin token that gates the read API.</summary>
    public const string ConfigKey = "Auth:ReadToken";
}

/// <summary>
/// Authenticates read-side requests with a single shared admin token.
///
/// The token is read from config (<see cref="ReadTokenDefaults.ConfigKey"/>) and
/// presented by the caller either as <c>Authorization: Bearer &lt;token&gt;</c> or,
/// where a header can't be set (SignalR WebSockets, image tags), as an
/// <c>access_token</c> query parameter.
///
/// Fail-open in Development when no token is configured (zero-config local runs);
/// fail-closed everywhere else so a misconfigured production server never serves
/// data unprotected.
/// </summary>
public sealed class ReadTokenAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly string? _configuredToken;
    private readonly bool _isDevelopment;

    public ReadTokenAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration,
        IHostEnvironment environment)
        : base(options, logger, encoder)
    {
        _configuredToken = configuration[ReadTokenDefaults.ConfigKey];
        _isDevelopment = environment.IsDevelopment();
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // No token configured: open locally, locked down otherwise.
        if (string.IsNullOrWhiteSpace(_configuredToken))
        {
            return Task.FromResult(_isDevelopment
                ? Success("dev")
                : AuthenticateResult.Fail(
                    "Read API is locked: set 'Auth:ReadToken' to enable access."));
        }

        var presented = ExtractToken();
        if (presented is null)
            return Task.FromResult(AuthenticateResult.NoResult());

        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented),
            Encoding.UTF8.GetBytes(_configuredToken));

        return Task.FromResult(matches
            ? Success("admin")
            : AuthenticateResult.Fail("Invalid read token."));
    }

    private string? ExtractToken()
    {
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var value = header["Bearer ".Length..].Trim();
            if (value.Length > 0)
                return value;
        }

        // WebSockets and <img> requests can't carry an Authorization header.
        var fromQuery = Request.Query["access_token"].ToString();
        return string.IsNullOrWhiteSpace(fromQuery) ? null : fromQuery;
    }

    private AuthenticateResult Success(string name)
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, name) },
            ReadTokenDefaults.Scheme);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity), ReadTokenDefaults.Scheme);
        return AuthenticateResult.Success(ticket);
    }
}
