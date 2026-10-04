using System.Data;
using Dapper;

namespace UnoTP.Data;

/// <summary>
/// An application's number, from the FD system's own procedure
/// (USP_FD_BTP_GetApplicationNo), which numbers a purchase and a renewal alike.
/// It is told who is asking: a partner, or a branch user.
/// </summary>
public static class ApplicationNumber
{
    // Who is asking, as the procedure names them.
    private const string Partner = "C";
    private const string BranchUser = "B";

    // @BusType of every purchase, and @DSource of every renewal.
    private const string PurchaseBusType = "B";
    private const string RenewalSource = "R";

    /// <summary>What the procedure is told of the user asking.</summary>
    /// <param name="CreatedBy">The signed-in user's Agency_Usr_Clustered_ID.</param>
    /// <param name="SessionId">The user's backend session, when it is a number; null otherwise.</param>
    public sealed record AskedBy(string CreatedBy, string UserName, string AgencyCode, string Ip, long? SessionId, bool BranchUser);

    /// <summary>
    /// What the procedure is asked with. A purchase goes with @BusType B and
    /// @DSource C for a partner, B for a branch user; a renewal with @DSource R and
    /// @BusType C for a partner, B for a branch user.
    /// </summary>
    public static (string BusType, string DSource) Codes(bool branchUser, bool renewal)
    {
        var who = branchUser ? BranchUser : Partner;
        if (renewal) return (who, RenewalSource);
        return (PurchaseBusType, who);
    }

    /// <summary>The next application number, for a purchase or a renewal.</summary>
    public static async Task<string> NextAsync(IDbConnection connection, IDbTransaction tx, AskedBy by, bool renewal)
    {
        var (busType, source) = Codes(by.BranchUser, renewal);

        var parameters = new DynamicParameters(new
        {
            by.CreatedBy,
            by.AgencyCode,
            BusType = busType,
            CreatedIP = by.Ip,
            CreatedByUName = by.UserName,
            SessionID = by.SessionId,
            DSource = source,
        });
        parameters.Add("NewApplicationNo", dbType: DbType.String, size: 50, direction: ParameterDirection.Output);

        await connection.ExecuteAsync("""
            EXEC dbo.USP_FD_BTP_GetApplicationNo
                @f_BatchId = 1,
                @f_Entity_Id = @CreatedBy,
                @f_Agency_Cd = @AgencyCode,
                @f_Entity_Type = '',
                @f_BranchCode = '',
                @f_BTP_Trans_ref_No = NULL,
                @BusType = @BusType,
                @CreatedIP = @CreatedIP,
                @CreatedUserId = @CreatedBy,
                @CreatedUserName = @CreatedByUName,
                @SessionId = @SessionID,
                @FormCode = 'Search',
                @DSource = @DSource,
                @App_No = @NewApplicationNo OUTPUT
            """, parameters, tx);

        var appNo = parameters.Get<string?>("NewApplicationNo")?.Trim();
        if (string.IsNullOrEmpty(appNo))
            throw new InvalidOperationException("USP_FD_BTP_GetApplicationNo gave no application number.");
        return appNo;
    }
}
