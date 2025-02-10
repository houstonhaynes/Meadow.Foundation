using Meadow.Hardware;
using Meadow.Peripherals;
using Meadow.Units;
using System;
using System.Threading.Tasks;
using AU = Meadow.Units.Angle.UnitType;

namespace Meadow.Foundation.Motors.Stepper
{
    /// <summary>
    /// Driver for the Trinamic TMC2209 stepper motor controller, supporting both Step/Dir and UART modes
    /// </summary>
    public class Tmc2209 : IDisposable
    {
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
            HighVelocity
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
            SpreadCycle
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
        /// TMC2209 register addresses
        /// </summary>
        protected static class Registers
        {
            /// <summary>Global configuration flags</summary>
            public const byte GCONF = 0x00;
            /// <summary>Global status flags</summary>
            public const byte GSTAT = 0x01;
            /// <summary>Chopper configuration</summary>
            public const byte CHOPCONF = 0x6C;
            /// <summary>Coolstep configuration</summary>
            public const byte COOLCONF = 0x6D;
            /// <summary>PWM configuration</summary>
            public const byte PWMCONF = 0x70;
            /// <summary>Driver status flags</summary>
            public const byte DRV_STATUS = 0x6F;
            /// <summary>Motor current control</summary>
            public const byte IHOLD_IRUN = 0x10;
            /// <summary>Velocity control</summary>
            public const byte VACTUAL = 0x22;
            /// <summary>Acceleration control</summary>
            public const byte AMAX = 0x22;
        }

        /// <summary>
        /// Register field bit masks
        /// </summary>
        protected static class RegFields
        {
            public const uint MRES_MASK = 0x0F000000;
            public const uint I_SCALE_ANALOG = 0x00000001;
            public const uint INTERNAL_RSENSE = 0x00000002;
            public const uint EN_SPREADCYCLE = 0x00000004;
            public const uint SHAFT = 0x00000008;
            public const uint TOFF_MASK = 0x0000000F;
            public const uint HSTRT_MASK = 0x00000070;
            public const uint HEND_MASK = 0x00000780;
        }

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
        /// Indicates if the driver has been disposed
        /// </summary>
        public bool IsDisposed { get; private set; }

        private readonly IDigitalOutputPort stepPort;
        private readonly IDigitalOutputPort directionPort;
        private readonly IDigitalOutputPort? enablePort;
        private readonly ISerialPort serialPort;
        private readonly object syncRoot = new();
        private readonly bool createdPorts = false;
        private Angle stepAngle;
        private byte nodeAddress;

        /// <summary>
        /// Initializes a new instance of TMC2209 in Step/Dir mode
        /// </summary>
        /// <param name="step">Step signal pin</param>
        /// <param name="direction">Direction signal pin</param>
        /// <param name="enable">Optional enable signal pin</param>
        public Tmc2209(IPin step, IPin direction, IPin? enable = null)
        {
            createdPorts = true;
            stepPort = step.CreateDigitalOutputPort();
            directionPort = direction.CreateDigitalOutputPort();
            enablePort = enable?.CreateDigitalOutputPort();
            CurrentMode = InterfaceMode.StepDir;
            StepAngle = new Angle(1.8, AU.Degrees);
            CurrentStepDivisor = StepDivisor.Divisor1;
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
            InitializeUartMode().Wait();
        }

        /// <summary>
        /// Rotates the motor by specified degrees
        /// </summary>
        /// <param name="degrees">Number of degrees to rotate</param>
        /// <param name="direction">Rotation direction</param>
        public void Rotate(float degrees, RotationDirection direction = RotationDirection.Clockwise)
        {
            Direction = direction;
            var steps = (int)(StepsPerRevolution / 360f * degrees);
            Step(steps);
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
        /// Sets the microstepping resolution
        /// </summary>
        /// <param name="divisor">Desired step divisor</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task SetMicrosteppingAsync(StepDivisor divisor)
        {
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
        /// Enables or disables the motor driver
        /// </summary>
        /// <param name="enabled">True to enable, false to disable</param>
        public void Enable(bool enabled)
        {
            if (enablePort != null)
            {
                enablePort.State = !enabled;
            }
            else if (CurrentMode == InterfaceMode.Uart)
            {
                WriteRegisterAsync(Registers.GCONF, enabled ? 0U : 1U).Wait();
            }
        }

        /// <summary>
        /// Configures the motor using a predefined motion profile
        /// </summary>
        /// <param name="profile">Motion profile to apply</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task ConfigureMotionProfileAsync(MotionProfile profile)
        {
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("Motion profiles require UART mode");
            }

            switch (profile)
            {
                case MotionProfile.HighPrecision:
                    await ConfigureChopperModeAsync(ChopperMode.StealthChop);
                    await SetMotorCurrentAsync(16, 8);
                    await SetAccelerationAsync(100);
                    break;
                case MotionProfile.Standard:
                    await ConfigureChopperModeAsync(ChopperMode.StealthChop);
                    await SetMotorCurrentAsync(24, 16);
                    await SetAccelerationAsync(200);
                    break;
                case MotionProfile.HighVelocity:
                    await ConfigureChopperModeAsync(ChopperMode.SpreadCycle);
                    await SetMotorCurrentAsync(31, 16);
                    await SetAccelerationAsync(400);
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
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("Chopper mode configuration requires UART mode");
            }

            uint chopconf = await ReadRegisterAsync(Registers.CHOPCONF);
            if (mode == ChopperMode.SpreadCycle)
            {
                chopconf |= RegFields.EN_SPREADCYCLE;
            }
            else
            {
                chopconf &= ~RegFields.EN_SPREADCYCLE;
            }
            await WriteRegisterAsync(Registers.CHOPCONF, chopconf);
        }

        /// <summary>
        /// Sets the motor current levels
        /// </summary>
        /// <param name="holdCurrent">Current when motor is stationary (0-31)</param>
        /// <param name="runCurrent">Current when motor is moving (0-31)</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task SetMotorCurrentAsync(int holdCurrent, int runCurrent)
        {
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("Motor current configuration requires UART mode");
            }

            uint ihold_irun = ((uint)runCurrent << 8) | (uint)holdCurrent;
            await WriteRegisterAsync(Registers.IHOLD_IRUN, ihold_irun);
        }

        /// <summary>
        /// Sets motor velocity directly using UART mode
        /// </summary>
        /// <param name="velocity">Target velocity in microsteps per second</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task SetVelocityAsync(int velocity)
        {
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("Velocity control requires UART mode");
            }

            await WriteRegisterAsync(Registers.VACTUAL, (uint)velocity);
        }

        /// <summary>
        /// Gets the current status of the motor driver
        /// </summary>
        /// <returns>Status flags indicating current driver state</returns>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task<DriverStatus> GetStatusAsync()
        {
            if (CurrentMode != InterfaceMode.Uart)
            {
                throw new InvalidOperationException("Status reading requires UART mode");
            }

            uint drvStatus = await ReadRegisterAsync(Registers.DRV_STATUS);
            return (DriverStatus)drvStatus;
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
            if (serialPort == null)
                throw new InvalidOperationException("Serial port not initialized");
            await Task.Run(() => serialPort.Read(data, offset, length));
        }

        /// <summary>
        /// Implementation of stepping using Step/Dir interface mode
        /// </summary>
        /// <param name="steps">Number of steps to move</param>
        protected virtual void StepUsingStepDir(int steps)
        {
            directionPort.State = Direction == RotationDirection.Clockwise;
            
            for (int i = 0; i < Math.Abs(steps); i++)
            {
                stepPort.State = true;
                Task.Delay(1).Wait();
                stepPort.State = false;
                Task.Delay(1).Wait();
            }
        }

        /// <summary>
        /// Implementation of stepping using UART interface mode
        /// </summary>
        /// <param name="steps">Number of steps to move</param>
        protected virtual async Task StepUsingUart(int steps)
        {
            int velocity = (steps > 0) ? 1000 : -1000;
            await SetVelocityAsync(velocity);
            await Task.Delay(Math.Abs(steps));
            await SetVelocityAsync(0);
        }

        /// <summary>
        /// Initializes the driver in UART mode with default settings
        /// </summary>
        protected virtual async Task InitializeUartMode()
        {
            await WriteRegisterAsync(Registers.GCONF, 0x00);
            await SetMicrosteppingAsync(CurrentStepDivisor);
            await ConfigureMotionProfileAsync(MotionProfile.Standard);
        }

        /// <summary>
        /// Sets the motor acceleration rate
        /// </summary>
        /// <param name="acceleration">Acceleration value in steps/second²</param>
        /// <exception cref="InvalidOperationException">Thrown when not in UART mode</exception>
        public async Task SetAccelerationAsync(int acceleration)
        {
            if (CurrentMode != InterfaceMode.Uart)
                throw new InvalidOperationException("Acceleration control requires UART mode");

            uint amax = (uint)acceleration;
            await WriteRegisterAsync(Registers.AMAX, amax);
        }

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
                if (disposing && createdPorts)
                {
                    stepPort?.Dispose();
                    directionPort?.Dispose();
                    enablePort?.Dispose();
                    serialPort?.Dispose();
                }
                IsDisposed = true;
            }
        }
    }}