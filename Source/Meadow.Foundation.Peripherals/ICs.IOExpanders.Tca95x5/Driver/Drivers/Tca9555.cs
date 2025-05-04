using Meadow.Hardware;

namespace Meadow.Foundation.ICs.IOExpanders;

public class Tca9555 : Tca95x5
{
    public Tca9555(II2cBus i2cBus, byte address = (byte)Addresses.Default)
        : base(i2cBus, address)
    {
    }
}
