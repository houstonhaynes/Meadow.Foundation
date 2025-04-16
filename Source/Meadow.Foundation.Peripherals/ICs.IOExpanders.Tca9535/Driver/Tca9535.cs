﻿using Meadow.Hardware;
using System;

namespace Meadow.Foundation.ICs.IOExpanders;

public partial class Tca9535 : II2cPeripheral,
    IDigitalInputOutputController
{
    private readonly byte address;

    public byte DefaultI2cAddress => (byte)Addresses.Default;

    private readonly II2cCommunications i2cComms;

    /// <summary>
    /// TCA9535 pin definitions
    /// </summary>
    public PinDefinitions Pins { get; }

    public Tca9535(II2cBus i2cBus, byte address = (byte)Addresses.Default)
    {
        Pins = new PinDefinitions(this)
        {
            Controller = this
        };

        this.address = address;
        i2cComms = new I2cCommunications(i2cBus, address);
    }

    private ushort portsInUse = 0x0000;
    private ushort configRegister = 0xffff; // power-up default is 0xffff
    private const int Input = 1;
    private const int Output = 0;

    /// <inheritdoc/>
    public IDigitalOutputPort CreateDigitalOutputPort(IPin pin, bool initialState = false, OutputType initialOutputType = OutputType.PushPull)
    {
        if (initialOutputType != OutputType.PushPull)
        {
            throw new ArgumentOutOfRangeException(nameof(initialOutputType));
        }

        var bit = (ushort)(1 << (byte)pin.Key);

        if ((portsInUse & bit) != 0) throw new PortInUseException();

        portsInUse |= bit;

        // set inital state - do this before setting the config to prevent an unwanted blip
        SetState((byte)pin.Key, initialState);

        Span<byte> readBuffer = stackalloc byte[2];
        i2cComms.ReadRegister(Registers.ConfigurationPort0, readBuffer);
        var currentConfig = (ushort)(readBuffer[1] << 8 | readBuffer[0]);

        Resolver.Log.Info($"cfg: 0x{configRegister:X4}");

        // outputs are zeros - clear this bit
        configRegister = (ushort)(currentConfig & ~bit);

        // write the config
        Resolver.Log.Info($"setting cfg: 0x{configRegister:X4}");
        i2cComms.WriteRegister(Registers.ConfigurationPort0, configRegister, ByteOrder.LittleEndian);

        return new DigitalOutputPort(this, pin, initialState);
    }

    private bool GetState(byte portNumber)
    {
        // read current states
        Span<byte> readBuffer = stackalloc byte[2];
        i2cComms.ReadRegister(Registers.OutputPort0, readBuffer);
        var currentState = (ushort)(readBuffer[1] << 8 | readBuffer[0]);

        return (currentState & (1 << portNumber)) != 0;
    }

    private void SetState(byte portNumber, bool state)
    {
        ushort bit = (ushort)(1 << portNumber);

        // read current states
        Span<byte> readBuffer = stackalloc byte[2];
        i2cComms.ReadRegister(Registers.OutputPort0, readBuffer);
        var currentState = (ushort)(readBuffer[1] << 8 | readBuffer[0]);
        Resolver.Log.Info($"current: 0x{currentState:X4}");

        if (state)
        {
            currentState |= bit;
        }
        else
        {
            currentState &= (ushort)~bit;
        }
        Resolver.Log.Info($"setting: 0x{currentState:X4}");
        i2cComms.WriteRegister(Registers.OutputPort0, currentState, ByteOrder.LittleEndian);
    }

    /// <inheritdoc/>
    public IDigitalInputPort CreateDigitalInputPort(IPin pin, ResistorMode resistorMode)
    {
        throw new NotImplementedException();
    }
}