using Meadow.Common;
using Meadow.Units;
using System;
using System.Collections.Generic;

namespace Meadow.Foundation;

public interface ISensor
{
    string Name { get; set; }
    Type UnitType { get; set; }
}

public interface ISensorService
{
    IEnumerable<ISensor> GetAvailableSensors();
    object GetSensorValue(string sensorName);
}

public class CurrentOutOfRangeException : Exception
{
    public CurrentOutOfRangeException(string message)
        : base(message)
    {
    }
}

public abstract class ProgrammableAnalogInputModuleBase : IProgrammableAnalogInputModule
{
    protected readonly ChannelConfig[] channelConfigs;
    public int ChannelCount => channelConfigs.Length;

    public abstract Voltage Read0_10V(int channelNumber);
    public abstract Current Read0_20mA(int channelNumber);
    public abstract Current Read4_20mA(int channelNumber);
    public abstract Voltage ReadChannelRaw(int channelNumber);
    public abstract Temperature ReadNtc(int channelNumber, double beta, Temperature referenceTemperature, Resistance resistanceAtRefTemp);

    protected ProgrammableAnalogInputModuleBase(int channelCount)
    {
        channelConfigs = new ChannelConfig[channelCount];
    }

    public virtual void ConfigureChannel(int channelNumber, ChannelConfig channelConfiguration)
    {
        if (channelNumber < 0 || channelNumber > ChannelCount - 1)
        {
            throw new ArgumentException("Invalid channelNumber");
        }

        channelConfigs[channelNumber] = channelConfiguration;

    }

    public Temperature ReadNtc(int channelNumber)
    {
        return ReadNtc(channelNumber, 3950, new Temperature(25, Temperature.UnitType.Celsius), new Resistance(10_000, Resistance.UnitType.Ohms));
    }

    public object ReadChannelAsConfiguredUnit(int channelNumber)
    {
        switch (channelConfigs[channelNumber].ChannelType)
        {
            case ConfigurableAnalogInputChannelType.Current_0_20:
            case ConfigurableAnalogInputChannelType.Current_4_20:
                return ReadCurrentAsUnits(channelNumber, channelConfigs[channelNumber].ChannelType);
            case ConfigurableAnalogInputChannelType.Voltage_0_10:
                return ReadVoltageAsUnits(channelNumber);
        }

        throw new NotSupportedException();
    }

    private object ReadCurrentAsUnits(int channelNumber, ConfigurableAnalogInputChannelType channelType)
    {
        Current current;

        switch (channelType)
        {
            case ConfigurableAnalogInputChannelType.Current_4_20:
                current = Read4_20mA(channelNumber);
                if (current.Milliamps < 3.9)
                {
                    throw new CurrentOutOfRangeException("Undercurrent condition.  Check the sensor and wiring");
                }
                break;
            case ConfigurableAnalogInputChannelType.Current_0_20:
                current = Read0_20mA(channelNumber);
                break;
            default:
                throw new ArgumentException();
        }

        if (current.Milliamps > 20.1)
        {
            throw new CurrentOutOfRangeException("Overcurrent condition.  Check the sensor and wiring");
        }

        var rawUnit = UnitFactory.CreateUnitFromCanonicalValue(current.Milliamps, channelConfigs[channelNumber].UnitType);

        if (rawUnit is Temperature temperature)
        {
            var c = temperature.Celsius * channelConfigs[channelNumber].Scale;
            c += channelConfigs[channelNumber].Offset;
            return new Temperature(c, Temperature.UnitType.Celsius);
        }
        if (rawUnit is Pressure pressure)
        {
            var p = pressure.Bar * channelConfigs[channelNumber].Scale;
            p += channelConfigs[channelNumber].Offset;
            return new Pressure(p);
        }

        throw new NotSupportedException();
    }

    private object ReadVoltageAsUnits(int channelNumber)
    {
        var current = Read0_10V(channelNumber);

        var rawUnit = UnitFactory.CreateUnitFromCanonicalValue(current.Volts, channelConfigs[channelNumber].UnitType);

        if (rawUnit is Temperature temperature)
        {
            var c = temperature.Celsius * channelConfigs[channelNumber].Scale;
            c += channelConfigs[channelNumber].Offset;
            return new Temperature(c);
        }
        if (rawUnit is Pressure pressure)
        {
            var p = pressure.Bar * channelConfigs[channelNumber].Scale;
            p += channelConfigs[channelNumber].Offset;
            return new Pressure(p);
        }

        throw new NotSupportedException();
    }
}
