/* =============================================================================
   Uno TP - marital status, the nominee's relation, the employee's relation and the
   source of funds are offered as the FD system's own masters list them, and saved
   as its codes:

     t_FD_BT_Marital_Status_Mst          -> t_FD_BT_Kyc_Data_Dtl.f_Kyc_MaritalStatus
     t_FD_CMN_Relation_Mst               -> t_FD_BT_Nominee_Dtl.f_Nominee_Relations
     t_FD_MMFSL_Employee_Relation_Mst    -> t_FD_BT_Investment_Dtl.f_EmpRelation
     t_FD_CMN_AML_Source_Of_Funds_Mst    -> f_AML_Source_Of_Funds, on the deposit and on the log

   The 'maritalStatuses', 'nomineeRelations', 'employeeRelations' and 'sourcesOfFunds'
   lists in t_Unotp_Ref_List are no longer read. Two things the masters do not say
   are settings:

     sourceOfFundsOther     the source of funds, by its master code, that takes a
                            typed remark
     employeeSelfRelation   the employee relation's master code for the employee
                            themselves. It stands when the employee is the first
                            holder, and is not chosen on the page, so it is taken
                            though the master has it inactive

   Review with the business: both must be codes the masters hold.

   Safe to run again: a setting is added only when it is not there yet.
   ============================================================================= */
SET NOCOUNT ON;
GO
INSERT dbo.t_Unotp_App_Config (c_Key, c_Value, c_Description, c_Created_By)
SELECT v.k, v.v, v.d, 'SEED'
FROM (VALUES
    (N'sourceOfFundsOther', N'11', N'The source of funds, by its code in t_FD_CMN_AML_Source_Of_Funds_Mst, that takes a typed remark'),
    (N'employeeSelfRelation', N'SEL', N'The code in t_FD_MMFSL_Employee_Relation_Mst for the employee themselves: the relation when the employee is the first holder')
) v (k, v, d)
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_Unotp_App_Config c WHERE c.c_Key = v.k);
GO
