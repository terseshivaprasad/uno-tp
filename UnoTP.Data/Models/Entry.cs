using System.Text.Json.Serialization;

namespace UnoTP.Models;

/// <summary>
/// How a partner comes into the app: the portal opens it with the user id and the
/// system code, both encrypted; once decrypted, the E-Sarathi auth API starts a
/// session for them and says which menus they may open.
/// </summary>
public interface ISessionApi
{
    /// <summary>
    /// POST auth/sessions: starts a session for the user on this system. Null when
    /// it is refused - an unknown user, or a system code that is not this app's.
    /// </summary>
    Task<UserSession?> StartAsync(SessionStart start, CancellationToken ct = default);

    /// <summary>GET app-menus/{userId}/{sysCode}: the menus the user may open on this system.</summary>
    Task<IReadOnlyList<MenuItem>> MenuAsync(string userId, string sysCode, CancellationToken ct = default);

    /// <summary>
    /// Whether the partner's session is still open: not ended or taken out of use.
    /// Asked on every page, so ending one signs the partner out.
    /// </summary>
    Task<bool> IsOpenAsync(CancellationToken ct = default) => Task.FromResult(true);
}

/// <summary>What the auth API is told when a session starts: who, on which system, from where and with which browser.</summary>
/// <param name="ServerIp">The address of the server the app runs on.</param>
/// <param name="DomainName">The host name the app was opened at.</param>
/// <param name="IpAddress">The address the user's browser called from.</param>
/// <param name="MacAddress">The server's network card, twelve hex digits.</param>
public sealed record SessionStart(
    string UserId, string SysCode, string ServerIp, string DomainName, string IpAddress, string MacAddress,
    string BrowserType, string BrowserVersion, string BrowserMajor, string BrowserMinor, string UserAgent);

/// <param name="SessionId">The session the auth API started (pk_Session_ID); sent with every call made for the user.</param>
/// <param name="UserId">The user the session is for; every call is made on their behalf.</param>
/// <param name="ExpiresAt">When the session ends; the user comes in from the portal again after that.</param>
/// <param name="Partner">Who the user is, as the pages show them.</param>
/// <param name="User">The user and their session, whole, as the auth API returned them.</param>
public sealed record UserSession(
    string SessionId, string UserId, DateTime ExpiresAt, PartnerProfile Partner, AgencyUserModel User);

/// <summary>
/// The user and their session, as POST auth/sessions returns them: the auth API's
/// own model, with its own names. Kept in the session for as long as it lasts.
/// </summary>
public sealed class AgencyUserModel
{
    [JsonPropertyName("pk_Agency_Usr_Mst_ID")]
    public int Pk_Agency_Usr_Mst_ID { get; set; }

    [JsonPropertyName("entityType")]
    public string EntityType { get; set; } = string.Empty;

    [JsonPropertyName("agency_Clustered_ID")]
    public string Agency_Clustered_ID { get; set; } = string.Empty;

    [JsonPropertyName("agency_Usr_Clustered_ID")]
    public string Agency_Usr_Clustered_ID { get; set; } = string.Empty;

    [JsonPropertyName("agency_Cd")]
    public string Agency_Cd { get; set; } = string.Empty;

    [JsonPropertyName("agency_Type")]
    public string Agency_Type { get; set; } = string.Empty;

    [JsonPropertyName("agency_Sub_Type")]
    public string Agency_Sub_Type { get; set; } = string.Empty;

    [JsonPropertyName("agency_Name")]
    public string Agency_Name { get; set; } = string.Empty;

    [JsonPropertyName("agency_Usr_Name")]
    public string Agency_Usr_Name { get; set; } = string.Empty;

    [JsonPropertyName("entity_Id")]
    public string Entity_Id { get; set; } = string.Empty;

    [JsonPropertyName("entity_Type_Code")]
    public string Entity_Type_Code { get; set; } = string.Empty;

    [JsonPropertyName("entity_Name")]
    public string Entity_Name { get; set; } = string.Empty;

    [JsonPropertyName("agency_Usr_EmailID")]
    public string Agency_Usr_EmailID { get; set; } = string.Empty;

    [JsonPropertyName("agency_Usr_MobileNo")]
    public string Agency_Usr_MobileNo { get; set; } = string.Empty;

    [JsonPropertyName("agency_Usr_Base_Loc_cd")]
    public string Agency_Usr_Base_Loc_cd { get; set; } = string.Empty;

    [JsonPropertyName("agency_Usr_Base_Loc_Desc")]
    public string Agency_Usr_Base_Loc_Desc { get; set; } = string.Empty;

    [JsonPropertyName("agency_Usr_Base_Role_cd")]
    public string Agency_Usr_Base_Role_cd { get; set; } = string.Empty;

    [JsonPropertyName("agency_Usr_Base_Role_Desc")]
    public string Agency_Usr_Base_Role_Desc { get; set; } = string.Empty;

    [JsonPropertyName("agency_Cont_User_Cd")]
    public string Agency_Cont_User_Cd { get; set; } = string.Empty;

    [JsonPropertyName("agency_Cont_User_Name")]
    public string Agency_Cont_User_Name { get; set; } = string.Empty;

    [JsonPropertyName("agency_Cont_User_EmailID")]
    public string Agency_Cont_User_EmailID { get; set; } = string.Empty;

    [JsonPropertyName("agency_cont_User_MobileNo")]
    public string Agency_cont_User_MobileNo { get; set; } = string.Empty;

    [JsonPropertyName("busi_Broker_Cd")]
    public string Busi_Broker_Cd { get; set; } = string.Empty;

    [JsonPropertyName("pk_Session_Ref_ID")]
    public string? Pk_Session_Ref_ID { get; set; }

    [JsonPropertyName("pk_Session_ID")]
    public int Pk_Session_ID { get; set; }
}

/// <param name="PageName">The page the menu opens, as the portal names it.</param>
/// <param name="Name">What the menu is called.</param>
public sealed record MenuItem(string PageName, string Name);
