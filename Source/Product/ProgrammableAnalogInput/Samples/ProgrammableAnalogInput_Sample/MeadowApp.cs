using Meadow;
using Meadow.Devices;
using Meadow.Foundation;
using Meadow.Units;
using System;
using System.Threading.Tasks;

namespace ProgrammableAnalogInput_Sample;

public class MeadowApp : App<F7CoreComputeV2>
{
    //<!=SNIP=>
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
        //            module.ConfigureChannel(0, ProgrammableAnalogInputModule.ChannelType.ThermistorNtc);
        module.ConfigureChannel(0, new ChannelConfig
        {
            ChannelNumber = 0,
            ChannelType = ConfigurableAnalogInputChannelType.Current_4_20,
            UnitType = "Temperature",
            Scale = 3.4725, //0-100F, but scale/offs in C
            Offset = -31.67
        });
        while (true)
        {
            var raw = module.Read4_20mA(0);
            Resolver.Log.Info($"CH1: {raw.Milliamps:N1} mA");
            try
            {
                var t1 = module.ReadChannelAsConfiguredUnit(0);
                Resolver.Log.Info($"t1: {t1.GetType().Name}");
                if (t1 is Temperature temp)
                {
                    Resolver.Log.Info($"  temp: {temp.Fahrenheit:N1}F");
                }
            }
            catch (Exception ex)
            {
                Resolver.Log.Error($"ERROR: {ex.Message}");
            }

            await Task.Delay(1000);
        }
    }

    //<!=SNOP=>
}