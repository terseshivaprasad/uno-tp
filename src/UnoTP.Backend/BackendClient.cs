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
        IReferenceApi, IPartnerApi, IDepositApi, IDemoApi, ISessionApi
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

    public async Task<IReadOnlyList<BankBranch>> SearchBranchesAsync(string query, CancellationToken ct = default) =>
        await List<BankBranch>($"ifsc?q={Seg(query)}", ct);

    public Task<DemoCases?> CasesAsync(CancellationToken ct = default) =>
        Get<DemoCases>("demo/cases", ct);

    public Task<DemoBanks?> BanksAsync(CancellationToken ct = default) =>
        Get<DemoBanks>("demo/banks", ct);

    // ----- Entry -------------------------------------------------------------

    // A refused user or system code is no session, not a failure.
    public async Task<UserSession?> StartAsync(string userId, string sysCode, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Post, "sessions", Body(new { userId, sysCode }));
        using var response = await SendAsync(request, ct);
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await Read<UserSession>(response, ct);
    }

    public async Task<IReadOnlyList<MenuItem>> MenuAsync(CancellationToken ct = default) =>
        await List<MenuItem>("menu", ct);

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

    public async Task<bool> SavePageAsync(string appNo, string page, string state, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Put, $"applications/{Seg(appNo)}/pages/{Seg(page)}", Body(new { state }));
        using var response = await SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    public Task<int?> SaveDetailsAsync(string appNo, int version, ApplicationDetails details, CancellationToken ct = default) =>
        PutVersioned($"applications/{Seg(appNo)}/details", version, details, ct);

    public Task<int?> SavePaymentAsync(string appNo, int version, PaymentDetails payment, CancellationToken ct = default) =>
        PutVersioned($"applications/{Seg(appNo)}/payment", version, payment, ct);

    public Task<int?> SaveDepositAsync(string appNo, int version, DepositDetails deposit, CancellationToken ct = default) =>
        PutVersioned($"applications/{Seg(appNo)}/deposit", version, deposit, ct);

    public async Task<Application?> SubmitAsync(string appNo, int version, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Post, $"applications/{Seg(appNo)}/submit", Body(new { }));
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version}\""));
        using var response = await SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed) return null;
        response.EnsureSuccessStatusCode();
        return await Read<Application>(response, ct);
    }

    public async Task<Submission?> ResendLinkAsync(string appNo, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Post, $"applications/{Seg(appNo)}/resend-link", Body(new { }));
        using var response = await SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict) return null;
        response.EnsureSuccessStatusCode();
        return await Read<Submission>(response, ct);
    }

    // A part of the application saved against the version it was read at: the new
    // version, or null when it changed in between and nothing was saved.
    private async Task<int?> PutVersioned(string path, int version, object body, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Put, path, Body(body));
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

    public Task<SlipRecord?> GenerateAsync(string appNo, CancellationToken ct = default) =>
        SendOrNull<SlipRecord>(HttpMethod.Post, $"payin-slips/{Seg(appNo)}", Body(new { }), ct);

    public Task<SentLinkRecord?> SendAsync(string appNo, string purpose, CancellationToken ct = default) =>
        SendOrNull<SentLinkRecord>(HttpMethod.Post, "links", Body(new { appNo, purpose }), ct);

    // An action the backend may turn down: its answer, or null for 404, 409 or 422.
    private async Task<T?> SendOrNull<T>(HttpMethod method, string path, HttpContent content, CancellationToken ct) where T : class
    {
        using var request = Request(method, path, content);
        using var response = await SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity) return null;
        response.EnsureSuccessStatusCode();
        return await Read<T>(response, ct);
    }

    public async Task<IReadOnlyList<SentLinkRecord>> SentAsync(CancellationToken ct = default) =>
        await List<SentLinkRecord>("links", ct);

    public async Task<IReadOnlyList<PendingRecord>> PendingAsync(CancellationToken ct = default) =>
        await List<PendingRecord>("links/pending", ct);

    public async Task<ConsoleSchedule> ScheduleAsync(CancellationToken ct = default) =>
        await Get<ConsoleSchedule>("console/schedule", ct) ?? new ConsoleSchedule([], []);

    public Task<WindowRecord> AddWindowAsync(NewWindow window, CancellationToken ct = default) =>
        Send<WindowRecord>(HttpMethod.Post, "console/windows", Body(window), ct);

    public Task<AnnouncementRecord> AddAnnouncementAsync(NewAnnouncement announcement, CancellationToken ct = default) =>
        Send<AnnouncementRecord>(HttpMethod.Post, "console/announcements", Body(announcement), ct);

    public Task<bool> EndWindowAsync(string id, CancellationToken ct = default) =>
        Done(HttpMethod.Post, $"console/windows/{Seg(id)}/end", Body(new { }), ct);

    public Task<bool> RemoveAnnouncementAsync(string id, CancellationToken ct = default) =>
        Done(HttpMethod.Delete, $"console/announcements/{Seg(id)}", null, ct);

    // An action with nothing to return: false when there was nothing to act on.
    private async Task<bool> Done(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using var request = Request(method, path, content);
        using var response = await SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }
}
