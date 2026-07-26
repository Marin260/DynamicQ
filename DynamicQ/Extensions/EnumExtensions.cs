using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace DynamicQuery.Extensions;

/// <summary>
/// Helpers for enum values and display metadata.
/// </summary>
internal static class EnumExtensions
{
    /// <summary>
    /// Returns the <see cref="DisplayAttribute.Name"/> for <paramref name="enumValue"/> if present; otherwise the enum member name.
    /// </summary>
    internal static string GetDisplayName(this Enum enumValue) =>
        enumValue.GetType()
            .GetMember(enumValue.ToString())
            .FirstOrDefault()?
            .GetCustomAttribute<DisplayAttribute>()
            ?.GetName() ?? enumValue.ToString();
}