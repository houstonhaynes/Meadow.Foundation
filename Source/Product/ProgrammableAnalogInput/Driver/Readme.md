# Meadow.Foundation.ProgrammableAnalogInput

**Meadow Programmable Analog Input module**

The **ProgrammableAnalogInput** library is included in the **Meadow.Foundation.ProgrammableAnalogInput** nuget package and is designed for the [Wilderness Labs](www.wildernesslabs.co) Meadow .NET IoT platform.

This driver is part of the [Meadow.Foundation](https://developer.wildernesslabs.co/Meadow/Meadow.Foundation/) peripherals library, an open-source repository of drivers and libraries that streamline and simplify adding hardware to your C# .NET Meadow IoT applications.

For more information on developing for Meadow, visit [developer.wildernesslabs.co](http://developer.wildernesslabs.co/).

To view all Wilderness Labs open-source projects, including samples, visit [github.com/wildernesslabs](https://github.com/wildernesslabs/).

## Installation

You can install the library from within Visual studio using the the NuGet Package Manager or from the command line using the .NET CLI:

`dotnet add package Meadow.Foundation.ProgrammableAnalogInput`
## Usage

```csharp
private ProgrammableAnalogInputModule module;

public override async Task Initialize()
{
    Resolver.Log.Info("Initialize...");

    var bus = Device.CreateI2cBus();

    module = new ProgrammableAnalogInputModule(
        bus,
        0x10,
        0x20,
        0x21);
}

public override async Task Run()
{
    Resolver.Log.Info("Run...");
}

public async Task Test4_20()
{
    for (var i = 0; i < 8; i++)
    {
        module.ConfigureChannel(new ChannelConfig
        {
            ChannelNumber = i,
            ChannelType = ConfigurableAnalogInputChannelType.Current_4_20,
            UnitType = "Temperature",
            Scale = 3.4725, //0-100F, but scale/offs in C
            Offset = -31.67
        });
    }

    while (true)
    {
        for (var i = 0; i < module.ChannelCount; i++)
        {
            try
            {
                var raw = module.Read4_20mA(i);
                Resolver.Log.Info($"CH{i}: {raw.Milliamps:N1} mA");
                var t1 = module.ReadChannelAsConfiguredUnit(i);
                if (t1 is Temperature temp)
                {
                    Resolver.Log.Info($"temp{i}: {temp.Fahrenheit:N1}F");
                }
            }
            catch (Exception ex)
            {
                Resolver.Log.Error($"ERROR: {ex.Message}");
            }
        }
        Resolver.Log.Info($"---");
        await Task.Delay(1000);
    }
}

public async Task Test0_10()
{
    for (var i = 0; i < 8; i++)
    {
        module.ConfigureChannel(new ChannelConfig
        {
            ChannelNumber = i,
            ChannelType = ConfigurableAnalogInputChannelType.Voltage_0_10,
            UnitType = "Temperature",
            Scale = 3.4725, //0-100F, but scale/offs in C
            Offset = -31.67
        });

        module.ConfigureChannel(new ChannelConfig
        {
            ChannelNumber = i,
            ChannelType = ConfigurableAnalogInputChannelType.ThermistorNtc
        });
    }

    while (true)
    {
        for (var i = 0; i < module.ChannelCount; i++)
        {
            try
            {
                var raw = module.Read0_10V(i);
                Resolver.Log.Info($"CH{i}: {raw.Volts:N2} V");
            }
            catch (Exception ex)
            {
                Resolver.Log.Error($"ERROR: {ex.Message}");
            }
        }
        Resolver.Log.Info($"---");
        await Task.Delay(1000);
    }
}

public async Task TestNtc()
{
    for (var i = 0; i < 8; i++)
    {
        module.ConfigureChannel(new ChannelConfig
        {
            ChannelNumber = i,
            ChannelType = ConfigurableAnalogInputChannelType.ThermistorNtc
        });
    }

    while (true)
    {
        for (var i = 0; i < module.ChannelCount; i++)
        {
            try
            {
                var raw = module.ReadNtc(i);
                Resolver.Log.Info($"CH{i}: {raw.Fahrenheit:N2} F");
            }
            catch (Exception ex)
            {
                Resolver.Log.Error($"ERROR: {ex.Message}");
            }
        }
        Resolver.Log.Info($"---");
        await Task.Delay(1000);
    }
}

```
## How to Contribute

- **Found a bug?** [Report an issue](https://github.com/WildernessLabs/Meadow_Issues/issues)
- Have a **feature idea or driver request?** [Open a new feature request](https://github.com/WildernessLabs/Meadow_Issues/issues)
- Want to **contribute code?** Fork the [Meadow.Foundation](https://github.com/WildernessLabs/Meadow.Foundation) repository and submit a pull request against the `develop` branch


## Need Help?

If you have questions or need assistance, please join the Wilderness Labs [community on Slack](http://slackinvite.wildernesslabs.co/).
## About Meadow

Meadow is a complete, IoT platform with defense-grade security that runs full .NET applications on embeddable microcontrollers and Linux single-board computers including Raspberry Pi and NVIDIA Jetson.

### Build

Use the full .NET platform and tooling such as Visual Studio and plug-and-play hardware drivers to painlessly build IoT solutions.

### Connect

Utilize native support for WiFi, Ethernet, and Cellular connectivity to send sensor data to the Cloud and remotely control your peripherals.

### Deploy

Instantly deploy and manage your fleet in the cloud for OtA, health-monitoring, logs, command + control, and enterprise backend integrations.


