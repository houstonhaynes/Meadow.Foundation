# Meadow.Foundation.ICs.IOExpanders.Tca95x5

**Texas Instruments TCA9535 and TCA9555 I2C I/O expander**

The **Tca95x5** library is included in the **Meadow.Foundation.ICs.IOExpanders.Tca95x5** nuget package and is designed for the [Wilderness Labs](www.wildernesslabs.co) Meadow .NET IoT platform.

This driver is part of the [Meadow.Foundation](https://developer.wildernesslabs.co/Meadow/Meadow.Foundation/) peripherals library, an open-source repository of drivers and libraries that streamline and simplify adding hardware to your C# .NET Meadow IoT applications.

For more information on developing for Meadow, visit [developer.wildernesslabs.co](http://developer.wildernesslabs.co/).

To view all Wilderness Labs open-source projects, including samples, visit [github.com/wildernesslabs](https://github.com/wildernesslabs/).

## Installation

You can install the library from within Visual studio using the the NuGet Package Manager or from the command line using the .NET CLI:

`dotnet add package Meadow.Foundation.ICs.IOExpanders.Tca95x5`
## Usage

```csharp
private Tca95x5 expander;

public override Task Initialize()
{
    Resolver.Log.Info("Initialize...");
    var i2CBus = Device.CreateI2cBus(1);

    expander = new Tca9535(i2CBus);

    return base.Initialize();
}

public override async Task Run()
{
    var port0 = expander.Pins.P00.CreateDigitalOutputPort();
    var port1 = expander.Pins.P01.CreateDigitalOutputPort();
    var port2 = expander.Pins.P02.CreateDigitalOutputPort();
    var port3 = expander.Pins.P03.CreateDigitalOutputPort();

    bool state = false;

    while (true)
    {
        Resolver.Log.Info($"state: {state}");
        port0.State = state;
        port1.State = state;
        port2.State = state;
        port3.State = state;

        state = !state;
        await Task.Delay(2000);
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


