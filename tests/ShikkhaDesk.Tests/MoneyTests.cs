using ShikkhaDesk.Core;
using Xunit;

namespace ShikkhaDesk.Tests;

public sealed class MoneyTests
{
    [Fact]
    public void Money_never_uses_floating_point_internals()
    {
        Money amount = Money.FromDecimal(0.1m) + Money.FromDecimal(0.2m);
        Assert.Equal(30L, amount.MinorUnits);
        Assert.Equal(0.30m, amount.ToDecimal());
    }

    [Theory]
    [InlineData("100", 10000L)]
    [InlineData("১০০", 10000L)]
    [InlineData("1,250.50", 125050L)]
    [InlineData("৳ 1250.50", 125050L)]
    [InlineData("(250)", -25000L)]
    [InlineData("-5", -500L)]
    public void Parse_accepts_bengali_and_english_digits(string input, long expected)
    {
        Assert.True(Money.TryParse(input, out Money value));
        Assert.Equal(expected, value.MinorUnits);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("   ")]
    public void Parse_rejects_garbage(string input)
    {
        Assert.False(Money.TryParse(input, out _));
    }

    [Fact]
    public void Addition_and_subtraction_do_not_overflow_silently()
    {
        Assert.Equal(Money.FromDecimal(1300), Money.FromDecimal(1000) + Money.FromDecimal(300));
        Assert.Equal(Money.FromDecimal(700), Money.FromDecimal(1000) - Money.FromDecimal(300));
    }

    [Fact]
    public void Allocate_never_loses_or_creates_a_single_paisa()
    {
        Money total = Money.FromMinorUnits(1000);
        IReadOnlyList<Money> parts = Money.Allocate(total, [1, 1, 1]);

        Assert.Equal(3, parts.Count);
        Assert.Equal(total, parts[0] + parts[1] + parts[2]);
        // 1000 / 3 -> 333.34, 333.33, 333.33 with the remainder going to the first bucket.
        Assert.Equal(334, parts[0].MinorUnits);
        Assert.Equal(333, parts[1].MinorUnits);
        Assert.Equal(333, parts[2].MinorUnits);
    }

    [Fact]
    public void Allocate_handles_zero_weights_without_dropping_money()
    {
        Money total = Money.FromDecimal(500);
        IReadOnlyList<Money> parts = Money.Allocate(total, [0, 0]);
        Assert.Equal(total, parts[0] + parts[1]);
    }

    [Fact]
    public void Allocate_is_deterministic()
    {
        Money total = Money.FromMinorUnits(7);
        IReadOnlyList<Money> first = Money.Allocate(total, [1, 1, 1]);
        IReadOnlyList<Money> second = Money.Allocate(total, [1, 1, 1]);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Percentage_rounds_half_away_from_zero()
    {
        Assert.Equal(Money.FromMajor(150), Money.FromDecimal(1000).Percentage(15));
        Assert.Equal(Money.FromMinorUnits(1501), Money.FromMinorUnits(10005).Percentage(15));
    }

    [Fact]
    public void Comparisons_and_equality_behave_like_numbers()
    {
        Assert.True(Money.FromDecimal(10) > Money.FromDecimal(9));
        Assert.True(Money.FromDecimal(10) == Money.FromMinorUnits(1000));
        Assert.False(Money.FromDecimal(10) != Money.FromMinorUnits(1000));
        Assert.Equal(Money.FromDecimal(10).GetHashCode(), Money.FromMinorUnits(1000).GetHashCode());
    }
}
