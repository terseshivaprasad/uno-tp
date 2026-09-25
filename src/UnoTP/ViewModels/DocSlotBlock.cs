namespace UnoTP.ViewModels;

/// <summary>
/// What the shared document slot (Views/Shared/_DocSlot.cshtml) draws: the slot,
/// where its Upload posts, and the form it posts with when it stands outside one.
/// </summary>
/// <param name="ProofType">For a proof of address, the type shown beside the card's
/// label: what the copy was identified as, "" while that is still to come from the
/// upload, or null for no tag at all.</param>
public sealed record DocSlotBlock(UploadDocumentsViewModel.SlotView View, string UploadUrl, string? Form = null, string? ProofType = null)
{
    /// <summary>What the card is called at its top left.</summary>
    public string Heading => View.Def.Key switch
    {
        "form" => "Application form",
        "pan" => "PAN",
        "photo" => "Photo",
        "poa" => "Proof of address",
        "mail" => "Communication address proof",
        "payment" => "Payment instrument",
        "empproof" => "Employee proof",
        _ => View.Def.Label,
    };
}
