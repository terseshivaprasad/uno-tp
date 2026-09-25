using UnoTP.Backend;
using UnoTP.Backend.External;
using Stage = UnoTP.ViewModels.InvestorIdentificationViewModel.Stage;

namespace UnoTP.ViewModels;

/// <summary>What the search fields post.</summary>
public sealed record SearchForm(string? By, string? Pan, string? Dd, string? Mm, string? Yyyy, string? Folio);

/// <summary>
/// One holder's search as the session holds it between a post and the page after it.
/// <see cref="Checked"/> is false while the fields are being filled in or corrected;
/// <see cref="Ocr"/> is the name OCR read off the PAN copy, and <see cref="Tried"/>
/// the name last typed for NSDL, null while it is OCR's reading that stands. The two
/// errors are said once and then dropped. <see cref="Added"/> is set once a joint
/// holder who was identified is added to the application.
/// </summary>
public sealed record SearchState(
    string By, string? Pan, string? Dd, string? Mm, string? Yyyy, string? Folio,
    bool Checked, string? CopyName = null, string? Ocr = null, string? Tried = null,
    string? CopyError = null, string? NameError = null, bool Added = false);

/// <summary>
/// Investor Identification's workflow, step by step, for any holder: the primary on
/// Investor Identification, and each joint holder on Investor Information. The
/// register is asked for the PAN and date of birth or the folio; a PAN with no folio
/// is established from its PAN copy, read by OCR and put to NSDL. Each step takes the
/// holder's search as the session holds it and gives back what to hold next.
/// </summary>
public sealed class HolderSearch(IInvestorApi investors, INsdlService nsdl, IOcrService ocr)
{
    public InvestorIdentificationViewModel NewModel() => new(investors, nsdl);

    /// <summary>
    /// The holder as the session left them: blank, being filled in, or checked. Gives
    /// back the search to hold from now on, with the errors that were said once gone.
    /// </summary>
    public async Task<SearchState?> ShowAsync(InvestorIdentificationViewModel model, SearchState? saved)
    {
        if (saved is null) return null;
        model.Fill(saved.By, saved.Pan, saved.Dd, saved.Mm, saved.Yyyy, saved.Folio);
        if (!saved.Checked) return saved;

        await model.CheckAsync();
        if (model.At == Stage.Pending && saved.CopyName is not null)
        {
            model.CopyName = saved.CopyName;
            model.NsdlName = saved.Tried;
            await model.RunNsdlAsync(saved.Tried ?? saved.Ocr ?? "", typed: saved.Tried is not null);
        }
        model.CopyError = saved.CopyError;
        model.NameError = saved.NameError ?? model.NameError;
        // Said once: a reload after it shows the page as it stands.
        return saved.CopyError is not null || saved.NameError is not null
            ? saved with { CopyError = null, NameError = null }
            : saved;
    }

    /// <summary>Check record: what was typed is kept, and the page checks it.</summary>
    public static SearchState Check(SearchForm form) =>
        new(form.By == "folio" ? "folio" : "pan", form.Pan, form.Dd, form.Mm, form.Yyyy, form.Folio, Checked: true);

    /// <summary>Search again: back to the fields, still holding what was typed.</summary>
    public static SearchState? Again(SearchState? saved) =>
        saved is null ? null : saved with { Checked = false, CopyName = null, Tried = null, CopyError = null, NameError = null, Added = false };

    /// <summary>
    /// The PAN copy is checked here for what it is as a file, and then read by OCR for
    /// the name on it; the file itself goes no further than that.
    /// </summary>
    public async Task<SearchState?> VerifyAsync(SearchState? saved, IFormFile? copy)
    {
        var model = NewModel();
        if (!await RestoreAsync(model, saved) || model.At != Stage.Pending) return saved;
        string? problem = null;
        byte[] bytes = [];
        if (copy is null || copy.Length == 0) problem = "Choose the PAN copy to upload";
        else if (Path.GetExtension(copy.FileName).ToLowerInvariant() is not (".pdf" or ".jpg" or ".jpeg"))
            problem = "Upload the PAN copy as a PDF or a JPEG";
        else
        {
            bytes = await UploadDocumentsViewModel.ReadAllAsync(copy);
            if (!UploadDocumentsViewModel.LooksLike(bytes, copy)) problem = "That file is not a readable PDF or JPEG";
        }
        if (problem is not null) return saved! with { CopyError = problem };

        var name = Path.GetFileName(copy!.FileName);
        try
        {
            var reading = await ocr.ReadAsync(DocumentKind.PanCard, "", new UploadFile(name, copy.ContentType, bytes),
                new OcrSubject(model.Pan!, model.Dob, ""), consent: false);
            return saved! with { CopyName = name, Ocr = reading.Name, Tried = null, CopyError = null };
        }
        catch (ExternalServiceException e)
        {
            return saved! with { CopyError = e.Message };
        }
    }

    /// <summary>The name typed from the PAN card, put to NSDL in place of OCR's reading.</summary>
    public async Task<SearchState?> RetryAsync(SearchState? saved, string? typedName)
    {
        var model = NewModel();
        if (!await RestoreAsync(model, saved) || model.At != Stage.Pending || saved!.CopyName is null) return saved;
        var typed = InvestorIdentificationViewModel.NormaliseName(typedName);
        return typed.Length < 3
            ? saved with { NameError = "Enter the name as printed on the PAN" }
            : saved with { Tried = typed, NameError = null };
    }

    /// <summary>
    /// The holder, checked again from the session before they go on: a record found,
    /// or a PAN that NSDL still matches against the name it was established with.
    /// Null unless they are identified.
    /// </summary>
    public async Task<InvestorIdentificationViewModel?> IdentifiedAsync(SearchState? saved)
    {
        var model = NewModel();
        if (!await RestoreAsync(model, saved)) return null;
        if (model.At == Stage.Pending && saved!.CopyName is not null)
        {
            model.CopyName = saved.CopyName;
            await model.RunNsdlAsync(saved.Tried ?? saved.Ocr ?? "", typed: saved.Tried is not null);
        }
        return model.Identified && model.Record is not null ? model : null;
    }

    /// <summary>
    /// A joint holder found by their search, checked again from the session: a folio
    /// the register holds, or a PAN with no folio - which is put to NSDL once their
    /// PAN copy is filed, so they are added on it as it stands. Null otherwise.
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
