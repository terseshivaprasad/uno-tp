using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using UnoTP.Backend;

namespace UnoTP.Data;

/// <summary>
/// The lists and rules, from t_Ref_List, t_Feature_Mst and t_App_Config
/// (db/002, seeded by db/003). Kept for a minute, so an edit in the tables is
/// seen within one; the web app keeps them longer on its side
/// (Backend:ReferenceCacheMinutes).
/// </summary>
public sealed class SqlReference(Db db, IMemoryCache cache) : IReferenceApi
{
    private static readonly TimeSpan KeptFor = TimeSpan.FromMinutes(1);

    // ----- Config ------------------------------------------------------------------

    /// <summary>Every setting in t_App_Config, by key.</summary>
    public Task<IReadOnlyDictionary<string, string>> SettingsAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync<IReadOnlyDictionary<string, string>>("t_App_Config", async entry =>
        {
            (entry.AbsoluteExpirationRelativeToNow, entry.Size) = (KeptFor, 1);
            await using var connection = await db.OpenAsync(ct);
            var rows = await connection.QueryAsync<(string Key, string Value)>("SELECT c_Key, c_Value FROM dbo.t_App_Config WHERE f_Active = 1");
            return rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);
        })!;

    /// <summary>A setting that must be there: a missing one is a deployment that skipped db/003.</summary>
    public async Task<string> SettingAsync(string key, CancellationToken ct = default) =>
        (await SettingsAsync(ct)).TryGetValue(key, out var value)
            ? value
            : throw new InvalidOperationException($"t_App_Config has no '{key}'. Run db/003_unotp_seed.sql.");

    public async Task<int> NumberAsync(string key, CancellationToken ct = default) =>
        int.Parse(await SettingAsync(key, ct), CultureInfo.InvariantCulture);

    public async Task<AppConfig> ConfigAsync(CancellationToken ct = default)
    {
        var s = await SettingsAsync(ct);
        string Text(string key) => s.TryGetValue(key, out var v) ? v : throw new InvalidOperationException($"t_App_Config has no '{key}'. Run db/003_unotp_seed.sql.");
        int Int(string key) => int.Parse(Text(key), CultureInfo.InvariantCulture);
        long Long(string key) => long.Parse(Text(key), CultureInfo.InvariantCulture);
        const string hours = "linkValidityHours.";
        return new AppConfig(
            Text("sourcingAgency"), Int("minAge"), Int("seniorAge"), Int("maxJointHolders"), Int("maxAttempts"),
            Long("minAmount"), Long("maxAmount"), Long("amountStep"), Int("cancellationDays"), Int("draftDays"),
            s.Where(p => p.Key.StartsWith(hours, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(p => p.Key[hours.Length..], p => int.Parse(p.Value, CultureInfo.InvariantCulture)),
            Int("renewFromDays"), Int("renewUntilDays"), Int("renewUntilDaysAutoRenewal"), Int("closeToCancelDays"));
    }

    // ----- Reference lists -----------------------------------------------------------

    private sealed record Entry(string List, string Code, string Name, string? Attrs);

    public Task<ReferenceData> ReferenceAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync("t_Ref_List", async cacheEntry =>
        {
            (cacheEntry.AbsoluteExpirationRelativeToNow, cacheEntry.Size) = (KeptFor, 1);
            await using var connection = await db.OpenAsync(ct);
            var entries = (await connection.QueryAsync<Entry>("""
                SELECT c_List AS List, c_Code AS Code, c_Name AS Name, j_Attrs AS Attrs
                FROM dbo.t_Ref_List WHERE f_Active = 1 ORDER BY c_List, n_Seq
                """)).ToLookup(e => e.List);
            var features = (await connection.QueryAsync<FeatureOption>("""
                SELECT c_Feature_Key AS Code, c_Name AS Name, c_Group AS [Group], c_Detail AS Detail, c_Off_Reason AS OffReason, f_Tile AS Tile
                FROM dbo.t_Feature_Mst WHERE f_Active = 1 ORDER BY n_Seq
                """)).ToList();
            var settings = await SettingsAsync(ct);
            return Build(entries, features, settings);
        })!;

    private static ReferenceData Build(ILookup<string, Entry> lists, IReadOnlyList<FeatureOption> features, IReadOnlyDictionary<string, string> settings)
    {
        // A text may name a setting, {renewFromDays}, which is filled in here.
        string Fill(string text) => settings.Aggregate(text, (t, s) => t.Replace("{" + s.Key + "}", s.Value, StringComparison.OrdinalIgnoreCase));
        IEnumerable<Entry> Of(string list) => lists[list];
        List<string> Names(string list) => Of(list).Select(e => Fill(e.Name)).ToList();
        List<Option> Options(string list) => Of(list).Select(e => new Option(e.Code, e.Name)).ToList();

        return new ReferenceData(
            ApplicationTypes: Options("applicationTypes"),
            Categories: Of("categories").Select(e => Attrs(e, a => new CategoryOption(e.Code, e.Name, Bool(a, "employee"), Bool(a, "women"), Bool(a, "senior")))).ToList(),
            PaymentModes: Of("paymentModes").Select(e => Attrs(e, a => new PaymentModeOption(e.Name, Str(a, "document")))).ToList(),
            SourcingModes: Of("sourcingModes").Select(e => Attrs(e, a => new SourcingModeOption(e.Code, e.Name,
                Str(a, "codeLabel") ?? "", Str(a, "nameLabel") ?? "", Str(a, "house") ?? "", Str(a, "search") ?? "",
                Str(a, "register") ?? "", Str(a, "sub") ?? "", Strs(a, "categories")))).ToList(),
            ProofsOfAddress: Of("proofsOfAddress").Select(e => Attrs(e, a => new ProofOption(e.Code, Str(a, "issuer") ?? "", Bool(a, "hasPhoto")))).ToList(),
            EmployeeHolders: Names("employeeHolders"),
            EmployeeRelations: Names("employeeRelations"),
            EmployeeProofs: Names("employeeProofs"),
            IncomeBands: Names("incomeBands"),
            Occupations: Names("occupations"),
            SubOccupations: Names("subOccupations"),
            MaritalStatuses: Names("maritalStatuses"),
            Genders: Names("genders"),
            NameTypes: Names("nameTypes"),
            NomineeRelations: Names("nomineeRelations"),
            Tenures: Of("tenures").Select(e => int.Parse(e.Code, CultureInfo.InvariantCulture)).ToList(),
            Payouts: Of("payouts").Select(e => Attrs(e, a => new PayoutOption(e.Code, e.Name, Int(a, "perYear"), Str(a, "each") ?? ""))).ToList(),
            RenewInstructions: Options("renewInstructions"),
            DeliveryTypes: Options("deliveryTypes"),
            CmsLocations: Names("cmsLocations"),
            RequiredDocuments: Of("requiredDocuments").Select(e => Attrs(e, a => new RequiredDocumentGroup(e.Name, Strs(a, "items"), Strs(a, "notes")))).ToList(),
            IdentificationNotes: Names("identificationNotes"),
            DashboardNotes: Names("dashboardNotes"),
            Declarations: Names("declarations"),
            NoticeKinds: Names("noticeKinds"),
            RenewalNotes: Names("renewalNotes"),
            Features: features);
    }

    // ----- An entry's attributes, j_Attrs -------------------------------------------

    private static T Attrs<T>(Entry e, Func<JsonElement, T> read)
    {
        using var doc = JsonDocument.Parse(e.Attrs ?? "{}");
        return read(doc.RootElement);
    }

    private static bool Bool(JsonElement a, string name) => a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static int Int(JsonElement a, string name) => a.TryGetProperty(name, out var v) && v.TryGetInt32(out var n) ? n : 0;

    private static string? Str(JsonElement a, string name) => a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static List<string> Strs(JsonElement a, string name) =>
        a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];
}
