namespace ShikkhaDesk.Core.Services;

public enum AllocationStrategy
{
    /// <summary>Oldest dues first (by due date, then by charge date, then by creation order).</summary>
    OldestFirst = 0,
    /// <summary>Spread across every open charge in proportion to the outstanding balance.</summary>
    ProRata = 1,
    /// <summary>Explicit per-charge amounts supplied by the operator.</summary>
    Manual = 2
}

public sealed record AllocationTarget(Guid ChargeId, Money Outstanding, DateTime DueDate, DateTime ChargeDate, int Order);

public sealed record AllocationResult(IReadOnlyList<(Guid ChargeId, Money Amount)> Allocations, Money Unallocated);

/// <summary>
/// Decides how received money is split between outstanding fees and advance credit. The allocator is
/// pure: it never touches a database and it can never allocate more than the money available, nor
/// more than any single charge still owes.
/// </summary>
public static class PaymentAllocator
{
    public static AllocationResult Allocate(
        Money amountAvailable,
        IReadOnlyList<AllocationTarget> targets,
        AllocationStrategy strategy,
        IReadOnlyList<(Guid ChargeId, Money Amount)>? manualAmounts = null)
    {
        if (amountAvailable.IsNegative)
        {
            throw new InvalidOperationException("উপলব্ধ অর্থ ঋণাত্মক হতে পারবে না।");
        }

        var open = targets
            .Where(t => t.Outstanding.IsPositive)
            .ToList();

        if (amountAvailable.IsZero || open.Count == 0)
        {
            return new AllocationResult(Array.Empty<(Guid, Money)>(), amountAvailable);
        }

        return strategy switch
        {
            AllocationStrategy.Manual => AllocateManual(amountAvailable, open, manualAmounts),
            AllocationStrategy.ProRata => AllocateProRata(amountAvailable, open),
            _ => AllocateOldestFirst(amountAvailable, open)
        };
    }

    private static AllocationResult AllocateOldestFirst(Money available, List<AllocationTarget> open)
    {
        var ordered = open
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.ChargeDate)
            .ThenBy(t => t.Order)
            .ToList();

        var allocations = new List<(Guid ChargeId, Money Amount)>();
        Money remaining = available;

        foreach (AllocationTarget target in ordered)
        {
            if (remaining.IsZero)
            {
                break;
            }

            Money applied = Money.Min(remaining, target.Outstanding);
            if (applied.IsPositive)
            {
                allocations.Add((target.ChargeId, applied));
                remaining -= applied;
            }
        }

        return new AllocationResult(allocations, remaining);
    }

    private static AllocationResult AllocateProRata(Money available, List<AllocationTarget> open)
    {
        Money totalOutstanding = open.Aggregate(Money.Zero, (sum, t) => sum + t.Outstanding);
        Money toSpread = Money.Min(available, totalOutstanding);

        IReadOnlyList<Money> parts = Money.Allocate(
            toSpread,
            open.Select(t => t.Outstanding.MinorUnits).ToList());

        var allocations = new List<(Guid ChargeId, Money Amount)>(open.Count);
        Money applied = Money.Zero;
        for (int i = 0; i < open.Count; i++)
        {
            Money amount = Money.Min(parts[i], open[i].Outstanding);
            if (amount.IsPositive)
            {
                allocations.Add((open[i].ChargeId, amount));
                applied += amount;
            }
        }

        return new AllocationResult(allocations, available - applied);
    }

    private static AllocationResult AllocateManual(
        Money available,
        List<AllocationTarget> open,
        IReadOnlyList<(Guid ChargeId, Money Amount)>? manualAmounts)
    {
        if (manualAmounts is null || manualAmounts.Count == 0)
        {
            throw new InvalidOperationException("ম্যানুয়াল বরাদ্দের জন্য পরিমাণ নির্ধারণ করতে হবে।");
        }

        var byCharge = open.ToDictionary(t => t.ChargeId);
        var allocations = new List<(Guid ChargeId, Money Amount)>();
        Money applied = Money.Zero;

        foreach ((Guid chargeId, Money amount) in manualAmounts)
        {
            if (amount.IsZero)
            {
                continue;
            }

            if (amount.IsNegative)
            {
                throw new InvalidOperationException("বরাদ্দের পরিমাণ ঋণাত্মক হতে পারবে না।");
            }

            if (!byCharge.TryGetValue(chargeId, out AllocationTarget? target))
            {
                throw new InvalidOperationException("নির্বাচিত ফি খাতটি এই শিক্ষার্থীর জন্য খোলা নেই।");
            }

            if (amount > target.Outstanding)
            {
                throw new InvalidOperationException(
                    $"একটি খাতে বকেয়ার চেয়ে বেশি ({amount.ToDecimal():N2} > {target.Outstanding.ToDecimal():N2}) বরাদ্দ করা যাবে না।");
            }

            allocations.Add((chargeId, amount));
            applied += amount;
        }

        if (applied > available)
        {
            throw new InvalidOperationException(
                $"মোট বরাদ্দ ({applied.ToDecimal():N2}) প্রাপ্ত অর্থের ({available.ToDecimal():N2}) চেয়ে বেশি।");
        }

        return new AllocationResult(allocations, available - applied);
    }
}
