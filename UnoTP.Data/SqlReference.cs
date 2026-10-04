using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using UnoTP.Models;

namespace UnoTP.Data;

/// <summary>
/// The lists and rules, from t_Unotp_Ref_List, t_Unotp_Feature_Mst and t_Unotp_App_Config
/// (db/create_tables.sql, filled by db/insert_seed.sql). Every list the pages offer is
/// rows of t_Unotp_Ref_List: f_Code is what is posted and saved, f_Name what is shown.
/// Kept for a minute, so an edit in the tables is seen within one; the web app keeps
/// them longer on its side (Backend:ReferenceCacheMinutes).
/// </summary>
public sealed partial class SqlReference(Db db, IMemoryCache cache) : IReferenceApi
{
    private static readonly TimeSpan KeptFor = TimeSpan.FromMinutes(1);

    // ----- Config ------------------------------------------------------------------

    /// <summary>Every setting in t_Unotp_App_Config, by key.</summary>
    public Task<IReadOnlyDictionary<string, string>> SettingsAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync<IReadOnlyDictionary<string, string>>("t_Unotp_App_Config", async entry =>
        {
            (entry.AbsoluteExpirationRelativeToNow, entry.Size) = (KeptFor, 1);
            await using var connection = await db.OpenAsync(ct);
            var rows = await connection.QueryAsync<(string Key, string Value)>("SELECT f_Key, f_Value FROM dbo.t_Unotp_App_Config WHERE f_Active = 1");
            return rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);
        })!;

    /// <summary>A setting that must be there: a missing one is a deployment that skipped db/insert_seed.sql.</summary>
    public async Task<string> SettingAsync(string key, CancellationToken ct = default) =>
        (await SettingsAsync(ct)).TryGetValue(key, out var value)
            ? value
            : throw new InvalidOperationException($"t_Unotp_App_Config has no '{key}'. Run db/insert_seed.sql.");

    public async Task<int> NumberAsync(string key, CancellationToken ct = default) =>
        int.Parse(await SettingAsync(key, ct), CultureInfo.InvariantCulture);

    public async Task<AppConfig> ConfigAsync(CancellationToken ct = default)
    {
        var s = await SettingsAsync(ct);
        string Text(string key) => s.TryGetValue(key, out var v) ? v : throw new InvalidOperationException($"t_Unotp_App_Config has no '{key}'. Run db/insert_seed.sql.");
        int ReadInt(string key) => int.Parse(Text(key), CultureInfo.InvariantCulture);
        long Long(string key) => long.Parse(Text(key), CultureInfo.InvariantCulture);
        const string hours = "linkValidityHours.";
        return new AppConfig(
            Text("sourcingAgency"), ReadInt("minAge"), ReadInt("seniorAge"), ReadInt("maxJointHolders"), ReadInt("maxAttempts"),
            Long("minAmount"), Long("maxAmount"), Long("amountStep"), ReadInt("cancellationDays"), ReadInt("draftDays"),
            s.Where(p => p.Key.StartsWith(hours, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(p => p.Key[hours.Length..], p => int.Parse(p.Value, CultureInfo.InvariantCulture)),
            ReadInt("renewFromDays"), ReadInt("renewUntilDays"), ReadInt("renewUntilDaysAutoRenewal"), ReadInt("closeToCancelDays"),
            QuoteAmount: Long("quoteAmount"),
            SourceOfFundsFrom: Long("sourceOfFundsFrom"),
            SourceOfFundsOccupations: await Names("sourceOfFundsOccupations"),
            SourceOfFundsIncomeBands: await Names("sourceOfFundsIncomeBands"),
            OverMaxAmountMessage: s.GetValueOrDefault("overMaxAmountMessage", ""),
            SourceOfFundsOther: s.GetValueOrDefault("sourceOfFundsOther", ""));
    }

    // A setting that lists names, one after another with a semicolon between:
    // "Homemaker; Student; Retired". A semicolon, because the names themselves can
    // carry commas ("Upto Rs.5,00,000").
    private async Task<IReadOnlyList<string>> Names(string key)
    {
        var text = await SettingAsync(key);
        return text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    // ----- Reference lists -----------------------------------------------------------

    // One row of t_Unotp_Ref_List: Parent is the code of the entry it belongs under, in another list.
    private sealed record Entry(string List, string Code, string Name, string? Parent, string? Attrs);

    public Task<ReferenceData> ReferenceAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync("t_Unotp_Ref_List", async cacheEntry =>
        {
            (cacheEntry.AbsoluteExpirationRelativeToNow, cacheEntry.Size) = (KeptFor, 1);
            await using var lists = await db.OpenAsync(ct);
            var entries = (await lists.QueryAsync<Entry>("""
                SELECT f_List AS List, f_Code AS Code, f_Name AS Name, f_Parent AS Parent, f_Attrs AS Attrs
                FROM dbo.t_Unotp_Ref_List WHERE f_Active = 1 ORDER BY f_List, f_Seq
                """)).ToLookup(e => e.List);
            await using var featureMaster = await db.OpenAsync(ct);
            var features = (await featureMaster.QueryAsync<FeatureOption>("""
                SELECT f_Feature_Key AS Code, f_Name AS Name, f_Group AS [Group], f_Detail AS Detail, f_Off_Reason AS OffReason, f_Tile AS Tile
                FROM dbo.t_Unotp_Feature_Mst WHERE f_Active = 1 ORDER BY f_Seq
                """)).ToList();
            await using var cmsMaster = await db.OpenAsync(ct);
            var cmsLocations = (await cmsMaster.QueryAsync<string>(
                $"SELECT c.Name FROM ({MasterQueries.CmsLocations}) c ORDER BY c.Name")).ToList();
            await using var gatewayMaster = await db.OpenAsync(ct);
            var gatewayBanks = (await gatewayMaster.QueryAsync<Option>(
                $"SELECT g.Code, g.Name FROM ({MasterQueries.GatewayBanks}) g ORDER BY g.Name")).ToList();
            var settings = await SettingsAsync(ct);
            return Build(entries, features, settings, cmsLocations, gatewayBanks);
        })!;

    private static ReferenceData Build(ILookup<string, Entry> lists, IReadOnlyList<FeatureOption> features, IReadOnlyDictionary<string, string> settings,
        IReadOnlyList<string> cmsLocations, IReadOnlyList<Option> gatewayBanks)
    {
        var masters = MasterListsOf(lists);

        // A text may name a setting, {renewFromDays}, which is filled in here.
        string Fill(string text) => settings.Aggregate(text, (t, s) => t.Replace("{" + s.Key + "}", s.Value, StringComparison.OrdinalIgnoreCase));
        IEnumerable<Entry> Of(string list) => lists[list];
        List<string> Names(string list) => Of(list).Select(e => Fill(e.Name)).ToList();
        List<Option> Options(string list) => Of(list).Select(e => new Option(e.Code, e.Name)).ToList();
        // The codes of a list's entries that belong under one entry of another list.
        List<string> Under(string list, string parent) => Of(list).Where(e => e.Parent == parent).Select(e => e.Code).ToList();

        return new ReferenceData(
            ApplicationTypes: Options("applicationTypes"),
            Categories: Of("categories").Select(e => Attrs(e, a => new CategoryOption(e.Code, e.Name, ReadBool(a, "employee"), ReadBool(a, "women"), ReadBool(a, "senior")))).ToList(),
            PaymentModes: Of("paymentModes").Select(e => Attrs(e, a => new PaymentModeOption(e.Name, ReadString(a, "document")))).ToList(),
            SourcingModes: Of("sourcingModes").Select(e => Attrs(e, a => new SourcingModeOption(e.Code, e.Name,
                ReadString(a, "codeLabel") ?? "", ReadString(a, "nameLabel") ?? "", ReadString(a, "house") ?? "", ReadString(a, "search") ?? "",
                ReadString(a, "register") ?? "", ReadString(a, "sub") ?? "", Under("sourcingModeCategories", e.Code), ReadString(a, "staff") ?? ""))).ToList(),
            ProofsOfAddress: Of("proofsOfAddress").Select(e => Attrs(e, a => new ProofOption(e.Code, ReadString(a, "issuer") ?? "", ReadBool(a, "hasPhoto")))).ToList(),
            EmployeeHolders: Names("employeeHolders"),
            EmployeeRelations: masters.EmployeeRelations.Select(o => o.Name).ToList(),
            EmployeeProofs: Names("employeeProofs"),
            IncomeBands: Names("incomeBands"),
            Occupations: masters.Occupations.Select(o => o.TypeName).Distinct().ToList(),
            SubOccupations: masters.Occupations.Select(o => o.SubTypeName).Distinct().ToList(),
            MaritalStatuses: masters.MaritalStatuses.Select(o => o.Name).ToList(),
            Genders: Names("genders"),
            NameTypes: Names("nameTypes"),
            NomineeRelations: masters.NomineeRelations.Select(o => o.Name).ToList(),
            Tenures: Of("tenures").Select(e => int.Parse(e.Code, CultureInfo.InvariantCulture)).ToList(),
            Payouts: Of("payouts").Select(e => Attrs(e, a => new PayoutOption(e.Code, e.Name, ReadInt(a, "perYear"), ReadString(a, "each") ?? "", ReadString(a, "scheme") ?? ""))).ToList(),
            RenewInstructions: Options("renewInstructions"),
            DeliveryTypes: Options("deliveryTypes"),
            CmsLocations: cmsLocations,
            RequiredDocuments: Of("requiredDocuments").Select(e => Attrs(e, a => new RequiredDocumentGroup(e.Name, ReadStrings(a, "items"), ReadStrings(a, "notes")))).ToList(),
            IdentificationNotes: Names("identificationNotes"),
            DashboardNotes: Names("dashboardNotes"),
            NoticeKinds: Names("noticeKinds"),
            RenewalNotes: Names("renewalNotes"),
            Features: features,
            SourcesOfFunds: Options("sourcesOfFunds"),
            OccupationsWithSubs: masters.Occupations.GroupBy(o => o.TypeName)
                .Select(type => new OccupationOption(type.Key, type.Select(o => o.SubTypeName).ToList())).ToList(),
            GatewayBanks: gatewayBanks,
            Masters: masters);
    }

    // ----- An entry's attributes, f_Attrs -------------------------------------------

    private static T Attrs<T>(Entry e, Func<JsonElement, T> read)
    {
        using var doc = JsonDocument.Parse(e.Attrs ?? "{}");
        return read(doc.RootElement);
    }

    /// <summary>A true/false attribute of a JSON entry (false when missing).</summary>
    private static bool ReadBool(JsonElement a, string name) => a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    // A list of names in the attributes, or none when it is not there.
    // A number in the attributes, or 0 when it is not there.
    private static decimal ReadDecimal(JsonElement a, string name)
    {
        if (!a.TryGetProperty(name, out var v)) return 0;
        if (v.ValueKind != JsonValueKind.Number) return 0;
        return v.GetDecimal();
    }

    /// <summary>A whole-number attribute of a JSON entry (0 when missing).</summary>
    private static int ReadInt(JsonElement a, string name) => a.TryGetProperty(name, out var v) && v.TryGetInt32(out var n) ? n : 0;

    /// <summary>A text attribute of a JSON entry, or null when missing.</summary>
    private static string? ReadString(JsonElement a, string name) => a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>A list-of-text attribute of a JSON entry (empty when missing).</summary>
    private static List<string> ReadStrings(JsonElement a, string name) =>
        a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];
}
