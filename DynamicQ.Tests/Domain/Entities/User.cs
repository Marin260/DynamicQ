namespace DynamicQ.Tests.Domain.Entities;

public class User
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Blog> OwnedBlogs { get; set; } = [];
    public ICollection<Post> AuthoredPosts { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<ContentReport> SubmittedReports { get; set; } = [];
}
