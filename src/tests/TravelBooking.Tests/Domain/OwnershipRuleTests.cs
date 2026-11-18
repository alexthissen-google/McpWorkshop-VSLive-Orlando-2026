using TravelBooking.Domain;
using TravelBooking.Domain.Rules;

namespace TravelBooking.Tests.Domain;

public class OwnershipRuleTests
{
    [Fact]
    public void Owner_may_act_on_their_own_record() => OwnershipRule.Permits("alice", "alice").Should().BeTrue();

    [Fact]
    public void A_different_caller_may_not() => OwnershipRule.Permits("alice", "bob").Should().BeFalse();

    [Fact]
    public void An_anonymous_caller_may_not_act_on_an_owned_record() => OwnershipRule.Permits("alice", null).Should().BeFalse();

    [Fact]
    public void Two_anonymous_parties_are_treated_as_a_match() => OwnershipRule.Permits(null, null).Should().BeTrue();

    [Fact]
    public void EnsurePermits_names_the_action()
    {
        var act = () => OwnershipRule.EnsurePermits("alice", "bob", "confirm this hold");

        act.Should().Throw<BookingDomainException>().WithMessage("Only the owner may confirm this hold.");
    }
}
