# Meadow.Foundation.Sensors.Atmospheric.Ms5611

**MS5611 I2C altimeter and barometric pressure sensor**

The **Ms5611** library is designed for the [Wilderness Labs](www.wildernesslabs.co) Meadow .NET IoT platform and is part of [Meadow.Foundation](https://developer.wildernesslabs.co/Meadow/Meadow.Foundation/).

The **Meadow.Foundation** peripherals library is an open-source repository of drivers and libraries that streamline and simplify adding hardware to your C# .NET Meadow IoT application.

For more information on developing for Meadow, visit [developer.wildernesslabs.co](http://developer.wildernesslabs.co/).

To view all Wilderness Labs open-source projects, including samples, visit [github.com/wildernesslabs](https://github.com/wildernesslabs/).

## Usage

```csharp
Ms5611 sensor;

public override Task Initialize()
{
    Resolver.Log.Info("Initializing...");

    sensor = new Ms5611(Device.CreateI2cBus());

    var consumer = Ms5611.CreateObserver(
        handler: result =>
        {
            Resolver.Log.Info($"Observer: Temp changed by threshold; new temp: {result.New.Temperature?.Celsius:N2}C, old: {result.Old?.Temperature?.Celsius:N2}C");
        },
        filter: result =>
        {
            if (result.Old is { } old)
            {
                return (
                (result.New.Temperature.Value - old.Temperature.Value).Abs().Celsius > 0.5
                &&
                (result.New.Pressure.Value - old.Pressure.Value).Millibar > 0.5
                );
            }
            return false;
        }
    );
    sensor.Subscribe(consumer);

    sensor.Updated += (sender, result) => {
        Resolver.Log.Info($"  Temperature: {result.New.Temperature?.Celsius:N2}C");
        Resolver.Log.Info($"  Pressure: {result.New.Pressure?.Millibar:N2}mbar ({result.New.Pressure?.Pascal:N2}Pa)");
    };

    return Task.CompletedTask;
}

public async override Task Run()
{
    var conditions = await sensor.Read();
    Resolver.Log.Info("Initial Readings:");
    Resolver.Log.Info($" Temperature: {conditions.Temperature?.Celsius:N2}C");
    Resolver.Log.Info($" Pressure: {conditions.Pressure?.Bar:N2}hPa");

    sensor.StartUpdating(TimeSpan.FromSeconds(1));
}

```
## How to Contribute

- **Found a bug?** [Report an issue](https://github.com/WildernessLabs/Meadow_Issues/issues)
- Have a **feature idea or driver request?** [Open a new feature request](https://github.com/WildernessLabs/Meadow_Issues/issues)
- Want to **contribute code?** Fork the [Meadow.Foundation](https://github.com/WildernessLabs/Meadow.Foundation) repository and submit a pull request against the `develop` branch


## Need Help?

If you have questions or need assistance, please join the Wilderness Labs [community on Slack](http://slackinvite.wildernesslabs.co/).