﻿using Meadow;
using Meadow.Foundation.Displays;
using Meadow.Foundation.Graphics.MicroLayout;
using Meadow.Foundation.ICs.IOExpanders;
using Meadow.Peripherals.Displays;

namespace MAX7219_Sample;

public class MeadowApp : App<Desktop>
{
    private DisplayScreen? screen;

    public override Task Initialize()
    {
        var expander = FtdiExpanderCollection.Devices[0];

        var display = new Max7219(
            expander.CreateSpiBus(),
            expander.Pins.C0.CreateDigitalOutputPort(), // CS
            deviceRows: 4,
            deviceColumns: 1);

        screen = new DisplayScreen(display, RotationType._270Degrees);
        screen.BackgroundColor = Color.Black;

        return base.Initialize();
    }

    public override Task Run()
    {
        Text();

        return base.Run();
    }

    public void Text()
    {
        var label = new Label(0, 0, screen!.Width, screen.Height);
        label.HorizontalAlignment = Meadow.Foundation.Graphics.HorizontalAlignment.Center;
        label.VerticalAlignment = Meadow.Foundation.Graphics.VerticalAlignment.Center;
        label.TextColor = Color.White;
        label.Text = "HELLO";

        screen.Controls.Add(label);
    }

    public void TextOnBox()
    {
        var box = new Box(0, 0, screen!.Width / 4, screen.Height);
        box.ForegroundColor = Color.Red;
        var label = new Label(0, 0, screen.Width / 4, screen.Height);
        label.HorizontalAlignment = Meadow.Foundation.Graphics.HorizontalAlignment.Center;
        label.VerticalAlignment = Meadow.Foundation.Graphics.VerticalAlignment.Center;
        label.TextColor = Color.Black;
        label.Text = "Meadow";

        screen.Controls.Add(box, label);

        while (true)
        {
            Thread.Sleep(1000);
            (box.ForegroundColor, label.TextColor) = (label.TextColor, box.ForegroundColor);
        }
    }

    public void Sweep()
    {
        var box = new Box(0, 0, screen!.Width / 4, screen.Height);
        box.ForegroundColor = Color.Red;

        screen.Controls.Add(box);

        var direction = 1;
        var speed = 1;

        while (true)
        {
            var left = box.Left + (speed * direction);

            box.Left = left;

            if ((box.Right >= screen.Width) || box.Left <= 0)
            {
                direction *= -1;
            }

            Thread.Sleep(50);
        }
    }
}