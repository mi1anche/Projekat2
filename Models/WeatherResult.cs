using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrugiProjekat.Models
{
    public class WeatherResult
    {
        public string? Sol { get; set; }
        public string? FirstUtc { get; set; }
        public string? LastUtc { get; set; }
        public string? Season { get; set; }
        public double? AvgTemperature { get; set; }
        public double? MinTemperature { get; set; }
        public double? MaxTemperature { get; set; }
        public double? AvgWindSpeed { get; set; }
        public double? MinWindSpeed { get; set; }
        public double? MaxWindSpeed { get; set; }
        public double? AvgPressure { get; set; }
        public double? MinPressure { get; set; }
        public double? MaxPressure { get; set; }
        public string? WindDirection { get; set; }
    }
}
