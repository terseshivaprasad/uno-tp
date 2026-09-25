namespace UnoTP.Models;

/// <summary>
/// A scheduled event the partner needs to know about before it happens: a rate
/// change, a window when the application is down, or maintenance on one of the
/// services behind it. Nothing here asks the partner to act; it warns them.
/// </summary>
/// <param name="Kind">Rate change, Downtime or Maintenance, shown as the chip.</param>
/// <param name="Tone">Which colour the chip and the timing take.</param>
/// <param name="Title">What happens, in one line.</param>
/// <param name="When">When it starts, and for a window, when it ends.</param>
/// <param name="Away">How long there is until then.</param>
/// <param name="Detail">What it means for work in flight.</param>
/// <param name="Id">The window or notice it was written from, so the admin
/// screen can take it back out of the bell without a reload.</param>
/// <param name="At">When it happens, which is what the bell sorts on.</param>
public record Notice(string Kind, string Tone, string Title, string When, string Away, string Detail, string Id = "", DateTime? At = null);
