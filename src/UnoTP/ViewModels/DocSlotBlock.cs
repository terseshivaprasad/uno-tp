using Microsoft.AspNetCore.Html;

namespace UnoTP.ViewModels;

/// <summary>
/// What the shared document slot (Views/Shared/_DocSlot.cshtml) draws: the slot,
/// where its Upload posts, and the form it posts with when it stands outside one.
/// An alternate is the slot as another choice would leave it, drawn hidden beside
/// the slot as it stands; it carries no id, so the page's own slot keeps its anchor.
/// </summary>
/// <param name="IsNext">The card that holds the next thing to do: the one the eye should find first.</param>
/// <param name="Head">The slot's label, drawn on one line with its toolbar - the label at the left, the
/// tools at the right - as a card's header. Left out where something is chosen over the slot (a proof
/// type, a payment mode): the toolbar then has the line under that to itself.</param>
public sealed record DocSlotBlock(DocumentsViewModel.SlotView View, string UploadUrl, string? Form = null, bool Alternate = false, bool IsNext = false,
    Func<object?, IHtmlContent>? Head = null);
