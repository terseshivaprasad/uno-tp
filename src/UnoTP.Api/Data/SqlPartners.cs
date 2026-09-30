using Dapper;
using UnoTP.Backend;

namespace UnoTP.Api.Data;

/// <summary>
/// Who may sign in, and what they may open: t_Partner_Mst, t_Partner_Menu and
/// t_User_Session. A session is started only for an active partner coming in with
/// Uno TP's system code (sysCode in t_App_Config), and lasts sessionHours.
/// </summary>
public sealed class SqlPartners(Db db, IPartner partner, SqlReference reference) : IPartnerApi, ISessionApi
{
    public async Task<UserSession?> StartAsync(string userId, string sysCode, CancellationToken ct = default)
    {
        if (!string.Equals(sysCode.Trim(), await reference.SettingAsync("sysCode", ct), StringComparison.OrdinalIgnoreCase)) return null;
        var hours = await reference.NumberAsync("sessionHours", ct);
        await using var connection = await db.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<UserSession>("""
            INSERT dbo.t_User_Session (c_Session_Id, c_User_Id, c_Sys_Code, d_Expires_On)
            OUTPUT inserted.c_Session_Id AS SessionId, inserted.c_User_Id AS UserId, inserted.d_Expires_On AS ExpiresAt
            SELECT @SessionId, p.c_User_Id, @SysCode, DATEADD(HOUR, @Hours, SYSDATETIME())
            FROM dbo.t_Partner_Mst p WHERE p.c_User_Id = @UserId AND p.f_Active = 1
            """, new { SessionId = Guid.NewGuid().ToString("n"), UserId = userId.Trim(), SysCode = sysCode.Trim(), Hours = hours });
    }

    // The session's own partner only: a session that has ended opens nothing.
    public async Task<IReadOnlyList<MenuItem>> MenuAsync(CancellationToken ct = default)
    {
        if (partner.SessionId is not { } session) return [];
        await using var connection = await db.OpenAsync(ct);
        return (await connection.QueryAsync<MenuItem>("""
            SELECT f.c_Feature_Key AS [Key], f.c_Name AS Name
            FROM dbo.t_User_Session s
            JOIN dbo.t_Partner_Menu m ON m.c_User_Id = s.c_User_Id
            JOIN dbo.t_Feature_Mst f ON f.c_Feature_Key = m.c_Feature_Key
            WHERE s.c_Session_Id = @Session AND s.c_User_Id = @Partner
              AND s.d_Ended_On IS NULL AND s.d_Expires_On > SYSDATETIME()
              AND s.f_Active = 1 AND m.f_Active = 1 AND f.f_Active = 1
            ORDER BY f.n_Seq
            """, new { Session = session, Partner = partner.Id })).ToList();
    }

    public async Task<PartnerProfile> MeAsync(CancellationToken ct = default) =>
        await FindAsync(partner.Id, ct) ?? throw new KeyNotFoundException($"No partner {partner.Id}.");

    public async Task<PartnerProfile?> FindAsync(string userId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<PartnerProfile>("""
            SELECT c_Name AS Name, c_Code AS Code, c_Agency_Type AS AgencyType, c_Broker_Code AS BrokerCode
            FROM dbo.t_Partner_Mst WHERE c_User_Id = @UserId AND f_Active = 1
            """, new { UserId = userId });
    }

    /// <summary>Whether the session is the partner's and still open: every call but entry's is checked with it.</summary>
    public async Task<bool> SessionOpenAsync(string userId, string sessionId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM dbo.t_User_Session
            WHERE c_Session_Id = @Session AND c_User_Id = @UserId AND d_Ended_On IS NULL AND d_Expires_On > SYSDATETIME()
              AND f_Active = 1
              AND EXISTS (SELECT 1 FROM dbo.t_Partner_Mst p WHERE p.c_User_Id = @UserId AND p.f_Active = 1)
            """, new { Session = sessionId, UserId = userId }) > 0;
    }

    /// <summary>Whether the partner's menu opens the feature.</summary>
    public async Task<bool> HasFeatureAsync(string key, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM dbo.t_Partner_Menu m JOIN dbo.t_Feature_Mst f ON f.c_Feature_Key = m.c_Feature_Key
            WHERE m.c_User_Id = @Partner AND m.c_Feature_Key = @Key AND m.f_Active = 1 AND f.f_Active = 1
            """,
            new { Partner = partner.Id, Key = key }) > 0;
    }
}
