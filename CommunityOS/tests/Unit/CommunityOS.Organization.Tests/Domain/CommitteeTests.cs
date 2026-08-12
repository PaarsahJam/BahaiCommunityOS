using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.ValueObjects;

namespace CommunityOS.Organization.Tests.Domain;

public class CommitteeTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static Jurisdiction LocalJurisdiction() =>
        Jurisdiction.Create(JurisdictionType.Local, Guid.NewGuid());

    private static Committee CreateCommittee() =>
        Committee.Create("Bahji Committee", "Bahji", Guid.NewGuid(), null, LocalJurisdiction());

    [Fact]
    public void Create_initializes_active_committee()
    {
        var committee = CreateCommittee();

        committee.Id.Should().NotBeEmpty();
        committee.Name.Should().Be("Bahji Committee");
        committee.IsActive.Should().BeTrue();
        committee.Members.Should().BeEmpty();
    }

    [Fact]
    public void AddMember_appends_effective_dated_member()
    {
        var committee = CreateCommittee();
        var personId = Guid.NewGuid();
        var period = EffectivePeriod.Create(Now.AddDays(-10));

        committee.AddMember(personId, "convener", period);

        committee.Members.Should().ContainSingle();
        var member = committee.Members.Single();
        member.PersonId.Should().Be(personId);
        member.RoleCode.Should().Be("convener");
        member.IsEffectiveAt(Now).Should().BeTrue();
    }

    [Fact]
    public void MemberAt_returns_member_effective_at_moment()
    {
        var committee = CreateCommittee();
        var personId = Guid.NewGuid();
        committee.AddMember(personId, "convener", EffectivePeriod.Create(Now.AddDays(-10)));

        committee.MemberAt(personId, Now).Should().NotBeNull();
        committee.MemberAt(personId, Now.AddDays(-11)).Should().BeNull();
    }

    [Fact]
    public void RemoveMember_removes_by_person_and_role()
    {
        var committee = CreateCommittee();
        var personId = Guid.NewGuid();
        committee.AddMember(personId, "convener", EffectivePeriod.Create(Now.AddDays(-10)));

        committee.RemoveMember(personId, "convener");

        committee.Members.Should().BeEmpty();
    }

    [Fact]
    public void RemoveMember_throws_when_not_a_member()
    {
        var committee = CreateCommittee();

        var act = () => committee.RemoveMember(Guid.NewGuid(), "convener");

        act.Should().Throw<CommitteeMemberNotFoundException>();
    }

    [Fact]
    public void Deactivate_marks_inactive()
    {
        var committee = CreateCommittee();

        committee.Deactivate();

        committee.IsActive.Should().BeFalse();
    }

    [Fact]
    public void UpdateDetails_changes_name_and_jurisdiction()
    {
        var committee = CreateCommittee();
        var newJurisdiction = Jurisdiction.Create(JurisdictionType.National, Guid.NewGuid());

        committee.UpdateDetails("National Committee", newJurisdiction);

        committee.Name.Should().Be("National Committee");
        committee.Jurisdiction.Should().Be(newJurisdiction);
    }
}
