-- Adds the institution profile fields shown on receipts and reports.
-- Existing installations keep every value already stored; new columns get the documented default.
ALTER TABLE institutions ADD COLUMN receipt_prefix TEXT NOT NULL DEFAULT 'SD';
ALTER TABLE institutions ADD COLUMN established_on TEXT NULL;
