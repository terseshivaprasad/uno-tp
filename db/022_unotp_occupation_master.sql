/* =============================================================================
   Uno TP - the occupation and sub occupation on Investor Information are offered as
   the FD system's occupation master lists them (t_FD_CMN_Ckyc_CustSeg_Mst, in the
   common database): its types as occupations and, under each, its sub-types. The
   save writes the chosen row's codes to t_FD_BT_Kyc_Data_Dtl - f_CustSeg_Type_Code,
   f_CustSeg_Subtype_Code, f_Kyc_Occupation_Code and f_Kyc_Occupation_Desc - beside
   the names. The 'occupations' and 'subOccupations' lists in t_Unotp_Ref_List are no
   longer read.

     occupationTypesLeftOut   the master's type codes the pages do not offer, a
                              semicolon between: those that are not an individual's

   The source of funds is asked by occupation (sourceOfFundsOccupations), and that
   setting names occupations as the pages offer them. Where it still holds the names
   of the app's own old list, it takes the master's names for the same three.

   Review with the business: the type codes and the names must be the master's own.

   Safe to run again: the setting is added only when it is not there yet, and the
   other is changed only while it holds the old names.
   ============================================================================= */
SET NOCOUNT ON;
GO
INSERT dbo.t_Unotp_App_Config (c_Key, c_Value, c_Description, c_Created_By)
SELECT v.k, v.v, v.d, 'SEED'
FROM (VALUES
    (N'occupationTypesLeftOut', N'6', N'The type codes of t_FD_CMN_Ckyc_CustSeg_Mst the pages do not offer as an occupation (a semicolon between): not an individual''s')
) v (k, v, d)
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_App_Config c WHERE c.c_Key = v.k);
GO
UPDATE dbo.t_Unotp_App_Config
SET c_Value = N'Housewife; Student; Retired', c_Updated_By = 'SEED', d_Updated_On = SYSDATETIME()
WHERE c_Key = N'sourceOfFundsOccupations' AND c_Value = N'Homemaker; Student; Retired';
GO
