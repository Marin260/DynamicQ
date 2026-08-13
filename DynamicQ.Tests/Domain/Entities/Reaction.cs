namespace DynamicQ.Tests.Domain.Entities;

public class Reaction
{
    public int ReactionId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int? PostId { get; set; }
    public Post? Post { get; set; }
    public int? CommentId { get; set; }
    public Comment? Comment { get; set; }
    public ReactionKind Kind { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public enum ReactionKind
{
    Like = 0,
    Love = 1,
    Insightful = 2,
}
