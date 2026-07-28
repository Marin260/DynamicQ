namespace DynamicQ.Tests.Domain.Entities;

public class Post
{
    public int PostId { get; set; }
    public int BlogId { get; set; }
    public Blog Blog { get; set; } = null!;
    public int AuthorUserId { get; set; }
    public User Author { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
    public PostStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

public enum PostStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
}