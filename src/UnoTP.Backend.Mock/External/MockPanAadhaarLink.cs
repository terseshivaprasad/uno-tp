using UnoTP.Backend.External;

namespace UnoTP.Backend.Mock.External;

/// <summary>
/// The mock PAN-Aadhaar link. Like the real one it is only asked once there is an
/// Aadhaar number, and then every PAN is linked but one: the holder of draft
/// FBBMFL26F55QP9, whose PAN copy Operations asked for again, so a demo can reach
/// the unlinked answer by continuing that draft and filing its PAN copy and an
/// Aadhaar as the proof of address.
/// </summary>
public sealed class MockPanAadhaarLink : IPanAadhaarLinkService
{
    private const string UnlinkedDraft = "FBBMFL26F55QP9";

    private static readonly HashSet<string> Unlinked =
        MockInFlight.Drafts.Where(d => d.Summary.AppNo == UnlinkedDraft).Select(d => d.Holder.Pan).ToHashSet();

    public Task<PanAadhaarLink> CheckAsync(string pan, string aadhaarNumber, CancellationToken ct = default) =>
        Task.FromResult(aadhaarNumber.Length == 0 ? PanAadhaarLink.NeedsAadhaar
            : Unlinked.Contains(pan) ? PanAadhaarLink.NotLinked
            : PanAadhaarLink.Linked);
}
