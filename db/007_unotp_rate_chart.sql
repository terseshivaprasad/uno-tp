/* =============================================================================
   Uno TP - the rate card as the published chart lays it out, and the Samruddhi
   chart of 3 August 2026.

   Before this script t_Unotp_Rate_Card held one rate per category and tenure. It
   now holds one row per line of the chart: category, tenure, scheme, payout
   frequency, rate, the smallest and largest deposit the line is offered for, the
   holder's gender and the application type (see db/004). Rows from before, with
   no payout on them, are taken out of use.

   The chart: public rates by tenure for the cumulative scheme and for monthly,
   quarterly, half-yearly and yearly payouts; a cumulative deposit from Rs 5,000,
   half-yearly and yearly from Rs 25,000, monthly and quarterly from Rs 50,000, all
   up to Rs 5 crore. Senior citizens and employees earn 0.35 over the public rate,
   women 0.05; the same rates for a purchase and a renewal.

   Safe to run again: each change is made only when it is not there yet.
   ============================================================================= */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

/* ----- The columns ------------------------------------------------------------- */
IF COL_LENGTH(N'dbo.t_Unotp_Rate_Card', N'c_Scheme') IS NULL
    ALTER TABLE dbo.t_Unotp_Rate_Card ADD c_Scheme VARCHAR(20) NOT NULL CONSTRAINT DF_Rate_Card_Scheme DEFAULT ('');
IF COL_LENGTH(N'dbo.t_Unotp_Rate_Card', N'c_Payout') IS NULL
    ALTER TABLE dbo.t_Unotp_Rate_Card ADD c_Payout VARCHAR(20) NOT NULL CONSTRAINT DF_Rate_Card_Payout DEFAULT ('');
IF COL_LENGTH(N'dbo.t_Unotp_Rate_Card', N'n_Min_Amount') IS NULL
    ALTER TABLE dbo.t_Unotp_Rate_Card ADD n_Min_Amount BIGINT NOT NULL CONSTRAINT DF_Rate_Card_Min_Amount DEFAULT (0);
IF COL_LENGTH(N'dbo.t_Unotp_Rate_Card', N'n_Max_Amount') IS NULL
    ALTER TABLE dbo.t_Unotp_Rate_Card ADD n_Max_Amount BIGINT NULL;
IF COL_LENGTH(N'dbo.t_Unotp_Rate_Card', N'c_Gender') IS NULL
    ALTER TABLE dbo.t_Unotp_Rate_Card ADD c_Gender VARCHAR(1) NOT NULL CONSTRAINT DF_Rate_Card_Gender DEFAULT ('');
IF COL_LENGTH(N'dbo.t_Unotp_Rate_Card', N'c_App_Type') IS NULL
    ALTER TABLE dbo.t_Unotp_Rate_Card ADD c_App_Type VARCHAR(10) NOT NULL CONSTRAINT DF_Rate_Card_App_Type DEFAULT ('');
GO

/* ----- The unique key, now over the whole line ------------------------------------ */
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_Rate_Card' AND parent_object_id = OBJECT_ID(N'dbo.t_Unotp_Rate_Card'))
    ALTER TABLE dbo.t_Unotp_Rate_Card DROP CONSTRAINT UQ_Rate_Card;
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_Rate_Card_Line' AND parent_object_id = OBJECT_ID(N'dbo.t_Unotp_Rate_Card'))
    ALTER TABLE dbo.t_Unotp_Rate_Card ADD CONSTRAINT UQ_Rate_Card_Line
        UNIQUE (c_Category, n_Tenure_Months, c_Payout, n_Min_Amount, c_Gender, c_App_Type, d_Effective_From);
GO

/* ----- Rows from before this layout: no payout on them, so out of use ------------- */
UPDATE dbo.t_Unotp_Rate_Card SET f_Active = 0 WHERE c_Payout = '' AND f_Active = 1;
GO

/* ----- Settings: the standing quote amount, and yearly compounding as the chart works it */
INSERT dbo.t_Unotp_App_Config (c_Key, c_Value, c_Description, c_Created_By)
SELECT N'quoteAmount', N'50000', N'The amount FD Configuration quotes the rate at before one is entered', 'SEED'
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_App_Config c WHERE c.c_Key = N'quoteAmount');

UPDATE dbo.t_Unotp_App_Config
SET c_Value = N'1', c_Description = N'Times a year a cumulative deposit compounds; the months after the last whole period earn simple interest'
WHERE c_Key = N'compoundingPerYear' AND c_Value <> N'1';
GO

/* ----- The Samruddhi chart, w.e.f. 3 August 2026 ----------------------------------
   One row per category, tenure and payout: the public rate of the line plus what
   the category earns over it. Women's categories are for gender F; the rest for
   either. Every row is for a purchase and a renewal alike (blank application type).
   ----------------------------------------------------------------------------- */
INSERT dbo.t_Unotp_Rate_Card
    (c_Category, n_Tenure_Months, c_Scheme, c_Payout, n_Rate, n_Min_Amount, n_Max_Amount, c_Gender, c_App_Type, d_Effective_From, c_Created_By)
SELECT cat.code, line.tenure, pay.scheme, pay.payout, line.rate + cat.extra, pay.min_amount, 50000000, cat.gender, '', '2026-08-03', 'CHART'
FROM (VALUES
    ('PUBLIC/GENERAL',   0.00, ''),
    ('WOMEN',            0.05, 'F'),
    ('SR CITIZEN',       0.35, ''),
    ('SR CITIZEN WOMEN', 0.40, 'F'),
    ('EMPLOYEE',         0.35, ''),
    ('EMPLOYEE WOMEN',   0.40, 'F')
) cat (code, extra, gender)
CROSS JOIN (VALUES
    -- tenure, payout, public rate
    (12, 'maturity', 6.60), (12, 'monthly', 6.40), (12, 'quarterly', 6.45), (12, 'halfyearly', 6.50), (12, 'yearly', 6.60),
    (18, 'maturity', 6.60), (18, 'monthly', 6.40), (18, 'quarterly', 6.45), (18, 'halfyearly', 6.50), (18, 'yearly', 6.60),
    (24, 'maturity', 6.85), (24, 'monthly', 6.65), (24, 'quarterly', 6.70), (24, 'halfyearly', 6.75), (24, 'yearly', 6.85),
    (30, 'maturity', 6.85), (30, 'monthly', 6.65), (30, 'quarterly', 6.70), (30, 'halfyearly', 6.75), (30, 'yearly', 6.85),
    (36, 'maturity', 7.40), (36, 'monthly', 7.15), (36, 'quarterly', 7.20), (36, 'halfyearly', 7.25), (36, 'yearly', 7.40),
    (42, 'maturity', 7.40), (42, 'monthly', 7.15), (42, 'quarterly', 7.20), (42, 'halfyearly', 7.25), (42, 'yearly', 7.40),
    (48, 'maturity', 7.45), (48, 'monthly', 7.20), (48, 'quarterly', 7.25), (48, 'halfyearly', 7.30), (48, 'yearly', 7.45),
    (60, 'maturity', 7.45), (60, 'monthly', 7.20), (60, 'quarterly', 7.25), (60, 'halfyearly', 7.30), (60, 'yearly', 7.45)
) line (tenure, payout, rate)
JOIN (VALUES
    -- payout, scheme, smallest deposit
    ('maturity',   'CUMULATIVE',     5000),
    ('monthly',    'NON-CUMULATIVE', 50000),
    ('quarterly',  'NON-CUMULATIVE', 50000),
    ('halfyearly', 'NON-CUMULATIVE', 25000),
    ('yearly',     'NON-CUMULATIVE', 25000)
) pay (payout, scheme, min_amount) ON pay.payout = line.payout
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.t_Unotp_Rate_Card x
    WHERE x.c_Category = cat.code AND x.n_Tenure_Months = line.tenure AND x.c_Payout = line.payout
      AND x.n_Min_Amount = pay.min_amount AND x.c_Gender = cat.gender AND x.c_App_Type = '' AND x.d_Effective_From = '2026-08-03');
GO
