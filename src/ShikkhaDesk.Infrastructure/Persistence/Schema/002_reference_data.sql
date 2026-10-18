-- =====================================================================================
-- ShikkhaDesk — reference data (version 002)
--
-- Seeds the starter chart of accounts, the default grade scale and default settings.
-- Accounts carry deterministic identifiers so the system account codes (see
-- ShikkhaDesk.Core.Services.SystemAccountCodes) can always be resolved, whichever
-- database is opened. Accounts are matched by code, never by name, so a user renaming
-- an account can never break the accounting engine.
-- =====================================================================================

-- ---------------------------------------------------------------- assets (1xxx)
INSERT OR IGNORE INTO accounts (id, code, name, type, parent_account_id, is_system, is_active, is_cash_equivalent, description, sort_order, created_at_utc) VALUES
 ('00000000-0000-4000-8000-000000001100','1100','হাতে নগদ (Cash in Hand)',0,NULL,1,1,1,'নগদ তহবিল নিয়ন্ত্রণ হিসাব',100,datetime('now')),
 ('00000000-0000-4000-8000-000000001200','1200','ব্যাংক হিসাব (Bank Accounts)',0,NULL,1,1,1,'ব্যাংক তহবিল নিয়ন্ত্রণ হিসাব',110,datetime('now')),
 ('00000000-0000-4000-8000-000000001300','1300','মোবাইল ব্যাংকিং (MFS)',0,NULL,1,1,1,'বিকাশ, নগদ, রকেট ইত্যাদি',120,datetime('now')),
 ('00000000-0000-4000-8000-000000001400','1400','শিক্ষার্থী বকেয়া (Student Receivables)',0,NULL,1,1,0,'ধার্যকৃত কিন্তু অপরিশোধিত ফি',130,datetime('now')),
 ('00000000-0000-4000-8000-000000001500','1500','অন্যান্য প্রাপ্য (Other Receivables)',0,NULL,0,1,0,'',140,datetime('now')),
 ('00000000-0000-4000-8000-000000001600','1600','স্থাবর সম্পদ (Equipment & Fixed Assets)',0,NULL,0,1,0,'আসবাব, কম্পিউটার ও অন্যান্য সরঞ্জাম',150,datetime('now'));

-- ---------------------------------------------------------------- liabilities (2xxx)
INSERT OR IGNORE INTO accounts (id, code, name, type, parent_account_id, is_system, is_active, is_cash_equivalent, description, sort_order, created_at_utc) VALUES
 ('00000000-0000-4000-8000-000000002100','2100','শিক্ষার্থী অগ্রিম ক্রেডিট (Student Advance Credit)',1,NULL,1,1,0,'অগ্রিম জমা কিন্তু অবরাদ্দকৃত অর্থ — দায়',200,datetime('now')),
 ('00000000-0000-4000-8000-000000002200','2200','প্রদেয় হিসাব (Accounts Payable)',1,NULL,1,1,0,'সরবরাহকারী বকেয়া',210,datetime('now')),
 ('00000000-0000-4000-8000-000000002300','2300','অপরিশোধিত বেতন (Salary Payable)',1,NULL,1,1,0,'',220,datetime('now')),
 ('00000000-0000-4000-8000-000000002400','2400','অন্যান্য দায় (Other Liabilities)',1,NULL,0,1,0,'',230,datetime('now'));

-- ---------------------------------------------------------------- equity (3xxx)
INSERT OR IGNORE INTO accounts (id, code, name, type, parent_account_id, is_system, is_active, is_cash_equivalent, description, sort_order, created_at_utc) VALUES
 ('00000000-0000-4000-8000-000000003100','3100','প্রারম্ভিক মূলধন (Opening Capital)',2,NULL,1,1,0,'প্রারম্ভিক স্থিতি সমন্বয়',300,datetime('now')),
 ('00000000-0000-4000-8000-000000003200','3200','সঞ্চিত আয় (Retained Earnings)',2,NULL,1,1,0,'',310,datetime('now'));

-- ---------------------------------------------------------------- income (4xxx)
INSERT OR IGNORE INTO accounts (id, code, name, type, parent_account_id, is_system, is_active, is_cash_equivalent, description, sort_order, created_at_utc) VALUES
 ('00000000-0000-4000-8000-000000004100','4100','টিউশন ফি আয় (Tuition Income)',3,NULL,1,1,0,'',400,datetime('now')),
 ('00000000-0000-4000-8000-000000004200','4200','ভর্তি ফি আয় (Admission Income)',3,NULL,1,1,0,'',410,datetime('now')),
 ('00000000-0000-4000-8000-000000004300','4300','কোর্স ফি আয় (Course Income)',3,NULL,1,1,0,'',420,datetime('now')),
 ('00000000-0000-4000-8000-000000004400','4400','পরীক্ষা ফি আয় (Exam Income)',3,NULL,1,1,0,'',430,datetime('now')),
 ('00000000-0000-4000-8000-000000004900','4900','অন্যান্য আয় (Other Income)',3,NULL,0,1,0,'',490,datetime('now'));

-- ---------------------------------------------------------------- expenses (5xxx)
INSERT OR IGNORE INTO accounts (id, code, name, type, parent_account_id, is_system, is_active, is_cash_equivalent, description, sort_order, created_at_utc) VALUES
 ('00000000-0000-4000-8000-000000005100','5100','শিক্ষক বেতন (Teacher Salaries)',4,NULL,1,1,0,'',500,datetime('now')),
 ('00000000-0000-4000-8000-000000005110','5110','কর্মচারী বেতন (Staff Salaries)',4,NULL,0,1,0,'',505,datetime('now')),
 ('00000000-0000-4000-8000-000000005200','5200','অফিস ভাড়া (Office Rent)',4,NULL,0,1,0,'',510,datetime('now')),
 ('00000000-0000-4000-8000-000000005300','5300','বিদ্যুৎ বিল (Electricity)',4,NULL,0,1,0,'',520,datetime('now')),
 ('00000000-0000-4000-8000-000000005400','5400','ইন্টারনেট (Internet)',4,NULL,0,1,0,'',530,datetime('now')),
 ('00000000-0000-4000-8000-000000005500','5500','স্টেশনারি (Stationery)',4,NULL,0,1,0,'',540,datetime('now')),
 ('00000000-0000-4000-8000-000000005600','5600','প্রচারণা (Marketing)',4,NULL,0,1,0,'',550,datetime('now')),
 ('00000000-0000-4000-8000-000000005700','5700','রক্ষণাবেক্ষণ (Maintenance)',4,NULL,0,1,0,'',560,datetime('now')),
 ('00000000-0000-4000-8000-000000005800','5800','সরঞ্জাম (Equipment)',4,NULL,0,1,0,'',570,datetime('now')),
 ('00000000-0000-4000-8000-000000005850','5850','পরিবহন (Transport)',4,NULL,0,1,0,'',575,datetime('now')),
 ('00000000-0000-4000-8000-000000005900','5900','ব্যাংক চার্জ (Bank Charges)',4,NULL,1,1,0,'',580,datetime('now')),
 ('00000000-0000-4000-8000-000000005990','5990','অন্যান্য ব্যয় (Other Operating Expenses)',4,NULL,0,1,0,'',590,datetime('now'));

-- ---------------------------------------------------------------- default grade scale
INSERT OR IGNORE INTO grade_scale (id, grade, minimum_percentage, grade_point, sort_order) VALUES
 ('00000000-0000-4000-8000-000000000001','A+',80,5.0,10),
 ('00000000-0000-4000-8000-000000000002','A', 70,4.0,20),
 ('00000000-0000-4000-8000-000000000003','A-',60,3.5,30),
 ('00000000-0000-4000-8000-000000000004','B', 50,3.0,40),
 ('00000000-0000-4000-8000-000000000005','C', 40,2.0,50),
 ('00000000-0000-4000-8000-000000000006','D', 33,1.0,60),
 ('00000000-0000-4000-8000-000000000007','F',  0,0.0,70);

-- ---------------------------------------------------------------- default settings
INSERT OR IGNORE INTO settings (key, value, updated_at_utc) VALUES
 ('receipt.prefix','RCP',datetime('now')),
 ('receipt.numberWidth','6',datetime('now')),
 ('receipt.includeYear','1',datetime('now')),
 ('receipt.resetPolicy','Yearly',datetime('now')),
 ('backup.automatic','1',datetime('now')),
 ('backup.retentionCount','30',datetime('now')),
 ('backup.location','',datetime('now')),
 ('app.hostMode','0',datetime('now')),
 ('app.firstRunCompleted','0',datetime('now')),
 ('lan.hostAddress','',datetime('now')),
 ('lan.port','5188',datetime('now')),
 ('academic.lateFeeEnabled','0',datetime('now')),
 ('academic.lateFeeAmount','0',datetime('now'));
