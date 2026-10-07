using System.Drawing.Drawing2D;

namespace SpiderSolitaire.Rendering;

internal static class Shapes
{
    public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        float diameter = Math.Max(0.01f, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
        GraphicsPath path = new();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static Color Lighten(Color color, float amount) => Color.FromArgb(
        color.A,
        (int)(color.R + (255 - color.R) * amount),
        (int)(color.G + (255 - color.G) * amount),
        (int)(color.B + (255 - color.B) * amount));

    public static Color Darken(Color color, float amount) => Color.FromArgb(
        color.A,
        (int)(color.R * (1 - amount)),
        (int)(color.G * (1 - amount)),
        (int)(color.B * (1 - amount)));

    /// <summary>The first of the named font families that is installed, or the generic sans serif.</summary>
    public static FontFamily FindFamily(params string[] names)
    {
        foreach (string name in names)
        {
            try
            {
                return new FontFamily(name);
            }
            catch (ArgumentException)
            {
                // Not installed; try the next one.
            }
        }

        return FontFamily.GenericSansSerif;
    }
}
