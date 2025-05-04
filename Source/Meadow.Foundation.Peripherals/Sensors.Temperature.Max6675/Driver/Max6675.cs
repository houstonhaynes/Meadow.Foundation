using Meadow.Hardware;
using Meadow.Peripherals.Sensors;
using Meadow.Units;
using System;
using System.Threading.Tasks;

namespace Meadow.Foundation.Sensors.Temperature;

public class Max6675
    : ITemperatureSensor, ISpiPeripheral, IDisposable
{
    private readonly SpiCommunications spiComms;
    private readonly Frequency defaultSpeed = new Frequency(4, Frequency.UnitType.Megahertz);

    private readonly IDigitalOutputPort? port;
    private readonly bool portCreated = false;

    public bool IsDisposed { get; private set; } = false;
    public Frequency DefaultSpiBusSpeed => defaultSpeed;
    public SpiClockConfiguration.Mode DefaultSpiBusMode => SpiClockConfiguration.Mode.Mode0;

    public Max6675(ISpiBus bus, IPin? chipSelect)
        : this(bus, chipSelect?.CreateDigitalOutputPort(true))
    {
        portCreated = true;
    }

    public Max6675(ISpiBus bus, IDigitalOutputPort? chipSelect)
    {
        port = chipSelect;
        spiComms = new SpiCommunications(bus, chipSelect, DefaultSpiBusSpeed);
    }


    public SpiClockConfiguration.Mode SpiBusMode
    {
        get => spiComms.BusMode;
        set => spiComms.BusMode = value;
    }

    public Frequency SpiBusSpeed
    {
        get => spiComms.BusSpeed;
        set => spiComms.BusSpeed = value;
    }

    public Task<Units.Temperature> Read()
    {
        Span<byte> buffer = stackalloc byte[2];

        spiComms.Read(buffer);

        var raw = (ushort)((buffer[0] << 8) | buffer[1]);

        Resolver.Log.Info($"RAW: {buffer[0]:X2} {buffer[1]:X2}");

        // if bit D2 is high, the thermocouple is open
        if ((raw & (1 << 2)) != 0)
        {
            throw new Exception("Thermocouple input is open");
        }

        var t = Convert.ToSingle((raw >> 3)) * 0.25;
        return Task.FromResult(new Units.Temperature(t, Units.Temperature.UnitType.Celsius));
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                if (portCreated)
                {
                    port?.Dispose();
                }
            }

            IsDisposed = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}