using DynamicQ.Tests.Domain.Entities;
using DynamicQuery.DataStructures;
using Microsoft.Extensions.Options;

namespace DynamicQuery.Tests.Services;

public class DataTableBuilderServiceTests
{
    [Fact]
    public void FlattenToDataTable_ShouldCreateOneRowPerNestedPost()
    {
        // Arrange
        var tableTree = new DynamicQTableTree
        {
            StartingTable = "Blog",
            JoinNodes =
            [
                new DynamicQNode
                {
                    TableType = typeof(Blog),
                    SelectedTableColumns = ["BlogId", "Title"],
                    MinimalIncludePath = []
                },
                new DynamicQNode
                {
                    TableType = typeof(Post),
                    SelectedTableColumns = ["PostId", "Title"],
                    MinimalIncludePath = ["Posts"]
                }
            ]
        };
        var blog = new Blog
        {
            BlogId = 1,
            Title = "DynamicQ",
            Posts =
            [
                new Post { PostId = 10, Title = "First post" },
                new Post { PostId = 11, Title = "Second post" }
            ]
        };
        var service = new DataTableBuilderService(Options.Create(new DynamicQOptions()));

        // Act
        var result = service.FlattenToDataTable([blog], tableTree);

        // Assert
        Assert.Equal(["1.Blog_BlogId", "2.Blog_Title", "3.Posts_PostId", "4.Posts_Title"],
            result.Columns.Cast<System.Data.DataColumn>().Select(column => column.ColumnName));
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal([1, "DynamicQ", 10, "First post"], result.Rows[0].ItemArray);
        Assert.Equal([1, "DynamicQ", 11, "Second post"], result.Rows[1].ItemArray);
    }
}
