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

    // Only ever the partner's own application.
    public Task<Application?> FindAsync(string appNo, CancellationToken ct = default) =>
        Task.FromResult(store.Get(partner.Id, appNo));

    public Task<bool> SavePageAsync(string appNo, string page, string state, CancellationToken ct = default) =>
        Task.FromResult(store.Touch(partner.Id, appNo, app => app.Pages[page] = state));

    public Task<int?> SaveUploadAsync(string appNo, int version, UploadState upload, CancellationToken ct = default) =>
        Task.FromResult(store.SaveUpload(partner.Id, appNo, version, upload));

    public Task<int?> SaveDetailsAsync(string appNo, int version, ApplicationDetails details, CancellationToken ct = default) =>
        Task.FromResult(store.Save(partner.Id, appNo, version, app => app.Details = details)?.Version);

    public Task<int?> SavePaymentAsync(string appNo, int version, PaymentDetails payment, CancellationToken ct = default) =>
        Task.FromResult(store.Save(partner.Id, appNo, version, app => app.Payment = payment)?.Version);

    public Task<int?> SaveDepositAsync(string appNo, int version, DepositDetails deposit, CancellationToken ct = default) =>
        Task.FromResult(store.Save(partner.Id, appNo, version, app => app.Deposit = deposit)?.Version);

    // Submitting sends the investor the payment link, to the mobile number and the
    // e-mail on the investor's details, open for the hours the rules give a payment link.
    public Task<Application?> SubmitAsync(string appNo, int version, CancellationToken ct = default) =>
        Task.FromResult(store.Save(partner.Id, appNo, version, app =>
        {
            var investor = app.Details?.Holders.FirstOrDefault(h => h.Holder == HolderType.Investor);
            var now = DateTime.Now;
            app.Submitted = new Submission(now, "payment-pending", Masks.Mobile(investor?.Mobile ?? ""),
                now.AddHours(MockReference.PaymentLinkHours), ResendsLeft: 1, LinkEmailedTo: Masks.Email(investor?.Email ?? ""));
        }));

    // One resend, which does not move the link's expiry.
    public Task<Submission?> ResendLinkAsync(string appNo, CancellationToken ct = default)
    {
        if (store.Get(partner.Id, appNo) is not { Submitted: { ResendsLeft: > 0 } } app) return Task.FromResult<Submission?>(null);
        return Task.FromResult(store.Save(partner.Id, appNo, app.Version, a => a.Submitted = a.Submitted! with { ResendsLeft = a.Submitted.ResendsLeft - 1 })?.Submitted);
    }

    // The partner's own applications, opened here and not yet submitted - nothing
    // made up - with the identifiers masked as the backend masks them.
    public Task<IReadOnlyList<DraftSummary>> DraftsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DraftSummary>>(store.List(partner.Id)
            .Where(a => a.Submitted is null)
            .Select(a => new DraftSummary(a.AppNo, Masks.Name(a.Holder.Name), Masks.Pan(a.Holder.Pan), Masks.Dob(a.Holder.Dob), a.Deposit?.Amount ?? 0))
            .ToList());

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

/// <summary>How the backend masks where a link went.</summary>
internal static class Masks
{
    /// <summary>9876543210 as ••••••3210.</summary>
    public static string Mobile(string mobile) =>
        mobile.Length >= 4 ? new string('•', mobile.Length - 4) + mobile[^4..] : mobile;

    /// <summary>RAHUL S TERSE as R•••• S T••••.</summary>
    public static string Name(string name) =>
        string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w.Length <= 1 ? w : w[0] + new string('•', w.Length - 1)));

    /// <summary>ABCPT1234Q as ABCPT••••Q.</summary>
    public static string Pan(string pan) =>
        pan.Length == 10 ? pan[..5] + "••••" + pan[^1] : pan;

    /// <summary>14-08-1988 as ••/••/1988.</summary>
    public static string Dob(string dob) =>
        dob.Length >= 4 ? "••/••/" + dob[^4..] : dob;

    /// <summary>investor@example.com as in••••@example.com.</summary>
    public static string Email(string email) =>
        email.IndexOf('@') is > 0 and var at ? email[..Math.Min(2, at)].ToLowerInvariant() + "••••" + email[at..].ToLowerInvariant() : "";
}
