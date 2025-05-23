﻿namespace Meadow.Foundation.Graphics.MicroLayout;

/// <summary>
/// Represents a rectangular display box in the user interface.
/// </summary>
public class Box : ThemedControl
{
    private Color _foregroundColor;
    private bool _isFilled = true;

    /// <summary>
    /// Gets or sets a value indicating whether the display box is filled with the foreground color.
    /// </summary>
    public bool IsFilled
    {
        get => _isFilled;
        set => SetInvalidatingProperty(ref _isFilled, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Box"/> class with the specified dimensions.
    /// </summary>
    /// <param name="left">The left coordinate of the display box.</param>
    /// <param name="top">The top coordinate of the display box.</param>
    /// <param name="width">The width of the display box.</param>
    /// <param name="height">The height of the display box.</param>
    public Box(int left, int top, int width, int height)
        : base(left, top, width, height)
    {
    }

    /// <summary>
    /// Applies the specified display theme to the display box.
    /// </summary>
    /// <param name="theme">The display theme to apply.</param>
    public override void ApplyTheme(DisplayTheme theme)
    {
        if (theme != null)
        {
            if (theme.ForegroundColor != null) this.ForegroundColor = theme.ForegroundColor.Value;
        }
    }

    /// <summary>
    /// Gets or sets the foreground color of the display box.
    /// </summary>
    public Color ForegroundColor
    {
        get => _foregroundColor;
        set => SetInvalidatingProperty(ref _foregroundColor, value);
    }

    /// <summary>
    /// Draws the display box on the specified <see cref="MicroGraphics"/> surface.
    /// </summary>
    /// <param name="graphics">The <see cref="MicroGraphics"/> surface to draw the display box on.</param>
    protected override void OnDraw(MicroGraphics graphics)
    {
        if (ForegroundColor != Color.Transparent)
        {
            graphics.DrawRectangle(Left + (Parent?.Left ?? 0), Top + (Parent?.Top ?? 0), Width, Height, ForegroundColor, IsFilled);
        }
    }
}
