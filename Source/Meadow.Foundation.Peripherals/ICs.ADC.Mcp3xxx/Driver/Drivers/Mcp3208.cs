﻿using Meadow.Hardware;

namespace Meadow.Foundation.ICs.ADC
{
    /// <summary>
    /// MCP3008 Analog to Digital Converter (ADC)
    /// </summary>
    public partial class Mcp3208 : Mcp3xxx
    {
        /// <summary>
        /// The pins
        /// </summary>
        public PinDefinitions Pins { get; }

        /// <summary>
        /// Constructs Mcp3208 instance
        /// </summary>
        /// <param name="spiBus">The SPI bus</param>
        /// <param name="chipSelectPort">Chip select port</param>
        public Mcp3208(ISpiBus spiBus, IDigitalOutputPort chipSelectPort)
            : base(spiBus, chipSelectPort, 4, 12)
        {
            Pins = new PinDefinitions(this);
        }
    }
}