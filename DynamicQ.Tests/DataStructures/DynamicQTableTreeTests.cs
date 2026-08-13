using DynamicQ.Tests.Domain.Entities;
using DynamicQuery.DataStructures;

namespace DynamicQuery.Tests.DataStructures;

public class DynamicQTableTreeTests
{
    [Fact]
    public void DynamicQTableTree_Build_ShouldReturn_DynamicQTableTree()
    {
        var tableTree = new DynamicQTableTree();
        tableTree.JoinNodes.Add(new DynamicQNode
        {
            TableType = typeof(Blog),
            SelectedTableColumns = ["BlogId", "Title", "Content"],
            MinimalIncludePath = [],
            OriginalIncludePath = null
        });
        tableTree.JoinNodes.Add(new DynamicQNode
        {
            TableType = typeof(User),
            SelectedTableColumns = ["UserName", "Email", "DisplayName"],
            MinimalIncludePath = ["Owner"],
            OriginalIncludePath = "Owner"
        });
        tableTree.JoinNodes.Add(new DynamicQNode
        {
            TableType = typeof(Comment),
            SelectedTableColumns = ["CommentId", "Body"],
            MinimalIncludePath = ["Owner", "Comments"],
            OriginalIncludePath = "Owner.Comments"
        });
        tableTree.JoinNodes.Add(new DynamicQNode
        {
            TableType = typeof(ContentReport),
            SelectedTableColumns = ["ReportId", "Reason", "Description"],
            MinimalIncludePath = ["Owner", "SubmittedReports"],
            OriginalIncludePath = "Owner.SubmittedReports"
        });
        tableTree.JoinNodes.Add(new DynamicQNode
        {
            TableType = typeof(Post),
            SelectedTableColumns = ["PostId", "Title", "Content"],
            MinimalIncludePath = ["Owner", "Comments", "Post"],
            OriginalIncludePath = "Owner.Comments.Post"
        });
        tableTree.JoinNodes.Add(new DynamicQNode
        {
            TableType = typeof(Reaction),
            SelectedTableColumns = ["ReactionId", "Kind"],
            MinimalIncludePath = ["Owner", "Comments", "Reactions"],
            OriginalIncludePath = "Owner.Comments.Reactions"
        });

        // The structure above should resemble a tree like this:
        // Blog
        //   - User
        //     - ContentReport
        //     - Comment
        //       - Post
        //       - Reaction
        var result = tableTree.Build();

        // First node
        Assert.Equal(typeof(Blog), result.TableType);
        Assert.Equal(["BlogId", "Title", "Content"], result.SelectedColumns);
        Assert.Null(result.OriginalIncludePath);
        Assert.Single(result.Children); // User is the only child table of Blog

        // Second node
        var userNode = result.Children[0];
        Assert.Equal(typeof(User), userNode.TableType);
        Assert.Equal(["UserName", "Email", "DisplayName"], userNode.SelectedColumns);
        Assert.Equal("Owner", userNode.OriginalIncludePath);
        Assert.Equal(2, userNode.Children.Count); // ContentReport and Comment should be the 2 child tables of user

        // Third node
        var commentNode = userNode.Children[0];
        Assert.Equal(typeof(Comment), commentNode.TableType);
        Assert.Equal(["CommentId", "Body"], commentNode.SelectedColumns);
        Assert.Equal("Owner.Comments", commentNode.OriginalIncludePath);
        Assert.Equal(2, commentNode.Children.Count); // Post and Reaction should be the 2 child tables of Comment

        // Fourth node
        var contentReportNode = userNode.Children[1];
        Assert.Equal(typeof(ContentReport), contentReportNode.TableType);
        Assert.Equal(["ReportId", "Reason", "Description"], contentReportNode.SelectedColumns);
        Assert.Equal("Owner.SubmittedReports", contentReportNode.OriginalIncludePath);

        // Fifth node
        var postNode = commentNode.Children[0];
        Assert.Equal(typeof(Post), postNode.TableType);
        Assert.Equal(["PostId", "Title", "Content"], postNode.SelectedColumns);
        Assert.Equal("Owner.Comments.Post", postNode.OriginalIncludePath);
        Assert.Empty(postNode.Children);

        // Sixth node
        var reactionNode = commentNode.Children[1];
        Assert.Equal(typeof(Reaction), reactionNode.TableType);
        Assert.Equal(["ReactionId", "Kind"], reactionNode.SelectedColumns);
        Assert.Equal("Owner.Comments.Reactions", reactionNode.OriginalIncludePath);
        Assert.Empty(reactionNode.Children);
    }
}