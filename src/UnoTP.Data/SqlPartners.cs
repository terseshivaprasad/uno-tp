using Dapper;
using UnoTP.Backend;

namespace UnoTP.Data;

/// <summary>
/// Who may sign in, and what they may open: t_Unotp_Partner_Mst, t_Unotp_Partner_Menu and
/// t_Unotp_User_Session. A session is started only for an active partner coming in with
/// Uno TP's system code (sysCode in t_Unotp_App_Config), and lasts sessionHours.
/// </summary>
public sealed class SqlPartners(Db db, IPartner partner, SqlReference reference) : IPartnerApi, ISessionApi
{
    public async Task<UserSession?> StartAsync(string userId, string sysCode, CancellationToken ct = default)
    {
        if (!string.Equals(sysCode.Trim(), await reference.SettingAsync("sysCode", ct), StringComparison.OrdinalIgnoreCase)) return null;
        var hours = await reference.NumberAsync("sessionHours", ct);
        await using var connection = await db.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<UserSession>("""
            INSERT dbo.t_Unotp_User_Session (c_Session_Id, c_User_Id, c_Sys_Code, d_Expires_On)
            OUTPUT inserted.c_Session_Id AS SessionId, inserted.c_User_Id AS UserId, inserted.d_Expires_On AS ExpiresAt
            SELECT @SessionId, p.c_User_Id, @SysCode, DATEADD(HOUR, @Hours, SYSDATETIME())
            FROM dbo.t_Unotp_Partner_Mst p WHERE p.c_User_Id = @UserId AND p.f_Active = 1
            """, new { SessionId = Guid.NewGuid().ToString("n"), UserId = userId.Trim(), SysCode = sysCode.Trim(), Hours = hours });
    }

    // The session's own partner only: a session that has ended opens nothing.
    public async Task<IReadOnlyList<MenuItem>> MenuAsync(CancellationToken ct = default)
    {
        if (partner.SessionId is not { } session) return [];
        // The keys on the partner's menu, from the main database; what each is called,
        // and their order, from the feature list (the masters database).
        await using var connection = await db.OpenAsync(ct);
        var keys = (await connection.QueryAsync<string>("""
            SELECT m.c_Feature_Key
            FROM dbo.t_Unotp_User_Session s
            JOIN dbo.t_Unotp_Partner_Menu m ON m.c_User_Id = s.c_User_Id
            WHERE s.c_Session_Id = @Session AND s.c_User_Id = @Partner
              AND s.d_Ended_On IS NULL AND s.d_Expires_On > SYSDATETIME()
              AND s.f_Active = 1 AND m.f_Active = 1
            """, new { Session = session, Partner = partner.Id })).ToList();

        var menu = new List<MenuItem>();
        foreach (var feature in (await reference.ReferenceAsync(ct)).Features ?? [])
        {
            if (keys.Contains(feature.Code)) menu.Add(new MenuItem(feature.Code, feature.Name));
        }
        return menu;
    }

    public async Task<PartnerProfile> MeAsync(CancellationToken ct = default) =>
        await FindAsync(partner.Id, ct) ?? throw new KeyNotFoundException($"No partner {partner.Id}.");

    public async Task<PartnerProfile?> FindAsync(string userId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<PartnerProfile>("""
            SELECT c_Name AS Name, c_Code AS Code, c_Agency_Type AS AgencyType, c_Broker_Code AS BrokerCode
            FROM dbo.t_Unotp_Partner_Mst WHERE c_User_Id = @UserId AND f_Active = 1
            """, new { UserId = userId });
    }

    public Task<bool> IsOpenAsync(CancellationToken ct = default) =>
        partner.SessionId is { } session ? SessionOpenAsync(partner.Id, session, ct) : Task.FromResult(false);

    /// <summary>Whether the session is the partner's and still open.</summary>
    public async Task<bool> SessionOpenAsync(string userId, string sessionId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM dbo.t_Unotp_User_Session
            WHERE c_Session_Id = @Session AND c_User_Id = @UserId AND d_Ended_On IS NULL AND d_Expires_On > SYSDATETIME()
              AND f_Active = 1
              AND EXISTS (SELECT 1 FROM dbo.t_Unotp_Partner_Mst p WHERE p.c_User_Id = @UserId AND p.f_Active = 1)
            """, new { Session = sessionId, UserId = userId }) > 0;
    }

}
