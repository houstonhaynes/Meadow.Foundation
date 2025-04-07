namespace Meadow.Foundation;

public class ChannelConfig
{
    public ConfigurableAnalogInputChannelType ChannelType { get; set; }
    public double Scale { get; set; } = 1.0;
    public double Offset { get; set; } = 0.0;
    public string UnitType { get; set; } // TODO: make this an IUnit
}
