using UnoTP.Backend.Mock.External;

namespace UnoTP.Backend.Mock;

/// <summary>
/// The mock backend's applications, and the documents filed against them, for the
/// partner asking.
/// </summary>
public sealed class MockApplications(MockStore store, IPartner partner) : IApplicationApi, IDocumentApi
{
    // ----- Applications ------------------------------------------------------

    // Every application opened gets a number of its own: the same PAN opened twice
    // is two applications.
    public Task<Application> OpenAsync(Holder holder, CancellationToken ct = default)
    {
        Application app;
        do app = New(NewAppNo(), holder);
        while (!store.TryAdd(partner.Id, app));
        return Task.FromResult(app);
    }

    // A draft is opened for whoever picks it up first; after that it is theirs.
    public Task<Application?> FindAsync(string appNo, CancellationToken ct = default)
    {
        var draft = MockInFlight.Drafts.FirstOrDefault(d => d.Summary.AppNo == appNo);
        return Task.FromResult(draft.Holder is null
            ? store.Get(partner.Id, appNo)
            : store.GetOrAdd(partner.Id, appNo, () => New(appNo, draft.Holder)));
    }

    public Task<int?> SaveUploadAsync(string appNo, int version, UploadState upload, CancellationToken ct = default) =>
        Task.FromResult(store.SaveUpload(partner.Id, appNo, version, upload));

    public Task<int?> SaveDetailsAsync(string appNo, int version, ApplicationDetails details, CancellationToken ct = default) =>
        Task.FromResult(store.Save(partner.Id, appNo, version, app => app.Details = details)?.Version);

    public Task<int?> SavePaymentAsync(string appNo, int version, PaymentDetails payment, CancellationToken ct = default) =>
        Task.FromResult(store.Save(partner.Id, appNo, version, app => app.Payment = payment)?.Version);

    public Task<int?> SaveDepositAsync(string appNo, int version, DepositDetails deposit, CancellationToken ct = default) =>
        Task.FromResult(store.Save(partner.Id, appNo, version, app => app.Deposit = deposit)?.Version);

    // Submitting sends the investor the payment link, to the mobile number on the
    // investor's details, open for the hours the rules give a payment link.
    public Task<Application?> SubmitAsync(string appNo, int version, CancellationToken ct = default) =>
        Task.FromResult(store.Save(partner.Id, appNo, version, app =>
        {
            var mobile = app.Details?.Holders.FirstOrDefault(h => h.Holder == HolderType.Investor)?.Mobile ?? "";
            var masked = mobile.Length >= 4 ? new string('•', mobile.Length - 4) + mobile[^4..] : mobile;
            var now = DateTime.Now;
            app.Submitted = new Submission(now, "payment-pending", masked, now.AddHours(MockReference.PaymentLinkHours));
        }));

    public Task<IReadOnlyList<DraftSummary>> DraftsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DraftSummary>>(MockInFlight.Drafts.Select(d => d.Summary).ToList());

    public Task<IReadOnlyList<ApplicationRecord>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ApplicationRecord>>(MockApplicationList.Build());

    private static Application New(string appNo, Holder holder) =>
        new() { AppNo = appNo, Holder = holder, Prior = PriorAttempts(holder).ToList() };

    // Same shape as the application numbers the console lists.
    private static string NewAppNo()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ0123456789";
        return "FBBMFL26F" + new string(Enumerable.Range(0, 6).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
    }

    // The attempts on record before the upload step was opened: the PAN that came
    // back as someone else's, the copy that stood, and a bill with no register
    // behind it.
    private static IEnumerable<LogEntry> PriorAttempts(Holder holder)
    {
        var bill = new LogEntry("past-3", "POA", 1, "10:47:51", "electricity_bill_aug.pdf · 1.2 MB");
        bill.Add("Identified as a proof of address.", "ok");
        bill.Add("OCR read: " + MockScans.Address);
        bill.Add("A utility bill has no register behind it to put that address to.", "warn");
        bill.End("Filed, address unchanged", "warn");
        yield return bill;

        if (holder.PanFiled)
        {
            var good = new LogEntry("past-2", "PAN copy", 2, "10:43:18", "PAN_front.jpg · 412 KB");
            good.Add("Identified as a PAN card.", "ok");
            good.Add($"OCR read: PAN {holder.Pan} · {holder.Name}");
            good.Add("NSDL confirmed the PAN, the name and the date of birth.", "ok");
            good.End("Filed", "ok");
            yield return good;
        }

        var bad = new LogEntry("past-1", "PAN copy", 1, "10:42:05", "scan_0417.jpg · 386 KB");
        bad.Add("Not identified as a PAN card.", "bad");
        bad.Add($"Copy kept for analysis as REJ-884199 until {InAWeek:dd MMM yyyy}, and deleted after.", "warn");
        bad.End("Refused", "bad");
        yield return bad;
    }

    // ----- Documents ---------------------------------------------------------

    public Task FileAsync(string appNo, string holder, string slot, UploadFile file, CancellationToken ct = default)
    {
        store.File(partner.Id, appNo, holder + "/" + slot, file);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string appNo, string holder, string slot, CancellationToken ct = default)
    {
        store.Delete(partner.Id, appNo, holder + "/" + slot);
        return Task.CompletedTask;
    }

    // Kept aside for a week under a reference of its own; the mock keeps nothing.
    public Task<RefusedCopy> KeepRefusedAsync(string appNo, string holder, string slot, UploadFile file, CancellationToken ct = default) =>
        Task.FromResult(new RefusedCopy(store.NextRejectRef(), InAWeek));

    public Task<UploadFile?> CopyAsync(string appNo, string holder, string slot, CancellationToken ct = default) =>
        Task.FromResult(store.Copy(partner.Id, appNo, holder + "/" + slot));

    private static DateTime InAWeek => DateTime.Today.AddDays(7);
}
