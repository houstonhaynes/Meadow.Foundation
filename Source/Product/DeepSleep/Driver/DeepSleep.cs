using Meadow.Foundation.RTCs;
using Meadow.Hardware;
using System;

namespace Meadow.Foundation;

/// <summary>
/// Wilderness Labs Meadow Deep Sleep power control module
/// </summary>
public partial class DeepSleep
{
    private readonly Pcf8523 _rtc;

    /// <summary>
    /// Creates a new DeepSleep object
    /// </summary>
    /// <param name="i2cBus">The I2C bus connected to the DeepSleep module</param>
    public DeepSleep(II2cBus i2cBus)
    {
        _rtc = new Pcf8523(i2cBus);
    }

    /// <summary>
    /// Set the current time for the real time clock
    /// </summary>
    /// <param name="time">The current time</param>
    public void SetTime(DateTimeOffset time) => _rtc.SetTime(time);

    /// <summary>
    /// Get the current time from the real time clock
    /// </summary>
    public DateTimeOffset GetTime() => _rtc.GetTime();

    /// <summary>
    /// Schedules a wake-up based on the RTC alarm
    /// </summary>
    /// <param name="wakeTime">The time to wake up.</param>
    public void ScheduleWakeUp(DateTimeOffset wakeTime)
    {
        _rtc.SetAlarm(wakeTime);
    }

    /// <summary>
    /// Schedules a wake-up after a delay using the RTC timer
    /// </summary>
    /// <param name="delay">The delay count (1 - 255).</param>
    /// <param name="unit">The time unit for the delay.</param>
    public void SetDelayWakeUp(byte delay, DelayTimeUnit unit)
    {
        _rtc.SetDelay(delay, unit);
    }

    /// <summary>
    /// Clears both the alarm and delay flags on the RTC
    /// </summary>
    public void ClearInterruptFlags() => _rtc.ClearFlags();
}