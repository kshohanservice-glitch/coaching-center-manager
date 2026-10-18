using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Dto;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;
using Xunit;

namespace ShikkhaDesk.IntegrationTests;

public sealed class FeeAndPaymentTests
{
    [Fact]
    public void Monthly_assessment_creates_charges_and_a_balanced_journal()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid batchId, Guid studentId) = host.CreateBatchWithStudent();
        host.CreateFeePlan(Money.FromDecimal(2000), batchId);

        FeeAssessmentResult result = host.Get<IFeeService>()
            .AssessMonthlyFeesAsync(new AssessMonthlyFeesRequest(batchId, "2026-03")).GetAwaiter().GetResult();

        Assert.Equal(1, result.ChargeCount);
        Assert.Equal(Money.FromDecimal(2000), result.TotalNet);
        Assert.NotEqual(Guid.Empty, result.JournalEntryId);

        StudentBalanceSummary balance = host.Get<IStudentService>()
            .GetBalanceAsync(studentId).GetAwaiter().GetResult();

        Assert.Equal(Money.FromDecimal(2500), balance.Outstanding);
    }

    [Fact]
    public void Running_the_same_assessment_twice_never_duplicates_charges()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid batchId, Guid studentId) = host.CreateBatchWithStudent();
        host.CreateFeePlan(Money.FromDecimal(2000), batchId);

        IFeeService fees = host.Get<IFeeService>();
        fees.AssessMonthlyFeesAsync(new AssessMonthlyFeesRequest(batchId, "2026-03")).GetAwaiter().GetResult();

        Assert.Throws<DomainException>(() => fees
            .AssessMonthlyFeesAsync(new AssessMonthlyFeesRequest(batchId, "2026-03")).GetAwaiter().GetResult());

        IReadOnlyList<FeeCharge> charges = fees.GetOutstandingChargesAsync(batchId).GetAwaiter().GetResult();
        Assert.Single(charges);
        Assert.Equal(Money.FromDecimal(2000), charges[0].NetAmount);
    }

    [Fact]
    public void Preview_matches_what_is_actually_posted()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid batchId, _) = host.CreateBatchWithStudent();
        host.CreateFeePlan(Money.FromDecimal(1500), batchId);

        IFeeService fees = host.Get<IFeeService>();
        var request = new AssessMonthlyFeesRequest(batchId, "2026-04");

        AssessmentPreview preview = fees.PreviewMonthlyAssessmentAsync(request).GetAwaiter().GetResult();
        FeeAssessmentResult result = fees.AssessMonthlyFeesAsync(request).GetAwaiter().GetResult();

        Assert.Equal(preview.ChargeCount, result.ChargeCount);
        Assert.Equal(preview.TotalNet, result.TotalNet);
    }

    [Fact]
    public void Partial_payment_allocates_and_reduces_outstanding()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid batchId, Guid studentId) = host.CreateBatchWithStudent();
        host.CreateFeePlan(Money.FromDecimal(2000), batchId);
        host.Get<IFeeService>().AssessMonthlyFeesAsync(new AssessMonthlyFeesRequest(batchId, "2026-03"))
            .GetAwaiter().GetResult();

        PaymentResult payment = host.Get<IPaymentService>().CollectAsync(new CollectPaymentRequest(
            studentId, Money.FromDecimal(1200), PaymentMethod.Cash)).GetAwaiter().GetResult();

        Assert.False(payment.WasDuplicate);
        Assert.Equal(Money.FromDecimal(1200), payment.Receipt.AmountAllocated);
        Assert.True(payment.Receipt.AdvanceCredit.IsZero);

        StudentBalanceSummary balance = host.Get<IStudentService>()
            .GetBalanceAsync(studentId).GetAwaiter().GetResult();

        Assert.Equal(Money.FromDecimal(1300), balance.Outstanding);
        Assert.Equal(Money.FromDecimal(1200), balance.TotalPaid);
    }

    [Fact]
    public void Overpayment_becomes_advance_credit_and_never_income()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid batchId, Guid studentId) = host.CreateBatchWithStudent();
        host.CreateFeePlan(Money.FromDecimal(2000), batchId);
        host.Get<IFeeService>().AssessMonthlyFeesAsync(new AssessMonthlyFeesRequest(batchId, "2026-03"))
            .GetAwaiter().GetResult();

        host.Get<IPaymentService>().CollectAsync(new CollectPaymentRequest(
            studentId, Money.FromDecimal(3000), PaymentMethod.Cash)).GetAwaiter().GetResult();

        StudentBalanceSummary balance = host.Get<IStudentService>()
            .GetBalanceAsync(studentId).GetAwaiter().GetResult();

        Assert.Equal(Money.FromDecimal(1000), balance.CreditBalance);
        Assert.Equal(Money.FromDecimal(500), balance.Outstanding);

        AccountMap map = host.Get<IAccountingService>().GetAccountMapAsync().GetAwaiter().GetResult();
        Money creditLiability = host.Get<IAccountingService>()
            .GetAccountBalanceAsync(map.StudentAdvanceCredit, null).GetAwaiter().GetResult();

        Assert.Equal(Money.FromDecimal(1000), creditLiability);
    }

    [Fact]
    public void The_same_money_is_never_both_settled_fees_and_available_credit()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid batchId, Guid studentId) = host.CreateBatchWithStudent();
        host.CreateFeePlan(Money.FromDecimal(2000), batchId);
        host.Get<IFeeService>().AssessMonthlyFeesAsync(new AssessMonthlyFeesRequest(batchId, "2026-03"))
            .GetAwaiter().GetResult();

        host.Get<IPaymentService>().CollectAsync(new CollectPaymentRequest(
            studentId, Money.FromDecimal(3000), PaymentMethod.Cash)).GetAwaiter().GetResult();

        StudentBalanceSummary before = host.Get<IStudentService>()
            .GetBalanceAsync(studentId).GetAwaiter().GetResult();

        Money applied = host.Get<IPaymentService>()
            .ApplyCreditAsync(studentId).GetAwaiter().GetResult();

        Assert.Equal(Money.FromDecimal(500), applied);

        StudentBalanceSummary after = host.Get<IStudentService>()
            .GetBalanceAsync(studentId).GetAwaiter().GetResult();

        Assert.Equal(Money.Zero, after.Outstanding);
        Assert.Equal(Money.FromDecimal(500), after.CreditBalance);
        Assert.Equal(before.Outstanding - applied, after.Outstanding);
    }

    [Fact]
    public void Retrying_with_the_same_idempotency_key_does_not_charge_twice()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid batchId, Guid studentId) = host.CreateBatchWithStudent();
        host.CreateFeePlan(Money.FromDecimal(1000), batchId);
        host.Get<IFeeService>().AssessMonthlyFeesAsync(new AssessMonthlyFeesRequest(batchId, "2026-03"))
            .GetAwaiter().GetResult();

        IPaymentService payments = host.Get<IPaymentService>();
        const string key = "retry-key-0001";

        PaymentResult first = payments.CollectAsync(new CollectPaymentRequest(
            studentId, Money.FromDecimal(1000), PaymentMethod.Cash, IdempotencyKey: key))
            .GetAwaiter().GetResult();

        PaymentResult second = payments.CollectAsync(new CollectPaymentRequest(
            studentId, Money.FromDecimal(1000), PaymentMethod.Cash, IdempotencyKey: key))
            .GetAwaiter().GetResult();

        Assert.False(first.WasDuplicate);
        Assert.True(second.WasDuplicate);
        Assert.Equal(first.ReceiptNumber, second.ReceiptNumber);

        PagedResult<PaymentListItem> all = payments.SearchAsync(new PaymentQuery()).GetAwaiter().GetResult();
        Assert.Single(all.Items);
    }

    [Fact]
    public void Receipt_numbers_are_sequential_and_never_reused()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid batchId, Guid studentId) = host.CreateBatchWithStudent();

        IPaymentService payments = host.Get<IPaymentService>();
        var numbers = new List<string>();

        for (int i = 0; i < 5; i++)
        {
            PaymentResult result = payments.CollectAsync(new CollectPaymentRequest(
                studentId, Money.FromDecimal(100), PaymentMethod.Cash)).GetAwaiter().GetResult();
            numbers.Add(result.ReceiptNumber);
        }

        Assert.Equal(numbers.Count, numbers.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(["RCP-2026-000001", "RCP-2026-000002", "RCP-2026-000003", "RCP-2026-000004",
            "RCP-2026-000005"], numbers);
    }

    [Fact]
    public void Concurrent_payments_get_unique_receipt_numbers_and_are_all_posted()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid batchId, Guid studentId) = host.CreateBatchWithStudent();

        IPaymentService payments = host.Get<IPaymentService>();
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 20, _ =>
        {
            PaymentResult result = payments.CollectAsync(new CollectPaymentRequest(
                studentId, Money.FromDecimal(50), PaymentMethod.Cash)).GetAwaiter().GetResult();
            results.Add(result.ReceiptNumber);
        });

        Assert.Equal(20, results.Count);
        Assert.Equal(20, results.Distinct(StringComparer.Ordinal).Count());

        StudentBalanceSummary balance = host.Get<IStudentService>()
            .GetBalanceAsync(studentId).GetAwaiter().GetResult();

        Assert.Equal(Money.FromDecimal(1000), balance.CreditBalance);
    }

    [Fact]
    public void Reprinting_a_receipt_does_not_create_a_new_payment()
    {
        using TestHost host = TestHost.Create().Setup();
        (_, Guid studentId) = host.CreateBatchWithStudent();

        IPaymentService payments = host.Get<IPaymentService>();
        PaymentResult original = payments.CollectAsync(new CollectPaymentRequest(
            studentId, Money.FromDecimal(800), PaymentMethod.Cash)).GetAwaiter().GetResult();

        ReceiptData reprint = payments.ReprintAsync(original.ReceiptNumber).GetAwaiter().GetResult();

        Assert.True(reprint.IsReprint);
        Assert.Equal(1, reprint.ReprintCount);
        Assert.Equal(original.ReceiptNumber, reprint.ReceiptNumber);

        PagedResult<PaymentListItem> all = payments.SearchAsync(new PaymentQuery()).GetAwaiter().GetResult();
        Assert.Single(all.Items);
        Assert.Equal(Money.FromDecimal(800), all.Items[0].AmountReceived);
    }

    [Fact]
    public void Refunding_unallocated_credit_releases_the_liability()
    {
        using TestHost host = TestHost.Create().Setup();
        (_, Guid studentId) = host.CreateBatchWithStudent();

        host.Get<IPaymentService>().CollectAsync(new CollectPaymentRequest(
            studentId, Money.FromDecimal(4000), PaymentMethod.Cash)).GetAwaiter().GetResult();

        Guid refundId = host.Get<IPaymentService>().RefundAsync(new RefundRequest(
            studentId, Money.FromDecimal(4000), "অতিরিক্ত জমা ফেরত", PaymentMethod.Cash))
            .GetAwaiter().GetResult();

        Assert.NotEqual(Guid.Empty, refundId);

        StudentBalanceSummary balance = host.Get<IStudentService>()
            .GetBalanceAsync(studentId).GetAwaiter().GetResult();

        Assert.Equal(Money.Zero, balance.CreditBalance);

        foreach (ReconciliationCheck check in host.Get<IReportingService>()
                     .RunReconciliationAsync().GetAwaiter().GetResult())
        {
            Assert.True(check.Passed, $"{check.Name}: expected {check.Expected.ToDecimal()} got {check.Actual.ToDecimal()}");
        }
    }

    [Fact]
    public void Reversing_a_payment_restores_the_outstanding_balance()
    {
        using TestHost host = TestHost.Create().Setup();
        (Guid batchId, Guid studentId) = host.CreateBatchWithStudent();
        host.CreateFeePlan(Money.FromDecimal(2000), batchId);
        host.Get<IFeeService>().AssessMonthlyFeesAsync(new AssessMonthlyFeesRequest(batchId, "2026-03"))
            .GetAwaiter().GetResult();

        IPaymentService payments = host.Get<IPaymentService>();
        PaymentResult payment = payments.CollectAsync(new CollectPaymentRequest(
            studentId, Money.FromDecimal(2000), PaymentMethod.Cash)).GetAwaiter().GetResult();

        StudentBalanceSummary before = host.Get<IStudentService>()
            .GetBalanceAsync(studentId).GetAwaiter().GetResult();
        Assert.Equal(Money.FromDecimal(500), before.Outstanding);

        payments.ReversePaymentAsync(payment.PaymentId, "ভুল কালেকশন").GetAwaiter().GetResult();

        StudentBalanceSummary after = host.Get<IStudentService>()
            .GetBalanceAsync(studentId).GetAwaiter().GetResult();

        Assert.Equal(Money.FromDecimal(2500), after.Outstanding);

        // The original payment is kept, marked reversed, and the ledger is mirrored rather than edited.
        PagedResult<PaymentListItem> all = payments.SearchAsync(new PaymentQuery()).GetAwaiter().GetResult();
        Assert.Single(all.Items);
        Assert.Equal(PaymentStatus.Reversed, all.Items[0].Status);
    }
}
