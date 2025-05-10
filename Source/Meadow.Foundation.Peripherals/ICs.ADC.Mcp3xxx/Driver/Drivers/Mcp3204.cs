﻿using Meadow.Hardware;

namespace Meadow.Foundation.ICs.ADC
{
    /// <summary>
    /// MCP3204 Analog to Digital Converter (ADC)
    /// </summary>
    public partial class Mcp3204 : Mcp3xxx
    {
        /// <summary>
        /// The pins
        /// </summary>
        public PinDefinitions Pins { get; }

        /// <summary>
        /// Constructs Mcp3204 instance
        /// </summary>
        /// <param name="spiBus">The SPI bus</param>
        /// <param name="chipSelectPort">Chip select port</param>
        public Mcp3204(ISpiBus spiBus, IDigitalOutputPort chipSelectPort)
            : base(spiBus, chipSelectPort, 4, 10)
        {
            Pins = new PinDefinitions(this);
        }
    }
}