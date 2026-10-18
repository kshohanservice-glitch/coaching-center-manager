-- Reversing a payment posts a mirror journal entry; it does not create a second payment. The link to
-- that entry therefore has to be its own column, because reversed_by_payment_id references payments(id)
-- and storing a journal id there fails the foreign key at commit time.
ALTER TABLE payments ADD COLUMN reversal_journal_entry_id TEXT REFERENCES journal_entries(id);
