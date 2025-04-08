using Meadow.Hardware;
using Meadow.Peripherals.Sensors;
using System;
using System.Threading.Tasks;

namespace Meadow.Foundation.Sensors.Temperature;

/// <summary>
/// LM75 Temperature sensor object
/// </summary>    
public partial class Mlx90614 : ITemperatureSensor
{
    private readonly I2cCommunications i2cComms;

    /// <summary>
    /// The default I2C address for the peripheral
    /// </summary>
    public byte DefaultI2cAddress => (byte)Addresses.Default;

    /// <summary>
    /// Create a new Mlx90614 object using the default configuration for the sensor
    /// </summary>
    /// <param name="i2cBus">The I2C bus</param>
    /// <param name="address">I2C address of the sensor</param>
    public Mlx90614(II2cBus i2cBus, byte address = (byte)Addresses.Default)
    {
        i2cComms = new I2cCommunications(i2cBus, address);
    }

    private Units.Temperature ReadTempRegister(Registers register)
    {
        Span<byte> buffer = stackalloc byte[2];

        i2cComms.Write((byte)register);
        i2cComms.Read(buffer);

        var raw = buffer[0] | buffer[1] << 8;

        if (raw == 0) throw new Exception("Invalid read");

        return new Units.Temperature((raw * 0.02d) - 273.15, Units.Temperature.UnitType.Celsius);
    }

    /// <summary>
    /// Reads the sensor (object) temperature
    /// </summary>
    public Task<Units.Temperature> Read()
    {
        return Task.FromResult(ReadTempRegister(Registers.TOBJ1));
    }

    /// <summary>
    /// Reads the sensor (object) temperature
    /// </summary>
    public Task<Units.Temperature> ReadAmbientTemperature()
    {
        return Task.FromResult(ReadTempRegister(Registers.TA));
    }
}