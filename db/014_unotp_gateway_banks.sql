/* =============================================================================
   Uno TP - the banks the payment gateway takes for online payment.

   gatewayBanks in t_Unotp_Ref_List: one row per bank, c_Code the IFSC bank code
   (its first four letters), c_Name the bank. Paid online, a repayment account at
   a bank not on the list is refused on Bank Details, with what to do: another
   account, or the payment mode changed to RTGS or Cheque. An empty list takes
   every bank. The rows below are a starting list: set them from the gateway's own.
   ============================================================================= */
SET NOCOUNT ON;
GO
INSERT dbo.t_Unotp_Ref_List (c_List, n_Seq, c_Code, c_Name, j_Attrs, c_Created_By)
SELECT v.l, v.s, v.c, v.n, NULL, 'SEED' FROM (VALUES
    (N'gatewayBanks', 1, N'HDFC', N'HDFC Bank'),
    (N'gatewayBanks', 2, N'ICIC', N'ICICI Bank'),
    (N'gatewayBanks', 3, N'SBIN', N'State Bank of India'),
    (N'gatewayBanks', 4, N'UTIB', N'Axis Bank'),
    (N'gatewayBanks', 5, N'KKBK', N'Kotak Mahindra Bank'),
    (N'gatewayBanks', 6, N'YESB', N'Yes Bank'),
    (N'gatewayBanks', 7, N'PUNB', N'Punjab National Bank'),
    (N'gatewayBanks', 8, N'BARB', N'Bank of Baroda')
) v (l, s, c, n)
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_Ref_List r WHERE r.c_List = v.l AND r.c_Code = v.c);
GO
