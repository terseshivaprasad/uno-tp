namespace UnoTp.Features;

public enum ConsentState { Accepted, AwaitingUpload, NotStarted }

/// <summary>One consent the holder has to give, offline or digitally.</summary>
public sealed record ConsentForm(
    string Key,
    string Title,
    string ShortName,
    string Purpose,
    ConsentState State,
    string? FileDetail = null)
{
    public string StatusLabel => State switch
    {
        ConsentState.Accepted => "Uploaded",
        ConsentState.AwaitingUpload => "Awaiting upload",
        _ => "Not started",
    };

    public string StatusChipClass => State switch
    {
        ConsentState.Accepted => "chip chip--success",
        ConsentState.AwaitingUpload => "chip chip--warn",
        _ => "chip chip--muted",
    };
}

/// <summary>
/// Which consents this application collects, and every sentence the consent step
/// says about them. Both the count and the prose come from <see cref="Forms"/>, so
/// switching a consent feature off can never leave the screen claiming "2 forms"
/// or "both must be accepted" when only one is on.
/// </summary>
public sealed class ConsentPlan
{
    public ConsentPlan(FeatureSet features)
    {
        var forms = new List<ConsentForm>();

        if (features.Flags.DpdpConsent)
        {
            forms.Add(new ConsentForm(
                "dpdp",
                "DPDP consent",
                "DPDP",
                "Consent to process the holder’s personal data.",
                ConsentState.Accepted,
                "dpdp-holder2.pdf · 1.1 MB · accepted 11:14"));
        }

        if (features.Flags.CkycConsent)
        {
            forms.Add(new ConsentForm(
                "ckyc",
                "CKYC (CERSAI) consent",
                "CERSAI",
                "Authorises the CKYC record download from CERSAI — the download itself runs at application completion.",
                ConsentState.AwaitingUpload,
                "Form downloaded 11:05 — signed copy not uploaded yet."));
        }

        Forms = forms;
        DigitalAvailable = features.Flags.DigitalConsent;
    }

    public IReadOnlyList<ConsentForm> Forms { get; }
    public bool DigitalAvailable { get; }

    public bool HasDpdp => Forms.Any(f => f.Key == "dpdp");
    public bool HasCkyc => Forms.Any(f => f.Key == "ckyc");

    public int Required => Forms.Count;
    public int Accepted => Forms.Count(f => f.State == ConsentState.Accepted);

    /// <summary>False when every consent feature is off - the step has nothing to collect.</summary>
    public bool AnyRequired => Required > 0;

    public bool OtpUnlocked => AnyRequired && Accepted == Required;

    // ----- Copy derived from the list above -----

    public string FormsChip => $"{Required} form{Plural} + OTP";

    /// <summary>"two signed forms and a consent OTP" - also used on the holder rows.</summary>
    public string OfflineSummary => $"{Spell(Required)} signed form{Plural} and a consent OTP";

    public string SeparateFormsNote => Required > 1
        ? "Each consent is a separate form with its own download and its own upload. Both must be accepted before the consent OTP is sent. No CKYC record is pulled here."
        : "The consent is a single form with its own download and upload. It must be accepted before the consent OTP is sent. No CKYC record is pulled here.";

    public string OtpHint => Required > 1
        ? $"Consent OTP unlocks when both forms are accepted — {Accepted} of {Required} so far."
        : $"Consent OTP unlocks when the form is accepted — {Accepted} of {Required} so far.";

    /// <summary>"DPDP and CERSAI", or just "DPDP" when CKYC consent is off.</summary>
    public string LinkCovers => Join(Forms.Select(f => f.ShortName));

    public string DigitalNote =>
        $"One link covering {LinkCovers} goes to the registered mobile and email. The holder accepts it — no upload, no consent OTP." +
        (HasCkyc ? " The CKYC download and its OTP come at application completion." : string.Empty);

    public string FooterHint => Required > 1
        ? $"Holder 2 · both signed forms must be accepted before the consent OTP is sent — <strong>{Accepted} of {Required} accepted</strong>"
        : $"Holder 2 · the signed form must be accepted before the consent OTP is sent — <strong>{Accepted} of {Required} accepted</strong>";

    private string Plural => Required == 1 ? string.Empty : "s";

    private static string Spell(int n) => n switch
    {
        0 => "no",
        1 => "one",
        2 => "two",
        3 => "three",
        _ => n.ToString(),
    };

    /// <summary>"A", "A and B", "A, B and C".</summary>
    private static string Join(IEnumerable<string> parts)
    {
        var list = parts.ToList();
        return list.Count switch
        {
            0 => "nothing",
            1 => list[0],
            _ => string.Join(", ", list.Take(list.Count - 1)) + " and " + list[^1],
        };
    }
}
