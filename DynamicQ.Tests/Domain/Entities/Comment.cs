namespace DynamicQ.Tests.Domain.Entities;

public class Comment
{
    public int CommentId { get; set; }
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
    public int? AuthorUserId { get; set; }
    public User? Author { get; set; }
    public int? ParentCommentId { get; set; }
    public Comment? ParentComment { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<Comment> Replies { get; set; } = [];
    public ICollection<Reaction> Reactions { get; set; } = [];
}

