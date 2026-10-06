using System.Globalization;

namespace UnoTP.Models;

/// <summary>
/// One deposit the folio check looks at: a deposit that is not cancelled, on a folio
/// still in use, with its first holder's PAN and date of birth.
/// </summary>
/// <param name="Dob">dd-MM-yyyy, or empty when the deposit holds no date of birth.</param>
public sealed record FolioDeposit(string Folio, string Pan, string Dob);

/// <summary>
/// The FD system's folio check, in its own order and its own words: whether an
/// investor searched for already has a folio, and what stops a deposit being booked
/// on it. The deposits it is given are the ones the investor is the first holder of.
/// </summary>
public static class FolioCheck
{
    public const string NonIndividual = Messages.InvestorIdentification.NonIndividual;
    public const string NoDob = Messages.InvestorIdentification.NoDobOnFolio;
    public const string DobMismatch = Messages.InvestorIdentification.DobDoesNotMatchFolio;
    public const string ManyFolios = Messages.InvestorIdentification.ManyFolios;
    public const string ExistingHolder = Messages.InvestorIdentification.ExistingHolder;

    /// <summary>What a check found.</summary>
    /// <param name="Folio">The folio found; empty when the investor has none.</param>
    /// <param name="Problem">What stops the folio being used; null when nothing does.</param>
    /// <param name="AboutDob">The problem is with the date of birth, not the PAN.</param>
    /// <param name="Folios">Every folio found, when the problem is that there is more than one.</param>
    public sealed record Answer(string Folio, string? Problem = null, bool AboutDob = false, IReadOnlyList<string>? Folios = null);

    /// <summary>A search by PAN and date of birth (dd-MM-yyyy), with the deposits the PAN is the first holder of.</summary>
    /// <param name="folioOnMaster">The folio the folio master holds the PAN as first holder of, where it has no deposit; empty for none.</param>
    public static Answer ByPan(string pan, string dob, IReadOnlyList<FolioDeposit> deposits, DateOnly today, string folioOnMaster = "")
    {
        // A PAN that is not a person's is not allowed, with a folio or without.
        if (!IsIndividual(pan)) return new Answer(deposits.Count > 0 ? deposits[0].Folio : "", NonIndividual);

        // No deposit against the PAN, but the folio master holds them as a first
        // holder: an existing holder, who is not taken as new.
        if (deposits.Count == 0 && folioOnMaster.Length > 0) return new Answer(folioOnMaster, ExistingHolder);

        // No folio against the PAN: a new investor.
        if (deposits.Count == 0) return new Answer("");

        var folio = deposits[0].Folio;
        if (deposits.All(d => d.Dob.Length == 0)) return new Answer(folio, NoDob, AboutDob: true);

        var matching = deposits.FirstOrDefault(d => d.Dob == dob);
        if (matching is null) return new Answer(folio, DobMismatch, AboutDob: true);

        var adultFolios = FoliosOfAdults(deposits, today);
        if (adultFolios.Count > 1) return new Answer(folio, ManyFolios, Folios: adultFolios);

        return new Answer(matching.Folio);
    }

    /// <summary>
    /// A search by folio number. Nothing was typed to match a date of birth with, so
    /// the folio only has to hold one; and its PAN must not be on another folio too.
    /// </summary>
    /// <param name="deposits">The folio's own deposits.</param>
    /// <param name="ofPan">Every deposit the folio's PAN is the first holder of, on this folio or another.</param>
    public static Answer ByFolio(IReadOnlyList<FolioDeposit> deposits, IReadOnlyList<FolioDeposit> ofPan, DateOnly today)
    {
        if (deposits.Count == 0) return new Answer("");

        var folio = deposits[0].Folio;
        if (!IsIndividual(deposits[0].Pan)) return new Answer(folio, NonIndividual);
        if (deposits.All(d => d.Dob.Length == 0)) return new Answer(folio, NoDob, AboutDob: true);

        var adultFolios = FoliosOfAdults(ofPan, today);
        if (adultFolios.Count > 1) return new Answer(folio, ManyFolios, Folios: adultFolios);

        return new Answer(folio);
    }

    // The folios among the deposits whose holder is an adult, each once.
    private static List<string> FoliosOfAdults(IReadOnlyList<FolioDeposit> deposits, DateOnly today) =>
        deposits.Where(d => IsAdult(d.Dob, today)).Select(d => d.Folio).Distinct().ToList();

    // The fourth letter of a PAN says what its holder is: P is a person.
    private static bool IsIndividual(string pan) => pan.Length >= 4 && char.ToUpperInvariant(pan[3]) == 'P';

    // Over 18 in completed years, as the FD system's check counts an adult.
    private static bool IsAdult(string dob, DateOnly today)
    {
        if (!DateOnly.TryParseExact(dob, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var born)) return false;
        var age = today.Year - born.Year;
        if (born > today.AddYears(-age)) age--;
        return age > 18;
    }
}
