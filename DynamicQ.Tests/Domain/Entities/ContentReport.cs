namespace DynamicQ.Tests.Domain.Entities;

public class ContentReport
{
    public int ContentReportId { get; set; }
    public int ReporterUserId { get; set; }
    public User Reporter { get; set; } = null!;
    public ReportTargetKind TargetKind { get; set; }
    public int TargetId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ReportStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public enum ReportTargetKind
{
    Post = 0,
    Comment = 1,
    User = 2,
}

public enum ReportStatus
{
    Open = 0,
    Reviewed = 1,
    Dismissed = 2,
}
