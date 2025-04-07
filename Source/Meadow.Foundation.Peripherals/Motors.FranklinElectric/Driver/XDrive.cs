using Meadow.Modbus;
using Meadow.Units;
using System.Threading.Tasks;

namespace Meadow.Foundation.VFDs.FranklinElectric;

public class XDrive
{
    private readonly ModbusRtuClient _modbus;
    private readonly byte _busAddress;

    public XDrive(ModbusRtuClient modbusRtuClient, byte busAddress)
    {
        _modbus = modbusRtuClient;
        _busAddress = busAddress;
    }

    public async Task Foo()
    {
        var r = await _modbus.ReadHoldingRegisters(1, 8449, 1);

        // 8832 = 8192 + 512 + 128        = 0b00100010_10000000
        // 9472 = 8192 + 1024 + 256       = 0b00100101_00000000
        // 
    }

    public Task Connect()
    {
        return _modbus.Connect();
    }

    public void Disconnect()
    {
    }

    public async Task<ushort> ReadErrorCodes()
    {
        var f = await ReadRegister(Registers.ErrorCode);
        return f;
    }

    public async Task<ushort> ReadOperationalStatus()
    {
        var f = await ReadRegister(Registers.OperationStatus);
        return f;
    }

    public async Task<Frequency> ReadOutputFrequency()
    {
        var f = await ReadRegister(Registers.OutputFrequency);
        return new Frequency(f / 100d, Frequency.UnitType.Hertz);
    }

    public async Task<Current> ReadOutputCurrent()
    {
        var f = await ReadRegister(Registers.OutputCurrent);
        return new Current(f / 10d, Current.UnitType.Amps);
    }

    public async Task<Voltage> ReadDCBusVoltage()
    {
        var f = await ReadRegister(Registers.DCBusVoltage);
        return new Voltage(f / 10d, Voltage.UnitType.Volts);
    }

    public async Task<Voltage> ReadOutputVoltage()
    {
        var f = await ReadRegister(Registers.OutputVoltage);
        return new Voltage(f / 10d, Voltage.UnitType.Volts);
    }

    public async Task<Temperature> ReadIGBTTemperature()
    {
        var f = await ReadRegister(Registers.IGBTTemperature);
        return new Temperature(f / 10d, Temperature.UnitType.Celsius);
    }

    public async Task<Temperature> ReadAmbientTemperature()
    {
        var f = await ReadRegister(Registers.AmbientTemperature);
        return new Temperature(f / 10d, Temperature.UnitType.Celsius);
    }

    public async Task<ushort> ReadDriveStatus()
    {
        return await ReadRegister(Registers.DriveStatus);
    }

    public async Task<ushort> ReadControlMode()
    {
        return await ReadRegister(Registers.ControlMode);
    }

    public async Task<ushort> ReadDigitalOutputStatus()
    {
        return await ReadRegister(Registers.DigitalOutputStatus);
    }

    public async Task<ushort> ReadDigitalInputStatus()
    {
        return await ReadRegister(Registers.DigitalInputStatus);
    }

    private async Task WriteRegister(Registers register, ushort value)
    {
        await _modbus.WriteHoldingRegister(_busAddress, (ushort)register, value);
    }

    private async Task<ushort> ReadRegister(Registers register)
    {
        var r = await _modbus.ReadHoldingRegisters(_busAddress, (ushort)register, 1);
        return r[0];
    }
}
