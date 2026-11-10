using Pakar.Domain.Entities;
using Pakar.Domain.Services;

namespace Pakar.Api.Tests;

public class AdminSafeguardTests
{
    [Fact]
    public void Admin_cannot_remove_own_access()
    {
        Assert.Equal(AdminSafeguard.SelfChangeMessage,
            AdminSafeguard.CheckRemoval(isSelf: true, targetIsActiveAdmin: true, activeAdminCount: 5));
    }

    [Fact]
    public void Last_active_admin_cannot_be_removed()
    {
        Assert.Equal(AdminSafeguard.LastAdminMessage,
            AdminSafeguard.CheckRemoval(isSelf: false, targetIsActiveAdmin: true, activeAdminCount: 1));
    }

    [Fact]
    public void Another_admin_can_be_removed_when_one_remains()
    {
        Assert.Null(AdminSafeguard.CheckRemoval(isSelf: false, targetIsActiveAdmin: true, activeAdminCount: 2));
    }

    [Fact]
    public void Regular_user_can_always_be_deactivated_by_an_admin()
    {
        Assert.Null(AdminSafeguard.CheckRemoval(isSelf: false, targetIsActiveAdmin: false, activeAdminCount: 1));
    }

    [Theory]
    [InlineData("admin", UserRoles.Admin)]
    [InlineData(" USER ", UserRoles.User)]
    [InlineData("Admin", UserRoles.Admin)]
    public void Roles_are_normalized(string input, string expected)
    {
        Assert.Equal(expected, UserRoles.Normalize(input));
    }

    [Theory]
    [InlineData("superuser")]
    [InlineData("")]
    [InlineData(null)]
    public void Unknown_roles_are_rejected(string? input)
    {
        Assert.Null(UserRoles.Normalize(input));
    }

    [Fact]
    public void Only_active_admins_count_as_active_admin()
    {
        Assert.True(new AppUser { Role = UserRoles.Admin }.IsActiveAdmin);
        Assert.False(new AppUser { Role = UserRoles.Admin, IsActive = false }.IsActiveAdmin);
        Assert.False(new AppUser { Role = UserRoles.User }.IsActiveAdmin);
    }
}
