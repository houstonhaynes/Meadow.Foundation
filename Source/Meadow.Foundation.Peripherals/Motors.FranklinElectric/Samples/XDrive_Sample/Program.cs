using Meadow.Foundation.VFDs.FranklinElectric;
using Meadow.Modbus;

namespace Motors.FranklinElectric.XDrive_Sample;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var port = new SerialPortShim("COM12", 9600, Meadow.Hardware.Parity.None, 8, Meadow.Hardware.StopBits.One);
        var modbus = new ModbusRtuClient(port);
        var vfd = new XDrive(modbus, 1);
        await vfd.Connect();

        var v = await vfd.ReadOutputVoltage();
        var i = await vfd.ReadOutputCurrent();
        var f = await vfd.ReadOutputFrequency();
        var dcb = await vfd.ReadDCBusVoltage();

        var o = await vfd.ReadOperationalStatus();
        var e = await vfd.ReadErrorCodes();

        var t1 = await vfd.ReadIGBTTemperature();
        var t2 = await vfd.ReadAmbientTemperature();

        var s = await vfd.ReadDriveStatus();
        var cm = await vfd.ReadControlMode();

        var dis = await vfd.ReadDigitalInputStatus();
        var dos = await vfd.ReadDigitalOutputStatus();
    }
}