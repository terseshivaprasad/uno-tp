namespace UnoTP.ViewModels;

// The two choices of Upload Documents that are drawn as switches: the application
// type, and where a holder's post goes.
public partial class DocumentsViewModel
{
    /// <summary>Application Type as a switch: digital is off, physical is on.</summary>
    public ChoiceSwitchBlock AppTypeSwitch()
    {
        // A CKYC application is completed online, so it cannot be filed on paper.
        string? whyNotPhysical = null;
        if (State.Ckyc)
        {
            whyNotPhysical = "A CKYC application is completed online, so it cannot be filed on paper.";
        }

        var digital = new SwitchOption("docsAppType" + Digital, Digital, AppTypeName(Digital), State.AppType == Digital);
        var physical = new SwitchOption("docsAppType" + Physical, Physical, AppTypeName(Physical), State.AppType == Physical,
            Disabled: State.Ckyc, WhyDisabled: whyNotPhysical);
        return new ChoiceSwitchBlock("appType", "docsAppTypeLabel", digital, physical);
    }

    /// <summary>
    /// A holder's Communication Address as a switch: same as permanent is off,
    /// different from permanent is on. Going back to "same" with a proof filed asks
    /// first, because the proof is taken off.
    /// </summary>
    /// <param name="name">The name the choice posts under.</param>
    /// <param name="idStart">What both options' ids start with.</param>
    /// <param name="labelledBy">The id of the label the choice sits beside.</param>
    /// <param name="submitVia">The button a change posts through; null where the page switches the card in place.</param>
    public ChoiceSwitchBlock MailingSwitch(DocHolder h, string name, string idStart, string labelledBy, string? submitVia)
    {
        var different = MailDifferentOf(h);
        var canDiffer = MailCanDiffer(h);
        var proof = View(MailSlot, h);

        string? askBeforeDroppingProof = null;
        if (different && proof.Doc is not null)
        {
            askBeforeDroppingProof = MailDropAsk;
        }

        string? whyNotDifferent = null;
        if (!canDiffer)
        {
            whyNotDifferent = proof.NotApplicable;
        }

        var same = new SwitchOption(idStart + "same", "same", "Same as Permanent", !different,
            SubmitVia: submitVia, Confirm: askBeforeDroppingProof);
        var other = new SwitchOption(idStart + "different", "different", "Different from Permanent", different,
            Disabled: !canDiffer, WhyDisabled: whyNotDifferent, SubmitVia: submitVia);
        return new ChoiceSwitchBlock(name, labelledBy, same, other);
    }

    // An application type's name from the list, or its code where the list does not hold it.
    private string AppTypeName(string code)
    {
        foreach (var type in ApplicationTypes)
        {
            if (type.Code == code)
            {
                return type.Name;
            }
        }
        return code;
    }
}
