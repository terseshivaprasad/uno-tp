using UnoTP.Backend;

namespace UnoTP.ViewModels;

/// <summary>
/// The rate card as FD Configuration offers it at one amount: which tenures and
/// payouts are on the card, the row for a tenure and payout at the amount, and why
/// a tenure or payout is not offered at it. The amount is the one entered, or the
/// standing quoteAmount (₹50,000) before one is - at which every line of the chart
/// is open.
/// </summary>
public sealed class RateTable
{
    private readonly IReadOnlyList<RateOption> card;
    private readonly ReferenceData reference;

    public RateTable(IReadOnlyList<RateOption> card, ReferenceData reference, long amount)
    {
        this.card = card;
        this.reference = reference;
        Amount = amount;
    }

    /// <summary>The amount the table is drawn at.</summary>
    public long Amount { get; }

    /// <summary>The lists the card's tenures and payouts are read against.</summary>
    public ReferenceData Reference => reference;

    /// <summary>The tenures on the card, in the order the backend lists them.</summary>
    public IReadOnlyList<int> Tenures
    {
        get
        {
            var tenures = new List<int>();
            foreach (var tenure in reference.Tenures)
            {
                if (card.Any(row => row.TenureMonths == tenure)) tenures.Add(tenure);
            }
            return tenures;
        }
    }

    /// <summary>The payouts on the card, in the order the backend lists them.</summary>
    public IReadOnlyList<PayoutOption> Payouts
    {
        get
        {
            var payouts = new List<PayoutOption>();
            foreach (var payout in reference.Payouts)
            {
                if (card.Any(row => row.Payout == payout.Code)) payouts.Add(payout);
            }
            return payouts;
        }
    }

    /// <summary>The row for a tenure and payout at the amount, or null when none is offered.</summary>
    public RateOption? Row(int tenureMonths, string payout)
    {
        foreach (var row in card)
        {
            if (row.TenureMonths != tenureMonths) continue;
            if (row.Payout != payout) continue;
            if (!row.Offers(Amount)) continue;
            return row;
        }
        return null;
    }

    /// <summary>Why a tenure is not offered at the amount ("from ₹ 25,000"), or null when it is.</summary>
    public string? TenureBlockedReason(int tenureMonths)
    {
        var rows = card.Where(row => row.TenureMonths == tenureMonths).ToList();
        return BlockedReason(rows);
    }

    /// <summary>Why a payout is not offered at the amount ("from ₹ 50,000"), or null when it is.</summary>
    public string? PayoutBlockedReason(string payout)
    {
        var rows = card.Where(row => row.Payout == payout).ToList();
        return BlockedReason(rows);
    }

    // Open if any of the rows is offered at the amount; otherwise the nearest limit the amount is outside.
    private string? BlockedReason(List<RateOption> rows)
    {
        if (rows.Count == 0) return "not offered";
        if (rows.Any(row => row.Offers(Amount))) return null;

        var above = rows.Where(row => row.MinAmount > Amount).ToList();
        if (above.Count > 0) return "from " + Money.Rupees(above.Min(row => row.MinAmount));

        var below = rows.Where(row => row.MaxAmount is not null && row.MaxAmount < Amount).ToList();
        if (below.Count > 0) return "up to " + Money.Rupees(below.Max(row => row.MaxAmount!.Value));

        return "not offered";
    }
}
