namespace GcToolkit.Core.Navigation;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class NavigationInfoAttribute : Attribute
{
    public NavigationSection Section { get; }

    public NavigationTransition Transition { get; }

    public NavigationInfoAttribute(NavigationSection section, NavigationTransition transition = NavigationTransition.Default)
    {
        Section = section;
        Transition = transition;
    }
}
