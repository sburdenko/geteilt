using Microsoft.AspNetCore.Components;

namespace Geteilt.Web.Localisation;

/// <summary>
/// A component that shows text, and therefore has to redraw when the language changes.
/// </summary>
/// <remarks>
/// Inheriting rather than repeating the subscription in every page: a page that forgot it would
/// keep the old language on screen until something else happened to re-render it, which looks
/// like the switch is broken rather than like a missing line.
/// </remarks>
public abstract class LocalisedComponent : ComponentBase, IDisposable
{
    [Inject]
    protected Localizer L { get; set; } = null!;

    [Inject]
    protected LanguageState Language { get; set; } = null!;

    protected override void OnInitialized() => Language.Changed += Redraw;

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Language.Changed -= Redraw;
        }
    }

    private void Redraw() => InvokeAsync(StateHasChanged);
}
