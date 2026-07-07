using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace OneClickClose.Core;

public interface IHardwareTemperatureProvider
{
    HardwareTemperatureReading ReadTemperatures();
}

public sealed class LibreHardwareTemperatureProvider : IHardwareTemperatureProvider
{
    public HardwareTemperatureReading ReadTemperatures()
    {
        try
        {
            var computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true
            };

            try
            {
                computer.Open();
                foreach (var hardware in computer.Hardware)
                {
                    UpdateHardware(hardware);
                }

                var cpu = new List<float>();
                var gpu = new List<float>();
                var board = new List<float>();

                foreach (var hardware in FlattenHardware(computer.Hardware))
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType != SensorType.Temperature || !sensor.Value.HasValue)
                        {
                            continue;
                        }

                        float value = sensor.Value.Value;
                        if (value <= 0 || value > 125)
                        {
                            continue;
                        }

                        if (hardware.HardwareType == HardwareType.Cpu)
                        {
                            cpu.Add(value);
                        }
                        else if (hardware.HardwareType == HardwareType.GpuAmd
                            || hardware.HardwareType == HardwareType.GpuIntel
                            || hardware.HardwareType == HardwareType.GpuNvidia)
                        {
                            gpu.Add(value);
                        }
                        else if (hardware.HardwareType == HardwareType.Motherboard
                            || hardware.HardwareType == HardwareType.SuperIO)
                        {
                            board.Add(value);
                        }
                    }
                }

                var reading = new HardwareTemperatureReading
                {
                    CpuTemperatureC = PickTemperature(cpu),
                    GpuTemperatureC = PickTemperature(gpu),
                    MotherboardTemperatureC = PickTemperature(board),
                    Source = "LibreHardwareMonitor"
                };

                if (!reading.HasAnyTemperature)
                {
                    reading.UnavailableReason = "未检测到温度传感器";
                }

                return reading;
            }
            finally
            {
                try { computer.Close(); } catch { }
            }
        }
        catch (UnauthorizedAccessException)
        {
            return new HardwareTemperatureReading
            {
                Source = "LibreHardwareMonitor",
                UnavailableReason = "未授权，可能需要管理员权限"
            };
        }
        catch (Exception ex)
        {
            string message = ex.Message;
            if (message.IndexOf("access", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("denied", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                message = "未授权，可能需要管理员权限";
            }

            return new HardwareTemperatureReading
            {
                Source = "LibreHardwareMonitor",
                UnavailableReason = string.IsNullOrWhiteSpace(message)
                    ? "硬件监控库不可用"
                    : message
            };
        }
    }

    private static void UpdateHardware(IHardware hardware)
    {
        try
        {
            hardware.Update();
        }
        catch
        {
            // A single blocked sensor should not hide the sensors that are readable.
        }

        foreach (var child in hardware.SubHardware)
        {
            UpdateHardware(child);
        }
    }

    private static IEnumerable<IHardware> FlattenHardware(IEnumerable<IHardware> hardware)
    {
        foreach (var item in hardware)
        {
            yield return item;
            foreach (var child in FlattenHardware(item.SubHardware))
            {
                yield return child;
            }
        }
    }

    private static float? PickTemperature(List<float> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        return values
            .OrderByDescending(v => v)
            .First();
    }
}
