using Meadow.Hardware;
using Meadow.Peripherals;
using Meadow.Units;
using System;
using System.Threading.Tasks;
using AU = Meadow.Units.Angle.UnitType;
using System.Diagnostics;

namespace Meadow.Foundation.Motors.Stepper
{   
    /// <summary>
    /// Driver for the Trinamic TMC2209 stepper motor controller, supporting both Step/Dir and UART modes
    /// with advanced features including StealthChop2, SpreadCycle, StallGuard4, and CoolStep
    /// </summary>
    public class Tmc2209 : IDisposable
    {
        #region Enums

        /// <summary>
        /// Defines the available interface modes for controlling the TMC2209
        /// </summary>
        public enum InterfaceMode
        {
            /// <summary>
            /// Traditional step/direction interface
            /// </summary>
            StepDir,

            /// <summary>
            /// Advanced UART configuration interface
            /// </summary>
            Uart
        }

        /// <summary>
        /// Predefined motion profiles optimized for different use cases
        /// </summary>
        public enum MotionProfile
        {
            /// <summary>
            /// Optimized for precise positioning with minimal noise
            /// </summary>
            HighPrecision,

            /// <summary>
            /// Balanced profile for general use
            /// </summary>
            Standard,

            /// <summary>
            /// Optimized for maximum speed
            /// </summary>
            HighVelocity,
            
            /// <summary>
            /// Optimized for quiet operation and energy efficiency
            /// </summary>
            LowPower
        }

        /// <summary>
        /// Available chopper modes for motor current control
        /// </summary>
        public enum ChopperMode
        {
            /// <summary>
            /// Quiet operation with smooth motion
            /// </summary>
            StealthChop,

            /// <summary>
            /// High-performance mode with improved torque
            /// </summary>
            SpreadCycle,
            
            /// <summary>
            /// Hybrid mode that switches between StealthChop and SpreadCycle based on velocity
            /// </summary>
            Hybrid
        }

        /// <summary>
        /// Microstepping divisors for motor control resolution
        /// </summary>
        public enum StepDivisor
        {
            /// <summary>Full step mode (1 step)</summary>
            Divisor1 = 1,
            /// <summary>Half step mode (1/2 step)</summary>
            Divisor2 = 2,
            /// <summary>Quarter step mode (1/4 step)</summary>
            Divisor4 = 4,
            /// <summary>Eighth step mode (1/8 step)</summary>
            Divisor8 = 8,
            /// <summary>Sixteenth step mode (1/16 step)</summary>
            Divisor16 = 16,
            /// <summary>1/32 step mode</summary>
            Divisor32 = 32,
            /// <summary>1/64 step mode</summary>
            Divisor64 = 64,
            /// <summary>1/128 step mode</summary>
            Divisor128 = 128,
            /// <summary>1/256 step mode (highest resolution)</summary>
            Divisor256 = 256
        }

        /// <summary>
        /// Status flags indicating the driver's current state and error conditions
        /// </summary>
        [Flags]
        public enum DriverStatus : uint
        {
            /// <summary>Motor is not moving</summary>
            Standstill = 1U << 31,
            /// <summary>StallGuard threshold reached</summary>
            StallGuard = 1U << 30,
            /// <summary>Temperature warning threshold exceeded</summary>
            OvertemperatureWarning = 1U << 29,
            /// <summary>Temperature shutdown threshold exceeded</summary>
            OvertemperatureShutdown = 1U << 28,
            /// <summary>Short to ground detected</summary>
            ShortToGround = 1U << 27,
            /// <summary>Open load detected</summary>
            OpenLoad = 1U << 26,
            /// <summary>Short circuit detected on low side</summary>
            ShortLS = 1U << 25,
            /// <summary>Short circuit detected on high side</summary>
            ShortHS = 1U << 24,
            /// <summary>Open load detected on phase A</summary>
            OpenLoadA = 1U << 23,
            /// <summary>Open load detected on phase B</summary>
            OpenLoadB = 1U << 22
        }
        
        /// <summary>
        /// Standstill power mode options
        /// </summary>
        public enum StandstillMode
        {
            /// <summary>Normal operation - no special power handling during standstill</summary>
            Normal,
            
            /// <summary>Freewheeling - motor is disconnected during standstill</summary>
            Freewheeling,
            
            /// <summary>Hold - maintain position with reduced current</summary>
            ReducedCurrent,
            
            /// <summary>Passive braking - short motor coils for passive braking effect</summary>
            PassiveBraking
        }

        #endregion

        #region Register Maps

        /// <summary>
        /// TMC2209 register addresses
        /// </summary>
        protected static class Registers
        {
            /// <summary>Global configuration flags</summary>
            public const byte GCONF = 0x00;
            /// <summary>Global status flags</summary>
            public const byte GSTAT = 0x01;
            /// <summary>Interface configuration</summary>
            public const byte IFCNT = 0x02;
            /// <summary>UART slave address</summary>
            public const byte SLAVECONF = 0x03;
            /// <summary>OTP configuration</summary>
            public const byte OTP_PROG = 0x04;
            /// <summary>Factory configuration</summary>
            public const byte OTP_READ = 0x05;
            /// <summary>Input pin states</summary>
            public const byte IOIN = 0x06;
            /// <summary>Driver current control</summary>
            public const byte IHOLD_IRUN = 0x10;
            /// <summary>Power down delay</summary>
            public const byte TPOWERDOWN = 0x11;
            /// <summary>Time between microsteps, 24bit</summary>
            public const byte TSTEP = 0x12;
            /// <summary>Upper velocity for StealthChop</summary>
            public const byte TPWMTHRS = 0x13;
            /// <summary>Velocity threshold for switching to SpreadCycle and enabling CoolStep</summary>
            public const byte TCOOLTHRS = 0x14;
            /// <summary>Upper velocity threshold for StallGuard</summary>
            public const byte THIGH = 0x15;
            /// <summary>RMS current value</summary>
            public const byte XDIRECT = 0x2D;
            /// <summary>Velocity value for motion</summary>
            public const byte VACTUAL = 0x22;
            /// <summary>Acceleration control</summary>
            public const byte AMAX = 0x22;
            /// <summary>StallGuard threshold</summary>
            public const byte SGTHRS = 0x40;
            /// <summary>StallGuard result</summary>
            public const byte SG_RESULT = 0x41;
            /// <summary>CoolStep configuration</summary>
            public const byte COOLCONF = 0x42;
            /// <summary>Chopper configuration</summary>
            public const byte CHOPCONF = 0x6C;
            /// <summary>Driver status flags</summary>
            public const byte DRV_STATUS = 0x6F;
            /// <summary>PWM configuration</summary>
            public const byte PWMCONF = 0x70;
            /// <summary>PWM Scale values</summary>
            public const byte PWM_SCALE = 0x71;
            /// <summary>PWM Auto configuration</summary>
            public const byte PWM_AUTO = 0x72;
        }

        /// <summary>
        /// Register field bit masks for various registers
        /// </summary>
        protected static class RegFields
        {
            // GCONF register fields (0x00)
            public const uint I_SCALE_ANALOG = 0x00000001;  // 0: Use internal ref from 5VOUT, 1: Use voltage supplied to VREF as current reference
            public const uint INTERNAL_RSENSE = 0x00000002; // 0: Operation with external sense resistors, 1: Internal sense resistors
            public const uint EN_SPREADCYCLE = 0x00000004;  // 0: StealthChop mode, 1: SpreadCycle mode
            public const uint SHAFT = 0x00000008;           // 1: Inverse motor direction
            public const uint INDEX_OTPW = 0x00000010;      // 0: INDEX shows first microstep position, 1: INDEX shows overtemperature prewarning
            public const uint INDEX_STEP = 0x00000020;      // 0: INDEX outputs N-channel open-drain, 1: INDEX outputs step pulses from internal pulse generator
            public const uint PDN_DISABLE = 0x00000040;     // 0: PDN_UART controls standstill current reduction, 1: PDN_UART input function disabled
            public const uint MSTEP_REG_SELECT = 0x00000080; // 0: Microstep resolution selected by pins, 1: Microstep resolution selected by MRES register
            public const uint MULTISTEP_FILT = 0x00000100;  // 0: No filtering of STEP pulses, 1: Software pulse generator optimization enabled
            public const uint TEST_MODE = 0x00000200;       // 0: Normal operation, 1: Enable test mode (not for normal operation)
            public const uint DIAG1_STALL = 0x01000000;     // 1: Enable DIAG1 active on stall (enable makes sense only in StealthChop mode)
            public const uint DIAG1_INDEX = 0x02000000;     // 1: Enable DIAG1 active on index position
            public const uint DIAG1_ONSTATE = 0x04000000;   // 1: Enable DIAG1 active when chopper is on
            public const uint DIAG1_STEPS_SKIPPED = 0x08000000; // 1: Enable DIAG1 active when steps are skipped
            public const uint DIAG0_STALL = 0x10000000;     // 1: Enable DIAG0 active on stall
            public const uint DIAG0_OTPW = 0x20000000;      // 1: Enable DIAG0 active on overtemperature prewarning
            public const uint DIAG0_OTPW_STALL = 0x40000000; // 1: Enable DIAG0 active on overtemperature or stall
            
            // CHOPCONF register fields (0x6C)
            public const uint TOFF_MASK = 0x0000000F;       // Off time setting, 0: Driver disable, 1-15: Off time setting
            public const uint HSTRT_MASK = 0x00000070;      // Hysteresis start setting (1-8)
            public const uint HEND_MASK = 0x00000780;       // Hysteresis end setting (0-15)
            public const uint TBL_MASK = 0x00018000;        // Blank time select (0-3)
            public const uint VSENSE = 0x00020000;          // 0: Low sensitivity, high current, 1: High sensitivity, low current
            public const uint MRES_MASK = 0x0F000000;       // Microstep resolution (0-8: 256, 128, 64, 32, 16, 8, 4, 2, 1 microsteps)
            public const uint INTPOL = 0x10000000;          // 1: Interpolate to 256 microsteps for smooth motion
            public const uint DEDGE = 0x20000000;           // 1: Enable step on both rising and falling edge
            public const uint DISS2G = 0x40000000;          // 1: Disable short to ground protection
            public const uint DISS2VS = 0x80000000;         // 1: Disable low-side short protection
            
            // COOLCONF register fields (0x42)
            public const uint SEMIN_MASK = 0x0000000F;      // Minimum CoolStep current (0-15), 0: CoolStep off
            public const uint SEUP_MASK = 0x00000060;       // Current increment step size (0-3: 1, 2, 4, 8)
            public const uint SEMAX_MASK = 0x00000F00;      // Maximum CoolStep current (0-15)
            public const uint SEDN_MASK = 0x00006000;       // Current decrement step size (0-3: 32, 8, 2, 1)
            public const uint SEIMIN = 0x00008000;          // Minimum CoolStep current, 0: 1/2 of IRUN, 1: 1/4 of IRUN
            public const uint SGT_MASK = 0x007F0000;        // StallGuard threshold value (-64 to +63)
            public const uint SFILT = 0x01000000;           // 0: Standard filtered mode, 1: StallGuard unfiltered mode
            
            // PWMCONF register fields (0x70)
            public const uint PWM_OFS_MASK = 0x000000FF;     // PWM amplitude offset (0-255)
            public const uint PWM_GRAD_MASK = 0x0000FF00;    // PWM amplitude gradient (0-255)
            public const uint PWM_FREQ_MASK = 0x00030000;    // PWM frequency (0-3: 2/1024, 2/683, 2/512, 2/410)
            public const uint PWM_AUTOSCALE = 0x00040000;    // PWM automatic amplitude scaling
            public const uint PWM_AUTOGRAD = 0x00080000;     // PWM automatic gradient adaptation
            public const uint FREEWHEEL_MASK = 0x00300000;   // Standstill mode (0-3: Normal, Freewheeling, LS short, HS short)
            public const uint PWM_REG_MASK = 0x0F000000;     // PWM regulation loop amplitude (0-15)
            public const uint PWM_LIM_MASK = 0xF0000000;     // PWM limit for regulation (0-15)
            
            // DRV_STATUS register fields (0x6F)
            public const uint SG_RESULT_MASK = 0x000003FF;   // StallGuard result (10-bit)
            public const uint FSACTIVE = 0x00000400;         // 1: Full step active
            public const uint CS_ACTUAL_MASK = 0x001F0000;   // Actual motor current scaling (0-31)
            public const uint STEALTH = 0x40000000;          // 1: StealthChop mode active
            public const uint STST = 0x80000000;             // 1: Standstill detected
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the motor's step angle
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when step angle is not positive</exception>
        public Angle StepAngle
        {
            get => stepAngle;
            set
            {
                if (value <= new Angle(0, AU.Degrees)) { throw new ArgumentOutOfRangeException(nameof(value), "Step angle must be positive"); }
                if (value == stepAngle) { return; }
                stepAngle = value;
            }
        }

        /// <summary>
        /// Gets the number of steps required for one full revolution based on current settings
        /// </summary>
        public int StepsPerRevolution => (int)(360 / stepAngle.Degrees) * (int)CurrentStepDivisor;

        /// <summary>
        /// Gets the current microstepping divisor setting
        /// </summary>
        public StepDivisor CurrentStepDivisor { get; private set; }

        /// <summary>
        /// Gets or sets the motor rotation direction
        /// </summary>
        public RotationDirection Direction { get; set; }

        /// <summary>
        /// Gets the current interface mode (Step/Dir or UART)
        /// </summary>
        public InterfaceMode CurrentMode { get; private set; }

        /// <summary>
        /// Gets or sets the current chopper mode
        /// </summary>
        public ChopperMode CurrentChopperMode { get; private set; }

        /// <summary>
        /// Gets the current motor velocity in steps per second (if using UART mode)
        /// </summary>
        public int CurrentVelocity { get; private set; }

        /// <summary>
        /// Gets or sets whether MicroPlyer step interpolation is enabled
        /// </summary>
        public bool MicroPlyerEnabled { get; private set; }

        /// <summary>
        /// Gets whether StallGuard is currently enabled
        /// </summary>
        public bool StallGuardEnabled { get; private set; }

        /// <summary>
        /// Gets whether CoolStep adaptive current control is enabled
        /// </summary>
        public bool CoolStepEnabled { get; private set; }

        /// <summary>
        /// Gets the motor coil inductance (used for optimized StallGuard configuration)
        /// </summary>
        public Inductance MotorInductance { get; private set; }

        /// <summary>
        /// Gets the motor coil resistance (used for optimized StallGuard configuration)
        /// </summary>
        public Resistance MotorResistance { get; private set; }

        /// <summary>
        /// Gets the current motor run current setting
        /// </summary>
        public Current RunCurrent { get; private set; }

        /// <summary>
        /// Gets the current motor hold current setting
        /// </summary>
        public Current HoldCurrent { get; private set; }

        /// <summary>
        /// Gets the raw motor run current scale (0-31)
        /// </summary>
        public int RunCurrentScale { get; private set; }

        /// <summary>
        /// Gets the raw motor hold current scale (0-31)
        /// </summary>
        public int HoldCurrentScale { get; private set; }

        /// <summary>
        /// Indicates if the driver has been disposed
        /// </summary>
        public bool IsDisposed { get; private set; }

        #endregion

        #region Fields

        private readonly IDigitalOutputPort? stepPort;
        private readonly IDigitalOutputPort? directionPort;
        private readonly IDigitalOutputPort? enablePort;
        private readonly ISerialPort? serialPort;
        private readonly object syncRoot = new();
        private readonly bool createdPorts = false;
        private Angle stepAngle;
        private byte nodeAddress;
        private int stallGuardThreshold;
        private int coolStepThreshold;
        private StandstillMode currentStandstillMode;
        private TimeSpan standstillDelay;
        private int standstillHoldPercent;
        private int hybridThresholdSpeed;
        private Voltage vsenseVoltage = new Voltage(0.325, Voltage.UnitType.Volts); // TMC2209 default vsense

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of TMC2209 in Step/Dir mode
        /// </summary>
        /// <param name="step">Step signal pin</param>
        /// <param name="direction">Direction signal pin</param>
        /// <param name="enable">Optional enable signal pin</param>
        public Tmc2209(IPin step, IPin direction, IPin? enable = null)
        {
            createdPorts = true;
            
            // Use the controller to create digital output ports
            stepPort = step.Controller.CreateDigitalOutputPort(step);
            directionPort = direction.Controller.CreateDigitalOutputPort(direction);
            if (enable != null)
            {
                enablePort = enable.Controller.CreateDigitalOutputPort(enable);
            }
            
            CurrentMode = InterfaceMode.StepDir;
            StepAngle = new Angle(1.8, AU.Degrees);
            CurrentStepDivisor = StepDivisor.Divisor1;
            CurrentChopperMode = ChopperMode.StealthChop;
            MicroPlyerEnabled = false;
            StallGuardEnabled = false;
            CoolStepEnabled = false;
            
            // Default current values
            RunCurrent = new Current(0.8, Current.UnitType.Amps);
            HoldCurrent = new Current(0.4, Current.UnitType.Amps);
            RunCurrentScale = 16;
            HoldCurrentScale = 8;
            
            stallGuardThreshold = 0;
            coolStepThreshold = 0;
            currentStandstillMode = StandstillMode.Normal;
            standstillDelay = TimeSpan.FromMilliseconds(20);
            standstillHoldPercent = 50;
            hybridThresholdSpeed = 0;
            
            // Default motor parameters - can be set later for more optimized performance
            MotorInductance = new Inductance(1.5, Inductance.UnitType.Millihenries);
            MotorResistance = new Resistance(2.8, Resistance.UnitType.Ohms);
        }

        /// <summary>
        /// Initializes a new instance of TMC2209 in UART mode
        /// </summary>
        /// <param name="serial">Serial port for UART communication</param>
        /// <param name="address">UART slave address (default: 0)</param>
        public Tmc2209(ISerialPort serial, byte address = 0)
        {
            serialPort = serial;
            nodeAddress = address;
            CurrentMode = InterfaceMode.Uart;
            StepAngle = new Angle(1.8, AU.Degrees);
            CurrentStepDivisor = StepDivisor.Divisor1;
            CurrentChopperMode = ChopperMode.StealthChop;
            MicroPlyerEnabled = false;
            StallGuardEnabled = false;
            CoolStepEnabled = false;
            
            // Default current values
            RunCurrent = new Current(0.8, Current.UnitType.Amps);
            HoldCurrent = new Current(0.4, Current.UnitType.Amps);
            RunCurrentScale = 16;
            HoldCurrentScale = 8;
            
            stallGuardThreshold = 0;
            coolStepThreshold = 0;
            currentStandstillMode = StandstillMode.Normal;
            standstillDelay = TimeSpan.FromMilliseconds(20);
            standstillHoldPercent = 50;
            hybridThresholdSpeed = 0;
            
            // Default motor parameters - can be set later for more optimized performance
            MotorInductance = new Inductance(1.5, Inductance.UnitType.Millihenries);
            MotorResistance = new Resistance(2.8, Resistance.UnitType.Ohms);
            
            InitializeUartMode().Wait();
        }

        #endregion

        #region Basic Motor Control Methods

        /// <summary>
        /// Rotates the motor by specified degrees
        /// </summary>
        /// <param name="degrees">Angle to rotate</param>
        /// <param name="direction">Rotation direction</param>
        public void Rotate(Angle angle, RotationDirection direction = RotationDirection.Clockwise)
        {
            Direction = direction;
            var steps = (int)(StepsPerRevolution * (angle.Degrees / 360.0));
            Step(steps);
        }

        /// <summary>
        /// Rotates the motor by specified degrees (legacy method)
        /// </summary>
        /// <param name="degrees">Number of degrees to rotate</param>
        /// <param name="direction">Rotation direction</param>
        public void Rotate(float degrees, RotationDirection direction = RotationDirection.Clockwise)
        {
            Rotate(new Angle(degrees, AU.Degrees), direction);
        }

        /// <summary>
        /// Steps the motor by specified number of steps
        /// </summary>
        /// <param name="steps">Number of steps (positive or negative)</param>
        /// <exception cref="ObjectDisposedException">Thrown when the driver is disposed</exception>
        public void Step(int steps)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }

            lock (syncRoot)
            {
                if (CurrentMode == InterfaceMode.StepDir)
                {
                    StepUsingStepDir(steps);
                }
                else
                {
                    StepUsingUart(steps).Wait();
                }
            }
        }

        /// <summary>
        /// Enables or disables the motor driver
        /// </summary>
        /// <param name="enabled">True to enable, false to disable</param>
        public void Enable(bool enabled)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }

            if (enablePort != null)
            {
                enablePort.State = !enabled;
            }
            else if (CurrentMode == InterfaceMode.Uart)
            {
                WriteRegisterAsync(Registers.GCONF, enabled ? 0U : 1U).Wait();
            }
        }
        
        #endregion

        #region UART Configuration Methods

        /// <summary>
        /// Sets the microstepping resolution
        /// </summary>
        /// <param name="divisor">Desired step divisor</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task SetMicrosteppingAsync(StepDivisor divisor)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("Microstepping configuration requires UART mode");
            }

            uint mres = (uint)(Math.Log((int)divisor) / Math.Log(2));
            uint chopconf = await ReadRegisterAsync(Registers.CHOPCONF);
            chopconf &= ~RegFields.MRES_MASK;
            chopconf |= (mres << 24);
            
            await WriteRegisterAsync(Registers.CHOPCONF, chopconf);
            CurrentStepDivisor = divisor;
        }

        /// <summary>
        /// Configures the motor using a predefined motion profile
        /// </summary>
        /// <param name="profile">Motion profile to apply</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task ConfigureMotionProfileAsync(MotionProfile profile)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("Motion profiles require UART mode");
            }

            switch (profile)
            {
                case MotionProfile.HighPrecision:
                    await ConfigureChopperModeAsync(ChopperMode.StealthChop);
                    await SetMicrosteppingAsync(StepDivisor.Divisor256);
                    await SetMicroPlyerInterpolationAsync(true);
                    await SetMotorCurrentAsync(new Current(0.8, Current.UnitType.Amps), new Current(0.4, Current.UnitType.Amps));
                    await SetAccelerationAsync(100);
                    break;
                    
                case MotionProfile.Standard:
                    await ConfigureChopperModeAsync(ChopperMode.StealthChop);
                    await SetMicrosteppingAsync(StepDivisor.Divisor16);
                    await SetMicroPlyerInterpolationAsync(true);
                    await SetMotorCurrentAsync(new Current(1.2, Current.UnitType.Amps), new Current(0.8, Current.UnitType.Amps));
                    await SetAccelerationAsync(200);
                    break;
                    
                case MotionProfile.HighVelocity:
                    await ConfigureChopperModeAsync(ChopperMode.SpreadCycle);
                    await SetMicrosteppingAsync(StepDivisor.Divisor8);
                    await SetMicroPlyerInterpolationAsync(true);
                    await SetMotorCurrentAsync(new Current(1.5, Current.UnitType.Amps), new Current(0.8, Current.UnitType.Amps));
                    await SetAccelerationAsync(400);
                    break;
                    
                case MotionProfile.LowPower:
                    await ConfigureChopperModeAsync(ChopperMode.StealthChop);
                    await SetMicrosteppingAsync(StepDivisor.Divisor32);
                    await ConfigureStandstillPowerAsync(true, TimeSpan.FromMilliseconds(500), 20);
                    await SetMotorCurrentAsync(new Current(0.7, Current.UnitType.Amps), new Current(0.2, Current.UnitType.Amps));
                    await SetAccelerationAsync(100);
                    await ConfigureCoolStepAsync(true, 5, 20);
                    break;
            }
        }

        /// <summary>
        /// Configures the chopper mode for current control
        /// </summary>
        /// <param name="mode">Desired chopper mode</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task ConfigureChopperModeAsync(ChopperMode mode)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("Chopper mode configuration requires UART mode");
            }

            uint gconf = await ReadRegisterAsync(Registers.GCONF);
            uint chopconf = await ReadRegisterAsync(Registers.CHOPCONF);
            uint pwmconf = await ReadRegisterAsync(Registers.PWMCONF);
            
            // Configure base mode first
            if (mode == ChopperMode.SpreadCycle || mode == ChopperMode.Hybrid)
            {
                gconf |= RegFields.EN_SPREADCYCLE;
            }
            else
            {
                gconf &= ~RegFields.EN_SPREADCYCLE;
            }
            
            await WriteRegisterAsync(Registers.GCONF, gconf);
            
            // Configure chopper settings
            // Set reasonable chopper defaults for the selected mode
            if (mode == ChopperMode.StealthChop || mode == ChopperMode.Hybrid)
            {
                // Enable PWM autoscale and autograd for StealthChop
                pwmconf |= RegFields.PWM_AUTOSCALE | RegFields.PWM_AUTOGRAD;
                
                // Set PWM frequency to a reasonable default (usually 2/683 works well)
                pwmconf &= ~RegFields.PWM_FREQ_MASK;
                pwmconf |= 1 << 16;  // Set to 2/683 fCLK
                
                await WriteRegisterAsync(Registers.PWMCONF, pwmconf);
                
                // For StealthChop, set TOFF (off time) to 5 for quiet operation
                chopconf &= ~RegFields.TOFF_MASK;
                chopconf |= 5;  // TOFF = 5, typical value
            }
            
            if (mode == ChopperMode.SpreadCycle || mode == ChopperMode.Hybrid)
            {
                // For SpreadCycle, set more aggressive chopper settings
                // Set TOFF (off time)
                chopconf &= ~RegFields.TOFF_MASK;
                chopconf |= 5;  // TOFF = 5, typical value
                
                // Set TBL (blank time)
                chopconf &= ~RegFields.TBL_MASK;
                chopconf |= 2 << 15;  // TBL = 2, typical value
                
                // Set HSTRT and HEND (hysteresis start and end values)
                chopconf &= ~RegFields.HSTRT_MASK;
                chopconf |= 4 << 4;  // HSTRT = 4, typical value
                
                chopconf &= ~RegFields.HEND_MASK;
                chopconf |= 1 << 7;  // HEND = 1, typical value
            }
            
            await WriteRegisterAsync(Registers.CHOPCONF, chopconf);
            
            // For hybrid mode, configure the velocity threshold for switching
            if (mode == ChopperMode.Hybrid && hybridThresholdSpeed > 0)
            {
                // TPWMTHRS = fCLK / velocity - threshold for switching to stealthChop
                uint tpwmthrs = (uint)(12000000 / hybridThresholdSpeed);
                await WriteRegisterAsync(Registers.TPWMTHRS, tpwmthrs);
            }
            
            CurrentChopperMode = mode;
        }

        /// <summary>
        /// Configures hybrid chopper mode that switches between StealthChop at low speeds
        /// and SpreadCycle at high speeds for optimal performance
        /// </summary>
        /// <param name="thresholdSpeed">Speed threshold in steps/s for switching between modes</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task ConfigureHybridChopperModeAsync(int thresholdSpeed)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("Hybrid chopper mode configuration requires UART mode");
            }

            // Ensure threshold is reasonable
            thresholdSpeed = Math.Max(10, thresholdSpeed);
            hybridThresholdSpeed = thresholdSpeed;
            
            // Configure chopper mode with optimized settings
            await ConfigureChopperModeAsync(ChopperMode.Hybrid);
            
            // Set TPWMTHRS - the velocity threshold for switching to StealthChop 
            // at lower velocities
            uint tpwmthrs = (uint)(12000000 / thresholdSpeed);
            await WriteRegisterAsync(Registers.TPWMTHRS, tpwmthrs);
        }
        
        /// <summary>
        /// Configures hybrid chopper mode with rotational speed threshold
        /// </summary>
        /// <param name="thresholdSpeed">Angular velocity threshold for switching between modes</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task ConfigureHybridChopperModeAsync(AngularVelocity thresholdSpeed)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            
            // Convert RPM to steps/second
            double stepsPerSecond = thresholdSpeed.RevolutionsPerMinute * StepsPerRevolution / 60.0;
            
            await ConfigureHybridChopperModeAsync((int)stepsPerSecond);
        }
        
        /// <summary>
        /// Sets the motor current levels using physical current values
        /// </summary>
        /// <param name="runCurrent">Current when motor is moving</param>
        /// <param name="holdCurrent">Current when motor is stationary</param>
        /// <param name="holdDelay">Delay before reducing to hold current</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task SetMotorCurrentAsync(Current runCurrent, Current holdCurrent, TimeSpan? holdDelay = null)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("Motor current configuration requires UART mode");
            }
            
            // Store the current values for reference
            RunCurrent = runCurrent;
            HoldCurrent = holdCurrent;
            
            // Convert to TMC2209 current scale (0-31)
            // Scale factor depends on vsense setting and sense resistor
            // Assuming vsense = 0.325V (high current mode) and rsense = 0.11 ohm (typical)
            int runScale = CurrentToScale(runCurrent);
            int holdScale = CurrentToScale(holdCurrent);
            
            // Use standard holdDelay if not specified
            var delayTimeMs = holdDelay ?? standstillDelay;
            
            // Set register values
            await SetMotorCurrentScaleAsync(runScale, holdScale, delayTimeMs);
        }
        
        /// <summary>
        /// Sets the motor current levels using scaling factors
        /// </summary>
        /// <param name="runCurrentScale">Current scale when motor is moving (0-31)</param>
        /// <param name="holdCurrentScale">Current scale when motor is stationary (0-31)</param>
        /// <param name="holdDelay">Delay before reducing to hold current</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when current values are out of valid range</exception>
        public async Task SetMotorCurrentScaleAsync(int runCurrentScale, int holdCurrentScale, TimeSpan holdDelay)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("Motor current configuration requires UART mode");
            }
            
            if (runCurrentScale < 0 || runCurrentScale > 31)
            {
                throw new ArgumentOutOfRangeException(nameof(runCurrentScale), "Run current must be between 0 and 31");
            }
            
            if (holdCurrentScale < 0 || holdCurrentScale > 31)
            {
                throw new ArgumentOutOfRangeException(nameof(holdCurrentScale), "Hold current must be between 0 and 31");
            }
            
            // Calculate appropriate IHOLDDELAY value (0-15)
            // TPOWERDOWN is in units of 2^18 clock cycles (approx. 21.8ms at 12MHz)
            int holdDelayValue = (int)(holdDelay.TotalMilliseconds * 12000 / 262144);
            holdDelayValue = Math.Clamp(holdDelayValue, 0, 15);

            // IHOLD_IRUN register: IHOLD(4:0), IRUN(12:8), IHOLDDELAY(19:16)
            uint ihold_irun = (uint)holdCurrentScale | ((uint)runCurrentScale << 8) | ((uint)holdDelayValue << 16);
            await WriteRegisterAsync(Registers.IHOLD_IRUN, ihold_irun);
            
            // Store the values for reference
            RunCurrentScale = runCurrentScale;
            HoldCurrentScale = holdCurrentScale;
            standstillDelay = holdDelay;
            
            // Calculate and update actual current values based on scale
            RunCurrent = ScaleToCurrent(runCurrentScale);
            HoldCurrent = ScaleToCurrent(holdCurrentScale);
        }
        
        /// <summary>
        /// Sets the motor current levels using scaling factors with millisecond delay
        /// </summary>
        /// <param name="runCurrentScale">Current scale when motor is moving (0-31)</param>
        /// <param name="holdCurrentScale">Current scale when motor is stationary (0-31)</param>
        /// <param name="holdDelayMs">Delay in milliseconds before reducing to hold current</param>
        public async Task SetMotorCurrentScaleAsync(int runCurrentScale, int holdCurrentScale, int holdDelayMs = 20)
        {
            await SetMotorCurrentScaleAsync(runCurrentScale, holdCurrentScale, TimeSpan.FromMilliseconds(holdDelayMs));
        }

        /// <summary>
        /// Sets motor velocity directly using UART mode
        /// </summary>
        /// <param name="velocity">Target velocity in microsteps per second</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task SetVelocityAsync(int velocity)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("Velocity control requires UART mode");
            }

            await WriteRegisterAsync(Registers.VACTUAL, (uint)velocity);
            CurrentVelocity = velocity;
        }
        
        /// <summary>
        /// Sets the rotational speed of the motor
        /// </summary>
        /// <param name="angularVelocity">Angular velocity</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task SetRotationalSpeedAsync(AngularVelocity angularVelocity)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            
            // Convert RPM to steps/second
            double stepsPerSecond = angularVelocity.RevolutionsPerMinute * StepsPerRevolution / 60.0;
            
            await SetVelocityAsync((int)stepsPerSecond);
        }
        
        /// <summary>
        /// Sets the motor acceleration rate
        /// </summary>
        /// <param name="acceleration">Acceleration value in steps/second²</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task SetAccelerationAsync(int acceleration)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
                throw new InvalidOperationException("Acceleration control requires UART mode");

            uint amax = (uint)acceleration;
            await WriteRegisterAsync(Registers.AMAX, amax);
        }
        
        /// <summary>
        /// Enables or disables MicroPlyer step interpolation for smoothing motion
        /// </summary>
        /// <param name="enabled">True to enable interpolation, false to disable</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task SetMicroPlyerInterpolationAsync(bool enabled)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("MicroPlyer configuration requires UART mode");
            }
            
            uint chopconf = await ReadRegisterAsync(Registers.CHOPCONF);
            if (enabled)
            {
                chopconf |= RegFields.INTPOL;
            }
            else
            {
                chopconf &= ~RegFields.INTPOL;
            }
            
            await WriteRegisterAsync(Registers.CHOPCONF, chopconf);
            MicroPlyerEnabled = enabled;
        }
        
        /// <summary>
        /// Configures automatic standstill power reduction settings
        /// </summary>
        /// <param name="enabled">Enable power reduction</param>
        /// <param name="delayTime">Delay time before reducing current</param>
        /// <param name="holdPercent">Percentage of run current to use during standstill (0-100)</param>
        /// <param name="mode">Standstill mode for power management</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task ConfigureStandstillPowerAsync(bool enabled, TimeSpan delayTime, int holdPercent, StandstillMode mode = StandstillMode.ReducedCurrent)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("Standstill power configuration requires UART mode");
            }
            
            // Validate input parameters
            delayTime = TimeSpan.FromMilliseconds(Math.Clamp(delayTime.TotalMilliseconds, 1, 5000));
            holdPercent = Math.Clamp(holdPercent, 0, 100);
            
            // Convert delay time to clock cycles (assuming 12MHz clock)
            // TPOWERDOWN is in units of 2^18 clock cycles (approx. 21.8ms per unit at 12MHz)
            uint tpowerdown = (uint)(delayTime.TotalMilliseconds * 12000 / 262144); // 2^18 = 262144
            tpowerdown = Math.Max(1, tpowerdown);
            
            // Set TPOWERDOWN register
            await WriteRegisterAsync(Registers.TPOWERDOWN, tpowerdown);
            
            // Get current run current scale
            int newHoldScale = (RunCurrentScale * holdPercent) / 100;
            newHoldScale = Math.Clamp(newHoldScale, 0, 31);
            
            // Update current settings with the new hold current
            await SetMotorCurrentScaleAsync(RunCurrentScale, newHoldScale, delayTime);
            
            // Configure standstill mode in PWMCONF register
            if (mode != StandstillMode.Normal)
            {
                uint pwmconf = await ReadRegisterAsync(Registers.PWMCONF);
                pwmconf &= ~RegFields.FREEWHEEL_MASK;
                
                switch (mode)
                {
                    case StandstillMode.Freewheeling:
                        pwmconf |= (1U << 20);
                        break;
                    case StandstillMode.ReducedCurrent:
                        // This is default behavior with IHOLD setting
                        break;
                    case StandstillMode.PassiveBraking:
                        pwmconf |= (2U << 20);
                        break;
                }
                
                await WriteRegisterAsync(Registers.PWMCONF, pwmconf);
            }
            
            // Enable/disable automatic current reduction via PDN_DISABLE bit in GCONF
            uint gconf = await ReadRegisterAsync(Registers.GCONF);
            if (!enabled)
            {
                gconf |= RegFields.PDN_DISABLE;
            }
            else
            {
                gconf &= ~RegFields.PDN_DISABLE;
            }
            await WriteRegisterAsync(Registers.GCONF, gconf);
            
            currentStandstillMode = mode;
            standstillDelay = delayTime;
            standstillHoldPercent = holdPercent;
        }
        
        /// <summary>
        /// Configures automatic standstill power reduction settings with millisecond delay
        /// </summary>
        /// <param name="enabled">Enable power reduction</param>
        /// <param name="delayTimeMs">Delay time in milliseconds before reducing current</param>
        /// <param name="holdPercent">Percentage of run current to use during standstill (0-100)</param>
        /// <param name="mode">Standstill mode for power management</param>
        public async Task ConfigureStandstillPowerAsync(bool enabled, int delayTimeMs, int holdPercent, StandstillMode mode = StandstillMode.ReducedCurrent)
        {
            await ConfigureStandstillPowerAsync(enabled, TimeSpan.FromMilliseconds(delayTimeMs), holdPercent, mode);
        }

        #endregion

        #region Advanced Feature Methods
        
        /// <summary>
        /// Configures StallGuard4 for sensorless homing and load measurement
        /// </summary>
        /// <param name="enabled">Enable or disable StallGuard</param>
        /// <param name="threshold">StallGuard threshold (-64 to +63), lower value = more sensitive</param>
        /// <param name="stopOnStall">Whether to stop motor when stall is detected</param>
        /// <param name="minVelocity">Minimum velocity for StallGuard operation in steps/s</param>
        /// <param name="maxVelocity">Maximum velocity for StallGuard operation in steps/s</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task ConfigureStallGuardAsync(bool enabled, int threshold, bool stopOnStall, int minVelocity = 10, int maxVelocity = 500)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("StallGuard configuration requires UART mode");
            }
            
            // Clamp threshold to valid range (-64 to 63)
            threshold = Math.Clamp(threshold, -64, 63);
            
            // SGT is sent as 7-bit 2's complement value
            uint sgtValue = (uint)(threshold & 0x7F);
            
            // Set SGTHRS register (determines when stall output becomes active)
            await WriteRegisterAsync(Registers.SGTHRS, sgtValue);
            
            // Configure COOLCONF register
            uint coolconf = 0;
            
            // Set SGT field (stall guard trigger level)
            coolconf |= ((sgtValue << 16) & RegFields.SGT_MASK);
            
            // Set SFILT bit (0 = standard filtered mode - recommended)
            if (!enabled)
            {
                coolconf |= RegFields.SFILT;
            }
            
            await WriteRegisterAsync(Registers.COOLCONF, coolconf);
            
            // Configure velocity thresholds for StallGuard operation
            // TCOOLTHRS: Lower velocity threshold - StallGuard enabled below this speed
            // TSTEP = fCLK / velocity (assuming 12MHz internal clock)
            uint tcoolthrs = (uint)(12000000 / minVelocity);
            await WriteRegisterAsync(Registers.TCOOLTHRS, tcoolthrs);
            
            // THIGH: Upper velocity threshold - StallGuard disabled above this speed
            if (maxVelocity > 0)
            {
                uint thigh = (uint)(12000000 / maxVelocity);
                await WriteRegisterAsync(Registers.THIGH, thigh);
            }
            
            // Configure DIAG output for stall indication if requested
            if (stopOnStall && enabled)
            {
                uint gconf = await ReadRegisterAsync(Registers.GCONF);
                // Set DIAG1_STALL bit to enable DIAG1 output on stall
                gconf |= RegFields.DIAG1_STALL;
                await WriteRegisterAsync(Registers.GCONF, gconf);
            }
            
            StallGuardEnabled = enabled;
            stallGuardThreshold = threshold;
        }
        
        /// <summary>
        /// Configures StallGuard4 with motor parameters for optimal sensitivity
        /// </summary>
        /// <param name="enabled">Enable or disable StallGuard</param>
        /// <param name="motorInductance">Motor coil inductance</param>
        /// <param name="motorCurrent">Motor operating current</param>
        /// <param name="stopOnStall">Whether to stop motor when stall is detected</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task ConfigureStallGuardWithMotorParamsAsync(bool enabled, Inductance motorInductance, Current motorCurrent, bool stopOnStall = true)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("StallGuard configuration requires UART mode");
            }
            
            // Store the motor parameters
            MotorInductance = motorInductance;
            
            // Calculate optimal threshold based on motor parameters
            int threshold = CalculateStallGuardThreshold(motorInductance, motorCurrent);
            
            // Apply the configuration
            await ConfigureStallGuardAsync(enabled, threshold, stopOnStall);
        }
        
        /// <summary>
        /// Reads the current StallGuard load measurement value
        /// </summary>
        /// <returns>StallGuard load value (0-1023), lower values mean higher load</returns>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task<int> GetStallGuardValueAsync()
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("StallGuard reading requires UART mode");
            }
            
            // Read DRV_STATUS register and extract SG_RESULT field (10-bit value)
            uint drvStatus = await ReadRegisterAsync(Registers.DRV_STATUS);
            return (int)(drvStatus & RegFields.SG_RESULT_MASK);
        }
        
        /// <summary>
        /// Auto-calibrates StallGuard threshold for optimal sensorless homing
        /// </summary>
        /// <param name="testVelocity">Test velocity in steps/s for calibration</param>
        /// <param name="stopOnStall">Whether to enable stopping when stall is detected</param>
        /// <returns>The optimum threshold value determined by calibration</returns>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task<int> CalibrateStallGuardAsync(int testVelocity = 60, bool stopOnStall = true)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("StallGuard calibration requires UART mode");
            }
            
            // Initial setup with high threshold (less sensitive) to avoid false triggers
            await ConfigureStallGuardAsync(true, 63, false, testVelocity / 2, testVelocity * 2);
            
            // Run the motor at the test velocity
            await SetVelocityAsync(testVelocity);
            await Task.Delay(500); // Allow motor to reach stable speed
            
            // Get initial StallGuard reading (should be measuring free running motor)
            int sgValue = await GetStallGuardValueAsync();
            
            // Stop the motor
            await SetVelocityAsync(0);
            await Task.Delay(100); // Allow motor to stop
            
            // Calculate an appropriate threshold
            // Per datasheet: SGTHRS should be below SG_RESULT when motor is free running
            // Recommended: use half of the SG_RESULT value
            int thresholdValue = Math.Max(0, sgValue / 2);
            
            // Apply the calculated threshold
            await ConfigureStallGuardAsync(true, thresholdValue, stopOnStall, testVelocity / 2, testVelocity * 2);
            
            return thresholdValue;
        }
        
        /// <summary>
        /// Configures CoolStep adaptive current control for energy efficiency
        /// </summary>
        /// <param name="enabled">Enable or disable CoolStep</param>
        /// <param name="lowerThreshold">Lower current increase threshold (0-15), 0 disables CoolStep</param>
        /// <param name="upperThreshold">Upper current decrease threshold (0-15)</param>
        /// <param name="currentIncStep">Step size for current increase (0-3: 1, 2, 4, 8)</param>
        /// <param name="currentDecStep">Step size for current decrease (0-3: 32, 8, 2, 1)</param>
        /// <param name="minCurrentScale">Minimum current scaling, false: 1/2 of IRUN, true: 1/4 of IRUN</param>
        /// <param name="coolStepThreshold">Minimum velocity for CoolStep operation in steps/s</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task ConfigureCoolStepAsync(bool enabled, int lowerThreshold = 1, int upperThreshold = 3, 
                                              int currentIncStep = 1, int currentDecStep = 0, 
                                              bool minCurrentScale = false, int coolStepThreshold = 200)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("CoolStep configuration requires UART mode");
            }
            
            // Validate parameters
            lowerThreshold = Math.Clamp(lowerThreshold, 0, 15);
            upperThreshold = Math.Clamp(upperThreshold, 0, 15);
            currentIncStep = Math.Clamp(currentIncStep, 0, 3);
            currentDecStep = Math.Clamp(currentDecStep, 0, 3);
            
            // Disable CoolStep if lower threshold is 0
            if (lowerThreshold == 0)
            {
                enabled = false;
            }
            
            // Construct COOLCONF register value
            uint coolconf = 0;
            
            if (enabled)
            {
                // SEMIN: Lower threshold for current increment - 4-bit value (0-15)
                coolconf |= (uint)(lowerThreshold & 0x0F);
                
                // SEMAX: Upper threshold for current decrement - 4-bit value (0-15)
                coolconf |= (uint)((upperThreshold & 0x0F) << 8);
                
                // SEUP: Current increment step size - 2-bit value (0-3)
                coolconf |= (uint)((currentIncStep & 0x03) << 5);
                
                // SEDN: Current decrement step size - 2-bit value (0-3)
                coolconf |= (uint)((currentDecStep & 0x03) << 13);
                
                // SEIMIN: Minimum current setting - 0: 1/2 IRUN, 1: 1/4 IRUN
                if (minCurrentScale)
                {
                    coolconf |= RegFields.SEIMIN;
                }
                
                // Keep any StallGuard configuration from before
                if (StallGuardEnabled)
                {
                    // SGT: StallGuard threshold value
                    coolconf |= ((uint)(stallGuardThreshold & 0x7F) << 16);
                }
                
                // Set velocity threshold for CoolStep operation
                // TCOOLTHRS: Threshold for enabling CoolStep
                uint tcoolthrs = (uint)(12000000 / coolStepThreshold);
                await WriteRegisterAsync(Registers.TCOOLTHRS, tcoolthrs);
            }
            
            // Write the COOLCONF register
            await WriteRegisterAsync(Registers.COOLCONF, coolconf);
            
            CoolStepEnabled = enabled;
            this.coolStepThreshold = lowerThreshold;
        }
        
        /// <summary>
        /// Auto-tunes CoolStep parameters based on StallGuard measurements
        /// </summary>
        /// <param name="testVelocity">Test velocity in steps/s for calibration</param>
        /// <returns>True if tuning was successful</returns>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task<bool> AutoTuneCoolStepAsync(int testVelocity = 200)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("CoolStep auto-tuning requires UART mode");
            }
            
            // First, we need StallGuard to be enabled and calibrated
            if (!StallGuardEnabled)
            {
                await CalibrateStallGuardAsync(testVelocity);
            }
            
            // Run motor at test velocity
            await SetVelocityAsync(testVelocity);
            await Task.Delay(500); // Let motor reach stable speed
            
            // Get StallGuard reading at normal running
            int sgResult = await GetStallGuardValueAsync();
            
            // Stop motor
            await SetVelocityAsync(0);
            await Task.Delay(100);
            
            // Calculate CoolStep parameters based on StallGuard readings
            // Per datasheet: SEMIN = 1 + SGTHRS/16
            int semin = 1 + (stallGuardThreshold / 16);
            semin = Math.Clamp(semin, 1, 15);
            
            // SEMAX: Usually 2-8 works well
            int semax = 4;
            
            // SEUP: Use 1 for 2 increment steps (good balance)
            int seup = 1;
            
            // SEDN: Use 1 for step size of 8 (good balance)
            int sedn = 1;
            
            // Configure CoolStep with derived parameters
            await ConfigureCoolStepAsync(true, semin, semax, seup, sedn, false, testVelocity / 2);
            
            return true;
        }
        
        /// <summary>
        /// Sets the motor's electrical parameters for optimized performance
        /// </summary>
        /// <param name="inductance">Motor coil inductance</param>
        /// <param name="resistance">Motor coil resistance</param>
        /// <param name="maxCurrent">Maximum motor current rating</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task SetMotorParametersAsync(Inductance inductance, Resistance resistance, Current maxCurrent)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("Motor parameter configuration requires UART mode");
            }
            
            // Store the motor parameters
            MotorInductance = inductance;
            MotorResistance = resistance;
            
            // Calculate optimal settings based on the motor parameters
            
            // Calculate L/R time constant in microseconds
            double lrTimeConstant = inductance.Microhenries / resistance.Ohms;
            
            // Choose appropriate chopper mode based on inductance
            // Low inductance motors work better in SpreadCycle
            // High inductance motors work better in StealthChop
            ChopperMode recommendedMode = inductance.Millihenries < 2.0 
                ? ChopperMode.SpreadCycle 
                : ChopperMode.StealthChop;
            
            // Configure current based on motor rating, but limit to TMC2209 capability
            Current runCurrent = maxCurrent * 0.8; // 80% of max for safety
            Current holdCurrent = maxCurrent * 0.5; // 50% for holding
            
            // Apply settings
            await ConfigureChopperModeAsync(recommendedMode);
            await SetMotorCurrentAsync(runCurrent, holdCurrent);
            
            // Configure other parameters based on motor characteristics
            
            // Chopper timing based on L/R time constant
            uint chopconf = await ReadRegisterAsync(Registers.CHOPCONF);
            
            // Set TBL (blank time) based on inductance
            chopconf &= ~RegFields.TBL_MASK;
            if (inductance.Millihenries < 1.0)
            {
                chopconf |= 1 << 15; // TBL = 1, shorter blank time for low inductance
            }
            else
            {
                chopconf |= 2 << 15; // TBL = 2, longer blank time for higher inductance
            }
            
            await WriteRegisterAsync(Registers.CHOPCONF, chopconf);
            
            // Configure VSENSE based on current range
            if (maxCurrent.Amps > 1.5)
            {
                // High current mode (VSENSE = 0.325V)
                chopconf &= ~RegFields.VSENSE;
                vsenseVoltage = new Voltage(0.325, Voltage.UnitType.Volts);
            }
            else
            {
                // Low current mode (VSENSE = 0.18V)
                chopconf |= RegFields.VSENSE;
                vsenseVoltage = new Voltage(0.18, Voltage.UnitType.Volts);
            }
            
            await WriteRegisterAsync(Registers.CHOPCONF, chopconf);
        }
        
        /// <summary>
        /// Gets detailed diagnostic information about the driver's current state
        /// </summary>
        /// <returns>Detailed driver diagnostics including temperature, load, and errors</returns>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task<DriverDiagnostics> GetDiagnosticsAsync()
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("Diagnostic reading requires UART mode");
            }
            
            uint drvStatus = await ReadRegisterAsync(Registers.DRV_STATUS);
            uint gstat = await ReadRegisterAsync(Registers.GSTAT);
            uint tstep = await ReadRegisterAsync(Registers.TSTEP);
            
            var diagnostics = new DriverDiagnostics
            {
                Status = (DriverStatus)drvStatus,
                StallGuardValue = (int)(drvStatus & RegFields.SG_RESULT_MASK),
                OvertemperatureWarning = (drvStatus & (1U << 29)) != 0,
                OvertemperatureShutdown = (drvStatus & (1U << 28)) != 0,
                ShortToGround = (drvStatus & (1U << 27)) != 0,
                OpenLoad = (drvStatus & (1U << 26)) != 0,
                OvertemperaturePreWarning = (drvStatus & (1U << 25)) != 0,
                StealthChopMode = (drvStatus & RegFields.STEALTH) != 0,
                StandstillDetected = (drvStatus & RegFields.STST) != 0,
                ActualMotorCurrent = (int)((drvStatus & RegFields.CS_ACTUAL_MASK) >> 16),
                StandstillMode = currentStandstillMode,
                CurrentVelocity = CurrentVelocity,
                CurrentChopperMode = CurrentChopperMode,
                MicroStepPosition = (int)((drvStatus >> 10) & 0x3FF),
                
                // TSTEP can be converted to steps/s: f_step = f_clk / TSTEP
                // Assuming 12MHz internal clock
                MeasuredSpeed = (tstep != 0) ? (int)(12000000 / tstep) : 0,
                
                // Reset status bits
                ResetFlag = (gstat & 0x01) != 0,
                DriverErrorFlag = (gstat & 0x02) != 0,
                ChargePumpUnderVoltageFlag = (gstat & 0x04) != 0,
                
                // Measured physical values
                MeasuredCurrent = ScaleToCurrent((int)((drvStatus & RegFields.CS_ACTUAL_MASK) >> 16)),
                RunCurrentSetting = RunCurrent,
                HoldCurrentSetting = HoldCurrent,
                MotorParametersSet = (MotorInductance.Millihenries > 0 && MotorResistance.Ohms > 0)
            };
            
            return diagnostics;
        }

        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Converts a physical current value to TMC2209 scale factor (0-31)
        /// </summary>
        protected int CurrentToScale(Current current)
        {
            // TMC2209 current scale depends on VSENSE setting and sense resistor
            // Assuming vsense and rsense are known values
            double maxCurrentRms = vsenseVoltage.Volts / (0.11 * 0.707); // Typical Rsense = 0.11Ω
            
            // Calculate the scale (0-31)
            int scale = (int)Math.Round(current.Amps * 31.0 / maxCurrentRms);
            
            // Clamp to valid range
            return Math.Clamp(scale, 0, 31);
        }
        
        /// <summary>
        /// Converts a TMC2209 scale factor to physical current
        /// </summary>
        protected Current ScaleToCurrent(int currentScale)
        {
            // TMC2209 current scale depends on VSENSE setting and sense resistor
            // Assuming vsense and rsense are known values
            double maxCurrentRms = vsenseVoltage.Volts / (0.11 * 0.707); // Typical Rsense = 0.11Ω
            
            // Calculate the current
            double amps = maxCurrentRms * currentScale / 31.0;
            
            return new Current(amps, Current.UnitType.Amps);
        }
        
        /// <summary>
        /// Calculates the optimal StallGuard threshold based on motor parameters
        /// </summary>
        protected int CalculateStallGuardThreshold(Inductance inductance, Current current)
        {
            // This is a simplified version - actual calculations might need to be more complex
            // and may vary based on motor characteristics
            
            // The L/R time constant affects the stall detection sensitivity
            // Lower inductance motors need more sensitive thresholds
            double lrRatio = inductance.Millihenries / (MotorResistance.Ohms);
            
            // 10 is a reasonable midpoint for the threshold
            // Adjust based on L/R ratio, which affects motor electrical behavior
            int baseThreshold = 10;
            
            // Adjust threshold based on L/R time constant
            // Lower L/R (faster electrical response) = more negative threshold (more sensitive)
            int adjustment = (int)(lrRatio * 20);
            
            // Clamp to valid range for SGT (-64 to 63)
            return Math.Clamp(baseThreshold - adjustment, -64, 63);
        }

        #endregion

        #region UART Communication Methods

        /// <summary>
        /// Implementation of stepping using UART interface mode
        /// </summary>
        /// <param name="steps">Number of steps to move</param>
        protected virtual async Task StepUsingUart(int steps)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            
            // Calculate direction-aware velocity
            int velocity = steps > 0 ? 1000 : -1000;
            
            // Start the motor
            await SetVelocityAsync(velocity);
            
            // Wait for calculated time to complete steps
            await Task.Delay(Math.Abs(steps));
            
            // Stop the motor
            await SetVelocityAsync(0);
        }
        
        /// <summary>
        /// Calculates CRC checksum for UART communication
        /// </summary>
        /// <param name="data">Data bytes to calculate CRC for</param>
        /// <param name="length">Number of bytes to include in calculation</param>
        /// <returns>Calculated CRC value</returns>
        protected byte CalculateCrc(byte[] data, int length)
        {
            const int CRC_POLYNOMIAL = 0b100000111;
            byte crc = 0;

            for (int i = 0; i < length; i++)
            {
                byte currentByte = data[i];
                for (int j = 0; j < 8; j++)
                {
                    if (((crc >> 7) ^ (currentByte & 0x01)) != 0)
                    {
                        crc = (byte)((crc << 1) ^ CRC_POLYNOMIAL);
                    }
                    else
                    {
                        crc = (byte)(crc << 1);
                    }
                    currentByte >>= 1;
                }
            }
            return crc;
        }

        /// <summary>
        /// Writes a value to a TMC2209 register using UART
        /// </summary>
        /// <param name="register">Target register address</param>
        /// <param name="value">Value to write to register</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        protected async Task WriteRegisterAsync(byte register, uint value)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
                throw new InvalidOperationException("WriteRegister requires UART mode");

            byte[] datagram = new byte[8];
            datagram[0] = 0x05;
            datagram[1] = nodeAddress;
            datagram[2] = (byte)(register | 0x80);
            datagram[3] = (byte)(value >> 24);
            datagram[4] = (byte)(value >> 16);
            datagram[5] = (byte)(value >> 8);
            datagram[6] = (byte)value;
            datagram[7] = CalculateCrc(datagram, 7);

            await WriteAsync(datagram, 0, datagram.Length);
        }

        /// <summary>
        /// Reads a value from a TMC2209 register using UART
        /// </summary>
        /// <param name="register">Register address to read from</param>
        /// <returns>Value read from register</returns>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode or CRC verification fails</exception>
        protected async Task<uint> ReadRegisterAsync(byte register)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (CurrentMode != InterfaceMode.Uart)
                throw new InvalidOperationException("ReadRegister requires UART mode");

            byte[] request = new byte[4];
            request[0] = 0x05;
            request[1] = nodeAddress;
            request[2] = register;
            request[3] = CalculateCrc(request, 3);

            await WriteAsync(request, 0, request.Length);

            byte[] response = new byte[8];
            await ReadAsync(response, 0, response.Length);

            byte receivedCrc = response[7];
            byte calculatedCrc = CalculateCrc(response, 7);
            if (receivedCrc != calculatedCrc)
            {
                throw new InvalidOperationException("CRC verification failed on read response");
            }

            return ((uint)response[3] << 24) |
                   ((uint)response[4] << 16) |
                   ((uint)response[5] << 8) |
                    response[6];
        }

        /// <summary>
        /// Writes data to the serial port
        /// </summary>
        /// <param name="data">Data buffer to write</param>
        /// <param name="offset">Starting offset in buffer</param>
        /// <param name="length">Number of bytes to write</param>
        /// <exception cref="InvalidOperationException">Thrown when serial port is not initialized</exception>
        protected async Task WriteAsync(byte[] data, int offset, int length)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (serialPort == null)
                throw new InvalidOperationException("Serial port not initialized");
            await Task.Run(() => serialPort.Write(data, offset, length));
        }

        /// <summary>
        /// Reads data from the serial port
        /// </summary>
        /// <param name="data">Buffer to store read data</param>
        /// <param name="offset">Starting offset in buffer</param>
        /// <param name="length">Number of bytes to read</param>
        /// <exception cref="InvalidOperationException">Thrown when serial port is not initialized</exception>
        protected async Task ReadAsync(byte[] data, int offset, int length)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (serialPort == null)
                throw new InvalidOperationException("Serial port not initialized");
            await Task.Run(() => serialPort.Read(data, offset, length));
        }

        /// <summary>
        /// Initializes the driver in UART mode with default settings
        /// </summary>
        protected virtual async Task InitializeUartMode()
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            
            // Wait for communication to establish
            await Task.Delay(10);
            
            // Initialize with default settings
            
            // 1. Global configuration - Start with a clean state
            // - Internal reference vs. analog scaling (I_SCALE_ANALOG=0)
            // - Normal operation without test modes
            // - No fancy index output configuration
            await WriteRegisterAsync(Registers.GCONF, 0x00);
            
            // 2. Configure chopper settings - CHOPCONF
            uint chopconf = 0x10000053; // Default value, includes:
            // - TOFF=3: Default off time
            // - HSTRT=4: Default hysteresis start
            // - HEND=1: Default hysteresis end
            // - TBL=2: Default blank time
            // - MRES=0: Full step resolution
            // - INTPOL=1: Enable microstep interpolation
            await WriteRegisterAsync(Registers.CHOPCONF, chopconf);
            
            // 3. Enable StealthChop PWM mode - PWMCONF
            uint pwmconf = 0xC10D0024; // Default value, includes:
            // - PWM_AUTOSCALE=1: Enable automatic current scaling
            // - PWM_AUTOGRAD=1: Enable automatic gradient adaptation
            // - PWM_FREQ=1: f_pwm=2/683 * f_clk
            // - PWM_GRAD=4: PWM gradient
            // - PWM_OFS=36: PWM offset value
            await WriteRegisterAsync(Registers.PWMCONF, pwmconf);
            
            // 4. Set motor current - IHOLD_IRUN
            // - IRUN=16: Run current (default)
            // - IHOLD=8: Hold current (half of run current)
            // - IHOLDDELAY=4: Delay before reducing current
            await SetMotorCurrentScaleAsync(RunCurrentScale, HoldCurrentScale, 4);
            
            // 5. Set power down delay - TPOWERDOWN
            await WriteRegisterAsync(Registers.TPOWERDOWN, 10); // ~218ms delay
            
            // 6. Set microstepping configuration
            await SetMicrosteppingAsync(CurrentStepDivisor);
            
            // 7. Configure CoolStep with default OFF settings
            uint coolconf = 0; // CoolStep disabled by default
            await WriteRegisterAsync(Registers.COOLCONF, coolconf);
            
            // 8. Configure StallGuard with default OFF settings
            await WriteRegisterAsync(Registers.SGTHRS, 0); // StallGuard disabled by default
            
            // 9. Apply default motion profile
            await ConfigureMotionProfileAsync(MotionProfile.Standard);
        }

        #endregion

        #region Step/Dir Implementation

        /// <summary>
        /// Implementation of stepping using Step/Dir interface mode
        /// </summary>
        /// <param name="steps">Number of steps to move</param>
        protected virtual void StepUsingStepDir(int steps)
        {
            if (IsDisposed) { throw new ObjectDisposedException(nameof(Tmc2209)); }
            if (stepPort == null || directionPort == null)
            {
                throw new InvalidOperationException("Step and direction ports must be initialized");
            }
            
            // Set direction based on sign of steps
            bool clockwise = steps >= 0 || Direction == RotationDirection.Clockwise;
            if (steps < 0)
            {
                clockwise = !clockwise;
            }
            
            directionPort.State = clockwise;
            
            // Allow time for direction signal to settle
            Task.Delay(1).Wait();
            
            // Generate step pulses
            for (int i = 0; i < Math.Abs(steps); i++)
            {
                stepPort.State = true;
                Task.Delay(1).Wait();
                stepPort.State = false;
                Task.Delay(1).Wait();
            }
        }

        #endregion

        #region Disposal Pattern

        /// <summary>
        /// Implements IDisposable
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern
        /// </summary>
        /// <param name="disposing">True when called from Dispose(), false when called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    // Stop the motor if it's moving
                    try
                    {
                        if (CurrentMode == InterfaceMode.Uart)
                        {
                            SetVelocityAsync(0).Wait();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error stopping motor during disposal: {ex.Message}");
                    }
                    
                    // Dispose of managed resources
                    if (createdPorts)
                    {
                        stepPort?.Dispose();
                        directionPort?.Dispose();
                        enablePort?.Dispose();
                    }
                    
                    // Dispose of serial port if we're in UART mode
                    if (CurrentMode == InterfaceMode.Uart && serialPort != null)
                    {
                        serialPort.Dispose();
                    }
                }

                IsDisposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Contains detailed diagnostic information about the TMC2209 driver
    /// </summary>
    public class DriverDiagnostics
    {
        /// <summary>
        /// Raw driver status flags
        /// </summary>
        public Tmc2209.DriverStatus Status { get; internal set; }
        
        /// <summary>
        /// Current StallGuard load measurement (0-1023)
        /// Lower values indicate higher motor load
        /// </summary>
        public int StallGuardValue { get; internal set; }
        
        /// <summary>
        /// Whether a temperature warning threshold has been exceeded
        /// </summary>
        public bool OvertemperatureWarning { get; internal set; }
        
        /// <summary>
        /// Whether temperature shutdown has occurred
        /// </summary>
        public bool OvertemperatureShutdown { get; internal set; }
        
        /// <summary>
        /// Whether a short to ground condition is detected
        /// </summary>
        public bool ShortToGround { get; internal set; }
        
        /// <summary>
        /// Whether an open load condition is detected (disconnected motor)
        /// </summary>
        public bool OpenLoad { get; internal set; }
        
        /// <summary>
        /// Pre-warning flag for approaching temperature limit
        /// </summary>
        public bool OvertemperaturePreWarning { get; internal set; }
        
        /// <summary>
        /// Indicates whether StealthChop mode is currently active
        /// </summary>
        public bool StealthChopMode { get; internal set; }
        
        /// <summary>
        /// Indicates whether motor standstill has been detected
        /// </summary>
        public bool StandstillDetected { get; internal set; }
        
        /// <summary>
        /// Actual motor current scaling being applied (0-31)
        /// </summary>
        public int ActualMotorCurrent { get; internal set; }
        
        /// <summary>
        /// Current standstill mode setting
        /// </summary>
        public Tmc2209.StandstillMode StandstillMode { get; internal set; }
        
        /// <summary>
        /// Current velocity in steps per second
        /// </summary>
        public int CurrentVelocity { get; internal set; }
        
        /// <summary>
        /// Current chopper mode setting
        /// </summary>
        public Tmc2209.ChopperMode CurrentChopperMode { get; internal set; }
        
        /// <summary>
        /// Current micro step position within full step
        /// </summary>
        public int MicroStepPosition { get; internal set; }
        
        /// <summary>
        /// Measured motor speed in steps per second
        /// </summary>
        public int MeasuredSpeed { get; internal set; }
        
        /// <summary>
        /// Indicates whether driver has been reset
        /// </summary>
        public bool ResetFlag { get; internal set; }
        
        /// <summary>
        /// Indicates whether driver error has occurred
        /// </summary>
        public bool DriverErrorFlag { get; internal set; }
        
        /// <summary>
        /// Indicates whether charge pump undervoltage has occurred
        /// </summary>
        public bool ChargePumpUnderVoltageFlag { get; internal set; }
        
        /// <summary>
        /// Actual measured motor current
        /// </summary>
        public Current MeasuredCurrent { get; internal set; }
        
        /// <summary>
        /// Current run current setting
        /// </summary>
        public Current RunCurrentSetting { get; internal set; }
        
        /// <summary>
        /// Current hold current setting
        /// </summary>
        public Current HoldCurrentSetting { get; internal set; }
        
        /// <summary>
        /// Indicates whether motor parameters have been set
        /// </summary>
        public bool MotorParametersSet { get; internal set; }
        
        /// <summary>
        /// Returns a formatted string with diagnostic information
        /// </summary>
        public override string ToString()
        {
            return $"TMC2209 Diagnostics:\n" +
                   $"Motor Current: {MeasuredCurrent.Amps:F2}A / {RunCurrentSetting.Amps:F2}A max\n" +
                   $"StallGuard Value: {StallGuardValue}\n" +
                   $"Measured Speed: {MeasuredSpeed} steps/s\n" +
                   $"Temperature Warning: {OvertemperatureWarning}\n" + 
                   $"Temperature Shutdown: {OvertemperatureShutdown}\n" +
                   $"Short To Ground: {ShortToGround}\n" +
                   $"Open Load: {OpenLoad}\n" +
                   $"Chopper Mode: {CurrentChopperMode}\n" +
                   $"StealthChop Active: {StealthChopMode}\n" +
                   $"Standstill Detected: {StandstillDetected}\n" +
                   $"Micro Step Position: {MicroStepPosition}\n" +
                   $"Reset Flag: {ResetFlag}\n" +
                   $"Driver Error: {DriverErrorFlag}";
        }
    }
}