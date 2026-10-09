namespace UnoTP.ViewModels;

/// <summary>One entry on the step rail down the left of a journey page.</summary>
/// <param name="Eyebrow">The small text over the name: "Start", "Step 2" or "Submit".</param>
/// <param name="CssClass">The entry's classes: its state (current, done) and whether it is an end of the rail.</param>
/// <param name="RuleBefore">A dashed line is drawn above this entry (before Review Summary).</param>
/// <param name="RuleAfter">A dashed line is drawn below this entry (after Investor Identification).</param>
public sealed record RailStep(
    string Eyebrow, string Name, string Glyph, bool IsCurrent, bool IsDone, string CssClass, bool RuleBefore, bool RuleAfter);

/// <summary>
/// The step rail for the step a page is on. The steps are the six in
/// NewApplicationViewModel.Steps: Investor Identification, the four booking steps,
/// and Review Summary. The four booking steps are the "4 easy steps" the dashboard promises.
/// </summary>
public sealed class StepRail
{
    private const string InvestorGlyph = "<circle cx=\"12\" cy=\"7.5\" r=\"4\"></circle><path d=\"M4.5 20.5a7.5 7.5 0 0 1 15 0\"></path>";
    /// <summary>The controller behind each step, in the order of NewApplicationViewModel.Steps.</summary>
    private static readonly string[] StepControllers = ["NewApplication", "Documents", "Investor", "Payment", "Deposit", "Review"];

    private const string ReviewGlyph = "<circle cx=\"12\" cy=\"12\" r=\"9.5\"></circle><path d=\"M8 12.5l2.8 2.8L16.5 9.5\"></path>";

    /// <summary>The phone's one-line summary: "Step 2 of 4", "Before the steps" or "All 4 steps done".</summary>
    public string Count { get; }

    /// <summary>The phone's current step name, or "Submitted" once the application is in.</summary>
    public string Now { get; }

    /// <summary>How far along the rail the page is, for the phone's progress bar.</summary>
    public int PercentDone { get; }

    /// <summary>
    /// The controller of the step before this one, for the phone's back arrow. None on
    /// Investor Identification, which has its own link back to the dashboard, and none
    /// once the application is submitted.
    /// </summary>
    public string? BackController { get; }

    /// <summary>The name of the step before this one: what the back arrow goes to.</summary>
    public string BackName { get; } = "";

    public IReadOnlyList<RailStep> Steps { get; }

    /// <param name="current">The index of the step the page is on (0 = Investor Identification, 5 = Review Summary). Submitted passes 6.</param>
    public static StepRail For(int current) => new(current);

    private StepRail(int current)
    {
        var names = NewApplicationViewModel.Steps;
        var last = names.Length - 1;
        var bookingSteps = last - 1;
        var submitted = current > last;

        if (submitted || current == last)
        {
            Count = $"All {bookingSteps} steps done";
        }
        else if (current == 0)
        {
            Count = "Before the steps";
        }
        else
        {
            Count = $"Step {current} of {bookingSteps}";
        }

        Now = submitted ? "Submitted" : names[current];

        if (current >= 1 && !submitted)
        {
            BackController = StepControllers[current - 1];
            BackName = names[current - 1];
        }
        PercentDone = Math.Min(100, current * 100 / last);

        var glyphs = new List<string> { InvestorGlyph };
        glyphs.AddRange(DashboardViewModel.StepGlyphs);
        glyphs.Add(ReviewGlyph);

        var steps = new List<RailStep>();
        for (var i = 0; i <= last; i++)
        {
            var isCurrent = i == current;
            var isDone = i < current;
            var isEnd = i == 0 || i == last;

            string eyebrow;
            if (i == 0) eyebrow = "Start";
            else if (i == last) eyebrow = "Submit";
            else eyebrow = $"Step {i}";

            var cssClass = "page-rail__step";
            if (isEnd) cssClass += " page-rail__step--end";
            if (isCurrent) cssClass += " page-rail__step--current";
            if (isDone) cssClass += " page-rail__step--done";

            steps.Add(new RailStep(eyebrow, names[i], glyphs[i], isCurrent, isDone, cssClass, RuleBefore: i == last, RuleAfter: i == 0));
        }
        Steps = steps;
    }
}
