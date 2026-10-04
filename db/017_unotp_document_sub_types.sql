/* =============================================================================
   Uno TP - the documents an application files are written to t_FD_BT_KYC_document
   coded as the FD system's document master codes them.

   The 'documentSubTypes' list says which master sub-type each document is: c_Code
   is the document as the app knows it - the slot it is filed in and, for a proof,
   which one it is - and c_Name the sub-type's code in
   T_FD_CMN_KYC_Document_Sub_Type_Mst. The type code and both descriptions are read
   off the master, not kept here. A communication address is proved by a proof of
   address, so it uses the "poa:" entries.

   Review with the business: an appointment letter is listed with the salary slip
   or HR letter.

   Safe to run again: an entry is added only when it is not there yet.
   ============================================================================= */
SET NOCOUNT ON;
GO
INSERT dbo.t_Unotp_Ref_List (c_List, n_Seq, c_Code, c_Name, j_Attrs, c_Created_By)
SELECT v.l, v.s, v.c, v.n, NULL, 'SEED' FROM (VALUES
    (N'documentSubTypes', 1, N'pan', N'PAN'),
    (N'documentSubTypes', 2, N'photo', N'Photograph'),
    (N'documentSubTypes', 3, N'poa:Aadhaar', N'AADHARCARD_A'),
    (N'documentSubTypes', 4, N'poa:Passport', N'PASSPORT_A'),
    (N'documentSubTypes', 5, N'poa:Driving Licence', N'DRVLIC_A'),
    (N'documentSubTypes', 6, N'poa:Voter ID', N'VOTERID_A'),
    (N'documentSubTypes', 7, N'form', N'APPLICATIONFRM'),
    (N'documentSubTypes', 8, N'payment', N'PAYMENT_CHEQUE'),
    (N'documentSubTypes', 9, N'empproof:Employee ID card', N'EMP_ID'),
    (N'documentSubTypes', 10, N'empproof:Appointment letter', N'EMP_PAY_SLIP_OR_HR_LETTER'),
    (N'documentSubTypes', 11, N'empproof:Latest salary slip', N'EMP_PAY_SLIP_OR_HR_LETTER'),
    (N'documentSubTypes', 12, N'tdsform', N'FORM121')
) v (l, s, c, n)
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_Ref_List r WHERE r.c_List = v.l AND r.c_Code = v.c);
GO
