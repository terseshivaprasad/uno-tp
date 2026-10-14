namespace UnoTP.Data;

/// <summary>
/// The SQL every page runs for its lists: the settings, the console's features, the
/// lists of t_Unotp_Ref_List, the payment gateway's banks, and how each document is
/// coded. Nothing but SQL is in this file.
/// </summary>
internal static class ListQueries
{
    /// <summary>
    /// The settings in use.
    /// </summary>
    public const string Settings = "SELECT f_Key, f_Value FROM dbo.t_Unotp_App_Config WHERE f_Active = 1";

    /// <summary>
    /// Every entry of every list in use, in the lists' own order.
    /// </summary>
    public const string Lists = """
        SELECT f_List AS List, f_Code AS Code, f_Name AS Name, f_Parent AS Parent, f_Attrs AS Attrs
        FROM dbo.t_Unotp_Ref_List WHERE f_Active = 1 ORDER BY f_List, f_Seq
        """;

    /// <summary>
    /// The console's features in use, in their own order.
    /// </summary>
    public const string Features = """
        SELECT f_Feature_Key AS Code, f_Name AS Name, f_Group AS [Group], f_Detail AS Detail,
               f_Off_Reason AS OffReason, f_Tile AS Tile
        FROM dbo.t_Unotp_Feature_Mst WHERE f_Active = 1 ORDER BY f_Seq
        """;

    /// <summary>
    /// The banks the payment gateway takes for online payment, by name. The inner read is the
    /// gateway's bank master, which must give back: Code (the bank's code, the first four letters of
    /// its IFSCs), Name. Put the gateway bank master's own table and columns there. Until then it
    /// reads the development rows kept under 'gatewayBanks' in t_Unotp_Ref_List.
    /// </summary>
    public const string GatewayBanks = """
        SELECT g.Code, g.Name
        FROM (
            SELECT g.f_Code AS Code, g.f_Name AS Name
            FROM dbo.t_Unotp_Ref_List g
            WHERE g.f_List = 'gatewayBanks' AND g.f_Active = 1
        ) g
        ORDER BY g.Name
        """;

    /// <summary>
    /// The lists that say which master document type and sub type each of the app's documents is.
    /// </summary>
    public const string DocumentCodeLists = """
        SELECT f_List AS List, f_Code AS Code, f_Name AS Name, f_Parent AS Parent, f_Attrs AS Attrs
        FROM dbo.t_Unotp_Ref_List
        WHERE f_List IN ('filedDocuments', 'documentSubTypes', 'documentTypes') AND f_Active = 1
        """;
}
