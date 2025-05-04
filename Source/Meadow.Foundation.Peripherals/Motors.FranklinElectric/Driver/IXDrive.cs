using Meadow.Units;
using System.Threading.Tasks;

namespace Meadow.Foundation.VFDs.FranklinElectric;
public interface IXDrive
{
    Task Connect();
    void Disconnect();
    Task<Temperature> ReadAmbientTemperature();
    Task<ushort> ReadControlMode();
    Task<Voltage> ReadDCBusVoltage();
    Task<ushort> ReadDigitalInputStatus();
    Task<ushort> ReadDigitalOutputStatus();
    Task<ushort> ReadDriveStatus();
    Task<ushort> ReadErrorCodes();
    Task<Temperature> ReadIGBTTemperature();
    Task<ushort> ReadOperationalStatus();
    Task<Current> ReadOutputCurrent();
    Task<Frequency> ReadOutputFrequency();
    Task<Voltage> ReadOutputVoltage();
}