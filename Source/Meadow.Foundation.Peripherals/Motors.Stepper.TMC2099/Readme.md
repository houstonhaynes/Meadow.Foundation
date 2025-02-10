# Meadow.Foundation.Motors.Stepper.TMC2099

**TMC2209 stepper motor controller**

The **TMC2209** library is included in the **Meadow.Foundation.Motors.Stepper.TMC2209** NuGet package and is designed for the [Wilderness Labs](www.wildernesslabs.co) Meadow .NET IoT platform.

This driver is part of the [Meadow.Foundation](https://developer.wildernesslabs.co/Meadow/Meadow.Foundation/) peripherals library, an open-source repository of drivers and libraries that streamline and simplify adding hardware to your C# .NET Meadow IoT applications.

For more information on developing for Meadow, visit [developer.wildernesslabs.co](http://developer.wildernesslabs.co/).

To view all Wilderness Labs open-source projects, including samples, visit [github.com/wildernesslabs](https://github.com/wildernesslabs/).

## Installation

```bash
dotnet add package Meadow.Foundation.Motors.Stepper.TMC2209
```

## Usage

The TMC2209 can be used in two modes:

### Step/Dir Mode

Basic control mode using step and direction signals:

```csharp
Tmc2209 stepper = new Tmc2209(
    step: Device.Pins.D01,
    direction: Device.Pins.D00,
    enable: Device.Pins.D02);

// Basic stepping
stepper.Step(200);  // Move 200 steps
stepper.Rotate(90); // Rotate 90 degrees

// Change microstepping
stepper.CurrentStepDivisor = StepDivisor.Divisor16; // 1/16 microstepping
```

### UART Mode

Advanced control mode with configuration options:

```csharp
ISerialPort uart = Device.CreateSerialPort(Device.SerialPortNames.Com1, 115200);
Tmc2209 stepper = new Tmc2209(uart, address: 0);

// Configure motion profile
await stepper.ConfigureMotionProfileAsync(MotionProfile.HighPrecision);

// Set microstepping
await stepper.SetMicrosteppingAsync(StepDivisor.Divisor256);

// Fine-tune motor parameters
await stepper.SetMotorCurrentAsync(holdCurrent: 16, runCurrent: 31);
await stepper.SetAccelerationAsync(200);
await stepper.SetVelocityAsync(1000);

// Monitor status
DriverStatus status = await stepper.GetStatusAsync();
if (status.HasFlag(DriverStatus.StallGuard)) {
    // Handle stall detection
}
```

## Features

- Step/Dir interface for basic control
- UART interface for advanced configuration
- Microstepping up to 1/256
- StealthChop quiet motion technology
- SpreadCycle high-performance mode
- Built-in stall detection
- Configurable motion profiles
- Current control and monitoring
- Temperature and error protection

## How to Contribute

- **Found a bug?** [Report an issue](https://github.com/WildernessLabs/Meadow_Issues/issues)
- Have a **feature idea or driver request?** [Open a new feature request](https://github.com/WildernessLabs/Meadow_Issues/issues)
- Want to **contribute code?** Fork the [Meadow.Foundation](https://github.com/WildernessLabs/Meadow.Foundation) repository and submit a pull request against the `develop` branch

## Need Help?

If you have questions or need assistance, please join the Wilderness Labs [community on Slack](http://slackinvite.wildernesslabs.co/).

## About Meadow

Meadow is a complete IoT platform with defense-grade security that runs full .NET applications on embeddable microcontrollers and Linux single-board computers including Raspberry Pi and NVIDIA Jetson.

### Build

Use the full .NET platform and tooling such as Visual Studio and plug-and-play hardware drivers to painlessly build IoT solutions.

### Connect

Utilize native support for WiFi, Ethernet, and Cellular connectivity to send sensor data to the Cloud and remotely control your peripherals.

### Deploy

Instantly deploy and manage your fleet in the cloud for OtA, health-monitoring, logs, command + control, and enterprise backend integrations.