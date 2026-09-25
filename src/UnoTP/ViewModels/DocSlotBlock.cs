namespace UnoTP.ViewModels;

/// <summary>
/// What the shared document slot (Views/Shared/_DocSlot.cshtml) draws: the slot,
/// where its Upload posts, and the form it posts with when it stands outside one.
/// </summary>
public sealed record DocSlotBlock(UploadDocumentsViewModel.SlotView View, string UploadUrl, string? Form = null);
