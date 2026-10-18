namespace ShikkhaDesk.Core.Services;

/// <summary>
/// Builds the double-entry journal draft for each business event. This is the single place where
/// accounting policy is expressed, so the same rules apply to UI actions, LAN requests and tests.
/// </summary>
public static class JournalBuilder
{
    // ---------------------------------------------------------------- fee assessment

    /// <summary>
    /// Accrual policy: assessing a fee recognises the receivable and the fee income at the same time.
    /// A later payment settles the receivable and must never recognise the income a second time.
    /// </summary>
    public static JournalDraft ForFeeAssessment(
        Guid assessmentId,
        string assessmentNumber,
        DateTime assessmentDate,
        IReadOnlyList<FeeAssessmentLine> lines,
        AccountMap map)
    {
        map.Validate();
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            throw new InvalidOperationException("ফি ধার্য করার জন্য অন্তত একটি খাত থাকতে হবে।");
        }

        var journalLines = new List<JournalLineDraft>();
        foreach (FeeAssessmentLine line in lines)
        {
            if (line.NetAmount.IsZero)
            {
                continue;
            }

            journalLines.Add(JournalLineDraft.Dr(map.StudentReceivables, line.NetAmount,
                studentId: line.StudentId, memo: line.Description));
            journalLines.Add(JournalLineDraft.Cr(map.IncomeAccountFor(line.Kind), line.NetAmount,
                studentId: line.StudentId, memo: line.Description));
        }

        var draft = new JournalDraft
        {
            SourceKind = TransactionSourceKind.FeeAssessment,
            SourceId = assessmentId,
            SourceReference = assessmentNumber,
            EntryDate = assessmentDate,
            Description = $"ফি ধার্য — {assessmentNumber}",
            Lines = journalLines
        };

        draft.Validate();
        return draft;
    }

    public sealed record FeeAssessmentLine(
        Guid StudentId,
        FeeChargeKind Kind,
        Money NetAmount,
        string Description);

    // ---------------------------------------------------------------- fee payment

    /// <summary>
    /// Money received is split between settling receivables and advance credit. Only the allocated
    /// part reduces the receivable; the remainder becomes a liability, never income.
    /// </summary>
    public static JournalDraft ForFeePayment(
        Guid paymentId,
        string receiptNumber,
        DateTime paymentDate,
        Guid studentId,
        Money amountReceived,
        Money allocatedToFees,
        Guid depositAccountId,
        Guid depositFinancialAccountId,
        AccountMap map)
    {
        map.Validate();
        if (amountReceived <= Money.Zero)
        {
            throw new InvalidOperationException("পেমেন্টের পরিমাণ শূন্যের বেশি হতে হবে।");
        }

        if (allocatedToFees > amountReceived)
        {
            throw new InvalidOperationException(
                "প্রাপ্ত অর্থের চেয়ে বেশি বরাদ্দ করা যাবে না।");
        }

        Money advance = amountReceived - allocatedToFees;
        var journalLines = new List<JournalLineDraft>
        {
            JournalLineDraft.Dr(depositAccountId, amountReceived,
                financialAccountId: depositFinancialAccountId, studentId: studentId,
                memo: receiptNumber)
        };

        if (allocatedToFees.IsPositive)
        {
            journalLines.Add(JournalLineDraft.Cr(map.StudentReceivables, allocatedToFees,
                studentId: studentId, memo: receiptNumber));
        }

        if (advance.IsPositive)
        {
            journalLines.Add(JournalLineDraft.Cr(map.StudentAdvanceCredit, advance,
                studentId: studentId, memo: "অগ্রিম জমা (অবরাদ্দকৃত)"));
        }

        var draft = new JournalDraft
        {
            SourceKind = TransactionSourceKind.FeePayment,
            SourceId = paymentId,
            SourceReference = receiptNumber,
            EntryDate = paymentDate,
            Description = $"ফি আদায় — {receiptNumber}",
            Lines = journalLines
        };

        draft.Validate();
        return draft;
    }

    // ---------------------------------------------------------------- credit allocation

    /// <summary>
    /// Applying advance credit settles a receivable and releases the liability. It moves money that
    /// was already received, so no cash, bank or income account is touched.
    /// </summary>
    public static JournalDraft ForCreditAllocation(
        Guid allocationId,
        DateTime allocationDate,
        Guid studentId,
        Money amount,
        AccountMap map)
    {
        map.Validate();
        var draft = new JournalDraft
        {
            SourceKind = TransactionSourceKind.CreditAllocation,
            SourceId = allocationId,
            EntryDate = allocationDate,
            Description = "অগ্রিম ক্রেডিট বরাদ্দ",
            Lines =
            [
                JournalLineDraft.Dr(map.StudentAdvanceCredit, amount, studentId: studentId),
                JournalLineDraft.Cr(map.StudentReceivables, amount, studentId: studentId)
            ]
        };

        draft.Validate();
        return draft;
    }

    // ---------------------------------------------------------------- refund

    /// <summary>
    /// Refund treatment (see docs/ACCOUNTING-DESIGN.md):
    /// * money that was never allocated was only ever a liability, so refunding it releases that
    ///   liability (Dr Student Advance Credit, Cr Cash/Bank);
    /// * money that had already been allocated to a fee makes that fee payable again, so the
    ///   receivable is restored (Dr Student Receivables, Cr Cash/Bank). The assessment and the income
    ///   recognised for it are left untouched, because the fee is still owed.
    /// In neither case is a refund posted as an expense. Cancelling a fee is a separate, explicit
    /// waiver action that reverses income.
    /// </summary>
    public static JournalDraft ForRefund(
        Guid refundId,
        string refundNumber,
        DateTime refundDate,
        Guid studentId,
        Money amount,
        Money amountFromUnallocatedCredit,
        Money amountFromAllocatedFees,
        Guid paymentAccountId,
        Guid paymentFinancialAccountId,
        Guid receivableAccountId,
        AccountMap map)
    {
        map.Validate();
        if (amount != amountFromUnallocatedCredit + amountFromAllocatedFees)
        {
            throw new InvalidOperationException("ফেরতের পরিমাণ ও উৎসের সমষ্টি মেলেনি।");
        }

        if (amount <= Money.Zero)
        {
            throw new InvalidOperationException("ফেরতের পরিমাণ শূন্যের বেশি হতে হবে।");
        }

        var journalLines = new List<JournalLineDraft>();
        if (amountFromUnallocatedCredit.IsPositive)
        {
            journalLines.Add(JournalLineDraft.Dr(map.StudentAdvanceCredit, amountFromUnallocatedCredit,
                studentId: studentId, memo: "অগ্রিম ক্রেডিট ফেরত"));
        }

        if (amountFromAllocatedFees.IsPositive)
        {
            journalLines.Add(JournalLineDraft.Dr(receivableAccountId, amountFromAllocatedFees,
                studentId: studentId, memo: "বকেয়া পুনরুদ্ধার (ফেরত)"));
        }

        journalLines.Add(JournalLineDraft.Cr(paymentAccountId, amount,
            financialAccountId: paymentFinancialAccountId, studentId: studentId, memo: refundNumber));

        var draft = new JournalDraft
        {
            SourceKind = TransactionSourceKind.Refund,
            SourceId = refundId,
            SourceReference = refundNumber,
            EntryDate = refundDate,
            Description = $"ফি ফেরত — {refundNumber}",
            Lines = journalLines
        };

        draft.Validate();
        return draft;
    }

    // ---------------------------------------------------------------- income / expense

    public static JournalDraft ForIncome(
        Guid incomeId,
        string incomeNumber,
        DateTime incomeDate,
        Money amount,
        Guid incomeAccountId,
        Guid depositAccountId,
        Guid depositFinancialAccountId,
        string description)
    {
        var draft = new JournalDraft
        {
            SourceKind = TransactionSourceKind.Income,
            SourceId = incomeId,
            SourceReference = incomeNumber,
            EntryDate = incomeDate,
            Description = description,
            Lines =
            [
                JournalLineDraft.Dr(depositAccountId, amount, financialAccountId: depositFinancialAccountId),
                JournalLineDraft.Cr(incomeAccountId, amount)
            ]
        };

        draft.Validate();
        return draft;
    }

    public static JournalDraft ForExpensePaidImmediately(
        Guid expenseId,
        string expenseNumber,
        DateTime expenseDate,
        Money amount,
        Guid expenseAccountId,
        Guid paymentAccountId,
        Guid paymentFinancialAccountId,
        Guid? supplierId,
        string description)
    {
        var draft = new JournalDraft
        {
            SourceKind = TransactionSourceKind.Expense,
            SourceId = expenseId,
            SourceReference = expenseNumber,
            EntryDate = expenseDate,
            Description = description,
            Lines =
            [
                JournalLineDraft.Dr(expenseAccountId, amount, supplierId: supplierId),
                JournalLineDraft.Cr(paymentAccountId, amount, financialAccountId: paymentFinancialAccountId,
                    supplierId: supplierId)
            ]
        };

        draft.Validate();
        return draft;
    }

    public static JournalDraft ForExpenseAccrual(
        Guid expenseId,
        string expenseNumber,
        DateTime expenseDate,
        Money amount,
        Guid expenseAccountId,
        Guid? supplierId,
        string description,
        AccountMap map)
    {
        map.Validate();
        var draft = new JournalDraft
        {
            SourceKind = TransactionSourceKind.PayableAccrual,
            SourceId = expenseId,
            SourceReference = expenseNumber,
            EntryDate = expenseDate,
            Description = description,
            Lines =
            [
                JournalLineDraft.Dr(expenseAccountId, amount, supplierId: supplierId),
                JournalLineDraft.Cr(map.AccountsPayable, amount, supplierId: supplierId)
            ]
        };

        draft.Validate();
        return draft;
    }

    /// <summary>
    /// Settling a payable clears the liability and reduces cash/bank. The expense was already
    /// recognised at accrual, so no expense account is touched here.
    /// </summary>
    public static JournalDraft ForPayableSettlement(
        Guid settlementId,
        string settlementNumber,
        DateTime settlementDate,
        Money amount,
        Guid paymentAccountId,
        Guid paymentFinancialAccountId,
        Guid? supplierId,
        AccountMap map)
    {
        map.Validate();
        var draft = new JournalDraft
        {
            SourceKind = TransactionSourceKind.PayableSettlement,
            SourceId = settlementId,
            SourceReference = settlementNumber,
            EntryDate = settlementDate,
            Description = $"সরবরাহকারী পরিশোধ — {settlementNumber}",
            Lines =
            [
                JournalLineDraft.Dr(map.AccountsPayable, amount, supplierId: supplierId),
                JournalLineDraft.Cr(paymentAccountId, amount, financialAccountId: paymentFinancialAccountId,
                    supplierId: supplierId)
            ]
        };

        draft.Validate();
        return draft;
    }

    // ---------------------------------------------------------------- payroll

    public static JournalDraft ForPayrollAccrual(
        Guid payrollRecordId,
        DateTime periodDate,
        Guid staffId,
        Money netSalary,
        string staffName,
        AccountMap map)
    {
        map.Validate();
        var draft = new JournalDraft
        {
            SourceKind = TransactionSourceKind.PayrollAccrual,
            SourceId = payrollRecordId,
            EntryDate = periodDate,
            Description = $"বেতন স্বীকৃতি — {staffName}",
            Lines =
            [
                JournalLineDraft.Dr(map.SalaryExpense, netSalary, staffId: staffId),
                JournalLineDraft.Cr(map.SalaryPayable, netSalary, staffId: staffId)
            ]
        };

        draft.Validate();
        return draft;
    }

    /// <summary>Salary payment clears the payable; it never recognises the salary expense again.</summary>
    public static JournalDraft ForPayrollPayment(
        Guid payrollRecordId,
        DateTime paymentDate,
        Guid staffId,
        Money netSalary,
        Guid paymentAccountId,
        Guid paymentFinancialAccountId,
        string staffName,
        AccountMap map)
    {
        map.Validate();
        var draft = new JournalDraft
        {
            SourceKind = TransactionSourceKind.PayrollPayment,
            SourceId = payrollRecordId,
            EntryDate = paymentDate,
            Description = $"বেতন পরিশোধ — {staffName}",
            Lines =
            [
                JournalLineDraft.Dr(map.SalaryPayable, netSalary, staffId: staffId),
                JournalLineDraft.Cr(paymentAccountId, netSalary, financialAccountId: paymentFinancialAccountId,
                    staffId: staffId)
            ]
        };

        draft.Validate();
        return draft;
    }

    // ---------------------------------------------------------------- transfer

    /// <summary>A transfer moves money between institution-owned accounts; it is never income or expense.</summary>
    public static JournalDraft ForTransfer(
        Guid transferId,
        string transferNumber,
        DateTime transferDate,
        Money amount,
        Money fee,
        Guid fromAccountId,
        Guid fromFinancialAccountId,
        Guid toAccountId,
        Guid toFinancialAccountId,
        AccountMap map)
    {
        map.Validate();
        if (amount <= Money.Zero)
        {
            throw new InvalidOperationException("স্থানান্তরের পরিমাণ শূন্যের বেশি হতে হবে।");
        }

        if (fromFinancialAccountId == toFinancialAccountId)
        {
            throw new InvalidOperationException("একই হিসাবে স্থানান্তর সম্ভব নয়।");
        }

        var journalLines = new List<JournalLineDraft>
        {
            JournalLineDraft.Dr(toAccountId, amount, financialAccountId: toFinancialAccountId, memo: transferNumber),
            JournalLineDraft.Cr(fromAccountId, amount, financialAccountId: fromFinancialAccountId, memo: transferNumber)
        };

        if (fee.IsPositive)
        {
            journalLines.Add(JournalLineDraft.Dr(map.BankChargeExpense, fee, memo: "স্থানান্তর ফি"));
            journalLines.Add(JournalLineDraft.Cr(fromAccountId, fee, financialAccountId: fromFinancialAccountId,
                memo: "স্থানান্তর ফি"));
        }

        var draft = new JournalDraft
        {
            SourceKind = TransactionSourceKind.Transfer,
            SourceId = transferId,
            SourceReference = transferNumber,
            EntryDate = transferDate,
            Description = $"হিসাব স্থানান্তর — {transferNumber}",
            Lines = journalLines
        };

        draft.Validate();
        return draft;
    }

    // ---------------------------------------------------------------- opening balance & reversal

    public static JournalDraft ForOpeningBalance(
        Guid financialAccountId,
        DateTime asOfDate,
        Money balance,
        Guid financialLedgerAccountId,
        string accountName,
        AccountMap map)
    {
        map.Validate();
        if (balance.IsZero)
        {
            throw new InvalidOperationException("প্রারম্ভিক স্থিতি শূন্য হলে কোনো এন্ট্রি প্রয়োজন নেই।");
        }

        bool isDebit = balance.IsPositive;
        var draft = new JournalDraft
        {
            SourceKind = TransactionSourceKind.OpeningBalance,
            SourceId = financialAccountId,
            EntryDate = asOfDate,
            Description = $"প্রারম্ভিক স্থিতি — {accountName}",
            Lines =
            [
                isDebit
                    ? JournalLineDraft.Dr(financialLedgerAccountId, balance.Abs(),
                        financialAccountId: financialAccountId)
                    : JournalLineDraft.Cr(financialLedgerAccountId, balance.Abs(),
                        financialAccountId: financialAccountId),
                isDebit
                    ? JournalLineDraft.Cr(map.OpeningBalanceEquity, balance.Abs())
                    : JournalLineDraft.Dr(map.OpeningBalanceEquity, balance.Abs())
            ]
        };

        draft.Validate();
        return draft;
    }

    /// <summary>
    /// A reversal mirrors the original entry and keeps a link to it. The original entry is marked
    /// reversed but never edited or deleted, so history stays reproducible.
    /// </summary>
    public static JournalDraft ForReversal(
        Guid originalJournalId,
        string originalJournalNumber,
        DateTime reversalDate,
        IReadOnlyList<(Guid AccountId, Money Debit, Money Credit, Guid? FinancialAccountId,
            Guid? StudentId, Guid? SupplierId, Guid? StaffId)> originalLines,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(originalLines);
        var journalLines = new List<JournalLineDraft>(originalLines.Count);
        foreach (var line in originalLines)
        {
            journalLines.Add(new JournalLineDraft
            {
                AccountId = line.AccountId,
                Debit = line.Credit,
                Credit = line.Debit,
                FinancialAccountId = line.FinancialAccountId,
                StudentId = line.StudentId,
                SupplierId = line.SupplierId,
                StaffId = line.StaffId,
                Memo = reason
            });
        }

        var draft = new JournalDraft
        {
            SourceKind = TransactionSourceKind.Reversal,
            SourceId = originalJournalId,
            SourceReference = originalJournalNumber,
            EntryDate = reversalDate,
            Description = $"বাতিল — {originalJournalNumber}: {reason}",
            Lines = journalLines
        };

        draft.Validate();
        return draft;
    }
}
