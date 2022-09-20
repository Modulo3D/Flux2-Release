using DynamicData.Kernel;
using Modulo3DStandard;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class RRF_ObjectModelMcuTemp
    {
        //[JsonProperty("current")]
        //public Optional<double> Current { get; set; }

        //[JsonProperty("max")]
        //public Optional<double> Max { get; set; }

        //[JsonProperty("min")]
        //public Optional<double> Min { get; set; }
    }

    public class RRF_ObjectModelVIn
    {
        //[JsonProperty("current")]
        //public Optional<double> Current { get; set; }

        //[JsonProperty("max")]
        //public Optional<double> Max { get; set; }

        //[JsonProperty("min")]
        //public Optional<double> Min { get; set; }
    }

    public class RRF_ObjectModelBoard
    {
        //[JsonProperty("firmwareDate")]
        //public Optional<string> FirmwareDate { get; set; }

        //[JsonProperty("firmwareFileName")]
        //public Optional<string> FirmwareFileName { get; set; }

        //[JsonProperty("firmwareName")]
        //public Optional<string> FirmwareName { get; set; }

        //[JsonProperty("firmwareVersion")]
        //public Optional<string> FirmwareVersion { get; set; }

        //[JsonProperty("iapFileNameSD")]
        //public Optional<string> IapFileNameSD { get; set; }

        //[JsonProperty("maxHeaters")]
        //public Optional<double> MaxHeaters { get; set; }

        //[JsonProperty("maxMotors")]
        //public Optional<double> MaxMotors { get; set; }

        //[JsonProperty("mcuTemp")]
        //public RRF_ObjectModelMcuTemp McuTemp { get; set; }

        //[JsonProperty("name")]
        //public Optional<string> Name { get; set; }

        //[JsonProperty("shortName")]
        //public Optional<string> ShortName { get; set; }

        //[JsonProperty("supportsDirectDisplay")]
        //public Optional<bool> SupportsDirectDisplay { get; set; }

        //[JsonProperty("uniqueId")]
        //public Optional<string> UniqueId { get; set; }

        //[JsonProperty("vIn")]
        //public RRF_ObjectModelVIn VIn { get; set; }
    }

    public class RRF_ObjectModelThermostatic
    {
        //    [JsonProperty("heaters")]
        //    public Optional<List<double>> Heaters { get; set; }

        //    [JsonProperty("highTemperature")]
        //    public Optional<double> HighTemperature { get; set; }

        //    [JsonProperty("lowTemperature")]
        //    public Optional<double> LowTemperature { get; set; }
    }

    public class RRF_ObjectModelFan
    {
        //[JsonProperty("actualValue")]
        //public Optional<double> ActualValue { get; set; }

        //[JsonProperty("blip")]
        //public Optional<double> Blip { get; set; }

        //[JsonProperty("max")]
        //public Optional<double> Max { get; set; }

        //[JsonProperty("min")]
        //public Optional<double> Min { get; set; }

        //[JsonProperty("name")]
        //public Optional<string> Name { get; set; }

        //[JsonProperty("requestedValue")]
        //public Optional<double> RequestedValue { get; set; }

        //[JsonProperty("rpm")]
        //public Optional<double> Rpm { get; set; }

        //[JsonProperty("thermostatic")]
        //public RRF_ObjectModelThermostatic Thermostatic { get; set; }
    }

    public class RRF_ObjectModelPid
    {
        //[JsonProperty("d")]
        //public Optional<double> D { get; set; }

        //[JsonProperty("i")]
        //public Optional<double> I { get; set; }

        //[JsonProperty("overridden")]
        //public Optional<bool> Overridden { get; set; }

        //[JsonProperty("p")]
        //public Optional<double> P { get; set; }

        //[JsonProperty("used")]
        //public Optional<bool> Used { get; set; }
    }

    public class RRF_ObjectModelModel
    {
        //[JsonProperty("deadTime")]
        //public Optional<double> DeadTime { get; set; }

        //[JsonProperty("enabled")]
        //public Optional<bool> Enabled { get; set; }

        //[JsonProperty("gain")]
        //public Optional<double> Gain { get; set; }

        //[JsonProperty("heatingRate")]
        //public Optional<double> HeatingRate { get; set; }

        //[JsonProperty("inverted")]
        //public Optional<bool> Inverted { get; set; }

        //[JsonProperty("maxPwm")]
        //public Optional<double> MaxPwm { get; set; }

        //[JsonProperty("pid")]
        //public RRF_ObjectModelPid Pid { get; set; }

        //[JsonProperty("standardVoltage")]
        //public Optional<double> StandardVoltage { get; set; }

        //[JsonProperty("timeConstant")]
        //public Optional<double> TimeConstant { get; set; }

        //[JsonProperty("timeConstantFansOn")]
        //public Optional<double> TimeConstantFansOn { get; set; }
    }

    public class RRF_ObjectModelMonitor
    {
        //[JsonProperty("action")]
        //public Optional<double> Action { get; set; }

        //[JsonProperty("condition")]
        //public Optional<string> Condition { get; set; }

        //[JsonProperty("limit")]
        //public Optional<double> Limit { get; set; }
    }

    public class RRF_ObjectModelHeater
    {
        [JsonProperty("active")]
        [JsonConverter(typeof(JsonConverters.OptionalConverter<double>))]
        public Optional<double> Active { get; set; }

        [JsonProperty("current")]
        [JsonConverter(typeof(JsonConverters.OptionalConverter<double>))]
        public Optional<double> Current { get; set; }

        //[JsonProperty("max")]
        //public Optional<double> Max { get; set; }

        //[JsonProperty("min")]
        //public Optional<double> Min { get; set; }

        //[JsonProperty("model")]
        //public RRF_ObjectModelModel Model { get; set; }

        //[JsonProperty("monitors")]
        //public Optional<List<RRF_ObjectModelMonitor>> Monitors { get; set; }

        [JsonProperty("sensor")]
        [JsonConverter(typeof(JsonConverters.OptionalConverter<short>))]
        public Optional<short> Sensor { get; set; }

        //[JsonProperty("standby")]
        //public Optional<double> Standby { get; set; }

        //[JsonProperty("state")]
        //public Optional<string> State { get; set; }

        public Optional<FLUX_Temp> GetTemperature()
        {
            if (!Active.HasValue || !Current.HasValue)
                return default;
            return new FLUX_Temp(Current.Value, Active.Value);
        }
    }

    public class RRF_ObjectModelHeat
    {
        //[JsonProperty("bedHeaters")]
        //public Optional<List<double>> BedHeaters { get; set; }

        //[JsonProperty("chamberHeaters")]
        //public Optional<List<double>> ChamberHeaters { get; set; }

        //[JsonProperty("coldExtrudeTemperature")]
        //public Optional<double> ColdExtrudeTemperature { get; set; }

        //[JsonProperty("coldRetractTemperature")]
        //public Optional<double> ColdRetractTemperature { get; set; }

        [JsonProperty("heaters")]
        [JsonConverter(typeof(JsonConverters.OptionalConverter<List<RRF_ObjectModelHeater>>))]
        public Optional<List<RRF_ObjectModelHeater>> Heaters { get; set; }
    }

    public class RRF_ObjectModelInput
    {
        //[JsonProperty("axesRelative")]
        //public Optional<bool> AxesRelative { get; set; }

        //[JsonProperty("compatibility")]
        //public Optional<string> Compatibility { get; set; }

        //[JsonProperty("distanceUnit")]
        //public Optional<string> DistanceUnit { get; set; }

        //[JsonProperty("drivesRelative")]
        //public Optional<bool> DrivesRelative { get; set; }

        //[JsonProperty("feedRate")]
        //public Optional<double> FeedRate { get; set; }

        //[JsonProperty("inMacro")]
        //public Optional<bool> InMacro { get; set; }

        [JsonProperty("lineNumber")]
        [JsonConverter(typeof(JsonConverters.OptionalConverter<double>))]
        public Optional<double> LineNumber { get; set; }

        //[JsonProperty("name")]
        //public Optional<string> Name { get; set; }

        [JsonProperty("stackDepth")]
        public Optional<double> StackDepth { get; set; }

        //[JsonProperty("state")]
        //public Optional<string> State { get; set; }

        //[JsonProperty("volumetric")]
        //public Optional<bool> Volumetric { get; set; }
    }

    public class RRF_ObjectModelObject
    {
        //[JsonProperty("cancelled")]
        //public Optional<bool> Cancelled { get; set; }

        //[JsonProperty("name")]
        //public Optional<string> Name { get; set; }

        //[JsonProperty("x")]
        //public Optional<List<double>> X { get; set; }

        //[JsonProperty("y")]
        //public Optional<List<double>> Y { get; set; }
    }

    public class RRF_ObjectModelBuild
    {
        //[JsonProperty("currentObject")]
        //public Optional<double> CurrentObject { get; set; }

        //[JsonProperty("m486Names")]
        //public Optional<bool> M486Names { get; set; }

        //[JsonProperty("m486Numbers")]
        //public Optional<bool> M486Numbers { get; set; }

        //[JsonProperty("objects")]
        //public Optional<List<RRF_ObjectModelObject>> Objects { get; set; }
    }

    public class RRF_ObjectModelFile
    {
        //[JsonProperty("filament")]
        //public Optional<List<double>> Filament { get; set; }

        [JsonProperty("fileName")]
        [JsonConverter(typeof(JsonConverters.OptionalConverter<string>))]
        public Optional<string> FileName { get; set; }

        //[JsonProperty("firstLayerHeight")]
        //public Optional<double> FirstLayerHeight { get; set; }

        //[JsonProperty("gcodeName")]
        //public Optional<string> GcodeName { get; set; }

        //[JsonProperty("generatedBy")]
        //public Optional<string> GeneratedBy { get; set; }

        //[JsonProperty("height")]
        //public Optional<double> Height { get; set; }

        //[JsonProperty("lastModified")]
        //public DateTime LastModified { get; set; }

        //[JsonProperty("layerHeight")]
        //public Optional<double> LayerHeight { get; set; }

        //[JsonProperty("numLayers")]
        //public Optional<double> NumLayers { get; set; }

        //[JsonProperty("prdoubleTime")]
        //public Optional<double> PrdoubleTime { get; set; }

        //[JsonProperty("simulatedTime")]
        //public object SimulatedTime { get; set; }

        [JsonProperty("size")]
        public Optional<long> Size { get; set; }
    }

    public class RRF_ObjectModelTimesLeft
    {
        //[JsonProperty("filament")]
        //public Optional<double> Filament { get; set; }

        //[JsonProperty("file")]
        //public Optional<double> File { get; set; }

        //[JsonProperty("layer")]
        //public Optional<double> Layer { get; set; }
    }

    public class RRF_ObjectModelJob
    {
        //[JsonProperty("build")]
        //public RRF_ObjectModelBuild Build { get; set; }

        //[JsonProperty("duration")]
        //public Optional<float> Duration { get; set; }

        [JsonProperty("file")]
        public Optional<RRF_ObjectModelFile> File { get; set; }

        [JsonProperty("filePosition")]
        public Optional<long> FilePosition { get; set; }

        //[JsonProperty("firstLayerDuration")]
        //public Optional<double> FirstLayerDuration { get; set; }

        //[JsonProperty("lastDuration")]
        //public object LastDuration { get; set; }

        [JsonProperty("lastFileName")]
        public Optional<string> LastFileName { get; set; }

        //[JsonProperty("layer")]
        //public Optional<double> Layer { get; set; }

        //[JsonProperty("layerTime")]
        //public Optional<double> LayerTime { get; set; }

        //[JsonProperty("timesLeft")]
        //public RRF_ObjectModelTimesLeft TimesLeft { get; set; }

        //[JsonProperty("warmUpDuration")]
        //public Optional<double> WarmUpDuration { get; set; }
    }

    public class RRF_ObjectModelMicrostepping
    {
        //[JsonProperty("doubleerpolated")]
        //public Optional<bool> doubleerpolated { get; set; }

        //[JsonProperty("value")]
        //public Optional<double> Value { get; set; }
    }

    public class RRF_ObjectModelAxis
    {
        //[JsonProperty("acceleration")]
        //public Optional<double> Acceleration { get; set; }

        //[JsonProperty("babystep")]
        //public Optional<double> Babystep { get; set; }

        [JsonProperty("current")]
        public Optional<double> Current { get; set; }

        //[JsonProperty("drivers")]
        //public Optional<List<Optional<string>>> Drivers { get; set; }

        [JsonProperty("homed")]
        public Optional<bool> Homed { get; set; }

        //[JsonProperty("jerk")]
        //public Optional<double> Jerk { get; set; }

        [JsonProperty("letter")]
        [JsonConverter(typeof(JsonConverters.OptionalConverter<string>))]
        public Optional<string> Letter { get; set; }

        [JsonProperty("machinePosition")]
        public Optional<double> MachinePosition { get; set; }

        //[JsonProperty("max")]
        //public Optional<double> Max { get; set; }

        //[JsonProperty("maxProbed")]
        //public Optional<bool> MaxProbed { get; set; }

        //[JsonProperty("microstepping")]
        //public RRF_ObjectModelMicrostepping Microstepping { get; set; }

        //[JsonProperty("min")]
        //public Optional<double> Min { get; set; }

        //[JsonProperty("minProbed")]
        //public Optional<bool> MinProbed { get; set; }

        //[JsonProperty("speed")]
        //public Optional<double> Speed { get; set; }

        //[JsonProperty("stepsPerMm")]
        //public Optional<double> StepsPerMm { get; set; }

        //[JsonProperty("userPosition")]
        //public Optional<double> UserPosition { get; set; }

        //[JsonProperty("visible")]
        //public Optional<bool> Visible { get; set; }

        //[JsonProperty("workplaceOffsets")]
        //public Optional<List<double>> WorkplaceOffsets { get; set; }

        public Optional<bool> IsEnabledDriver()
        {
            return Current.Convert(c => c > 0);
        }
    }

    public class RRF_ObjectModelFinal
    {
        //[JsonProperty("deviation")]
        //public Optional<double> Deviation { get; set; }

        //[JsonProperty("mean")]
        //public Optional<double> Mean { get; set; }
    }

    public class RRF_ObjectModelInitial
    {
        //[JsonProperty("deviation")]
        //public Optional<double> Deviation { get; set; }

        //[JsonProperty("mean")]
        //public Optional<double> Mean { get; set; }
    }

    public class RRF_ObjectModelCalibration
    {
        //[JsonProperty("final")]
        //public RRF_ObjectModelFinal Final { get; set; }

        //[JsonProperty("initial")]
        //public RRF_ObjectModelInitial Initial { get; set; }

        //[JsonProperty("numFactors")]
        //public Optional<double> NumFactors { get; set; }
    }

    public class RRF_ObjectModelProbeGrid
    {
        //[JsonProperty("radius")]
        //public Optional<double> Radius { get; set; }

        //[JsonProperty("xMax")]
        //public Optional<double> XMax { get; set; }

        //[JsonProperty("xMin")]
        //public Optional<double> XMin { get; set; }

        //[JsonProperty("xSpacing")]
        //public Optional<double> XSpacing { get; set; }

        //[JsonProperty("yMax")]
        //public Optional<double> YMax { get; set; }

        //[JsonProperty("yMin")]
        //public Optional<double> YMin { get; set; }

        //[JsonProperty("ySpacing")]
        //public Optional<double> YSpacing { get; set; }
    }

    public class RRF_ObjectModelSkew
    {
        //[JsonProperty("compensateXY")]
        //public Optional<bool> CompensateXY { get; set; }

        //[JsonProperty("tanXY")]
        //public Optional<double> TanXY { get; set; }

        //[JsonProperty("tanXZ")]
        //public Optional<double> TanXZ { get; set; }

        //[JsonProperty("tanYZ")]
        //public Optional<double> TanYZ { get; set; }
    }

    public class RRF_ObjectModelCompensation
    {
        //[JsonProperty("fadeHeight")]
        //public object FadeHeight { get; set; }

        //[JsonProperty("file")]
        //public object File { get; set; }

        //[JsonProperty("meshDeviation")]
        //public object MeshDeviation { get; set; }

        //[JsonProperty("probeGrid")]
        //public RRF_ObjectModelProbeGrid ProbeGrid { get; set; }

        //[JsonProperty("skew")]
        //public RRF_ObjectModelSkew Skew { get; set; }

        //[JsonProperty("type")]
        //public Optional<string> Type { get; set; }
    }

    public class RRF_ObjectModelCurrentMove
    {
        //[JsonProperty("acceleration")]
        //public Optional<double> Acceleration { get; set; }

        //[JsonProperty("deceleration")]
        //public Optional<double> Deceleration { get; set; }

        //[JsonProperty("laserPwm")]
        //public object LaserPwm { get; set; }

        //[JsonProperty("requestedSpeed")]
        //public Optional<double> RequestedSpeed { get; set; }

        //[JsonProperty("topSpeed")]
        //public Optional<double> TopSpeed { get; set; }
    }

    public class RRF_ObjectModelDaa
    {
        //[JsonProperty("enabled")]
        //public Optional<bool> Enabled { get; set; }

        //[JsonProperty("minimumAcceleration")]
        //public Optional<double> MinimumAcceleration { get; set; }

        //[JsonProperty("period")]
        //public Optional<double> Period { get; set; }
    }

    public class RRF_ObjectModelNonlinear
    {
        //[JsonProperty("a")]
        //public Optional<double> A { get; set; }

        //[JsonProperty("b")]
        //public Optional<double> B { get; set; }

        //[JsonProperty("upperLimit")]
        //public Optional<double> UpperLimit { get; set; }
    }

    public class RRF_ObjectModelExtruder
    {
        //[JsonProperty("acceleration")]
        //public Optional<double> Acceleration { get; set; }

        //[JsonProperty("current")]
        //public Optional<double> Current { get; set; }

        //[JsonProperty("driver")]
        //public Optional<string> Driver { get; set; }

        //[JsonProperty("factor")]
        //public Optional<double> Factor { get; set; }

        //[JsonProperty("filamentGuid")]
        //public Optional<string> FilamentGuid { get; set; }

        //[JsonProperty("filamentName")]
        //public Optional<string> FilamentName { get; set; }

        //[JsonProperty("jerk")]
        //public Optional<double> Jerk { get; set; }

        //[JsonProperty("microstepping")]
        //public RRF_ObjectModelMicrostepping Microstepping { get; set; }

        //[JsonProperty("nonlinear")]
        //public RRF_ObjectModelNonlinear Nonlinear { get; set; }

        //[JsonProperty("nozzleGuid")]
        //public Optional<string> NozzleGuid { get; set; }

        //[JsonProperty("nozzleName")]
        //public Optional<string> NozzleName { get; set; }

        [JsonProperty("position")]
        public Optional<double> Position { get; set; }

        //[JsonProperty("pressureAdvance")]
        //public Optional<double> PressureAdvance { get; set; }

        //[JsonProperty("rawPosition")]
        //public Optional<double> RawPosition { get; set; }

        //[JsonProperty("speed")]
        //public Optional<double> Speed { get; set; }

        //[JsonProperty("stepsPerMm")]
        //public Optional<double> StepsPerMm { get; set; }
    }

    public class RRF_ObjectModelIdle
    {
        //[JsonProperty("factor")]
        //public Optional<double> Factor { get; set; }

        //[JsonProperty("timeout")]
        //public Optional<double> Timeout { get; set; }
    }

    public class RRF_ObjectModelTiltCorrection
    {
        //[JsonProperty("correctionFactor")]
        //public Optional<double> CorrectionFactor { get; set; }

        //[JsonProperty("lastCorrections")]
        //public Optional<List<object>> LastCorrections { get; set; }

        //[JsonProperty("maxCorrection")]
        //public Optional<double> MaxCorrection { get; set; }

        //[JsonProperty("screwPitch")]
        //public Optional<double> ScrewPitch { get; set; }

        //[JsonProperty("screwX")]
        //public Optional<List<object>> ScrewX { get; set; }

        //[JsonProperty("screwY")]
        //public Optional<List<object>> ScrewY { get; set; }
    }

    public class RRF_ObjectModelKinematics
    {
        //[JsonProperty("forwardMatrix")]
        //public Optional<List<double>> ForwardMatrix { get; set; }

        //[JsonProperty("inverseMatrix")]
        //public Optional<List<double>> InverseMatrix { get; set; }

        //[JsonProperty("name")]
        //public Optional<string> Name { get; set; }

        //[JsonProperty("tiltCorrection")]
        //public RRF_ObjectModelTiltCorrection TiltCorrection { get; set; }
    }

    public class RRF_ObjectModelMove
    {
        [JsonProperty("axes")]
        public Optional<List<RRF_ObjectModelAxis>> Axes { get; set; }

        //[JsonProperty("calibration")]
        //public RRF_ObjectModelCalibration Calibration { get; set; }

        //[JsonProperty("compensation")]
        //public RRF_ObjectModelCompensation Compensation { get; set; }

        //[JsonProperty("currentMove")]
        //public RRF_ObjectModelCurrentMove CurrentMove { get; set; }

        //[JsonProperty("daa")]
        //public RRF_ObjectModelDaa Daa { get; set; }

        [JsonProperty("extruders")]
        public Optional<List<RRF_ObjectModelExtruder>> Extruders { get; set; }

        //[JsonProperty("idle")]
        //public RRF_ObjectModelIdle Idle { get; set; }

        //[JsonProperty("kinematics")]
        //public RRF_ObjectModelKinematics Kinematics { get; set; }

        //[JsonProperty("prdoubleingAcceleration")]
        //public Optional<double> PrdoubleingAcceleration { get; set; }

        //[JsonProperty("speedFactor")]
        //public Optional<double> SpeedFactor { get; set; }

        //[JsonProperty("travelAcceleration")]
        //public Optional<double> TravelAcceleration { get; set; }

        //[JsonProperty("virtualEPos")]
        //public Optional<double> VirtualEPos { get; set; }

        //[JsonProperty("workplaceNumber")]
        //public Optional<double> WorkplaceNumber { get; set; }

        //[JsonProperty("workspaceNumber")]
        //public Optional<double> WorkspaceNumber { get; set; }

        public Optional<bool> IsHomed()
        {
            if (!Axes.HasValue)
                return default;
            foreach (var axis in Axes.Value)
            {
                if (!axis.Homed.HasValue)
                    return default;
                if (!axis.Homed.Value)
                    return false;
            }
            return true;
        }
    }

    public class RRF_ObjectModelAnalog
    {
        [JsonProperty("lastReading")]
        public Optional<double> LastReading { get; set; }

        //[JsonProperty("name")]
        //public Optional<string> Name { get; set; }

        //[JsonProperty("type")]
        //public Optional<string> Type { get; set; }
    }

    public class RRF_ObjectModelEndstop
    {
        [JsonProperty("triggered")]
        public Optional<bool> Triggered { get; set; }

        //[JsonProperty("type")]
        //public Optional<string> Type { get; set; }
    }

    public class RRF_ObjectModelGpIn
    {
        [JsonProperty("value")]
        public Optional<double> Value { get; set; }
    }

    public class RRF_ObjectModelGpOut
    {
        [JsonProperty("pwm")]
        public Optional<double> Pwm { get; set; }
    }

    public class RRF_ObjectModelProbe
    {
        //[JsonProperty("calibrationTemperature")]
        //public Optional<double> CalibrationTemperature { get; set; }

        //[JsonProperty("deployedByUser")]
        //public Optional<bool> DeployedByUser { get; set; }

        //[JsonProperty("disablesHeaters")]
        //public Optional<bool> DisablesHeaters { get; set; }

        //[JsonProperty("diveHeight")]
        //public Optional<double> DiveHeight { get; set; }

        //[JsonProperty("lastStopHeight")]
        //public Optional<double> LastStopHeight { get; set; }

        //[JsonProperty("maxProbeCount")]
        //public Optional<double> MaxProbeCount { get; set; }

        //[JsonProperty("offsets")]
        //public Optional<List<double>> Offsets { get; set; }

        //[JsonProperty("recoveryTime")]
        //public Optional<double> RecoveryTime { get; set; }

        //[JsonProperty("speed")]
        //public Optional<double> Speed { get; set; }

        //[JsonProperty("temperatureCoefficient")]
        //public Optional<double> TemperatureCoefficient { get; set; }

        //[JsonProperty("temperatureCoefficients")]
        //public Optional<List<double>> TemperatureCoefficients { get; set; }

        //[JsonProperty("threshold")]
        //public Optional<double> Threshold { get; set; }

        //[JsonProperty("tolerance")]
        //public Optional<double> Tolerance { get; set; }

        //[JsonProperty("travelSpeed")]
        //public Optional<double> TravelSpeed { get; set; }

        //[JsonProperty("triggerHeight")]
        //public Optional<double> TriggerHeight { get; set; }

        //[JsonProperty("type")]
        //public Optional<double> Type { get; set; }

        [JsonProperty("value")]
        public Optional<List<double>> Value { get; set; }
    }

    public class RRF_ObjectModelSensors
    {
        [JsonProperty("analog")]
        public Optional<List<RRF_ObjectModelAnalog>> Analog { get; set; }

        [JsonProperty("endstops")]
        [JsonConverter(typeof(JsonConverters.OptionalConverter<List<RRF_ObjectModelEndstop>>))]
        public Optional<List<RRF_ObjectModelEndstop>> Endstops { get; set; }

        [JsonProperty("filamentMonitors")]
        public Optional<List<RRF_FilamentMonitor>> FilamentMonitors { get; set; }

        [JsonProperty("gpIn")]
        public Optional<List<RRF_ObjectModelGpIn>> GpIn { get; set; }

        [JsonProperty("probes")]
        [JsonConverter(typeof(JsonConverters.OptionalConverter<List<RRF_ObjectModelProbe>>))]
        public Optional<List<RRF_ObjectModelProbe>> Probes { get; set; }

        public Optional<List<double>> GetProbeLevels(int probe)
        {
            if (!Probes.HasValue)
                return default;
            return Probes.Value[probe].Value;
        }
    }

    public class RRF_FilamentMonitor
    {
        [JsonProperty("status")]
        [JsonConverter(typeof(JsonConverters.OptionalConverter<string>))]
        public Optional<string> Status { get; set; }
    }

    public class RRF_ObjectModelSeqs
    {
        //[JsonProperty("boards")]
        //public Optional<double> Boards { get; set; }

        //[JsonProperty("directories")]
        //public Optional<double> Directories { get; set; }

        //[JsonProperty("fans")]
        //public Optional<double> Fans { get; set; }

        //[JsonProperty("heat")]
        //public Optional<double> Heat { get; set; }

        //[JsonProperty("inputs")]
        //public Optional<double> Inputs { get; set; }

        //[JsonProperty("job")]
        //public Optional<double> Job { get; set; }

        //[JsonProperty("move")]
        //public Optional<double> Move { get; set; }

        //[JsonProperty("network")]
        //public Optional<double> Network { get; set; }

        //[JsonProperty("reply")]
        //public Optional<double> Reply { get; set; }

        //[JsonProperty("scanner")]
        //public Optional<double> Scanner { get; set; }

        //[JsonProperty("sensors")]
        //public Optional<double> Sensors { get; set; }

        //[JsonProperty("spindles")]
        //public Optional<double> Spindles { get; set; }

        //[JsonProperty("state")]
        //public Optional<double> State { get; set; }

        //[JsonProperty("tools")]
        //public Optional<double> Tools { get; set; }

        //[JsonProperty("volumes")]
        //public Optional<double> Volumes { get; set; }
    }

    public class RRF_ObjectModelSpindle
    {
        //[JsonProperty("active")]
        //public Optional<double> Active { get; set; }

        //[JsonProperty("current")]
        //public Optional<double> Current { get; set; }

        //[JsonProperty("frequency")]
        //public Optional<double> Frequency { get; set; }

        //[JsonProperty("max")]
        //public Optional<double> Max { get; set; }

        //[JsonProperty("min")]
        //public Optional<double> Min { get; set; }

        //[JsonProperty("tool")]
        //public Optional<double> Tool { get; set; }
    }

    public class RRF_ObjectModelRestorePodouble
    {
        //[JsonProperty("coords")]
        //public Optional<List<double>> Coords { get; set; }

        //[JsonProperty("extruderPos")]
        //public Optional<double> ExtruderPos { get; set; }

        //[JsonProperty("feedRate")]
        //public Optional<double> FeedRate { get; set; }

        //[JsonProperty("ioBits")]
        //public Optional<double> IoBits { get; set; }

        //[JsonProperty("laserPwm")]
        //public object LaserPwm { get; set; }

        //[JsonProperty("spindleSpeeds")]
        //public Optional<List<double>> SpindleSpeeds { get; set; }

        //[JsonProperty("toolNumber")]
        //public Optional<double> ToolNumber { get; set; }
    }

    public class RRF_ObjectModelState
    {
        //[JsonProperty("atxPower")]
        //public object AtxPower { get; set; }

        //[JsonProperty("beep")]
        //public object Beep { get; set; }

        [JsonProperty("currentTool")]
        [JsonConverter(typeof(JsonConverters.OptionalConverter<short>))]
        public Optional<short> CurrentTool { get; set; }

        //[JsonProperty("displayMessage")]
        //public Optional<string> DisplayMessage { get; set; }

        [JsonProperty("gpOut")]
        public Optional<List<RRF_ObjectModelGpOut>> GpOut { get; set; }

        //[JsonProperty("laserPwm")]
        //public object LaserPwm { get; set; }

        //[JsonProperty("logFile")]
        //public Optional<string> LogFile { get; set; }

        //[JsonProperty("logLevel")]
        //public Optional<string> LogLevel { get; set; }

        //[JsonProperty("machineMode")]
        //public Optional<string> MachineMode { get; set; }

        //[JsonProperty("messageBox")]
        //public object MessageBox { get; set; }

        //[JsonProperty("msUpTime")]
        //public Optional<double> MsUpTime { get; set; }

        //[JsonProperty("nextTool")]
        //public Optional<double> NextTool { get; set; }

        //[JsonProperty("powerFailScript")]
        //public Optional<string> PowerFailScript { get; set; }

        //[JsonProperty("previousTool")]
        //public Optional<double> PreviousTool { get; set; }

        //[JsonProperty("restorePodoubles")]
        //public Optional<List<RRF_ObjectModelRestorePodouble>> RestorePodoubles { get; set; }

        [JsonProperty("status")]
        [JsonConverter(typeof(JsonConverters.OptionalConverter<string>))]
        public Optional<string> Status { get; set; }

        //[JsonProperty("time")]
        //public DateTime Time { get; set; }

        //[JsonProperty("upTime")]
        //public Optional<double> UpTime { get; set; }

        public Optional<FLUX_ProcessStatus> GetProcessStatus()
        {
            return Status.Convert(s =>
            {
                switch (s)
                {
                    case "idle":
                    case "paused":
                        return FLUX_ProcessStatus.IDLE;

                    case "halted":
                        return FLUX_ProcessStatus.ERROR;

                    case "busy":
                    case "pausing":
                    case "resuming":
                    case "cancelling":
                    case "simulating":
                    case "processing":
                    case "changingTool":
                        return FLUX_ProcessStatus.CYCLE;

                    default:
                        return FLUX_ProcessStatus.NONE;
                }
            });
        }

        public Optional<bool> IsInChange()
        {
            return Status.Convert(s =>
            {
                switch (s)
                {
                    case "changingTool":
                        return true;
                    default:
                        return false;
                }
            });
        }
    }



    public class RRF_ObjectModelRetraction
    {
        //[JsonProperty("extraRestart")]
        //public Optional<double> ExtraRestart { get; set; }

        //[JsonProperty("length")]
        //public Optional<double> Length { get; set; }

        //[JsonProperty("speed")]
        //public Optional<double> Speed { get; set; }

        //[JsonProperty("unretractSpeed")]
        //public Optional<double> UnretractSpeed { get; set; }

        //[JsonProperty("zHop")]
        //public Optional<double> ZHop { get; set; }
    }

    public class RRF_ObjectModelTool
    {
        //[JsonProperty("active")]
        //public Optional<List<double>> Active { get; set; }

        //[JsonProperty("axes")]
        //public Optional<List<double>> Axes { get; set; }

        //[JsonProperty("extruders")]
        //public Optional<List<double>> Extruders { get; set; }

        //[JsonProperty("fans")]
        //public Optional<List<double>> Fans { get; set; }

        //[JsonProperty("filamentExtruder")]
        //public Optional<double> FilamentExtruder { get; set; }

        //[JsonProperty("heaters")]
        //public Optional<List<double>> Heaters { get; set; }

        [JsonProperty("mix")]
        public Optional<List<double>> Mix { get; set; }

        //[JsonProperty("name")]
        //public Optional<string> Name { get; set; }

        [JsonProperty("number")]
        public Optional<int> Number { get; set; }

        //[JsonProperty("offsets")]
        //public Optional<List<double>> Offsets { get; set; }

        //[JsonProperty("offsetsProbed")]
        //public Optional<double> OffsetsProbed { get; set; }

        //[JsonProperty("retraction")]
        //public RRF_ObjectModelRetraction Retraction { get; set; }

        //[JsonProperty("standby")]
        //public Optional<List<double>> Standby { get; set; }

        //[JsonProperty("state")]
        //public Optional<string> State { get; set; }
    }

    public class RRF_GlobalModelConverter : JsonConverter<RRF_ObjectModelGlobal>
    {
        public override RRF_ObjectModelGlobal ReadJson(JsonReader reader, Type objectType, RRF_ObjectModelGlobal existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            try
            {
                var model = serializer.Deserialize<JObject>(reader);
                return new RRF_ObjectModelGlobal(model);
            }
            catch (Exception)
            {
                return existingValue;
            }
        }

        public override void WriteJson(JsonWriter writer, RRF_ObjectModelGlobal value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }


    [JsonConverter(typeof(RRF_GlobalModelConverter))]
    public class RRF_ObjectModelGlobal : JObject
    {
        public RRF_ObjectModelGlobal(JObject model) : base(model) { }
    }

    public class RRF_ObjectModel : ReactiveObject
    {
        //private Optional<List<RRF_ObjectModelBoard>> _Boards;
        //public Optional<List<RRF_ObjectModelBoard>> Boards { get => _Boards; set => this.RaiseAndSetIfChanged(ref _Boards, value); }

        //private Optional<List<RRF_ObjectModelFan>> _Fans;
        //public Optional<List<RRF_ObjectModelFan>> Fans { get => _Fans; set => this.RaiseAndSetIfChanged(ref _Fans, value); }

        private Optional<RRF_ObjectModelHeat> _Heat;
        public Optional<RRF_ObjectModelHeat> Heat { get => _Heat; set => this.RaiseAndSetIfChanged(ref _Heat, value); }

        private Optional<List<RRF_ObjectModelInput>> _Inputs;
        public Optional<List<RRF_ObjectModelInput>> Inputs { get => _Inputs; set => this.RaiseAndSetIfChanged(ref _Inputs, value); }

        private Optional<RRF_ObjectModelJob> _Job;
        public Optional<RRF_ObjectModelJob> Job { get => _Job; set => this.RaiseAndSetIfChanged(ref _Job, value); }

        private Optional<RRF_ObjectModelMove> _Move;
        public Optional<RRF_ObjectModelMove> Move { get => _Move; set => this.RaiseAndSetIfChanged(ref _Move, value); }

        private Optional<RRF_ObjectModelSensors> _Sensors;
        public Optional<RRF_ObjectModelSensors> Sensors { get => _Sensors; set => this.RaiseAndSetIfChanged(ref _Sensors, value); }

        //private Optional<RRF_ObjectModelSeqs> _Seqs;
        //public Optional<RRF_ObjectModelSeqs> Seqs { get => _Seqs; set => this.RaiseAndSetIfChanged(ref _Seqs, value); }

        /*private Optional<List<RRF_ObjectModelSpindle>> _Spindles;
        public Optional<List<RRF_ObjectModelSpindle>> Spindles { get => _Spindles; set => this.RaiseAndSetIfChanged(ref _Spindles, value); }*/

        private Optional<RRF_ObjectModelState> _State;
        public Optional<RRF_ObjectModelState> State { get => _State; set => this.RaiseAndSetIfChanged(ref _State, value); }

        private Optional<List<RRF_ObjectModelTool>> _Tools;
        public Optional<List<RRF_ObjectModelTool>> Tools { get => _Tools; set => this.RaiseAndSetIfChanged(ref _Tools, value); }

        private Optional<RRF_ObjectModelGlobal> _Global;
        public Optional<RRF_ObjectModelGlobal> Global { get => _Global; set => this.RaiseAndSetIfChanged(ref _Global, value); }

        private Optional<FLUX_FileList> _Queue;
        public Optional<FLUX_FileList> Queue { get => _Queue; set => this.RaiseAndSetIfChanged(ref _Queue, value); }

        private Optional<FLUX_FileList> _Storage;
        public Optional<FLUX_FileList> Storage { get => _Storage; set => this.RaiseAndSetIfChanged(ref _Storage, value); }
    }

    public class RRF_ObjectModelResponse<T>
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("flags")]
        public string Flags { get; set; }

        [JsonProperty("result")]
        public T Result { get; set; }
    }

    public static class RRF_DataUtils
    {
        public static Optional<ParamacroProgress> GetParamacroProgress(this RRF_ObjectModelJob job)
        {
            try
            {
                var file = job.File;
                if (!file.HasValue)
                    return default;
                var file_name = file.Value.FileName;
                if (!file_name.HasValue)
                    return default;
                var file_size = file.Value.Size;
                if (!file_size.HasValue)
                    return default;
                var file_position = job.FilePosition;
                if (!file_position.HasValue)
                    return default;
                var percentage = (double)file_position.Value / file_size.Value * 100.0;
                return new ParamacroProgress(file_name.Value, percentage);
            }
            catch
            {
                return default;
            }
        }

        public static Optional<LineNumber> GetBlockNum(this (RRF_ObjectModelJob job, RRF_ObjectModelState state, List<RRF_ObjectModelInput> input) data)
        {
            try
            {
                if (!data.job.File.HasValue)
                    return default;

                var file = data.job.File.Value;
                if (!file.FileName.HasValue)
                    return (LineNumber)0;
                if (!data.input[2].StackDepth.HasValue)
                    return (LineNumber)0;

                if (!data.state.Status.HasValue)
                    return (LineNumber)0;
                var line_number = data.input[2].LineNumber;
                if (!line_number.HasValue)
                    return (LineNumber)0;

                if (string.IsNullOrEmpty(file.FileName.Value))
                    return (LineNumber)0;
                if (data.input[2].StackDepth.Value > 0)
                    return (LineNumber)0;
                return data.state.Status.Value switch
                {
                    "idle" or "halted" => default,
                    _ => (LineNumber)(line_number.Value / 2),
                };
            }
            catch
            {
                return default;
            }
        }

        public static Optional<MCodePartProgram> GetPartProgram(this (RRF_ObjectModelJob job, RRF_ObjectModelGlobal global, FLUX_FileList storage, FLUX_FileList queue) data)
        {
            try
            {
                if (!data.global.TryGetValue("queue_pos", out var queue_pos_obj))
                    return default;

                var queue_pos = (QueuePosition)queue_pos_obj.ToObject<short>();
                if (queue_pos.Value < 0)
                    return default;

                var job_queue = data.queue.GetJobDictionaryFromQueue();
                if (!job_queue.TryGetValue(queue_pos.Value, out var current_job))
                    return default;

                var storage_dict = data.storage.GetPartProgramDictionaryFromStorage();
                if (!storage_dict.ContainsKey(current_job.MCodeGuid))
                    return default;

                // Full part program from filename
                var partprogram_filename = data.job.File
                    .Convert(f => f.FileName)
                    .ValueOrOptional(() => data.job.LastFileName)
                    .ValueOr(() => "");

                if (MCodePartProgram.TryParse(partprogram_filename, out var full_part_program) && 
                    full_part_program.MCodeGuid == current_job.MCodeGuid)
                { 
                    if (storage_dict.TryGetValue(full_part_program.MCodeGuid, out var part_programs) &&
                        part_programs.TryGetValue(full_part_program.StartBlock, out var part_program))
                        return part_program;
                }

                return storage_dict.FirstOrOptional(kvp => kvp.Key == current_job.MCodeGuid)
                    .Convert(p => p.Value.Values.FirstOrDefault());
            }
            catch
            {
                return default;
            }
        }

        public static async Task<Optional<IFLUX_MCodeRecovery>> GetMCodeRecoveryAsync(this FLUX_FileList files, RRF_ConnectionProvider connection_provider)
        {
            try
            {
                var connection = connection_provider.Connection;
                if (!connection.HasValue)
                    return default;

                if (!files.Files.Any(f => f.Name == "resurrect.g"))
                    return default;

                using var get_resurrect_cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var resurrect_source = await connection.Value.DownloadFileAsync(f => f.StoragePath, "resurrect.g", get_resurrect_cts.Token);
                if (!resurrect_source.HasValue)
                    return default;

                // language=regex
                var hold_file = resurrect_source.Match("M23 \"(?<resurrect_source>.*)\"");
                if (!hold_file.HasValue)
                    return default;

                var hold_mcode_partprogram = hold_file.Value.Groups.Lookup("resurrect_source")
                    .Convert(m => MCodePartProgram.Parse(m.Value));
                if (!hold_mcode_partprogram.HasValue)
                    return default;

                var hold_mcode_vm = connection.Value.Flux.MCodes.AvaiableMCodes.Lookup(hold_mcode_partprogram.Value.MCodeGuid);
                if (!hold_mcode_vm.HasValue)
                    return default;

                // language=regex
                var match_recovery = "M26 S(?<block_num>[+-]?[0-9]+)";
                var hold_recovery = resurrect_source.Match(match_recovery);
                if (!hold_recovery.HasValue)
                    return default;

                var hold_block_num = hold_recovery.Value.Groups.Lookup("block_num")
                    .Convert(t => uint.TryParse(t.Value, out var block_num) ? (BlockNumber)block_num : default);
                if (!hold_block_num.HasValue)
                    return default;

                // language=regex
                var hold_tools = resurrect_source.Matches("T(?<tool>[+-]?[0-9]+)");
                if (!hold_tools.HasValue)
                    return default;

                var hold_tool = hold_tools.Value.LastOrDefault().Groups.Lookup("tool")
                    .Convert(t => short.TryParse(t.Value, out var tool) ? tool : default);
                if (!hold_tool.HasValue)
                    return default;

                var mcode_recovery = new RRF_MCodeRecovery(
                    hold_mcode_partprogram.Value.MCodeGuid,
                    hold_block_num.Value,
                    hold_tool.Value);

                // upload file
                if (!files.Files.Any(f => f.Name == mcode_recovery.FileName))
                {
                    // language=regex
                    var match_plates = "M140 P([+-]?[0-9]+) S([+-]?[0-9]*[.]?[0-9]+)";
                    var hold_plates = resurrect_source.Matches(match_plates);

                    // language=regex
                    var match_chambers = "M141 P([+-]?[0-9]+) S([+-]?[0-9]*[.]?[0-9]+)";
                    var hold_chambers = resurrect_source.Matches(match_chambers);

                    // language=regex
                    var match_e_pos = "G92 E([+-]?[0-9]*[.]?[0-9]+)";
                    var hold_e_pos = resurrect_source.Match(match_e_pos);
                    if (!hold_e_pos.HasValue)
                        return default;

                    // language=regex
                    var match_moves = "G0 ([a-zA-Z0-9. ]*)";
                    var hold_moves = resurrect_source.Matches(match_moves);
                    if (!hold_moves.HasValue)
                        return default;

                    // language=regex
                    var match_temps = "G10 P([+-]?[0-9]+) S([+-]?[0-9]*[.]?[0-9]+) R([+-]?[0-9]*[.]?[0-9]+)";
                    var hold_temps = resurrect_source.Matches(match_temps);
                    if (!hold_temps.HasValue)
                        return default;

                    // language=regex
                    var match_feedrate = "G1 F([+-]?[0-9]*[.]?[0-9]+) P([+-]?[0-9]+)";
                    var hold_feedrate = resurrect_source.Match(match_feedrate);
                    if (!hold_feedrate.HasValue)
                        return default;

                    using var put_resurrect_cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    if (!await connection.Value.PutFileAsync(
                        f => f.StoragePath,
                        mcode_recovery.FileName,
                        true,
                        put_resurrect_cts.Token,
                        get_recovery_lines().ToOptional()))
                        return default;

                    IEnumerable<string> get_recovery_lines()
                    {
                        if (hold_chambers.HasValue)
                            foreach (Match hold_chamber in hold_chambers.Value)
                                yield return hold_chamber.Value;

                        if (hold_plates.HasValue)
                            foreach (Match hold_plate in hold_plates.Value)
                                yield return hold_plate.Value;

                        foreach (Match hold_temp in hold_temps.Value)
                            yield return hold_temp.Value;

                        yield return $"T{hold_tool.Value}";
                        yield return hold_e_pos.Value.Value;

                        yield return "M116";

                        yield return hold_file.Value.Value;
                        yield return hold_recovery.Value.Value;

                        foreach (Match hold_move in hold_moves.Value)
                            yield return hold_move.Value;

                        yield return hold_feedrate.Value.Value;

                        yield return "M24";
                    }
                }

                return mcode_recovery;
            }
            catch (Exception ex)
            {
                return default;
            }
        }
    }

    public class RRF_MCodeRecovery : IFLUX_MCodeRecovery
    {
        public bool IsSelected => true;
        public Guid MCodeGuid { get; }
        public short ToolNumber { get; }
        public BlockNumber StartBlock { get; }
        public string FileName => $"{MCodeGuid}.{StartBlock.Value}";

        public RRF_MCodeRecovery(Guid mcode_guid, BlockNumber start_block, short tool_number)
        {
            MCodeGuid = mcode_guid;
            StartBlock = start_block;
            ToolNumber = tool_number;
        }
    }
}
