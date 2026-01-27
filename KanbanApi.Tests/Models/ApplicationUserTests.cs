using KanbanApi.Models;
using FluentAssertions;
using Xunit;

namespace KanbanApi.Tests.Models;

public class ApplicationUserTests
{
    [Fact]
    public void ApplicationUser_ShouldBeCreated()
    {
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@test.com"
        };

        user.UserName.Should().Be("testuser");
        user.Email.Should().Be("test@test.com");
    }

    [Fact]
    public void ApplicationUser_ShouldHaveBoardMembershipsCollection()
    {
        var user = new ApplicationUser();

        user.BoardMemberships.Should().NotBeNull();
        user.BoardMemberships.Should().BeEmpty();
    }
}
