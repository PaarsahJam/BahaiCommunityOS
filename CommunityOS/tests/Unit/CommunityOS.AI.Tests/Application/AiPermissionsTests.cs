using CommunityOS.AI.Application.Permissions;
using CommunityOS.Authorization.Application.Permissions;
using FluentAssertions;
using Xunit;

namespace CommunityOS.AI.Tests.Application;

public class AiPermissionsTests
{
    [Fact]
    public void AiPermissions_ShouldContainExactlyTwoPermissions()
    {
        AiPermissions.All.Should().HaveCount(2);
    }

    [Fact]
    public void AiPermissions_ShouldContainAssistInvoke()
    {
        AiPermissions.All.Should().Contain(AiPermissions.AssistInvoke);
    }

    [Fact]
    public void AiPermissions_ShouldContainPlatformManage()
    {
        AiPermissions.All.Should().Contain(AiPermissions.PlatformManage);
    }

    [Fact]
    public void AiPermissions_AssistInvoke_ShouldMatchPermissionCatalog()
    {
        AiPermissions.AssistInvoke.Should().Be(PermissionCatalog.AiAssistInvoke);
    }

    [Fact]
    public void AiPermissions_PlatformManage_ShouldMatchPermissionCatalog()
    {
        AiPermissions.PlatformManage.Should().Be(PermissionCatalog.AiPlatformManage);
    }

    [Fact]
    public void PermissionCatalog_ShouldContainAiPermissions()
    {
        PermissionCatalog.AiAssistInvoke.Should().Be("ai.assist.invoke");
        PermissionCatalog.AiPlatformManage.Should().Be("ai.platform.manage");
    }
}
