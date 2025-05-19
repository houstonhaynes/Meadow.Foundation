# Meadow.Foundation.Motors.Stepper.TMC2209

**TMC2209 Advanced Stepper Motor Controller Driver**

The **TMC2209** driver is included in the **Meadow.Foundation.Motors.Stepper.TMC2209** NuGet package and is designed for the [Wilderness Labs](https://www.wildernesslabs.co) Meadow .NET IoT platform.

This driver supports Trinamic's TMC2209 stepper motor controller, featuring StealthChop2 quiet operation, SpreadCycle high-performance mode, StallGuard4 sensorless homing, and CoolStep adaptive energy optimization.

This driver is part of the [Meadow.Foundation](https://developer.wildernesslabs.co/Meadow/Meadow.Foundation/) peripherals library, an open-source repository of drivers and libraries that streamline and simplify adding hardware to your C# .NET Meadow IoT applications.

For more information on developing for Meadow, visit [developer.wildernesslabs.co](http://developer.wildernesslabs.co/).

## Installation

```bash
dotnet add package Meadow.Foundation.Motors.Stepper.TMC2209
```

## Usage

The TMC2209 can be used in two modes:

### Step/Dir Mode

Basic control mode using step and direction signals:

```csharp
// Create a new TMC2209 controller in Step/Dir mode
Tmc2209 stepper = new Tmc2209(
    step: Device.Pins.D01,
    direction: Device.Pins.D00,
    enable: Device.Pins.D02);

// Basic stepping
stepper.Step(200);              // Move 200 steps
stepper.Rotate(90);             // Rotate 90 degrees clockwise
stepper.Rotate(45, RotationDirection.CounterClockwise); // Rotate 45 degrees counterclockwise

// Enable/disable the motor
stepper.Enable(true);           // Enable motor (energize coils)
stepper.Enable(false);          // Disable motor (de-energize coils)
```

### UART Mode

Advanced control mode with rich configuration options:

```csharp
// Configure the serial port for UART communication
ISerialPort uart = Device.CreateSerialPort(Device.SerialPortNames.Com1, 115200);

// Create a new TMC2209 controller in UART mode
Tmc2209 stepper = new Tmc2209(uart, address: 0);

// Configure a predefined motion profile
await stepper.ConfigureMotionProfileAsync(MotionProfile.HighPrecision);
// Available profiles:
// - MotionProfile.HighPrecision (optimized for precise positioning with minimal noise)
// - MotionProfile.Standard (balanced profile for general use)
// - MotionProfile.HighVelocity (optimized for maximum speed)
// - MotionProfile.LowPower (optimized for energy efficiency)

// Set microstepping resolution
await stepper.SetMicrosteppingAsync(StepDivisor.Divisor256);

// Fine-tune motor parameters
await stepper.SetMotorCurrentAsync(runCurrent: 24, holdCurrent: 12, holdDelay: 4);
await stepper.SetAccelerationAsync(200);
await stepper.SetVelocityAsync(1000);

// Get diagnostic information
var diagnostics = await stepper.GetDiagnosticsAsync();
Console.WriteLine(diagnostics.ToString());
```

## Advanced Features

### Chopper Modes

The TMC2209 supports different chopper modes for different applications:

```csharp
// Configure quiet StealthChop mode for silent operation
await stepper.ConfigureChopperModeAsync(ChopperMode.StealthChop);

// Configure high-performance SpreadCycle mode for high-speed operation
await stepper.ConfigureChopperModeAsync(ChopperMode.SpreadCycle);

// Configure hybrid mode that automatically switches between StealthChop at low speeds
// and SpreadCycle at higher speeds for optimal performance
await stepper.ConfigureHybridChopperModeAsync(thresholdSpeed: 200);  // Speed in steps/second
```

### StallGuard4 Sensorless Homing

StallGuard4 can detect motor stalls and be used for sensorless homing:

```csharp
// Configure StallGuard for sensorless homing
await stepper.ConfigureStallGuardAsync(
    enabled: true,
    threshold: 10,              // Lower values = more sensitive (-64 to +63)
    stopOnStall: true,          // Automatically stop motor on stall detection
    minVelocity: 20,            // Minimum velocity for StallGuard operation
    maxVelocity: 300            // Maximum velocity for StallGuard operation
);

// Auto-calibrate StallGuard threshold
int calibratedThreshold = await stepper.CalibrateStallGuardAsync(
    testVelocity: 60,           // Test velocity for calibration
    stopOnStall: true           // Enable stopping on stall detection
);

// Read current StallGuard load value
int loadValue = await stepper.GetStallGuardValueAsync();
// Lower values indicate higher motor load
Console.WriteLine($"Motor load: {loadValue}");
```

### CoolStep Adaptive Energy Optimization

CoolStep automatically adjusts motor current based on load:

```csharp
// Configure CoolStep for energy optimization
await stepper.ConfigureCoolStepAsync(
    enabled: true,
    lowerThreshold: 2,          // Current increase threshold (0-15)
    upperThreshold: 4,          // Current decrease threshold (0-15)
    currentIncStep: 1,          // Step size for current increase (0-3)
    currentDecStep: 0,          // Step size for current decrease (0-3)
    minCurrentScale: false,     // false: 1/2 of IRUN, true: 1/4 of IRUN
    coolStepThreshold: 200      // Minimum velocity for CoolStep in steps/s
);

// Auto-tune CoolStep parameters based on StallGuard measurements
await stepper.AutoTuneCoolStepAsync(testVelocity: 200);
```

### Standstill Power Management

Configure power reduction during motor standstill:

```csharp
// Configure standstill power reduction
await stepper.ConfigureStandstillPowerAsync(
    enabled: true,
    delayTime: 1000,            // Delay before reducing current (milliseconds)
    holdPercent: 30,            // Percentage of run current to use during standstill
    mode: StandstillMode.ReducedCurrent // Standstill power mode
);

// Available standstill modes:
// - StandstillMode.Normal (no special power handling)
// - StandstillMode.Freewheeling (motor disconnected during standstill)
// - StandstillMode.ReducedCurrent (maintain position with reduced current)
// - StandstillMode.PassiveBraking (short motor coils for braking effect)
```

### MicroPlyer Interpolation

Smooth motion with step interpolation:

```csharp
// Enable MicroPlyer step interpolation
await stepper.SetMicroPlyerInterpolationAsync(enabled: true);
```

## Diagnostics

Monitor the driver state and motor conditions:

```csharp
// Get detailed diagnostic information
var diagnostics = await stepper.GetDiagnosticsAsync();

// Check specific conditions
if (diagnostics.StallGuardValue < 50) {
    Console.WriteLine("High motor load detected");
}

if (diagnostics.OvertemperatureWarning) {
    Console.WriteLine("Temperature warning - reduce current or improve cooling");
}

if (diagnostics.StealthChopMode) {
    Console.WriteLine("Operating in quiet StealthChop mode");
}

Console.WriteLine($"Actual motor current: {diagnostics.ActualMotorCurrent}/31");
Console.WriteLine($"Measured speed: {diagnostics.MeasuredSpeed} steps/s");
```

## API Reference

### Constructors

- `Tmc2209(IPin step, IPin direction, IPin? enable = null)` - Create in Step/Dir mode
- `Tmc2209(ISerialPort serial, byte address = 0)` - Create in UART mode

### Properties

- `Angle StepAngle` - Motor step angle
- `int StepsPerRevolution` - Steps required for one full revolution
- `StepDivisor CurrentStepDivisor` - Current microstepping divisor
- `RotationDirection Direction` - Motor rotation direction
- `InterfaceMode CurrentMode` - Current interface mode (Step/Dir or UART)
- `ChopperMode CurrentChopperMode` - Current chopper mode
- `int CurrentVelocity` - Current motor velocity in steps per second
- `bool MicroPlyerEnabled` - Whether step interpolation is enabled
- `bool StallGuardEnabled` - Whether StallGuard is enabled
- `bool CoolStepEnabled` - Whether CoolStep is enabled
- `int RunCurrent` - Current motor run current setting (0-31)
- `int HoldCurrent` - Current motor hold current setting (0-31)

### Basic Methods

- `void Step(int steps)` - Step the motor
- `void Rotate(float degrees, RotationDirection direction = RotationDirection.Clockwise)` - Rotate the motor
- `void Enable(bool enabled)` - Enable or disable the motor

### Configuration Methods (UART mode only)

- `Task SetMicrosteppingAsync(StepDivisor divisor)` - Set microstepping resolution
- `Task ConfigureMotionProfileAsync(MotionProfile profile)` - Configure motion profile
- `Task ConfigureChopperModeAsync(ChopperMode mode)` - Set chopper mode
- `Task ConfigureHybridChopperModeAsync(int thresholdSpeed)` - Configure hybrid mode
- `Task SetMotorCurrentAsync(int runCurrent, int holdCurrent, int holdDelay = 4)` - Set motor currents
- `Task SetVelocityAsync(int velocity)` - Set motor velocity
- `Task SetAccelerationAsync(int acceleration)` - Set motor acceleration
- `Task SetMicroPlyerInterpolationAsync(bool enabled)` - Configure step interpolation
- `Task ConfigureStandstillPowerAsync(bool enabled, int delayTime, int holdPercent, StandstillMode mode)` - Configure standstill power

### Advanced Feature Methods (UART mode only)

- `Task ConfigureStallGuardAsync(bool enabled, int threshold, bool stopOnStall, int minVelocity, int maxVelocity)` - Configure StallGuard
- `Task<int> GetStallGuardValueAsync()` - Get StallGuard load value
- `Task<int> CalibrateStallGuardAsync(int testVelocity, bool stopOnStall)` - Auto-calibrate StallGuard
- `Task ConfigureCoolStepAsync(bool enabled, int lowerThreshold, int upperThreshold, int currentIncStep, int currentDecStep, bool minCurrentScale, int coolStepThreshold)` - Configure CoolStep
- `Task<bool> AutoTuneCoolStepAsync(int testVelocity)` - Auto-tune CoolStep
- `Task<DriverDiagnostics> GetDiagnosticsAsync()` - Get detailed diagnostics

### Enums

- `InterfaceMode` - StepDir, Uart
- `MotionProfile` - HighPrecision, Standard, HighVelocity, LowPower
- `ChopperMode` - StealthChop, SpreadCycle, Hybrid
- `StepDivisor` - Divisor1 to Divisor256
- `DriverStatus` - Status flags (Standstill, StallGuard, etc.)
- `StandstillMode` - Normal, Freewheeling, ReducedCurrent, PassiveBraking

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