using DynamicQ.Tests.Domain.Entities;
using Microsoft.Extensions.Options;
using DynamicQuery.DataStructures;

namespace DynamicQuery.Tests.Substitutes;

public static class DynamicQSubstitute
{
    public static DynamicQ GetDynamicQSubstitute()
    {
        var options = new DynamicQOptions();

        // Same CLR type can be registered per navigation path; VirtualNavigationName must match the property on the parent (e.g. Blog.Owner).
        options.RegisteredTables.Add(new RegisteredTable(typeof(Blog),          "Blog",             "Blog",                          ["BlogId"]));
        options.RegisteredTables.Add(new RegisteredTable(typeof(User),          "Owner",            "Blog.Owner",                    null));
        options.RegisteredTables.Add(new RegisteredTable(typeof(Comment),       "Comments",         "Blog.Owner.Comments",           null));
        options.RegisteredTables.Add(new RegisteredTable(typeof(ContentReport), "SubmittedReports", "Blog.Owner.SubmittedReports",   null));
        options.RegisteredTables.Add(new RegisteredTable(typeof(Post),          "Post",             "Blog.Owner.Comments.Post",      null));
        options.RegisteredTables.Add(new RegisteredTable(typeof(Reaction),      "Reactions",        "Blog.Owner.Comments.Reactions", null));
        return new(Options.Create(options));
    }
}