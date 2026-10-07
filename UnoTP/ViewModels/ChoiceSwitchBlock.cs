namespace UnoTP.ViewModels;

/// <summary>
/// A choice between two, drawn as a switch (Views/Shared/_ChoiceSwitch.cshtml), the
/// way the FATCA questions are: the first option is the switch off, the second the
/// switch on. The two are still radio buttons of one name, so the form posts the
/// chosen option's value exactly as a pair of radios does.
/// </summary>
/// <param name="Name">The name both options post under.</param>
/// <param name="LabelledBy">The id of the label the choice sits beside.</param>
/// <param name="Off">The option the switch stands for when it is off.</param>
/// <param name="On">The option the switch stands for when it is on.</param>
public sealed record ChoiceSwitchBlock(string Name, string LabelledBy, SwitchOption Off, SwitchOption On);

/// <summary>One of the two options of a switch.</summary>
/// <param name="Id">The option's element id.</param>
/// <param name="Value">What the form posts when it is chosen.</param>
/// <param name="Label">Its name, written beside the switch.</param>
/// <param name="Chosen">Whether it is the one chosen now.</param>
/// <param name="Disabled">Whether it cannot be chosen here.</param>
/// <param name="WhyDisabled">Said on hover when it cannot be chosen; null when it can.</param>
/// <param name="SubmitVia">The id of the button a change to it posts the form through; null where nothing is posted and the page follows the choice at once.</param>
/// <param name="Confirm">What is asked before a change to it is posted; null for nothing.</param>
public sealed record SwitchOption(string Id, string Value, string Label, bool Chosen,
    bool Disabled = false, string? WhyDisabled = null, string? SubmitVia = null, string? Confirm = null);
