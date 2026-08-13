using DynamicQ.Tests.Domain.Entities;
using DynamicQuery.Models;
using DynamicQuery.Tests.Substitutes;

namespace DynamicQuery.Tests.Services;

public class DynamicQTests
{
    private readonly DynamicQ _dynamicQ;

    public DynamicQTests() => _dynamicQ = DynamicQSubstitute.GetDynamicQSubstitute();

    [Fact]
    public void DynamicQTests_CreateTableTree_Should_Create_Tree()
    {
        // Arrange
        var request = MockRequest();

        // Act
        var tableTree = _dynamicQ.CreateTableTree(request);

        // Assert
        Assert.NotNull(tableTree);
    }

    [Fact]
    public void DynamicQTests_CreateSelector_Should_Create_Selector()
    {
        // Arrange
        var tableTree = _dynamicQ.CreateTableTree(MockRequest());

        Assert.NotNull(tableTree);

        // Act
        var selector = _dynamicQ.CreateSelector<Blog>(tableTree);

        // Assert
        Assert.NotNull(selector);
    }

    private static DynamicQTableRequest MockRequest() => new()
    {
        TableSelection = [
            new DynamicQSelectedFields
                {
                    PathToTable = "Blog",
                    SelectedTableColumns = ["BlogId", "Title"]
                },
                new DynamicQSelectedFields
                {
                    PathToTable = "Blog.Owner",
                    SelectedTableColumns = ["UserName", "Email", "DisplayName"]
                },
                new DynamicQSelectedFields
                {
                    PathToTable = "Blog.Owner.Comments",
                    SelectedTableColumns = ["CommentId", "Body"]
                },
                new DynamicQSelectedFields
                {
                    PathToTable = "Blog.Owner.SubmittedReports",
                    SelectedTableColumns = ["ReportId", "Reason", "Description"]
                },
                new DynamicQSelectedFields
                {
                    PathToTable = "Blog.Owner.Comments.Post",
                    SelectedTableColumns = ["PostId", "Title", "Content"]
                },
                new DynamicQSelectedFields
                {
                    PathToTable = "Blog.Owner.Comments.Reactions",
                    SelectedTableColumns = ["ReactionId", "Kind"]
                }
            ]
    };
}