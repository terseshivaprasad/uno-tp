/* =============================================================================
   Uno TP - every step of a renewal can be edited, as a purchase's can, so the two
   notes on Renew FD about changes to the depositors become one:

     2  "You are not allowed to make any changes of depositors in this module."
     6  "Changes in the Second holder/Third holder OR any other information shall
         be executed as per the details mentioned in Renewal Application/FDR."

   Note 2 takes the one wording; note 6 goes out of use (f_Active = 0), not deleted.

   Safe to run again: the notes are changed only while note 2 holds its old wording.
   ============================================================================= */
SET NOCOUNT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.t_Unotp_Ref_List WHERE c_List = 'renewalNotes' AND c_Code = '2'
           AND c_Name = N'You are not allowed to make any changes of depositors in this module.')
BEGIN
    UPDATE dbo.t_Unotp_Ref_List
    SET c_Name = N'Changes to the depositors (second holder, third holder) or any other information shall be made as per the details mentioned in the Renewal Application/FDR.',
        c_Updated_By = 'SEED', d_Updated_On = SYSDATETIME()
    WHERE c_List = 'renewalNotes' AND c_Code = '2';

    UPDATE dbo.t_Unotp_Ref_List
    SET f_Active = 0, c_Updated_By = 'SEED', d_Updated_On = SYSDATETIME()
    WHERE c_List = 'renewalNotes' AND c_Code = '6';
END
GO
