/* =============================================================================
   Uno TP - the platform's own values: config, the console's features and every
   reference list. Run after create_new_tables.sql.

   Taken over from the lists and wording the old site used. Review with the
   business before go-live; change them here or in the tables, never in code.
   An entry already in a table is left as it is, so running this again never
   overwrites a value that was changed since.

   Order matters in two lists: the first of 'employeeHolders' is the primary
   holder, and the first of 'employeeRelations' is Self - an employee who is the
   primary holder is Self, and no other holder can be.

   Not seeded: t_Unotp_Ref_List 'cmsLocations' (the Axis CMS locations cheques are
   presented at) must be loaded from Axis's own list before go-live, and the
   partners and their menus come from the portal's user master.
   ============================================================================= */
SET NOCOUNT ON;
GO

INSERT dbo.t_Unotp_App_Config (c_Key, c_Value, c_Description, c_Created_By)
SELECT v.k, v.v, v.d, 'SEED' FROM (VALUES
    (N'sourcingAgency', N'1033', N'The agency type that sources as the house; any other sources as a broker'),
    (N'minAge', N'18', N'Youngest a holder may be'),
    (N'seniorAge', N'60', N'Age from which a senior citizen category applies'),
    (N'maxJointHolders', N'2', N'Joint holders beside the investor'),
    (N'maxAttempts', N'3', N'Refused uploads in a row before a document locks'),
    (N'minAmount', N'5000', N'Smallest deposit, rupees'),
    (N'maxAmount', N'20000000', N'Largest deposit, rupees'),
    (N'amountStep', N'1000', N'A deposit is a multiple of this, rupees'),
    (N'cancellationDays', N'14', N'Days an unpaid application stands before it cancels itself'),
    (N'closeToCancelDays', N'3', N'Days before cancelling that the dashboard flags an application'),
    (N'draftDays', N'14', N'Days since its last save an unsubmitted application stays in Continue an incomplete application'),
    (N'linkValidityHours.payment', N'48', N'Hours a payment link stays open'),
    (N'linkValidityHours.acceptance', N'72', N'Hours an acceptance link stays open'),
    (N'linkResends', N'1', N'Times a payment link may be sent again after submit'),
    (N'renewFromDays', N'61', N'Days before maturity renewal entry opens'),
    (N'renewUntilDays', N'7', N'Days before maturity renewal entry closes'),
    (N'renewUntilDaysAutoRenewal', N'10', N'The same, for a deposit tagged for auto renewal'),
    (N'sessionHours', N'8', N'Hours a session lasts after entry'),
    (N'sysCode', N'UNOTP', N'The system code the portal enters Uno TP with'),
    (N'appNoPrefix', N'FBBMFL', N'Application numbers: {prefix}{yy}F{running number}'),
    (N'slipNoPrefix', N'AXPIS', N'Pay-in slip numbers: {prefix}{running number}'),
    (N'defaultRateCategory', N'PUBLIC/GENERAL', N'The rate card category a deposit takes when its own has no rate'),
    (N'compoundingPerYear', N'1', N'Times a year a cumulative deposit compounds; the months after the last whole period earn simple interest'),
    (N'quoteAmount', N'50000', N'The amount FD Configuration quotes the rate at before one is entered')
) v (k, v, d)
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_App_Config c WHERE c.c_Key = v.k);
GO

INSERT dbo.t_Unotp_Feature_Mst (c_Feature_Key, c_Name, c_Group, c_Detail, c_Off_Reason, n_Seq, f_Tile, c_Created_By)
SELECT v.k, v.n, v.g, v.d, v.o, v.s, v.t, 'SEED' FROM (VALUES
    (N'new-fd', N'Create New FD', N'Apply for a new FD', N'The booking wizard end to end, from investor search to submission.', N'Unavailable', 1, 1),
    (N'pis', N'PIS Generation - Axis', N'Apply for a new FD', N'Making and reprinting Axis pay-in slips. A slip already printed stays valid.', N'Unavailable', 2, 1),
    (N'view-app', N'View existing application', N'Apply for a new FD', N'Looking an application up by number, folio or date.', N'Unavailable', 3, 1),
    (N'short-url', N'Short URL', N'Apply for a new FD', N'The payment and acceptance links sent to investors. A link already sent stops opening.', N'Unavailable', 4, 1),
    (N'app-status', N'Application status', N'FD Services', N'Where an application stands, holder by holder.', N'Under revamp', 5, 1),
    (N'renew', N'Renew FD', N'FD Services', N'Rolling a maturing deposit over into a new one.', N'Coming soon', 6, 1),
    (N'admin', N'Console Admin', N'Administration', N'Scheduling downtime windows and notices for the console.', N'Unavailable', 7, 0)
) v (k, n, g, d, o, s, t)
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_Feature_Mst f WHERE f.c_Feature_Key = v.k);
GO

INSERT dbo.t_Unotp_Ref_List (c_List, n_Seq, c_Code, c_Name, j_Attrs, c_Created_By)
SELECT v.l, v.s, v.c, v.n, v.a, 'SEED' FROM (VALUES
    (N'applicationTypes', 1, N'DIGITAL', N'Digital', NULL),
    (N'applicationTypes', 2, N'PHYSICAL', N'Physical', NULL),
    (N'categories', 1, N'PUBLIC/GENERAL', N'Public / General', N'{"employee":false,"women":false,"senior":false}'),
    (N'categories', 2, N'WOMEN', N'Women', N'{"employee":false,"women":true,"senior":false}'),
    (N'categories', 3, N'SR CITIZEN', N'Senior citizen', N'{"employee":false,"women":false,"senior":true}'),
    (N'categories', 4, N'SR CITIZEN WOMEN', N'Senior citizen women', N'{"employee":false,"women":true,"senior":true}'),
    (N'categories', 5, N'EMPLOYEE', N'Employee', N'{"employee":true,"women":false,"senior":false}'),
    (N'categories', 6, N'EMPLOYEE WOMEN', N'Employee women', N'{"employee":true,"women":true,"senior":false}'),
    (N'paymentModes', 1, N'Online', N'Online', N'{"document":null}'),
    (N'paymentModes', 2, N'RTGS', N'RTGS', N'{"document":null}'),
    (N'paymentModes', 3, N'Cheque', N'Cheque', N'{"document":"cheque"}'),
    (N'sourcingModes', 1, N'2', N'BROKER', N'{"codeLabel":"Broker Code","nameLabel":"Broker Name","house":"","search":"source","register":"brokers","sub":"free","categories":["PUBLIC/GENERAL","WOMEN","SR CITIZEN","SR CITIZEN WOMEN"]}'),
    (N'sourcingModes', 2, N'1', N'MMFSS - BRANCH', N'{"codeLabel":"Sourcing Employee Code","nameLabel":"Sourcing Employee Name","house":"MFL","search":"","register":"","sub":"house","categories":["PUBLIC/GENERAL","WOMEN","SR CITIZEN","SR CITIZEN WOMEN"]}'),
    (N'sourcingModes', 3, N'5', N'MFL-EX', N'{"codeLabel":"Sourcing Employee Code","nameLabel":"Sourcing Employee Name","house":"MFL-EX","search":"sub","register":"employees","sub":"employee","categories":["EMPLOYEE","EMPLOYEE WOMEN"]}'),
    (N'sourcingModes', 4, N'6', N'MFIS/FD', N'{"codeLabel":"Sourcing Employee Code","nameLabel":"Sourcing Employee Name","house":"MIBS","search":"sub","register":"employees","sub":"employeeShut","categories":["PUBLIC/GENERAL","WOMEN","SR CITIZEN","SR CITIZEN WOMEN"]}'),
    (N'proofsOfAddress', 1, N'Aadhaar', N'Aadhaar', N'{"issuer":"UIDAI","hasPhoto":true}'),
    (N'proofsOfAddress', 2, N'Passport', N'Passport', N'{"issuer":"Passport Seva","hasPhoto":true}'),
    (N'proofsOfAddress', 3, N'Driving Licence', N'Driving Licence', N'{"issuer":"Sarathi","hasPhoto":true}'),
    (N'proofsOfAddress', 4, N'Voter ID', N'Voter ID', N'{"issuer":"the Election Commission","hasPhoto":true}'),
    (N'employeeHolders', 1, N'First holder', N'First holder', NULL),
    (N'employeeHolders', 2, N'Second holder', N'Second holder', NULL),
    (N'employeeHolders', 3, N'Third holder', N'Third holder', NULL),
    (N'employeeRelations', 1, N'Self', N'Self', NULL),
    (N'employeeRelations', 2, N'Spouse', N'Spouse', NULL),
    (N'employeeRelations', 3, N'Parent', N'Parent', NULL),
    (N'employeeRelations', 4, N'Child', N'Child', NULL),
    (N'employeeRelations', 5, N'Sibling', N'Sibling', NULL),
    (N'employeeProofs', 1, N'Employee ID card', N'Employee ID card', NULL),
    (N'employeeProofs', 2, N'Appointment letter', N'Appointment letter', NULL),
    (N'employeeProofs', 3, N'Latest salary slip', N'Latest salary slip', NULL),
    (N'incomeBands', 1, N'Upto Rs.5,00,000', N'Upto Rs.5,00,000', NULL),
    (N'incomeBands', 2, N'Rs.5,00,000 - Rs.10,00,000', N'Rs.5,00,000 - Rs.10,00,000', NULL),
    (N'incomeBands', 3, N'Rs.10,00,000 - Rs.25,00,000', N'Rs.10,00,000 - Rs.25,00,000', NULL),
    (N'incomeBands', 4, N'Above Rs.25,00,000', N'Above Rs.25,00,000', NULL),
    (N'occupations', 1, N'Salaried', N'Salaried', NULL),
    (N'occupations', 2, N'Self-employed', N'Self-employed', NULL),
    (N'occupations', 3, N'Business', N'Business', NULL),
    (N'occupations', 4, N'Retired', N'Retired', NULL),
    (N'occupations', 5, N'Homemaker', N'Homemaker', NULL),
    (N'occupations', 6, N'Student', N'Student', NULL),
    (N'subOccupations', 1, N'MMFSL Employee', N'MMFSL Employee', NULL),
    (N'subOccupations', 2, N'Private sector', N'Private sector', NULL),
    (N'subOccupations', 3, N'Public sector', N'Public sector', NULL),
    (N'subOccupations', 4, N'Government service', N'Government service', NULL),
    (N'subOccupations', 5, N'Professional', N'Professional', NULL),
    (N'maritalStatuses', 1, N'Married', N'Married', NULL),
    (N'maritalStatuses', 2, N'Single', N'Single', NULL),
    (N'maritalStatuses', 3, N'Widowed', N'Widowed', NULL),
    (N'maritalStatuses', 4, N'Divorced', N'Divorced', NULL),
    (N'genders', 1, N'Male', N'Male', NULL),
    (N'genders', 2, N'Female', N'Female', NULL),
    (N'genders', 3, N'Transgender', N'Transgender', NULL),
    (N'nameTypes', 1, N'Father', N'Father', NULL),
    (N'nameTypes', 2, N'Mother', N'Mother', NULL),
    (N'nameTypes', 3, N'Spouse', N'Spouse', NULL),
    (N'nomineeRelations', 1, N'Son', N'Son', NULL),
    (N'nomineeRelations', 2, N'Daughter', N'Daughter', NULL),
    (N'nomineeRelations', 3, N'Spouse', N'Spouse', NULL),
    (N'nomineeRelations', 4, N'Father', N'Father', NULL),
    (N'nomineeRelations', 5, N'Mother', N'Mother', NULL),
    (N'nomineeRelations', 6, N'Brother', N'Brother', NULL),
    (N'nomineeRelations', 7, N'Sister', N'Sister', NULL),
    (N'tenures', 1, N'12', N'12', NULL),
    (N'tenures', 2, N'18', N'18', NULL),
    (N'tenures', 3, N'24', N'24', NULL),
    (N'tenures', 4, N'30', N'30', NULL),
    (N'tenures', 5, N'36', N'36', NULL),
    (N'tenures', 6, N'42', N'42', NULL),
    (N'tenures', 7, N'48', N'48', NULL),
    (N'tenures', 8, N'60', N'60', NULL),
    (N'payouts', 1, N'maturity', N'On maturity', N'{"perYear":0,"each":""}'),
    (N'payouts', 2, N'yearly', N'Yearly', N'{"perYear":1,"each":"year"}'),
    (N'payouts', 3, N'halfyearly', N'Half yearly', N'{"perYear":2,"each":"half year"}'),
    (N'payouts', 4, N'quarterly', N'Quarterly', N'{"perYear":4,"each":"quarter"}'),
    (N'payouts', 5, N'monthly', N'Monthly', N'{"perYear":12,"each":"month"}'),
    (N'renewInstructions', 1, N'principal', N'Principal only', NULL),
    (N'renewInstructions', 2, N'principal-interest', N'Principal and interest', NULL),
    (N'deliveryTypes', 1, N'ereceipt', N'E-receipt', NULL),
    (N'deliveryTypes', 2, N'physical', N'Physical', NULL),
    (N'requiredDocuments', 1, N'Individual KYC', N'Individual KYC', N'{"items":["Passport","Driving license","Permanent Account Number (PAN) card","‘Election Commission of India’ Voter identity card","‘NREGA’ Job card, duly signed by an officer of the State Government","‘Unique Identification Authority of India’ letter containing details of name, address and Aadhaar Number, or any document as notified by the Central Government in consultation with the regulator"],"notes":["The PAN copy is collected first, before any proof of address.","An Aadhaar is accepted masked or unmasked, when the name and date of birth on it match the PAN. Where its 12-digit number cannot be read, it is typed for the PAN–Aadhaar link."]}'),
    (N'requiredDocuments', 2, N'Sole Proprietorship', N'Sole Proprietorship', N'{"items":["ID & address proof of the proprietor with self attestation","PAN card of proprietor with self attestation","Proprietor Address proof with self attestation","Photograph","If the sole proprietorship is in a different name, the bank statement or registration certificate","GST & Udyam Certificate or Trade License required","Cancelled cheque leaf for bank account verification"],"notes":[]}'),
    (N'requiredDocuments', 3, N'NRI - Individual KYC', N'NRI - Individual KYC', N'{"items":["Passport with valid visa/ OCI","Overseas employment letter (optional for confirmation of residential status and overseas address)","PIO cards","PAN card","Local proof of address, if different from the passport address","Bank account statement or passbook","Local Property papers with registration deed","EB Bill card","Voter ID or driving license","Tax Residency Certificate from the Income Tax department of the country of which the investor is a resident","Copy of the passport as of the beginning of the current financial year and end of the financial year"],"notes":[]}'),
    (N'requiredDocuments', 4, N'HUF Deposits', N'HUF Deposits', N'{"items":["HUF Pan copy with self attestation","Latest HUF address proof required","Valid address proof & identity proof of Karta","Karta Photograph","Cancelled cheque leaf for bank account verification","HUF declaration required"],"notes":[]}'),
    (N'requiredDocuments', 5, N'Companies', N'Companies', N'{"items":["PAN card","Address proof (Valid GST Certificate/bank statement/telephone bill)","Certificate of incorporation","Memorandum of article & association with latest board resolution and specimen signatures","Authorised signatory list","Photograph of the signatories","ID & address proof of authorised signatories","Cancelled cheque leaf for bank account verification","FATCA Declaration","Beneficial Owner form along with BO Owners self attested KYC documents"],"notes":[]}'),
    (N'requiredDocuments', 6, N'Partnership Firms', N'Partnership Firms', N'{"items":["PAN card","Address proof (bank statement, telephone bill)","Firm Registration certificate","Partnership deed","Resolution copy","ID & address proof of all authorised signatories","Photograph of the signatories","Cancelled cheque leaf for bank account verification","FATCA Declaration","Beneficial Owner form along with BO Owners self attested KYC documents"],"notes":[]}'),
    (N'requiredDocuments', 7, N'Trust and Foundations', N'Trust and Foundations', N'{"items":["PAN card","Address proof (Valid GST Certificate/bank statement/telephone bill)","Non Profit Organisation(Yes/No)","Darpan Registration mandatory for NPO''s","Registration certificate of the Trust/Charitable/Family & Foundation","Memorandum/Deed of the Trust/Charitable/Family & Foundation with latest board resolution and specimen signatures","Authorised signatory list","ID & address proof of authorised signatories with self attestation","Photograph of the Trustees & signatories","Cancelled cheque leaf for bank account verification","FATCA Declaration","Beneficial Owner form along with BO Owners self attested KYC documents"],"notes":[]}'),
    (N'requiredDocuments', 8, N'Charitable Trust', N'Charitable Trust', N'{"items":["PAN card","Address proof (Valid GST Certificate/bank statement/telephone bill)","Non Profit Organisation(Yes/No)","Darpan Registration mandatory for NPO''s","Registration certificate of the Trust/Charitable/Family & Foundation","Memorandum/Deed of the Trust/Charitable/Family & Foundation with latest board resolution and specimen signatures","Authorised signatory list","ID & address proof of authorised signatories with self attestation","Photograph of the Trustees & signatories","Cancelled cheque leaf for bank account verification","FATCA Declaration","Beneficial Owner form along with BO Owners self attested KYC documents"],"notes":[]}'),
    (N'requiredDocuments', 9, N'Family Trust', N'Family Trust', N'{"items":["PAN card","Address proof (Valid GST Certificate/bank statement/telephone bill)","Registration certificate of the Trust/Charitable/Family & Foundation","Memorandum/Deed of the Trust/Charitable/Family & Foundation with latest board resolution and specimen signatures","Authorised signatory list","ID & address proof of authorised signatories with self attestation","Photograph of the Trustees & signatories","Cancelled cheque leaf for bank account verification","FATCA Declaration","Beneficial Owner form along with BO Owners self attested KYC documents"],"notes":[]}'),
    (N'requiredDocuments', 10, N'Club, Association, Society', N'Club, Association, Society', N'{"items":["Copy of the Registration Certificate, if registered.","Acknowledgment of registration application, if applied for.","PAN card copy","Address proof","List of signatories","MOA & Resolution","Individual KYC of all signatories","Photograph of the Trustees & signatories","Cancelled cheque leaf for bank account verification","FATCA Declaration","Beneficial Owner form along with BO Owners self attested KYC documents"],"notes":[]}'),
    (N'identificationNotes', 1, N'1', N'Only individual depositors 18 years and above are allowed to make investments.', NULL),
    (N'identificationNotes', 2, N'2', N'We strongly advice the depositor(s) to avail the nomination', NULL),
    (N'identificationNotes', 3, N'3', N'For investment above Rs.5 Cr, please write to fixeddeposit@mahindrafinance.com', NULL),
    (N'dashboardNotes', 1, N'1', N'Currently Esarathi is enabled with individual and sole proprietorship investment only.', NULL),
    (N'dashboardNotes', 2, N'2', N'Verification of the Fixed Deposit is subject to validation of the submitted documents by the Operations team.', NULL),
    (N'declarations', 1, N'1', N'I have met every holder, seen the original documents, and the uploads are true copies of them.', NULL),
    (N'declarations', 2, N'2', N'The deposit terms above, including the rate and that it locks on realisation, were read out to the investor.', NULL),
    (N'declarations', 3, N'3', N'I have not received or promised any cash consideration outside this application.', NULL),
    (N'noticeKinds', 1, N'Rate change', N'Rate change', NULL),
    (N'noticeKinds', 2, N'Maintenance', N'Maintenance', NULL),
    (N'renewalNotes', 1, N'1', N'Deposits due for renewal only will be displayed in this module.', NULL),
    (N'renewalNotes', 2, N'2', N'You are not allowed to make any changes of depositors in this module.', NULL),
    (N'renewalNotes', 3, N'3', N'Please keep the renewal documents ready to upload before proceeding.', NULL),
    (N'renewalNotes', 4, N'4', N'Details appearing in Renewal Application/FDR will be considered to renew the Deposits.', NULL),
    (N'renewalNotes', 5, N'5', N'You will be able to make renewal entry in this module only from {renewFromDays} days upto {renewUntilDays} days prior to maturity.', NULL),
    (N'renewalNotes', 6, N'6', N'Changes in the Second holder/Third holder OR any other information shall be executed as per the details mentioned in Renewal Application/FDR.', NULL),
    (N'renewalNotes', 7, N'7', N'Any renewal of Deposit in advance of its maturity date, will be subject to the rate of interest and other terms & conditions prevailing on the date of said maturity.', NULL),
    (N'renewalNotes', 8, N'8', N'The Auto renewal tag cases will be able to make renewal entry in this module only from {renewFromDays} days up to {renewUntilDaysAutoRenewal} days prior to maturity.', NULL)
) v (l, s, c, n, a)
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_Ref_List r WHERE r.c_List = v.l AND r.c_Code = v.c);
GO
