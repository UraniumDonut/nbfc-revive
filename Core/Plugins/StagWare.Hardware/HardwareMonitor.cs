using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StagWare.Hardware
{
    public sealed class HardwareMonitor
    {
        #region Private Fields

        private static object syncRoot = new object();
        private static volatile HardwareMonitor instance;

        private Computer computer;
        private IHardware[] cpus;
        private ISensor[][] cpuTempSensors;
        private IHardware[] gpus;
        private ISensor[][] gpuTempSensors;

        #endregion

        #region Constructor

        private HardwareMonitor()
        {
            this.computer = new Computer();
            this.computer.IsCpuEnabled = true;
            this.computer.Open();
        }

        #endregion

        #region Properties

        public static HardwareMonitor Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                        {
                            instance = new HardwareMonitor();
                        }
                    }
                }

                return instance;
            }
        }

        public KeyValuePair<string, double>[] CpuTemperatures
        {
            get
            {
                if (this.cpus == null)
                {
                    InitializeCpuSensors();
                }

                var results = new KeyValuePair<string, double>[this.cpus.Length];

                for (int i = 0; i < this.cpus.Length; i++)
                {
                    this.cpus[i].Update();
                    results[i] = new KeyValuePair<string, double>(
                        this.cpus[i].Name, 
                        GetAverageTemperature(this.cpuTempSensors[i]));
                }

                return results;
            }
        }

        public KeyValuePair<string, double>[] GpuTemperatures
        {
            get
            {
                if (this.gpus == null)
                {
                    InitializeGpuSensors();
                }

                var results = new KeyValuePair<string, double>[this.gpus.Length];

                for (int i = 0; i < this.gpus.Length; i++)
                {
                    this.gpus[i].Update();
                    results[i] = new KeyValuePair<string, double>(
                        this.gpus[i].Name,
                        GetAverageTemperature(this.gpuTempSensors[i]));
                }

                return results;
            }
        }

        #endregion

        #region Private Methods

        private static double GetAverageTemperature(ISensor[] sensors)
        {
            double temperatureSum = 0;
            int count = 0;

            foreach (ISensor sensor in sensors)
            {
                if (sensor.Value.HasValue)
                {
                    temperatureSum += sensor.Value.Value;
                    count++;
                }
            }

            return temperatureSum / count;
        }

        private static ISensor[] GetGpuTemperatureSensors(IHardware gpu)
        {
            return gpu.Sensors.Where(
                x => x.SensorType == SensorType.Temperature).ToArray();
        }

        private static ISensor[] GetCpuTemperatureSensors(IHardware cpu)
        {
            var sensors = new List<ISensor>();
            ISensor tctl = null;
            cpu.Update();

            foreach (ISensor s in cpu.Sensors)
            {
                if (s.SensorType == SensorType.Temperature)
                {
                    string name = s.Name.ToUpper();

                    if (name.Contains("PACKAGE") || name.Contains("TOTAL"))
                    {
                        return new ISensor[] { s };
                    }
                    else if (name.Contains("TCTL"))
                    {
                        // AMD Zen: "Core (Tctl/Tdie)" or "Core (Tctl)"
                        if (tctl == null || name.Contains("TCTL/TDIE"))
                        {
                            tctl = s;
                        }
                    }
                    else if (!name.Contains("DISTANCE") && !name.Contains("MAX")
                        && !name.Contains("AVERAGE") && !name.Contains("TDIE"))
                    {
                        // Skip derived sensors, average the core sensors only
                        sensors.Add(s);
                    }
                }
            }

            if (tctl != null)
            {
                return new ISensor[] { tctl };
            }

            return sensors.ToArray();
        }

        private void InitializeCpuSensors()
        {
            IHardware[] cpus = GetHardware(HardwareType.Cpu);
            var cpuTempSensors = new ISensor[cpus.Length][];
            int sensorsTotal = 0;

            for (int i = 0; i < cpus.Length; i++)
            {
                ISensor[] sensors = GetCpuTemperatureSensors(cpus[i]);
                cpuTempSensors[i] = sensors;

                // Sensors without a value can't be read (e.g. PawnIO is not installed)
                sensorsTotal += sensors.Count(x => x.Value.HasValue);
            }

            if (sensorsTotal <= 0)
            {
                throw new PlatformNotSupportedException("Failed to access CPU temperature sensors(s).");
            }

            this.cpuTempSensors = cpuTempSensors;
            this.cpus = cpus;
        }

        private void InitializeGpuSensors()
        {
            var list = new List<IHardware>();
            list.AddRange(GetHardware(HardwareType.GpuAmd));
            list.AddRange(GetHardware(HardwareType.GpuNvidia));
            list.AddRange(GetHardware(HardwareType.GpuIntel));

            this.gpus = list.ToArray();
            this.gpuTempSensors = new ISensor[this.gpus.Length][];
            int sensorsTotal = 0;

            for (int i = 0; i < this.gpus.Length; i++)
            {
                ISensor[] sensors = GetGpuTemperatureSensors(this.gpus[i]);
                sensorsTotal += sensors.Length;
                this.gpuTempSensors[i] = sensors;
            }

            if (sensorsTotal <= 0)
            {
                throw new PlatformNotSupportedException("Failed to access GPU temperature sensors(s).");
            }
        }

        private IHardware[] GetHardware(HardwareType type)
        {
            return this.computer.Hardware.Where(x => x.HardwareType == type).ToArray();
        }

        #endregion
    }
}
