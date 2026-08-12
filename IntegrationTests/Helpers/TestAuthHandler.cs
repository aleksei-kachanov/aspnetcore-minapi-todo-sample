using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationTests.Helpers;

public class TestAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public string IsAdmin { get; set; } = "false";
}

public class TestAuthHandler : AuthenticationHandler<TestAuthenticationSchemeOptions>
{
    public TestAuthHandler(IOptionsMonitor<TestAuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("admin", Options.IsAdmin)
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        var result = AuthenticateResult.Success(ticket);

        return Task.FromResult(result);
    }
}
