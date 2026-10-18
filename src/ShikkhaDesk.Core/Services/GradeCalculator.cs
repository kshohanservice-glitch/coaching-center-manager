using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Core.Services;

/// <summary>
/// Result grading with documented rules: percentages are rounded half-up to two decimals and grade
/// boundaries are inclusive of the minimum percentage.
/// </summary>
public static class GradeCalculator
{
    public static decimal Percentage(decimal obtained, decimal maximum)
    {
        if (maximum <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "পূর্ণমান শূন্যের বেশি হতে হবে।");
        }

        return Math.Round(obtained / maximum * 100m, 2, MidpointRounding.AwayFromZero);
    }

    public static bool IsPassed(decimal obtained, decimal passMarks, bool isAbsent) =>
        !isAbsent && obtained >= passMarks;

    public static string? GradeFor(decimal percentage, IReadOnlyList<GradeScaleItem> scale)
    {
        if (scale.Count == 0)
        {
            return null;
        }

        foreach (GradeScaleItem item in scale.OrderByDescending(s => s.MinimumPercentage))
        {
            if (percentage >= item.MinimumPercentage)
            {
                return item.Grade;
            }
        }

        return scale.OrderBy(s => s.MinimumPercentage).First().Grade;
    }

    public static decimal? GradePointFor(string? grade, IReadOnlyList<GradeScaleItem> scale)
    {
        if (string.IsNullOrEmpty(grade) || scale.Count == 0)
        {
            return null;
        }

        return scale.FirstOrDefault(s => string.Equals(s.Grade, grade, StringComparison.OrdinalIgnoreCase))
            ?.GradePoint;
    }
}
