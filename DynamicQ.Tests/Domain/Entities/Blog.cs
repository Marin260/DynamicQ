namespace DynamicQ.Tests.Domain.Entities;

public class Blog
{
    public int BlogId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public int? OwnerUserId { get; set; }
    public User? Owner { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Post> Posts { get; set; } = [];
}
