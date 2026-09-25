using System.Net;
using System.Net.Http.Headers;

namespace UnoTP.Backend;

/// <summary>
/// The backend API over HTTP: the app's own data. Every route the app calls is
/// here, and nowhere else; docs/backend-api.md lists them for the backend team.
/// The outside services each have a client of their own (see the External folder).
/// </summary>
public sealed class BackendClient(HttpClient http, IPartner partner)
    : ApiClient(http, partner), IInvestorApi, IApplicationApi, IDocumentApi, ISourcingApi, IPayInSlipApi, ILinkApi, IConsoleApi,
        IReferenceApi, IPartnerApi, IDepositApi
{
    // ----- Reference, config and the partner -----------------------------------

    public async Task<ReferenceData> ReferenceAsync(CancellationToken ct = default) =>
        await Get<ReferenceData>("reference", ct) ?? throw new HttpRequestException("GET reference answered 404.");

    public async Task<AppConfig> ConfigAsync(CancellationToken ct = default) =>
        await Get<AppConfig>("config", ct) ?? throw new HttpRequestException("GET config answered 404.");

    public async Task<PartnerProfile> MeAsync(CancellationToken ct = default) =>
        await Get<PartnerProfile>("me", ct) ?? throw new HttpRequestException("GET me answered 404.");

    // ----- Deposits ------------------------------------------------------------

    public Task<DepositQuote> QuoteAsync(QuoteRequest request, CancellationToken ct = default) =>
        Send<DepositQuote>(HttpMethod.Post, "deposits/quote", Body(request), ct);

    public Task<BankBranch?> BranchAsync(string ifsc, CancellationToken ct = default) =>
        Get<BankBranch>($"ifsc/{Seg(ifsc)}", ct);

    // ----- Investors ---------------------------------------------------------

    public async Task<IReadOnlyList<FolioRecord>> FoliosByPanAsync(string pan, CancellationToken ct = default) =>
        await List<FolioRecord>($"investors/folios?pan={Seg(pan)}", ct);

    public Task<FolioRecord?> FolioAsync(string folio, CancellationToken ct = default) =>
        Get<FolioRecord>($"investors/folios/{Seg(folio)}", ct);

    // ----- Applications ------------------------------------------------------

    public Task<Application> OpenAsync(Holder holder, CancellationToken ct = default) =>
        Send<Application>(HttpMethod.Post, "applications", Body(new { holder }), ct);

    public Task<Application?> FindAsync(string appNo, CancellationToken ct = default) =>
        Get<Application>($"applications/{Seg(appNo)}", ct);

    public async Task<int?> SaveUploadAsync(string appNo, int version, UploadState upload, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Put, $"applications/{Seg(appNo)}/upload", Body(upload));
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version}\""));
        using var response = await SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed) return null;
        response.EnsureSuccessStatusCode();
        return (await Read<Saved>(response, ct)).Version;
    }

    public async Task<IReadOnlyList<DraftSummary>> DraftsAsync(CancellationToken ct = default) =>
        await List<DraftSummary>("applications/drafts", ct);

    public async Task<IReadOnlyList<ApplicationRecord>> ListAsync(CancellationToken ct = default) =>
        await List<ApplicationRecord>("applications", ct);

    private sealed record Saved(int Version);

    // ----- Documents ---------------------------------------------------------

    public Task FileAsync(string appNo, string holder, string slot, UploadFile file, CancellationToken ct = default) =>
        Send(HttpMethod.Post, Doc(appNo, holder, slot), Form(file), ct);

    // Deleting a copy that is not there is not a failure.
    public async Task DeleteAsync(string appNo, string holder, string slot, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Delete, Doc(appNo, holder, slot));
        using var response = await SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.NotFound) response.EnsureSuccessStatusCode();
    }

    public Task<RefusedCopy> KeepRefusedAsync(string appNo, string holder, string slot, UploadFile file, CancellationToken ct = default) =>
        Send<RefusedCopy>(HttpMethod.Post, Doc(appNo, holder, slot) + "/refused", Form(file), ct);

    public async Task<UploadFile?> CopyAsync(string appNo, string holder, string slot, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Get, Doc(appNo, holder, slot));
        using var response = await SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var content = response.Content;
        return new UploadFile(
            content.Headers.ContentDisposition?.FileName?.Trim('"') ?? slot,
            content.Headers.ContentType?.MediaType ?? "application/octet-stream",
            await content.ReadAsByteArrayAsync(ct));
    }

    private static string Doc(string appNo, string holder, string slot) =>
        $"applications/{Seg(appNo)}/documents/{Seg(holder)}/{Seg(slot)}";

    // ----- Registers ---------------------------------------------------------

    public async Task<IReadOnlyList<Party>> BrokersAsync(CancellationToken ct = default) =>
        await List<Party>("sourcing/brokers", ct);

    public async Task<IReadOnlyList<Party>> StaffAsync(CancellationToken ct = default) =>
        await List<Party>("sourcing/staff", ct);

    public async Task<IReadOnlyList<SlipRecord>> SlipsAsync(CancellationToken ct = default) =>
        await List<SlipRecord>("payin-slips", ct);

    public async Task<IReadOnlyList<SentLinkRecord>> SentAsync(CancellationToken ct = default) =>
        await List<SentLinkRecord>("links", ct);

    public async Task<IReadOnlyList<PendingRecord>> PendingAsync(CancellationToken ct = default) =>
        await List<PendingRecord>("links/pending", ct);

    public async Task<ConsoleSchedule> ScheduleAsync(CancellationToken ct = default) =>
        await Get<ConsoleSchedule>("console/schedule", ct) ?? new ConsoleSchedule([], []);
}
