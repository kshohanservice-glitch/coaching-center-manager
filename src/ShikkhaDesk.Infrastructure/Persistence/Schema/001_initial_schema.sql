-- =====================================================================================
-- ShikkhaDesk — initial schema (version 001)
--
-- Conventions
--   * Identifiers are GUIDs stored as TEXT.
--   * All money is stored as INTEGER minor units (1 BDT = 100 poisa). Never REAL.
--   * Dates are TEXT in ISO-8601 (YYYY-MM-DD or YYYY-MM-DDTHH:MM:SS).
--   * Enumerations are INTEGER.
--   * Critical business invariants are enforced with UNIQUE constraints as well as in
--     the service layer, so a duplicate can never be created by a concurrent writer.
-- =====================================================================================

PRAGMA foreign_keys = ON;

-- ----------------------------------------------------------------- institution & settings
CREATE TABLE IF NOT EXISTS institutions (
    id                 TEXT PRIMARY KEY,
    name               TEXT NOT NULL,
    name_bn            TEXT,
    address            TEXT,
    phone              TEXT,
    email              TEXT,
    logo_path          TEXT,
    registration_number TEXT,
    currency_code      TEXT NOT NULL DEFAULT 'BDT',
    currency_symbol    TEXT NOT NULL DEFAULT '৳',
    digit_preference   INTEGER NOT NULL DEFAULT 0,
    created_at_utc     TEXT NOT NULL,
    updated_at_utc     TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS settings (
    key            TEXT PRIMARY KEY,
    value          TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);

-- ----------------------------------------------------------------- academic
CREATE TABLE IF NOT EXISTS academic_sessions (
    id           TEXT PRIMARY KEY,
    name         TEXT NOT NULL,
    start_date   TEXT NOT NULL,
    end_date     TEXT NOT NULL,
    is_active    INTEGER NOT NULL DEFAULT 0,
    created_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS class_grades (
    id             TEXT PRIMARY KEY,
    name           TEXT NOT NULL,
    sort_order     INTEGER NOT NULL DEFAULT 0,
    is_active      INTEGER NOT NULL DEFAULT 1,
    created_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS courses (
    id              TEXT PRIMARY KEY,
    name            TEXT NOT NULL,
    code            TEXT,
    class_grade_id  TEXT REFERENCES class_grades(id),
    duration_months INTEGER NOT NULL DEFAULT 0,
    description     TEXT,
    is_active       INTEGER NOT NULL DEFAULT 1,
    created_at_utc  TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS subjects (
    id             TEXT PRIMARY KEY,
    name           TEXT NOT NULL,
    code           TEXT,
    course_id      TEXT REFERENCES courses(id),
    is_active      INTEGER NOT NULL DEFAULT 1,
    created_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS batches (
    id             TEXT PRIMARY KEY,
    name           TEXT NOT NULL,
    session_id     TEXT NOT NULL REFERENCES academic_sessions(id),
    course_id      TEXT REFERENCES courses(id),
    class_grade_id TEXT REFERENCES class_grades(id),
    teacher_id     TEXT REFERENCES staff(id),
    start_date     TEXT NOT NULL,
    end_date       TEXT,
    capacity       INTEGER,
    schedule_note  TEXT,
    is_active      INTEGER NOT NULL DEFAULT 1,
    created_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS batch_schedules (
    id         TEXT PRIMARY KEY,
    batch_id   TEXT NOT NULL REFERENCES batches(id) ON DELETE CASCADE,
    day_of_week INTEGER NOT NULL,
    start_time TEXT NOT NULL,
    end_time   TEXT NOT NULL,
    room       TEXT
);

CREATE TABLE IF NOT EXISTS staff (
    id                     TEXT PRIMARY KEY,
    staff_code             TEXT NOT NULL UNIQUE,
    full_name              TEXT NOT NULL,
    designation            TEXT,
    phone                  TEXT,
    email                  TEXT,
    address                TEXT,
    joining_date           TEXT NOT NULL,
    leaving_date           TEXT,
    status                 INTEGER NOT NULL DEFAULT 0,
    basic_salary           INTEGER NOT NULL DEFAULT 0,
    house_rent_allowance   INTEGER NOT NULL DEFAULT 0,
    medical_allowance      INTEGER NOT NULL DEFAULT 0,
    transport_allowance    INTEGER NOT NULL DEFAULT 0,
    other_allowance        INTEGER NOT NULL DEFAULT 0,
    provident_fund_deduction INTEGER NOT NULL DEFAULT 0,
    tax_deduction          INTEGER NOT NULL DEFAULT 0,
    other_deduction        INTEGER NOT NULL DEFAULT 0,
    bank_account_name      TEXT,
    bank_account_number    TEXT,
    notes                  TEXT,
    created_at_utc         TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS staff_assignments (
    id          TEXT PRIMARY KEY,
    staff_id    TEXT NOT NULL REFERENCES staff(id) ON DELETE CASCADE,
    batch_id    TEXT REFERENCES batches(id),
    subject_id  TEXT REFERENCES subjects(id),
    role        TEXT,
    assigned_on TEXT NOT NULL,
    is_active   INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS students (
    id              TEXT PRIMARY KEY,
    student_code    TEXT NOT NULL UNIQUE,
    full_name       TEXT NOT NULL,
    guardian_name   TEXT,
    guardian_phone  TEXT,
    student_phone   TEXT,
    address         TEXT,
    date_of_birth   TEXT,
    gender          TEXT,
    school_or_college TEXT,
    school_class    TEXT,
    photo_path      TEXT,
    notes           TEXT,
    status          INTEGER NOT NULL DEFAULT 0,
    created_at_utc  TEXT NOT NULL,
    updated_at_utc  TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_students_name ON students(full_name);
CREATE INDEX IF NOT EXISTS ix_students_guardian_phone ON students(guardian_phone);
CREATE INDEX IF NOT EXISTS ix_students_status ON students(status);

CREATE TABLE IF NOT EXISTS admissions (
    id               TEXT PRIMARY KEY,
    admission_number TEXT NOT NULL UNIQUE,
    student_id       TEXT NOT NULL REFERENCES students(id),
    session_id       TEXT NOT NULL REFERENCES academic_sessions(id),
    admission_date   TEXT NOT NULL,
    course_id        TEXT REFERENCES courses(id),
    class_grade_id   TEXT REFERENCES class_grades(id),
    batch_id         TEXT REFERENCES batches(id),
    admission_fee    INTEGER NOT NULL DEFAULT 0,
    fee_plan_id      TEXT REFERENCES fee_plans(id),
    receipt_id       TEXT REFERENCES receipts(id),
    notes            TEXT,
    created_by_user_id TEXT NOT NULL,
    created_at_utc   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_admissions_date ON admissions(admission_date);

CREATE TABLE IF NOT EXISTS enrollments (
    id             TEXT PRIMARY KEY,
    student_id     TEXT NOT NULL REFERENCES students(id),
    batch_id       TEXT NOT NULL REFERENCES batches(id),
    session_id     TEXT NOT NULL REFERENCES academic_sessions(id),
    enrolled_on    TEXT NOT NULL,
    left_on        TEXT,
    status         INTEGER NOT NULL DEFAULT 0,
    fee_plan_id    TEXT REFERENCES fee_plans(id),
    notes          TEXT,
    created_at_utc TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_enrollments_student_batch
    ON enrollments(student_id, batch_id);
CREATE INDEX IF NOT EXISTS ix_enrollments_batch ON enrollments(batch_id);

CREATE TABLE IF NOT EXISTS attendance_records (
    id                     TEXT PRIMARY KEY,
    batch_id               TEXT NOT NULL REFERENCES batches(id),
    student_id             TEXT NOT NULL REFERENCES students(id),
    attendance_date        TEXT NOT NULL,
    status                 INTEGER NOT NULL DEFAULT 0,
    note                   TEXT,
    recorded_by_user_id    TEXT NOT NULL,
    recorded_at_utc        TEXT NOT NULL,
    corrected_from_record_id TEXT REFERENCES attendance_records(id)
);
-- One attendance record per student, per batch, per day. Enforced by the database too.
CREATE UNIQUE INDEX IF NOT EXISTS ux_attendance_student_batch_date
    ON attendance_records(batch_id, student_id, attendance_date);
CREATE INDEX IF NOT EXISTS ix_attendance_date ON attendance_records(attendance_date);

CREATE TABLE IF NOT EXISTS exams (
    id             TEXT PRIMARY KEY,
    name           TEXT NOT NULL,
    exam_type      TEXT,
    session_id     TEXT NOT NULL REFERENCES academic_sessions(id),
    exam_date      TEXT NOT NULL,
    batch_id       TEXT REFERENCES batches(id),
    status         INTEGER NOT NULL DEFAULT 0,
    created_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS exam_subjects (
    id            TEXT PRIMARY KEY,
    exam_id       TEXT NOT NULL REFERENCES exams(id) ON DELETE CASCADE,
    subject_id    TEXT REFERENCES subjects(id),
    subject_name  TEXT NOT NULL,
    maximum_marks REAL NOT NULL DEFAULT 100,
    pass_marks    REAL NOT NULL DEFAULT 33,
    exam_date     TEXT,
    sort_order    INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS marks (
    id                  TEXT PRIMARY KEY,
    exam_subject_id     TEXT NOT NULL REFERENCES exam_subjects(id) ON DELETE CASCADE,
    student_id          TEXT NOT NULL REFERENCES students(id),
    marks_obtained      REAL,
    is_absent           INTEGER NOT NULL DEFAULT 0,
    grade               TEXT,
    grade_point         REAL,
    remarks             TEXT,
    entered_by_user_id  TEXT NOT NULL,
    entered_at_utc      TEXT NOT NULL,
    corrected_from_mark_id TEXT REFERENCES marks(id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_marks_exam_subject_student
    ON marks(exam_subject_id, student_id);

CREATE TABLE IF NOT EXISTS grade_scale (
    id                 TEXT PRIMARY KEY,
    grade              TEXT NOT NULL,
    minimum_percentage REAL NOT NULL,
    grade_point        REAL NOT NULL,
    sort_order         INTEGER NOT NULL DEFAULT 0
);

-- ----------------------------------------------------------------- fees & payments
CREATE TABLE IF NOT EXISTS fee_plans (
    id             TEXT PRIMARY KEY,
    name           TEXT NOT NULL,
    course_id      TEXT REFERENCES courses(id),
    class_grade_id TEXT REFERENCES class_grades(id),
    batch_id       TEXT REFERENCES batches(id),
    effective_from TEXT NOT NULL,
    effective_to   TEXT,
    is_active      INTEGER NOT NULL DEFAULT 1,
    created_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS fee_plan_items (
    id                TEXT PRIMARY KEY,
    fee_plan_id       TEXT NOT NULL REFERENCES fee_plans(id) ON DELETE CASCADE,
    kind              INTEGER NOT NULL,
    description       TEXT NOT NULL,
    amount            INTEGER NOT NULL DEFAULT 0,
    recurrence_months INTEGER NOT NULL DEFAULT 1,
    installment_count INTEGER,
    sort_order        INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_fee_plan_items_plan ON fee_plan_items(fee_plan_id);

CREATE TABLE IF NOT EXISTS fee_assessments (
    id                 TEXT PRIMARY KEY,
    assessment_number  TEXT NOT NULL UNIQUE,
    period_key         TEXT,
    fee_plan_id        TEXT REFERENCES fee_plans(id),
    batch_id           TEXT REFERENCES batches(id),
    assessment_date    TEXT NOT NULL,
    journal_entry_id   TEXT REFERENCES journal_entries(id),
    description        TEXT,
    created_by_user_id TEXT NOT NULL,
    created_at_utc     TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS fee_charges (
    id                 TEXT PRIMARY KEY,
    student_id         TEXT NOT NULL REFERENCES students(id),
    batch_id           TEXT REFERENCES batches(id),
    enrollment_id      TEXT REFERENCES enrollments(id),
    assessment_id      TEXT REFERENCES fee_assessments(id),
    kind               INTEGER NOT NULL,
    description        TEXT NOT NULL,
    period_key         TEXT,
    charge_date        TEXT NOT NULL,
    due_date           TEXT NOT NULL,
    gross_amount       INTEGER NOT NULL DEFAULT 0,
    discount_amount    INTEGER NOT NULL DEFAULT 0,
    net_amount         INTEGER NOT NULL DEFAULT 0,
    paid_amount        INTEGER NOT NULL DEFAULT 0,
    waived_amount      INTEGER NOT NULL DEFAULT 0,
    is_void            INTEGER NOT NULL DEFAULT 0,
    voided_by_charge_id TEXT REFERENCES fee_charges(id),
    idempotency_key    TEXT NOT NULL,
    created_at_utc     TEXT NOT NULL
);
-- Bulk assessment idempotency: re-running the same assessment can never duplicate a charge.
CREATE UNIQUE INDEX IF NOT EXISTS ux_fee_charges_idempotency ON fee_charges(idempotency_key);
CREATE INDEX IF NOT EXISTS ix_fee_charges_student ON fee_charges(student_id);
CREATE INDEX IF NOT EXISTS ix_fee_charges_due ON fee_charges(due_date);
CREATE INDEX IF NOT EXISTS ix_fee_charges_period ON fee_charges(period_key);

CREATE TABLE IF NOT EXISTS payments (
    id                   TEXT PRIMARY KEY,
    student_id           TEXT NOT NULL REFERENCES students(id),
    receipt_number       TEXT NOT NULL UNIQUE,
    payment_date         TEXT NOT NULL,
    amount_received      INTEGER NOT NULL DEFAULT 0,
    amount_allocated     INTEGER NOT NULL DEFAULT 0,
    amount_refunded      INTEGER NOT NULL DEFAULT 0,
    method               INTEGER NOT NULL DEFAULT 0,
    financial_account_id TEXT REFERENCES financial_accounts(id),
    transaction_reference TEXT,
    notes                TEXT,
    status               INTEGER NOT NULL DEFAULT 0,
    collected_by_user_id TEXT NOT NULL,
    journal_entry_id     TEXT REFERENCES journal_entries(id),
    reversed_by_payment_id TEXT REFERENCES payments(id),
    idempotency_key      TEXT,
    created_at_utc       TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_payments_idempotency
    ON payments(student_id, idempotency_key) WHERE idempotency_key IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_payments_date ON payments(payment_date);
CREATE INDEX IF NOT EXISTS ix_payments_student ON payments(student_id);

CREATE TABLE IF NOT EXISTS payment_allocations (
    id                   TEXT PRIMARY KEY,
    payment_id           TEXT NOT NULL REFERENCES payments(id) ON DELETE CASCADE,
    fee_charge_id        TEXT NOT NULL REFERENCES fee_charges(id),
    amount               INTEGER NOT NULL DEFAULT 0,
    allocated_at_utc     TEXT NOT NULL,
    credit_transaction_id TEXT REFERENCES student_credit_transactions(id)
);
CREATE INDEX IF NOT EXISTS ix_allocations_payment ON payment_allocations(payment_id);
CREATE INDEX IF NOT EXISTS ix_allocations_charge ON payment_allocations(fee_charge_id);

CREATE TABLE IF NOT EXISTS student_credit_transactions (
    id                 TEXT PRIMARY KEY,
    student_id         TEXT NOT NULL REFERENCES students(id),
    kind               INTEGER NOT NULL,
    amount             INTEGER NOT NULL DEFAULT 0,
    transaction_date   TEXT NOT NULL,
    payment_id         TEXT REFERENCES payments(id),
    allocation_id      TEXT REFERENCES payment_allocations(id),
    refund_id          TEXT REFERENCES refunds(id),
    description        TEXT,
    journal_entry_id   TEXT REFERENCES journal_entries(id),
    created_at_utc     TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_credit_student ON student_credit_transactions(student_id);

CREATE TABLE IF NOT EXISTS refunds (
    id                   TEXT PRIMARY KEY,
    refund_number        TEXT NOT NULL UNIQUE,
    student_id           TEXT NOT NULL REFERENCES students(id),
    payment_id           TEXT REFERENCES payments(id),
    refund_date          TEXT NOT NULL,
    amount               INTEGER NOT NULL DEFAULT 0,
    method               INTEGER NOT NULL DEFAULT 0,
    financial_account_id TEXT REFERENCES financial_accounts(id),
    reason               TEXT NOT NULL,
    journal_entry_id     TEXT REFERENCES journal_entries(id),
    processed_by_user_id TEXT NOT NULL,
    created_at_utc       TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS receipt_sequences (
    id           TEXT PRIMARY KEY,
    scope        TEXT NOT NULL,
    prefix       TEXT NOT NULL,
    period_key   TEXT NOT NULL,
    last_number  INTEGER NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_receipt_sequences ON receipt_sequences(scope, prefix, period_key);

CREATE TABLE IF NOT EXISTS receipts (
    id              TEXT PRIMARY KEY,
    receipt_number  TEXT NOT NULL UNIQUE,
    document_kind   TEXT NOT NULL DEFAULT 'Fee',
    student_id      TEXT REFERENCES students(id),
    payment_id      TEXT REFERENCES payments(id),
    issued_at       TEXT NOT NULL,
    reprint_count   INTEGER NOT NULL DEFAULT 0,
    last_reprinted_at TEXT,
    payload_json    TEXT
);

-- ----------------------------------------------------------------- accounting
CREATE TABLE IF NOT EXISTS accounts (
    id                 TEXT PRIMARY KEY,
    code               TEXT NOT NULL UNIQUE,
    name               TEXT NOT NULL,
    type               INTEGER NOT NULL,
    parent_account_id  TEXT REFERENCES accounts(id),
    is_system          INTEGER NOT NULL DEFAULT 0,
    is_active          INTEGER NOT NULL DEFAULT 1,
    is_cash_equivalent INTEGER NOT NULL DEFAULT 0,
    description        TEXT,
    sort_order         INTEGER NOT NULL DEFAULT 0,
    created_at_utc     TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS financial_accounts (
    id              TEXT PRIMARY KEY,
    name            TEXT NOT NULL,
    kind            INTEGER NOT NULL DEFAULT 0,
    account_id      TEXT NOT NULL REFERENCES accounts(id),
    bank_name       TEXT,
    account_number  TEXT,
    branch          TEXT,
    opening_balance INTEGER NOT NULL DEFAULT 0,
    is_active       INTEGER NOT NULL DEFAULT 1,
    created_at_utc  TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS journal_entries (
    id                    TEXT PRIMARY KEY,
    journal_number        TEXT NOT NULL UNIQUE,
    entry_date            TEXT NOT NULL,
    description           TEXT NOT NULL,
    source_kind           INTEGER NOT NULL DEFAULT 0,
    source_id             TEXT,
    source_reference      TEXT,
    status                INTEGER NOT NULL DEFAULT 1,
    reversal_of_journal_id TEXT REFERENCES journal_entries(id),
    reversed_by_journal_id TEXT REFERENCES journal_entries(id),
    created_by_user_id    TEXT NOT NULL,
    created_at_utc        TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_journal_date ON journal_entries(entry_date);
CREATE INDEX IF NOT EXISTS ix_journal_source ON journal_entries(source_kind, source_id);

CREATE TABLE IF NOT EXISTS journal_lines (
    id                   TEXT PRIMARY KEY,
    journal_entry_id     TEXT NOT NULL REFERENCES journal_entries(id) ON DELETE CASCADE,
    account_id           TEXT NOT NULL REFERENCES accounts(id),
    debit                INTEGER NOT NULL DEFAULT 0,
    credit               INTEGER NOT NULL DEFAULT 0,
    financial_account_id TEXT REFERENCES financial_accounts(id),
    student_id           TEXT REFERENCES students(id),
    supplier_id          TEXT REFERENCES suppliers(id),
    staff_id             TEXT REFERENCES staff(id),
    memo                 TEXT,
    line_order           INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_journal_lines_entry ON journal_lines(journal_entry_id);
CREATE INDEX IF NOT EXISTS ix_journal_lines_account ON journal_lines(account_id);
CREATE INDEX IF NOT EXISTS ix_journal_lines_financial ON journal_lines(financial_account_id);

CREATE TABLE IF NOT EXISTS transfers (
    id                     TEXT PRIMARY KEY,
    transfer_number        TEXT NOT NULL UNIQUE,
    transfer_date          TEXT NOT NULL,
    from_financial_account_id TEXT NOT NULL REFERENCES financial_accounts(id),
    to_financial_account_id   TEXT NOT NULL REFERENCES financial_accounts(id),
    amount                 INTEGER NOT NULL DEFAULT 0,
    fee                    INTEGER NOT NULL DEFAULT 0,
    reference              TEXT,
    notes                  TEXT,
    journal_entry_id       TEXT REFERENCES journal_entries(id),
    created_by_user_id     TEXT NOT NULL,
    created_at_utc         TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS income_records (
    id                   TEXT PRIMARY KEY,
    income_number        TEXT NOT NULL UNIQUE,
    income_date          TEXT NOT NULL,
    income_account_id    TEXT NOT NULL REFERENCES accounts(id),
    financial_account_id TEXT NOT NULL REFERENCES financial_accounts(id),
    amount               INTEGER NOT NULL DEFAULT 0,
    description          TEXT NOT NULL,
    source               TEXT,
    reference            TEXT,
    attachment_path      TEXT,
    is_approved          INTEGER NOT NULL DEFAULT 1,
    journal_entry_id     TEXT REFERENCES journal_entries(id),
    created_by_user_id   TEXT NOT NULL,
    created_at_utc       TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_income_date ON income_records(income_date);

CREATE TABLE IF NOT EXISTS expense_records (
    id                   TEXT PRIMARY KEY,
    expense_number       TEXT NOT NULL UNIQUE,
    expense_date         TEXT NOT NULL,
    expense_account_id   TEXT NOT NULL REFERENCES accounts(id),
    amount               INTEGER NOT NULL DEFAULT 0,
    description          TEXT NOT NULL,
    supplier_id          TEXT REFERENCES suppliers(id),
    reference            TEXT,
    attachment_path      TEXT,
    is_approved          INTEGER NOT NULL DEFAULT 1,
    is_accrued           INTEGER NOT NULL DEFAULT 0,
    payable_id           TEXT REFERENCES payables(id),
    journal_entry_id     TEXT REFERENCES journal_entries(id),
    created_by_user_id   TEXT NOT NULL,
    created_at_utc       TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_expense_date ON expense_records(expense_date);

CREATE TABLE IF NOT EXISTS suppliers (
    id              TEXT PRIMARY KEY,
    code            TEXT NOT NULL UNIQUE,
    name            TEXT NOT NULL,
    contact_person  TEXT,
    phone           TEXT,
    email           TEXT,
    address         TEXT,
    notes           TEXT,
    opening_balance INTEGER NOT NULL DEFAULT 0,
    is_active       INTEGER NOT NULL DEFAULT 1,
    created_at_utc  TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS payables (
    id                     TEXT PRIMARY KEY,
    bill_number            TEXT NOT NULL UNIQUE,
    supplier_id            TEXT NOT NULL REFERENCES suppliers(id),
    bill_date              TEXT NOT NULL,
    due_date               TEXT,
    expense_account_id     TEXT REFERENCES accounts(id),
    amount                 INTEGER NOT NULL DEFAULT 0,
    paid_amount            INTEGER NOT NULL DEFAULT 0,
    status                 INTEGER NOT NULL DEFAULT 0,
    description            TEXT,
    accrual_journal_entry_id TEXT REFERENCES journal_entries(id),
    created_by_user_id     TEXT NOT NULL,
    created_at_utc         TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_payables_supplier ON payables(supplier_id);

CREATE TABLE IF NOT EXISTS payable_settlements (
    id                   TEXT PRIMARY KEY,
    settlement_number    TEXT NOT NULL UNIQUE,
    payable_id           TEXT NOT NULL REFERENCES payables(id) ON DELETE CASCADE,
    settlement_date      TEXT NOT NULL,
    amount               INTEGER NOT NULL DEFAULT 0,
    financial_account_id TEXT NOT NULL REFERENCES financial_accounts(id),
    method               INTEGER NOT NULL DEFAULT 0,
    reference            TEXT,
    journal_entry_id     TEXT REFERENCES journal_entries(id),
    created_by_user_id   TEXT NOT NULL,
    created_at_utc       TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_settlements_payable ON payable_settlements(payable_id);

CREATE TABLE IF NOT EXISTS payroll_periods (
    id              TEXT PRIMARY KEY,
    period_key      TEXT NOT NULL UNIQUE,
    name            TEXT NOT NULL,
    period_start    TEXT NOT NULL,
    period_end      TEXT NOT NULL,
    payment_date    TEXT,
    is_closed       INTEGER NOT NULL DEFAULT 0,
    created_at_utc  TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS payroll_records (
    id                      TEXT PRIMARY KEY,
    payroll_period_id       TEXT NOT NULL REFERENCES payroll_periods(id) ON DELETE CASCADE,
    staff_id                TEXT NOT NULL REFERENCES staff(id),
    basic_salary            INTEGER NOT NULL DEFAULT 0,
    allowances              INTEGER NOT NULL DEFAULT 0,
    deductions              INTEGER NOT NULL DEFAULT 0,
    net_salary              INTEGER NOT NULL DEFAULT 0,
    status                  INTEGER NOT NULL DEFAULT 0,
    accrual_journal_entry_id TEXT REFERENCES journal_entries(id),
    payment_journal_entry_id TEXT REFERENCES journal_entries(id),
    financial_account_id    TEXT REFERENCES financial_accounts(id),
    payment_date            TEXT,
    payment_method          INTEGER,
    notes                   TEXT,
    created_by_user_id      TEXT NOT NULL,
    created_at_utc          TEXT NOT NULL
);
-- One payroll record per employee per period: prevents duplicate salary posting.
CREATE UNIQUE INDEX IF NOT EXISTS ux_payroll_period_staff
    ON payroll_records(payroll_period_id, staff_id);

-- ----------------------------------------------------------------- security & system
CREATE TABLE IF NOT EXISTS users (
    id                  TEXT PRIMARY KEY,
    user_name           TEXT NOT NULL UNIQUE,
    full_name           TEXT NOT NULL,
    password_hash       TEXT NOT NULL,
    password_salt       TEXT NOT NULL,
    role_id             TEXT REFERENCES roles(id),
    is_active           INTEGER NOT NULL DEFAULT 1,
    must_change_password INTEGER NOT NULL DEFAULT 0,
    last_login_at_utc   TEXT,
    failed_login_attempts INTEGER NOT NULL DEFAULT 0,
    locked_until_utc    TEXT,
    created_at_utc      TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS roles (
    id             TEXT PRIMARY KEY,
    name           TEXT NOT NULL UNIQUE,
    description    TEXT,
    is_system      INTEGER NOT NULL DEFAULT 0,
    created_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS permissions (
    id          TEXT PRIMARY KEY,
    code        TEXT NOT NULL UNIQUE,
    description TEXT,
    group_name  TEXT
);

CREATE TABLE IF NOT EXISTS role_permissions (
    id            TEXT PRIMARY KEY,
    role_id       TEXT NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    permission_id TEXT NOT NULL REFERENCES permissions(id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_role_permissions ON role_permissions(role_id, permission_id);

CREATE TABLE IF NOT EXISTS audit_logs (
    id              TEXT PRIMARY KEY,
    occurred_at_utc TEXT NOT NULL,
    user_id         TEXT,
    user_name       TEXT,
    action          TEXT NOT NULL,
    entity_type     TEXT NOT NULL,
    entity_id       TEXT,
    summary         TEXT,
    before_json     TEXT,
    after_json      TEXT,
    source_device   TEXT
);
CREATE INDEX IF NOT EXISTS ix_audit_time ON audit_logs(occurred_at_utc);
CREATE INDEX IF NOT EXISTS ix_audit_entity ON audit_logs(entity_type, entity_id);

CREATE TABLE IF NOT EXISTS backup_records (
    id                TEXT PRIMARY KEY,
    started_at_utc    TEXT NOT NULL,
    finished_at_utc   TEXT,
    file_path         TEXT NOT NULL,
    file_size_bytes   INTEGER NOT NULL DEFAULT 0,
    status            INTEGER NOT NULL DEFAULT 2,
    error_message     TEXT,
    is_automatic      INTEGER NOT NULL DEFAULT 0,
    integrity_hash    TEXT,
    integrity_verified INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_backup_started ON backup_records(started_at_utc);

CREATE TABLE IF NOT EXISTS licenses (
    id                  TEXT PRIMARY KEY,
    license_id          TEXT NOT NULL,
    institution_id      TEXT NOT NULL,
    product_code        TEXT NOT NULL,
    license_type        TEXT NOT NULL,
    device_limit        INTEGER NOT NULL DEFAULT 5,
    version_entitlement TEXT,
    issued_to           TEXT,
    issued_at_utc       TEXT NOT NULL,
    expires_at_utc      TEXT,
    signature           TEXT,
    public_key_id       TEXT,
    activated_at_utc    TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS licensed_devices (
    id                 TEXT PRIMARY KEY,
    device_id          TEXT NOT NULL UNIQUE,
    machine_name       TEXT NOT NULL,
    windows_user_name  TEXT,
    notes              TEXT,
    registered_at_utc  TEXT NOT NULL,
    last_seen_at_utc   TEXT NOT NULL,
    status             INTEGER NOT NULL DEFAULT 0,
    replaced_by_device_id TEXT
);

CREATE TABLE IF NOT EXISTS migration_history (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    version        TEXT NOT NULL UNIQUE,
    description    TEXT NOT NULL,
    applied_at_utc TEXT NOT NULL,
    checksum       TEXT NOT NULL
);
