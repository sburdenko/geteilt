namespace Geteilt.Web.Localisation;

/// <summary>
/// The CLDR plural categories the supported languages use. Not every language uses every one:
/// German and English need <see cref="One"/> and <see cref="Other"/>, Ukrainian needs all four.
/// </summary>
public enum PluralCategory
{
    One,
    Few,
    Many,
    Other,
}
