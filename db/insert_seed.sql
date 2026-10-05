/* =============================================================================
   Uno TP - the settings, the console's features and every list, from start.
   Run once on a new database, after create_tables.sql:

       sqlcmd -d UnoTP -i db/create_tables.sql
       sqlcmd -d UnoTP -i db/insert_seed.sql

   Three tables are filled, with one INSERT each:

     t_Unotp_App_Config    the settings: limits, ages, days and prefixes
     t_Unotp_Feature_Mst   the console's features, in the order the dashboard lays them out
     t_Unotp_Ref_List      every list the pages offer

   t_Unotp_Ref_List is the one master for the small lists. A row is one entry of a
   list: f_Code is what is posted and saved, f_Name what is shown, f_Seq the order,
   f_Parent the entry it belongs under, f_Attrs the entry's own settings as JSON,
   and f_Active 0 takes it off the pages. To change a list, change its rows:
   nothing about a list is written in the code. The README ("The lists") says what
   each list is for and which settings it takes.

   Not in t_Unotp_Ref_List, because each is a table of its own, looked up where
   it is (UnoTP.Data/MasterQueries.cs): investor folios, brokers, staff, bank
   branches, PIN codes, the rate card (t_FD_BOTC_SCHEME), the Axis CMS locations
   and the payment gateway's banks.

   Review with the business before go-live. The codes of 'maritalStatuses',
   'nomineeRelations', 'employeeRelations', 'occupations', 'subOccupations',
   'subOccupationCkyc', 'sourcesOfFunds', 'documentTypes' and 'documentSubTypes'
   are saved into the FD system's tables, and the codes of 'categories' and
   'payouts' are matched against its rate card: each must be the FD system's own,
   letter for letter. The rows here are the development database's; replace them
   with the FD system's full lists.

   The script is three INSERTs and nothing else, for empty tables. To load from start
   again, empty the three tables first:

       DELETE dbo.t_Unotp_Ref_List; DELETE dbo.t_Unotp_Feature_Mst; DELETE dbo.t_Unotp_App_Config;
   ============================================================================= */

/* ----- The settings ----------------------------------------------------------- */
INSERT dbo.t_Unotp_App_Config (f_Key, f_Value, f_Description, f_Active, f_Created_By) VALUES
    (N'sourcingAgency', N'1033', N'The agency type that sources as the house; any other sources as a broker', 1, 'SEED'),
    (N'minAge', N'18', N'Youngest a holder may be', 1, 'SEED'),
    (N'seniorAge', N'60', N'Age from which a senior citizen category applies', 1, 'SEED'),
    (N'maxJointHolders', N'2', N'Joint holders beside the investor', 1, 'SEED'),
    (N'maxAttempts', N'3', N'Refused uploads in a row before a document locks', 1, 'SEED'),
    (N'minAmount', N'5000', N'Smallest deposit, rupees', 1, 'SEED'),
    (N'maxAmount', N'50000000', N'Largest deposit, rupees', 1, 'SEED'),
    (N'amountStep', N'1', N'A deposit is a multiple of this, rupees; 1 for any amount', 1, 'SEED'),
    (N'overMaxAmountMessage', N'For investment above Rs.5 Cr, please write to fixeddeposit@mahindrafinance.com', N'What the amount field says over maxAmount; blank for "Above the maximum"', 1, 'SEED'),
    (N'quoteAmount', N'50000', N'The amount FD Configuration quotes the rate at before one is entered', 1, 'SEED'),
    (N'cancellationDays', N'14', N'Days an unpaid application stands before it cancels itself', 1, 'SEED'),
    (N'closeToCancelDays', N'3', N'Days before cancelling that the dashboard flags an application', 1, 'SEED'),
    (N'draftDays', N'14', N'Days since its last save an unsubmitted application stays in Continue an incomplete application', 1, 'SEED'),
    (N'linkValidityHours.payment', N'72', N'Hours a payment link stays open, from when it is sent or sent again', 1, 'SEED'),
    (N'linkValidityHours.acceptance', N'72', N'Hours an acceptance link stays open', 1, 'SEED'),
    (N'linkResends', N'1', N'Times a payment link may be sent again after submit', 1, 'SEED'),
    (N'renewFromDays', N'61', N'Days before maturity renewal entry opens', 1, 'SEED'),
    (N'renewUntilDays', N'7', N'Days before maturity renewal entry closes', 1, 'SEED'),
    (N'renewUntilDaysAutoRenewal', N'10', N'The same, for a deposit tagged for auto renewal', 1, 'SEED'),
    (N'slipNoPrefix', N'AXPIS', N'Pay-in slip numbers: {prefix}{running number}', 1, 'SEED'),
    (N'defaultRateCategory', N'PUBLIC', N'The category (a code of the ''categories'' list) whose rates a deposit takes when its own category has none', 1, 'SEED'),
    (N'compoundingPerYear', N'1', N'Times a year a cumulative deposit compounds; the months after the last whole period earn simple interest', 1, 'SEED'),
    (N'sourceOfFundsFrom', N'10000000', N'The source of funds is asked once the investor''s active deposits, with the new one, pass this many rupees...', 1, 'SEED'),
    (N'sourceOfFundsOccupations', N'Housewife; Student; Retired', N'...and their occupation is one of these (names of the ''occupations'' list, a semicolon between)...', 1, 'SEED'),
    (N'sourceOfFundsIncomeBands', N'Upto Rs.5,00,000', N'...or their annual income band is one of these (names of the ''incomeBands'' list, a semicolon between)', 1, 'SEED'),
    (N'sourceOfFundsOther', N'11', N'The source of funds (a code of the ''sourcesOfFunds'' list) that takes a typed remark', 1, 'SEED');

/* ----- The console's features --------------------------------------------------
   f_Group is the dashboard section a feature's tile sits in; f_Off_Reason is what
   the tile says while the feature is switched off; f_Tile 0 gives it no tile.
   ----------------------------------------------------------------------------- */
INSERT dbo.t_Unotp_Feature_Mst (f_Feature_Key, f_Name, f_Group, f_Detail, f_Off_Reason, f_Seq, f_Tile, f_Active, f_Created_By) VALUES
    (N'new-fd', N'Create New FD', N'Apply for a new FD', N'The booking wizard end to end, from investor search to submission.', N'Unavailable', 1, 1, 1, 'SEED'),
    (N'pis', N'PIS Generation - Axis', N'Apply for a new FD', N'Making and reprinting Axis pay-in slips. A slip already printed stays valid.', N'Unavailable', 2, 1, 1, 'SEED'),
    (N'view-app', N'View existing application', N'Apply for a new FD', N'Looking an application up by number, folio or date.', N'Unavailable', 3, 1, 1, 'SEED'),
    (N'short-url', N'Short URL', N'Apply for a new FD', N'The payment and acceptance links sent to investors. A link already sent stops opening.', N'Unavailable', 4, 1, 1, 'SEED'),
    (N'app-status', N'Application status', N'FD Services', N'Where an application stands, holder by holder.', N'Under revamp', 5, 1, 1, 'SEED'),
    (N'fd-calc', N'FD Calculator', N'FD Services', N'Working out what a deposit earns before it is booked.', N'Under development', 6, 1, 1, 'SEED'),
    (N'renew', N'Renew FD', N'FD Services', N'Rolling a maturing deposit over into a new one.', N'Coming soon', 7, 1, 1, 'SEED'),
    (N'admin', N'Console Admin', N'Administration', N'Scheduling downtime windows and notices for the console.', N'Unavailable', 8, 0, 1, 'SEED');

/* ----- The lists ---------------------------------------------------------------
   One INSERT for every list. The columns:

     f_List     the list the row is an entry of
     f_Seq      its place in the list
     f_Code     what is saved
     f_Name     what is shown
     f_Parent   the code of the entry this one belongs under, in another list; NULL
                where it belongs under none. This is how one drop-down depends on
                another: a sub occupation's parent is its occupation, and a row of
                'sourcingModeCategories' has the sourcing mode as its parent
     f_Attrs    the entry's own settings, as JSON; NULL where it has none. The
                settings each list takes are listed in the README ("The lists")
     f_Active   1 the entry is offered, 0 it is taken off the pages
   ----------------------------------------------------------------------------- */
INSERT dbo.t_Unotp_Ref_List (f_List, f_Seq, f_Code, f_Name, f_Parent, f_Attrs, f_Active, f_Created_By) VALUES
    -- applicationTypes: how an application is signed
    (N'applicationTypes', 1, N'DIGITAL', N'Digital', NULL, NULL, 1, 'SEED'),
    (N'applicationTypes', 2, N'PHYSICAL', N'Physical', NULL, NULL, 1, 'SEED'),

    -- categories: the deposit categories. The code and the name are the rate card's CATEGORY: the rates are read by it, it is what is saved,
    -- and it is what is shown. extraRate is what the category earns over the public rate, for the page's wording
    (N'categories', 1, N'PUBLIC', N'PUBLIC', NULL, N'{"employee":false,"women":false,"senior":false}', 1, 'SEED'),
    (N'categories', 2, N'GENERAL-WOMEN', N'GENERAL-WOMEN', NULL, N'{"employee":false,"women":true,"senior":false}', 1, 'SEED'),
    (N'categories', 3, N'SR CITIZEN', N'SR CITIZEN', NULL, N'{"employee":false,"women":false,"senior":true}', 1, 'SEED'),
    (N'categories', 4, N'SR CITIZEN-WOMEN', N'SR CITIZEN-WOMEN', NULL, N'{"employee":false,"women":true,"senior":true}', 1, 'SEED'),
    (N'categories', 5, N'EMPLOYEE', N'EMPLOYEE', NULL, N'{"employee":true,"women":false,"senior":false}', 1, 'SEED'),
    (N'categories', 6, N'EMPLOYEE-WOMEN', N'EMPLOYEE-WOMEN', NULL, N'{"employee":true,"women":true,"senior":false}', 1, 'SEED'),

    -- sourcingModes: how an application is sourced, and how its two code fields behave
    (N'sourcingModes', 1, N'2', N'BROKER', NULL, N'{"codeLabel":"Broker Code","nameLabel":"Broker Name","house":"","search":"source","register":"brokers","sub":"free"}', 1, 'SEED'),
    (N'sourcingModes', 2, N'1', N'MMFSS - BRANCH', NULL, N'{"codeLabel":"Sourcing Employee Code","nameLabel":"Sourcing Employee Name","house":"MFL","search":"","register":"","sub":"house","staff":"branch"}', 1, 'SEED'),
    (N'sourcingModes', 3, N'5', N'MFL-EX', NULL, N'{"codeLabel":"Sourcing Employee Code","nameLabel":"Sourcing Employee Name","house":"MFL-EX","search":"sub","register":"employees","sub":"employee","staff":"mflEx"}', 1, 'SEED'),
    (N'sourcingModes', 4, N'6', N'MFIS/FD', NULL, N'{"codeLabel":"Sourcing Employee Code","nameLabel":"Sourcing Employee Name","house":"MIBS","search":"sub","register":"employees","sub":"employeeShut","staff":"mfis"}', 1, 'SEED'),

    -- sourcingModeCategories: the categories a deposit under a sourcing mode may be booked as. The parent is the sourcing mode's code, the code a category's.
    -- (Which staff a mode takes is its 'staff' setting above: branch, mfis or mflEx, each a query in UnoTP.Data/MasterQueries.cs.)
    (N'sourcingModeCategories', 1, N'PUBLIC', N'PUBLIC', N'2', NULL, 1, 'SEED'),
    (N'sourcingModeCategories', 2, N'GENERAL-WOMEN', N'GENERAL-WOMEN', N'2', NULL, 1, 'SEED'),
    (N'sourcingModeCategories', 3, N'SR CITIZEN', N'SR CITIZEN', N'2', NULL, 1, 'SEED'),
    (N'sourcingModeCategories', 4, N'SR CITIZEN-WOMEN', N'SR CITIZEN-WOMEN', N'2', NULL, 1, 'SEED'),
    (N'sourcingModeCategories', 5, N'PUBLIC', N'PUBLIC', N'1', NULL, 1, 'SEED'),
    (N'sourcingModeCategories', 6, N'GENERAL-WOMEN', N'GENERAL-WOMEN', N'1', NULL, 1, 'SEED'),
    (N'sourcingModeCategories', 7, N'SR CITIZEN', N'SR CITIZEN', N'1', NULL, 1, 'SEED'),
    (N'sourcingModeCategories', 8, N'SR CITIZEN-WOMEN', N'SR CITIZEN-WOMEN', N'1', NULL, 1, 'SEED'),
    (N'sourcingModeCategories', 9, N'EMPLOYEE', N'EMPLOYEE', N'5', NULL, 1, 'SEED'),
    (N'sourcingModeCategories', 10, N'EMPLOYEE-WOMEN', N'EMPLOYEE-WOMEN', N'5', NULL, 1, 'SEED'),
    (N'sourcingModeCategories', 11, N'PUBLIC', N'PUBLIC', N'6', NULL, 1, 'SEED'),
    (N'sourcingModeCategories', 12, N'GENERAL-WOMEN', N'GENERAL-WOMEN', N'6', NULL, 1, 'SEED'),
    (N'sourcingModeCategories', 13, N'SR CITIZEN', N'SR CITIZEN', N'6', NULL, 1, 'SEED'),
    (N'sourcingModeCategories', 14, N'SR CITIZEN-WOMEN', N'SR CITIZEN-WOMEN', N'6', NULL, 1, 'SEED'),

    -- paymentModes: how a deposit is paid; "document" is the instrument a copy is filed for
    (N'paymentModes', 1, N'Online', N'Online', NULL, N'{"document":null}', 1, 'SEED'),
    (N'paymentModes', 2, N'RTGS', N'RTGS', NULL, N'{"document":null}', 1, 'SEED'),
    (N'paymentModes', 3, N'Cheque', N'Cheque', NULL, N'{"document":"cheque"}', 1, 'SEED'),

    -- proofsOfAddress: the proofs of address taken, who confirms each, and whether it carries a photograph
    (N'proofsOfAddress', 1, N'Aadhaar', N'Aadhaar', NULL, N'{"issuer":"UIDAI","hasPhoto":true}', 1, 'SEED'),
    (N'proofsOfAddress', 2, N'Passport', N'Passport', NULL, N'{"issuer":"Passport Seva","hasPhoto":true}', 1, 'SEED'),
    (N'proofsOfAddress', 3, N'Driving Licence', N'Driving Licence', NULL, N'{"issuer":"Sarathi","hasPhoto":true}', 1, 'SEED'),
    (N'proofsOfAddress', 4, N'Voter ID', N'Voter ID', NULL, N'{"issuer":"the Election Commission","hasPhoto":true}', 1, 'SEED'),

    -- employeeHolders: which holder is the employee. The first is the primary holder
    (N'employeeHolders', 1, N'First holder', N'First holder', NULL, NULL, 1, 'SEED'),
    (N'employeeHolders', 2, N'Second holder', N'Second holder', NULL, NULL, 1, 'SEED'),
    (N'employeeHolders', 3, N'Third holder', N'Third holder', NULL, NULL, 1, 'SEED'),

    -- employeeRelations: what the employee is to the primary holder. The code is what is saved. The first is the employee themselves
    (N'employeeRelations', 1, N'SEL', N'SELF', NULL, NULL, 1, 'SEED'),
    (N'employeeRelations', 2, N'BRO', N'BROTHER', NULL, NULL, 1, 'SEED'),
    (N'employeeRelations', 3, N'DAU', N'DAUGHTER', NULL, NULL, 1, 'SEED'),
    (N'employeeRelations', 4, N'FAT', N'FATHER', NULL, NULL, 1, 'SEED'),
    (N'employeeRelations', 5, N'HUS', N'HUSBAND', NULL, NULL, 1, 'SEED'),
    (N'employeeRelations', 6, N'MOT', N'MOTHER', NULL, NULL, 1, 'SEED'),
    (N'employeeRelations', 7, N'SIS', N'SISTER', NULL, NULL, 1, 'SEED'),
    (N'employeeRelations', 8, N'SON', N'SON', NULL, NULL, 1, 'SEED'),
    (N'employeeRelations', 9, N'WIF', N'WIFE', NULL, NULL, 1, 'SEED'),

    -- employeeProofs: the proofs of employment taken
    (N'employeeProofs', 1, N'Employee ID card', N'Employee ID card', NULL, NULL, 1, 'SEED'),
    (N'employeeProofs', 2, N'Appointment letter', N'Appointment letter', NULL, NULL, 1, 'SEED'),
    (N'employeeProofs', 3, N'Latest salary slip', N'Latest salary slip', NULL, NULL, 1, 'SEED'),

    -- documentTypes: the types a document can be. The code is what is saved
    (N'documentTypes', 1, N'ADDITIONAL', N'Additional Documents', NULL, NULL, 1, 'SEED'),
    (N'documentTypes', 2, N'ADDPRF', N'Address Proof', NULL, NULL, 1, 'SEED'),
    (N'documentTypes', 3, N'APPFRM', N'Application Form', NULL, NULL, 1, 'SEED'),
    (N'documentTypes', 4, N'BANK', N'BANK', NULL, NULL, 1, 'SEED'),
    (N'documentTypes', 5, N'EMPPRF', N'Employee Proof', NULL, NULL, 1, 'SEED'),
    (N'documentTypes', 6, N'IDENTPRF', N'Identity Proof', NULL, NULL, 1, 'SEED'),
    (N'documentTypes', 7, N'TAX_DOCS', N'Income Tax Related Documents', NULL, NULL, 1, 'SEED'),
    (N'documentTypes', 8, N'Photograph', N'Photograph', NULL, NULL, 1, 'SEED'),
    (N'documentTypes', 9, N'Signature', N'Signature', NULL, NULL, 1, 'SEED'),
    (N'documentTypes', 10, N'Video', N'Video', NULL, NULL, 1, 'SEED'),

    -- documentSubTypes: the sub-types under each document type. The code is what is saved; the parent is the document type's code
    (N'documentSubTypes', 1, N'INTRNL_1', N'Internal 1', N'ADDITIONAL', NULL, 1, 'SEED'),
    (N'documentSubTypes', 2, N'INTRNL_2', N'Internal 2', N'ADDITIONAL', NULL, 1, 'SEED'),
    (N'documentSubTypes', 3, N'AADHARCARD_A', N'Aadhar card', N'ADDPRF', NULL, 1, 'SEED'),
    (N'documentSubTypes', 4, N'AADHARCARD_XML', N'Aadhar card', N'ADDPRF', NULL, 1, 'SEED'),
    (N'documentSubTypes', 5, N'DRVLIC_A', N'Driving License', N'ADDPRF', NULL, 1, 'SEED'),
    (N'documentSubTypes', 6, N'PASSPORT_A', N'Passport', N'ADDPRF', NULL, 1, 'SEED'),
    (N'documentSubTypes', 7, N'VOTERID_A', N'Voter ID', N'ADDPRF', NULL, 1, 'SEED'),
    (N'documentSubTypes', 8, N'APPLICATIONFRM', N'Application Form', N'APPFRM', NULL, 1, 'SEED'),
    (N'documentSubTypes', 9, N'CANCEL_CHEQUE', N'Cancel Cheque', N'BANK', NULL, 1, 'SEED'),
    (N'documentSubTypes', 10, N'PAYMENT_CHEQUE', N'Payment Cheque', N'BANK', NULL, 1, 'SEED'),
    (N'documentSubTypes', 11, N'EMP_ID', N'Employee ID Card', N'EMPPRF', NULL, 1, 'SEED'),
    (N'documentSubTypes', 12, N'Retirement_letter', N'Retirement letter or any other proof', N'EMPPRF', NULL, 1, 'SEED'),
    (N'documentSubTypes', 13, N'EMP_PAY_SLIP_OR_HR_LETTER', N'Salary Slip or HR Letter', N'EMPPRF', NULL, 1, 'SEED'),
    (N'documentSubTypes', 14, N'PAN', N'PAN', N'IDENTPRF', NULL, 1, 'SEED'),
    (N'documentSubTypes', 15, N'FORM121', N'Form 121', N'TAX_DOCS', NULL, 1, 'SEED'),
    (N'documentSubTypes', 16, N'Photograph', N'Photo', N'Photograph', NULL, 1, 'SEED'),
    (N'documentSubTypes', 17, N'Signature', N'Signature', N'Signature', NULL, 1, 'SEED'),
    (N'documentSubTypes', 18, N'Video', N'Video', N'Video', NULL, 1, 'SEED'),

    -- filedDocuments: each document the app files, and the sub-type it is filed under. The code is the document as the app knows it
    -- (a proof of address is poa: and its proof type; an employee proof is empproof: and its proof); the parent is the sub-type's code
    (N'filedDocuments', 1, N'pan', N'PAN copy', N'PAN', NULL, 1, 'SEED'),
    (N'filedDocuments', 2, N'photo', N'Photograph', N'Photograph', NULL, 1, 'SEED'),
    (N'filedDocuments', 3, N'poa:Aadhaar', N'Proof of address - Aadhaar', N'AADHARCARD_A', NULL, 1, 'SEED'),
    (N'filedDocuments', 4, N'poa:Passport', N'Proof of address - Passport', N'PASSPORT_A', NULL, 1, 'SEED'),
    (N'filedDocuments', 5, N'poa:Driving Licence', N'Proof of address - Driving Licence', N'DRVLIC_A', NULL, 1, 'SEED'),
    (N'filedDocuments', 6, N'poa:Voter ID', N'Proof of address - Voter ID', N'VOTERID_A', NULL, 1, 'SEED'),
    (N'filedDocuments', 7, N'form', N'Application form', N'APPLICATIONFRM', NULL, 1, 'SEED'),
    (N'filedDocuments', 8, N'payment', N'Payment instrument copy', N'PAYMENT_CHEQUE', NULL, 1, 'SEED'),
    (N'filedDocuments', 9, N'empproof:Employee ID card', N'Employee proof - Employee ID card', N'EMP_ID', NULL, 1, 'SEED'),
    (N'filedDocuments', 10, N'empproof:Appointment letter', N'Employee proof - Appointment letter', N'EMP_PAY_SLIP_OR_HR_LETTER', NULL, 1, 'SEED'),
    (N'filedDocuments', 11, N'empproof:Latest salary slip', N'Employee proof - Latest salary slip', N'EMP_PAY_SLIP_OR_HR_LETTER', NULL, 1, 'SEED'),
    (N'filedDocuments', 12, N'tdsform', N'Form 121', N'FORM121', NULL, 1, 'SEED'),

    -- genders: the genders offered
    (N'genders', 1, N'Male', N'Male', NULL, NULL, 1, 'SEED'),
    (N'genders', 2, N'Female', N'Female', NULL, NULL, 1, 'SEED'),
    (N'genders', 3, N'Transgender', N'Transgender', NULL, NULL, 1, 'SEED'),

    -- maritalStatuses: the marital statuses offered. The code is what is saved
    (N'maritalStatuses', 1, N'01', N'Married', NULL, NULL, 1, 'SEED'),
    (N'maritalStatuses', 2, N'02', N'Unmarried', NULL, NULL, 1, 'SEED'),
    (N'maritalStatuses', 3, N'03', N'Others', NULL, NULL, 1, 'SEED'),

    -- nameTypes: whose name is given beside the holder's: father, mother or spouse
    (N'nameTypes', 1, N'Father', N'Father', NULL, NULL, 1, 'SEED'),
    (N'nameTypes', 2, N'Mother', N'Mother', NULL, NULL, 1, 'SEED'),
    (N'nameTypes', 3, N'Spouse', N'Spouse', NULL, NULL, 1, 'SEED'),

    -- occupations: the occupations offered. The code is what is saved
    (N'occupations', 1, N'1', N'Salaried', NULL, NULL, 1, 'SEED'),
    (N'occupations', 2, N'2', N'Self Employed', NULL, NULL, 1, 'SEED'),
    (N'occupations', 3, N'4', N'Agriculture', NULL, NULL, 1, 'SEED'),
    (N'occupations', 4, N'7', N'Pensioner', NULL, NULL, 1, 'SEED'),
    (N'occupations', 5, N'8', N'Retired', NULL, NULL, 1, 'SEED'),
    (N'occupations', 6, N'9', N'Housewife', NULL, NULL, 1, 'SEED'),
    (N'occupations', 7, N'10', N'Student', NULL, NULL, 1, 'SEED'),

    -- subOccupations: the sub occupations. The parent is the code of the occupation it is offered under
    (N'subOccupations', 1, N'1', N'Central Govt', N'1', NULL, 1, 'SEED'),
    (N'subOccupations', 2, N'2', N'State Govt', N'1', NULL, 1, 'SEED'),
    (N'subOccupations', 3, N'3', N'Corporate/Private', N'1', NULL, 1, 'SEED'),
    (N'subOccupations', 4, N'12', N'Doctor', N'2', NULL, 1, 'SEED'),
    (N'subOccupations', 5, N'19', N'Trader', N'2', NULL, 1, 'SEED'),
    (N'subOccupations', 6, N'42', N'Dairy', N'4', NULL, 1, 'SEED'),
    (N'subOccupations', 7, N'65', N'Pensioner', N'7', NULL, 1, 'SEED'),
    (N'subOccupations', 8, N'66', N'Retired', N'8', NULL, 1, 'SEED'),
    (N'subOccupations', 9, N'67', N'Housewife', N'9', NULL, 1, 'SEED'),
    (N'subOccupations', 10, N'68', N'Student', N'10', NULL, 1, 'SEED'),

    -- subOccupationCkyc: the CKYC occupation saved with a sub occupation: its code and name. The parent is the sub occupation's code
    (N'subOccupationCkyc', 1, N'PS', N'Public Sector', N'1', NULL, 1, 'SEED'),
    (N'subOccupationCkyc', 2, N'GS', N'Government Service', N'2', NULL, 1, 'SEED'),
    (N'subOccupationCkyc', 3, N'PV', N'Private Sector', N'3', NULL, 1, 'SEED'),
    (N'subOccupationCkyc', 4, N'3', N'Professional', N'12', NULL, 1, 'SEED'),
    (N'subOccupationCkyc', 5, N'SE', N'Self Employed', N'19', NULL, 1, 'SEED'),
    (N'subOccupationCkyc', 6, N'4', N'Agriculture', N'42', NULL, 1, 'SEED'),
    (N'subOccupationCkyc', 7, N'5', N'Retired', N'65', NULL, 1, 'SEED'),
    (N'subOccupationCkyc', 8, N'5', N'Retired', N'66', NULL, 1, 'SEED'),
    (N'subOccupationCkyc', 9, N'6', N'Housewife', N'67', NULL, 1, 'SEED'),
    (N'subOccupationCkyc', 10, N'7', N'Student', N'68', NULL, 1, 'SEED'),

    -- incomeBands: the annual income bands offered
    (N'incomeBands', 1, N'Upto Rs.5,00,000', N'Upto Rs.5,00,000', NULL, NULL, 1, 'SEED'),
    (N'incomeBands', 2, N'Rs.5,00,000 - Rs.10,00,000', N'Rs.5,00,000 - Rs.10,00,000', NULL, NULL, 1, 'SEED'),
    (N'incomeBands', 3, N'Rs.10,00,000 - Rs.25,00,000', N'Rs.10,00,000 - Rs.25,00,000', NULL, NULL, 1, 'SEED'),
    (N'incomeBands', 4, N'Above Rs.25,00,000', N'Above Rs.25,00,000', NULL, NULL, 1, 'SEED'),

    -- nomineeRelations: what a nominee is to the primary holder. The code is what is saved
    (N'nomineeRelations', 1, N'BRO', N'BROTHER', NULL, NULL, 1, 'SEED'),
    (N'nomineeRelations', 2, N'DAU', N'DAUGHTER', NULL, NULL, 1, 'SEED'),
    (N'nomineeRelations', 3, N'FAT', N'FATHER', NULL, NULL, 1, 'SEED'),
    (N'nomineeRelations', 4, N'GRD', N'GRAND DAUGHTER', NULL, NULL, 1, 'SEED'),
    (N'nomineeRelations', 5, N'GRM', N'GRAND MOTHER', NULL, NULL, 1, 'SEED'),
    (N'nomineeRelations', 6, N'GRS', N'GRAND SON', NULL, NULL, 1, 'SEED'),
    (N'nomineeRelations', 7, N'HUS', N'HUSBAND', NULL, NULL, 1, 'SEED'),
    (N'nomineeRelations', 8, N'MOT', N'MOTHER', NULL, NULL, 1, 'SEED'),
    (N'nomineeRelations', 9, N'NEP', N'NEPHEW', NULL, NULL, 1, 'SEED'),
    (N'nomineeRelations', 10, N'NEI', N'NIECE', NULL, NULL, 1, 'SEED'),
    (N'nomineeRelations', 11, N'OTH', N'OTHERS', NULL, NULL, 1, 'SEED'),
    (N'nomineeRelations', 12, N'SIS', N'SISTER', NULL, NULL, 1, 'SEED'),
    (N'nomineeRelations', 13, N'SON', N'SON', NULL, NULL, 1, 'SEED'),
    (N'nomineeRelations', 14, N'WIF', N'WIFE', NULL, NULL, 1, 'SEED'),

    -- tenures: the tenures offered, months
    (N'tenures', 1, N'12', N'12', NULL, NULL, 1, 'SEED'),
    (N'tenures', 2, N'18', N'18', NULL, NULL, 1, 'SEED'),
    (N'tenures', 3, N'24', N'24', NULL, NULL, 1, 'SEED'),
    (N'tenures', 4, N'30', N'30', NULL, NULL, 1, 'SEED'),
    (N'tenures', 5, N'36', N'36', NULL, NULL, 1, 'SEED'),
    (N'tenures', 6, N'42', N'42', NULL, NULL, 1, 'SEED'),
    (N'tenures', 7, N'48', N'48', NULL, NULL, 1, 'SEED'),
    (N'tenures', 8, N'60', N'60', NULL, NULL, 1, 'SEED'),

    -- payouts: how often a deposit pays interest. The code is the rate card's INTEREST_FREQ: the rates are read by it and it is what is saved.
    -- "scheme" is the rate card's SCHEME the payout's rows are under; perYear 0 is on maturity
    (N'payouts', 1, N'COMP. ANNUALY', N'On maturity', NULL, N'{"perYear":0,"each":"","scheme":"CUMULATIVE"}', 1, 'SEED'),
    (N'payouts', 2, N'YEARLY', N'Yearly', NULL, N'{"perYear":1,"each":"year","scheme":"NON-CUMULATIVE"}', 1, 'SEED'),
    (N'payouts', 3, N'HALF YEARLY', N'Half yearly', NULL, N'{"perYear":2,"each":"half year","scheme":"NON-CUMULATIVE"}', 1, 'SEED'),
    (N'payouts', 4, N'QUARTERLY', N'Quarterly', NULL, N'{"perYear":4,"each":"quarter","scheme":"NON-CUMULATIVE"}', 1, 'SEED'),
    (N'payouts', 5, N'MONTHLY', N'Monthly', NULL, N'{"perYear":12,"each":"month","scheme":"NON-CUMULATIVE"}', 1, 'SEED'),

    -- renewInstructions: what a deposit under auto renewal renews
    (N'renewInstructions', 1, N'principal', N'Principal only', NULL, NULL, 1, 'SEED'),
    (N'renewInstructions', 2, N'principal-interest', N'Principal and interest', NULL, NULL, 1, 'SEED'),

    -- deliveryTypes: how the deposit receipt is sent
    (N'deliveryTypes', 1, N'ereceipt', N'E-receipt', NULL, NULL, 1, 'SEED'),
    (N'deliveryTypes', 2, N'physical', N'Physical', NULL, NULL, 1, 'SEED'),

    -- sourcesOfFunds: the sources of funds offered. The code is what is saved
    (N'sourcesOfFunds', 1, N'01', N'Proceeds from sale of immovable property', NULL, NULL, 1, 'SEED'),
    (N'sourcesOfFunds', 2, N'05', N'Personal savings from salary / business income', NULL, NULL, 1, 'SEED'),
    (N'sourcesOfFunds', 3, N'09', N'Inheritance / will proceeds', NULL, NULL, 1, 'SEED'),
    (N'sourcesOfFunds', 4, N'11', N'Others (please specify clearly)', NULL, NULL, 1, 'SEED'),

    -- requiredDocuments: the documents each kind of investor hands over, with the notes that go with them
    (N'requiredDocuments', 1, N'Individual KYC', N'Individual KYC', NULL, N'{"items":["Passport","Driving license","Permanent Account Number (PAN) card","‘Election Commission of India’ Voter identity card","‘NREGA’ Job card, duly signed by an officer of the State Government","‘Unique Identification Authority of India’ letter containing details of name, address and Aadhaar Number, or any document as notified by the Central Government in consultation with the regulator"],"notes":["The PAN copy is collected first, before any proof of address.","An Aadhaar is accepted masked or unmasked, when the name and date of birth on it match the PAN. Where its 12-digit number cannot be read, it is typed for the PAN–Aadhaar link."]}', 1, 'SEED'),
    (N'requiredDocuments', 2, N'Sole Proprietorship', N'Sole Proprietorship', NULL, N'{"items":["ID & address proof of the proprietor with self attestation","PAN card of proprietor with self attestation","Proprietor Address proof with self attestation","Photograph","If the sole proprietorship is in a different name, the bank statement or registration certificate","GST & Udyam Certificate or Trade License required","Cancelled cheque leaf for bank account verification"],"notes":[]}', 1, 'SEED'),
    (N'requiredDocuments', 3, N'NRI - Individual KYC', N'NRI - Individual KYC', NULL, N'{"items":["Passport with valid visa/ OCI","Overseas employment letter (optional for confirmation of residential status and overseas address)","PIO cards","PAN card","Local proof of address, if different from the passport address","Bank account statement or passbook","Local Property papers with registration deed","EB Bill card","Voter ID or driving license","Tax Residency Certificate from the Income Tax department of the country of which the investor is a resident","Copy of the passport as of the beginning of the current financial year and end of the financial year"],"notes":[]}', 1, 'SEED'),
    (N'requiredDocuments', 4, N'HUF Deposits', N'HUF Deposits', NULL, N'{"items":["HUF Pan copy with self attestation","Latest HUF address proof required","Valid address proof & identity proof of Karta","Karta Photograph","Cancelled cheque leaf for bank account verification","HUF declaration required"],"notes":[]}', 1, 'SEED'),
    (N'requiredDocuments', 5, N'Companies', N'Companies', NULL, N'{"items":["PAN card","Address proof (Valid GST Certificate/bank statement/telephone bill)","Certificate of incorporation","Memorandum of article & association with latest board resolution and specimen signatures","Authorised signatory list","Photograph of the signatories","ID & address proof of authorised signatories","Cancelled cheque leaf for bank account verification","FATCA Declaration","Beneficial Owner form along with BO Owners self attested KYC documents"],"notes":[]}', 1, 'SEED'),
    (N'requiredDocuments', 6, N'Partnership Firms', N'Partnership Firms', NULL, N'{"items":["PAN card","Address proof (bank statement, telephone bill)","Firm Registration certificate","Partnership deed","Resolution copy","ID & address proof of all authorised signatories","Photograph of the signatories","Cancelled cheque leaf for bank account verification","FATCA Declaration","Beneficial Owner form along with BO Owners self attested KYC documents"],"notes":[]}', 1, 'SEED'),
    (N'requiredDocuments', 7, N'Trust and Foundations', N'Trust and Foundations', NULL, N'{"items":["PAN card","Address proof (Valid GST Certificate/bank statement/telephone bill)","Non Profit Organisation(Yes/No)","Darpan Registration mandatory for NPO''s","Registration certificate of the Trust/Charitable/Family & Foundation","Memorandum/Deed of the Trust/Charitable/Family & Foundation with latest board resolution and specimen signatures","Authorised signatory list","ID & address proof of authorised signatories with self attestation","Photograph of the Trustees & signatories","Cancelled cheque leaf for bank account verification","FATCA Declaration","Beneficial Owner form along with BO Owners self attested KYC documents"],"notes":[]}', 1, 'SEED'),
    (N'requiredDocuments', 8, N'Charitable Trust', N'Charitable Trust', NULL, N'{"items":["PAN card","Address proof (Valid GST Certificate/bank statement/telephone bill)","Non Profit Organisation(Yes/No)","Darpan Registration mandatory for NPO''s","Registration certificate of the Trust/Charitable/Family & Foundation","Memorandum/Deed of the Trust/Charitable/Family & Foundation with latest board resolution and specimen signatures","Authorised signatory list","ID & address proof of authorised signatories with self attestation","Photograph of the Trustees & signatories","Cancelled cheque leaf for bank account verification","FATCA Declaration","Beneficial Owner form along with BO Owners self attested KYC documents"],"notes":[]}', 1, 'SEED'),
    (N'requiredDocuments', 9, N'Family Trust', N'Family Trust', NULL, N'{"items":["PAN card","Address proof (Valid GST Certificate/bank statement/telephone bill)","Registration certificate of the Trust/Charitable/Family & Foundation","Memorandum/Deed of the Trust/Charitable/Family & Foundation with latest board resolution and specimen signatures","Authorised signatory list","ID & address proof of authorised signatories with self attestation","Photograph of the Trustees & signatories","Cancelled cheque leaf for bank account verification","FATCA Declaration","Beneficial Owner form along with BO Owners self attested KYC documents"],"notes":[]}', 1, 'SEED'),
    (N'requiredDocuments', 10, N'Club, Association, Society', N'Club, Association, Society', NULL, N'{"items":["Copy of the Registration Certificate, if registered.","Acknowledgment of registration application, if applied for.","PAN card copy","Address proof","List of signatories","MOA & Resolution","Individual KYC of all signatories","Photograph of the Trustees & signatories","Cancelled cheque leaf for bank account verification","FATCA Declaration","Beneficial Owner form along with BO Owners self attested KYC documents"],"notes":[]}', 1, 'SEED'),

    -- identificationNotes: the notes on Investor Identification
    (N'identificationNotes', 1, N'1', N'Only individual depositors 18 years and above are allowed to make investments.', NULL, NULL, 1, 'SEED'),
    (N'identificationNotes', 2, N'2', N'We strongly advice the depositor(s) to avail the nomination', NULL, NULL, 1, 'SEED'),
    (N'identificationNotes', 3, N'3', N'For investment above Rs.5 Cr, please write to fixeddeposit@mahindrafinance.com', NULL, NULL, 1, 'SEED'),

    -- dashboardNotes: the notes on the dashboard
    (N'dashboardNotes', 1, N'1', N'Currently Esarathi is enabled with individual and sole proprietorship investment only.', NULL, NULL, 1, 'SEED'),
    (N'dashboardNotes', 2, N'2', N'Verification of the Fixed Deposit is subject to validation of the submitted documents by the Operations team.', NULL, NULL, 1, 'SEED'),

    -- renewalNotes: the notes on Renew FD. {renewFromDays} and the like are filled in from t_Unotp_App_Config
    (N'renewalNotes', 1, N'1', N'Deposits due for renewal only will be displayed in this module.', NULL, NULL, 1, 'SEED'),
    (N'renewalNotes', 2, N'2', N'Changes to the depositors (second holder, third holder) or any other information shall be made as per the details mentioned in the Renewal Application/FDR.', NULL, NULL, 1, 'SEED'),
    (N'renewalNotes', 3, N'3', N'Please keep the renewal documents ready to upload before proceeding.', NULL, NULL, 1, 'SEED'),
    (N'renewalNotes', 4, N'4', N'Details appearing in Renewal Application/FDR will be considered to renew the Deposits.', NULL, NULL, 1, 'SEED'),
    (N'renewalNotes', 5, N'5', N'You will be able to make renewal entry in this module only from {renewFromDays} days upto {renewUntilDays} days prior to maturity.', NULL, NULL, 1, 'SEED'),
    (N'renewalNotes', 6, N'7', N'Any renewal of Deposit in advance of its maturity date, will be subject to the rate of interest and other terms & conditions prevailing on the date of said maturity.', NULL, NULL, 1, 'SEED'),
    (N'renewalNotes', 7, N'8', N'The Auto renewal tag cases will be able to make renewal entry in this module only from {renewFromDays} days up to {renewUntilDaysAutoRenewal} days prior to maturity.', NULL, NULL, 1, 'SEED'),

    -- noticeKinds: the kinds of notice Console Admin can post
    (N'noticeKinds', 1, N'Rate change', N'Rate change', NULL, NULL, 1, 'SEED'),
    (N'noticeKinds', 2, N'Maintenance', N'Maintenance', NULL, NULL, 1, 'SEED');
