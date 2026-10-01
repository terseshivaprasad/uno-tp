using UnoTP.Models;

namespace UnoTP.ViewModels;

/// <summary>What the shared document card (Views/Shared/_DocSlot.cshtml) draws.</summary>
/// <param name="View">The slot: whether it is asked for, what is in it, and what can be done with it.</param>
/// <param name="UploadUrl">Where the slot's Upload button posts.</param>
/// <param name="Form">The id of the form the card posts with, when the card stands outside that form.</param>
/// <param name="Alternate">
/// The card as another choice would leave it, drawn hidden beside the real card so the page can swap
/// it in the moment the choice changes. It carries no id, so the real card keeps its anchor.
/// </param>
/// <param name="IsNext">The card holding the next thing to do; it is highlighted.</param>
/// <param name="Label">
/// The label at the left of the card's header. Null when something is chosen above the card (a proof
/// type picker, the payment mode): the toolbar then has the header line to itself.
/// </param>
/// <param name="LabelOptional">Adds "(Non-mandatory)" after the label.</param>
/// <param name="ShowsProofType">The identified type of a proof is shown after the label (see _ProofType).</param>
/// <param name="ProofType">The identified type; "" while it will be set from the upload; null when the proof is not asked for.</param>
public sealed record DocSlotBlock(
    DocumentsViewModel.SlotView View,
    string UploadUrl,
    string? Form = null,
    bool Alternate = false,
    bool IsNext = false,
    string? Label = null,
    bool LabelOptional = false,
    bool ShowsProofType = false,
    string? ProofType = null);

/// <summary>
/// The tools at the right of a document card's header, worked out from the slot's state:
/// the text behind the (i) mark, whether View can open the copy, and what the action
/// button says and why it may be blocked. What cannot be done now is shown disabled with
/// its reason, never left out, so the toolbar keeps its shape as the card changes.
/// </summary>
public sealed class SlotToolbar
{
    /// <summary>What the (i) mark says: the checks the copy went through, or why the slot is not used.</summary>
    public string Info { get; private set; } = "";

    /// <summary>"is-warn" or "is-bad" when a filed copy's checks warned or failed; otherwise null.</summary>
    public string? InfoTone { get; private set; }

    /// <summary>Whether View can open the copy (there is a copy on this application to show).</summary>
    public bool CanView { get; private set; }

    /// <summary>Why View is disabled.</summary>
    public string NoViewReason { get; private set; } = "";

    /// <summary>What the action button says: Upload, Replace, Newer proof, Final or No tries left.</summary>
    public string ActionLabel { get; private set; } = "Upload";

    /// <summary>Why the action cannot be done now, or null when it can.</summary>
    public string? ActionBlockedReason { get; private set; }

    /// <summary>An address the folio already holds, standing in for a proof that is not filed. Null otherwise.</summary>
    public ReadCard? Held { get; private set; }

    public static SlotToolbar For(DocumentsViewModel.SlotView slot)
    {
        var toolbar = new SlotToolbar();
        var filed = slot.Doc is not null;
        var held = filed ? null : slot.Read;
        toolbar.Held = held;

        // The (i) mark.
        if (!slot.Used)
        {
            toolbar.Info = slot.NotApplicable ?? "Not asked for on this application.";
        }
        else if (filed)
        {
            toolbar.Info = slot.Doc!.Check.Length > 0 ? slot.Doc.Check : "Filed.";
        }
        else if (held is not null)
        {
            toolbar.Info = $"{held.State}. {held.From}";
        }
        else
        {
            toolbar.Info = slot.With ?? DocumentsViewModel.NoCheckOf(slot.Def.Key);
        }

        if (filed && slot.Doc!.CheckKind == "warn") toolbar.InfoTone = "is-warn";
        if (filed && slot.Doc!.CheckKind == "bad") toolbar.InfoTone = "is-bad";

        // View.
        toolbar.CanView = filed && !slot.Doc!.Before;
        if (!slot.Used)
        {
            toolbar.NoViewReason = "Not applicable to this application.";
        }
        else if (filed)
        {
            toolbar.NoViewReason = "Filed before this step: there is no copy here to show.";
        }
        else if (held is not null)
        {
            toolbar.NoViewReason = "Held on record: there is no copy on this application to show.";
        }
        else
        {
            toolbar.NoViewReason = "Nothing uploaded yet.";
        }

        // The action, and why it may be blocked.
        if (!slot.Used)
        {
            toolbar.ActionLabel = "Upload";
            toolbar.ActionBlockedReason = slot.NotApplicable ?? "Not applicable to this application.";
        }
        else if (slot.Final is not null)
        {
            toolbar.ActionLabel = "Final";
            toolbar.ActionBlockedReason = slot.Final;
        }
        else if (slot.Spent)
        {
            toolbar.ActionLabel = "No tries left";
            toolbar.ActionBlockedReason = slot.Tries ?? "Refused too many times in a row: this document now goes to Operations.";
        }
        else if (filed)
        {
            toolbar.ActionLabel = "Replace";
        }
        else if (slot.Locked is not null)
        {
            toolbar.ActionLabel = "Upload";
            toolbar.ActionBlockedReason = slot.Locked;
        }
        else if (held is not null)
        {
            toolbar.ActionLabel = "Newer proof";
        }
        else
        {
            toolbar.ActionLabel = "Upload";
        }

        return toolbar;
    }
}
