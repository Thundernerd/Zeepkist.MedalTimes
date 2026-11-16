namespace TNRD.Zeepkist.MedalTimes.Medals;

public sealed class MedalVisibility
{
    public bool Active { get; set; }
    public bool TestLevel { get; set; }
    public bool ChatOpen { get; set; }
    public bool UserVisible { get; set; } = true;
    public bool IsVisible(bool enabled) => enabled && Active && !TestLevel && !ChatOpen && UserVisible;
}
