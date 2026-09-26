namespace UnoTP.ViewModels;

/// <summary>
/// What the shared document slot (Views/Shared/_DocSlot.cshtml) draws: the slot,
/// where its Upload posts, and the form it posts with when it stands outside one.
/// An alternate is the slot as another choice would leave it, drawn hidden beside
/// the slot as it stands; it carries no id, so the page's own slot keeps its anchor.
/// </summary>
public sealed record DocSlotBlock(UploadDocumentsViewModel.SlotView View, string UploadUrl, string? Form = null, bool Alternate = false);
