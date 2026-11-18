using System.Security.Claims;
using TravelBooking.API.Authorization;

namespace TravelBooking.Tests.Api;

public class ScopeClaimsTests
{
    [Fact]
    public void HasScope_looks_at_every_scope_claim_not_just_the_first()
    {
        var user = Principal(new Claim("scope", "mcp:tools"), new Claim("scope", "booking:write"));

        user.HasScope("booking:write").Should().BeTrue("a JSON array in the token becomes one claim per scope");
    }

    [Fact]
    public void HasScope_accepts_a_single_space_delimited_claim()
    {
        var user = Principal(new Claim("scope", "mcp:tools booking:write"));

        user.HasScope("booking:write").Should().BeTrue();
    }

    [Fact]
    public void HasScope_is_false_when_the_scope_was_not_granted()
    {
        var user = Principal(new Claim("scope", "mcp:tools"));

        user.HasScope("booking:write").Should().BeFalse();
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "test"));
}
