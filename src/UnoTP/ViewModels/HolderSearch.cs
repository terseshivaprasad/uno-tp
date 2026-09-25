using UnoTP.Backend;
using UnoTP.Backend.External;
using Stage = UnoTP.ViewModels.InvestorIdentificationViewModel.Stage;

namespace UnoTP.ViewModels;

/// <summary>What the search fields post.</summary>
public sealed record SearchForm(string? By, string? Pan, string? Dd, string? Mm, string? Yyyy, string? Folio);

/// <summary>
/// One holder's search as the session holds it between a post and the page after it.
/// <see cref="Checked"/> is false while the fields are being filled in or corrected.
/// <see cref="Added"/> is set once a joint holder who was found is added to the application.
/// </summary>
public sealed record SearchState(
    string By, string? Pan, string? Dd, string? Mm, string? Yyyy, string? Folio,
    bool Checked, bool Added = false);

/// <summary>
/// Investor Identification's workflow, step by step, for any holder: the primary on
/// Investor Identification, and each joint holder on Investor Information. The
/// register is asked for the PAN and date of birth or the folio; a PAN with no folio
/// goes on as it stands, and is put to NSDL once its PAN copy is filed with the
/// holder's documents. Each step takes the
/// holder's search as the session holds it and gives back what to hold next.
/// </summary>
public sealed class HolderSearch(IInvestorApi investors, UnoTP.Features.Lookups lookups)
{
    public InvestorIdentificationViewModel NewModel() => new(investors, lookups);

    /// <summary>
    /// The holder as the session left them: blank, being filled in, or checked. Gives
    /// back the search to hold from now on.
    /// </summary>
    public async Task<SearchState?> ShowAsync(InvestorIdentificationViewModel model, SearchState? saved)
    {
        if (saved is null) return null;
        model.Fill(saved.By, saved.Pan, saved.Dd, saved.Mm, saved.Yyyy, saved.Folio);
        if (!saved.Checked) return saved;

        await model.CheckAsync();
        return saved;
    }

    /// <summary>Check record: what was typed is kept, and the page checks it.</summary>
    public static SearchState Check(SearchForm form) =>
        new(form.By == "folio" ? "folio" : "pan", form.Pan, form.Dd, form.Mm, form.Yyyy, form.Folio, Checked: true);

    /// <summary>Search again: back to the fields, still holding what was typed.</summary>
    public static SearchState? Again(SearchState? saved) =>
        saved is null ? null : saved with { Checked = false, Added = false };

    /// <summary>
    /// A holder found by their search, checked again from the session: a folio the
    /// register holds, or a PAN with no folio - which is put to NSDL once their PAN
    /// copy is filed, so they go on as it stands. Null otherwise. The investor
    /// proceeds on it, and a joint holder is added on it.
    /// </summary>
    public async Task<InvestorIdentificationViewModel?> FoundAsync(SearchState? saved)
    {
        var model = NewModel();
        if (!await RestoreAsync(model, saved)) return null;
        return model.At is Stage.Found or Stage.Pending && model.Record is not null ? model : null;
    }

    /// <summary>
    /// The holder an application carries, from the record that identified them. The
    /// PAN copy is on it already when the register holds one or the PAN was
    /// established from one; what the folio holds is carried only for a holder on one.
    /// </summary>
    public static Holder ApplicationHolder(InvestorIdentificationViewModel.Holder record)
    {
        var onFolio = record.Folio.Length > 0;
        return new Holder(record.Pan, record.Dob, record.Name, record.Folio, record.Docs.Pan,
            record.Address, onFolio ? record.Docs : null, Genders.Of(record.Gender));
    }

    // The search as the session holds it, checked again. False with nothing checked.
    private static async Task<bool> RestoreAsync(InvestorIdentificationViewModel model, SearchState? saved)
    {
        if (saved is not { Checked: true }) return false;
        model.Fill(saved.By, saved.Pan, saved.Dd, saved.Mm, saved.Yyyy, saved.Folio);
        return await model.CheckAsync();
    }
}
