using Meadow.Hardware;

namespace Meadow.Foundation.ICs.IOExpanders;

public class Tca9535 : Tca95x5
{
    public Tca9535(II2cBus i2cBus, byte address = (byte)Addresses.Default)
        : base(i2cBus, address)
    {
    }
}
