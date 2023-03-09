using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    [DataContract]
    public class Heightmap
    {
        [DataMember(Name = "axis0")]
        public char Axis0 { get; set; }

        [DataMember(Name = "axis1")]
        public char Axis1 { get; set; }

        [DataMember(Name = "min0")]
        public double Min0 { get; set; }

        [DataMember(Name = "min1")]
        public double Min1 { get; set; }

        [DataMember(Name = "max0")]
        public double Max0 { get; set; }

        [DataMember(Name = "max1")]
        public double Max1 { get; set; }

        [DataMember(Name = "radius")]
        public double Radius { get; set; }

        [DataMember(Name = "spacing0")]
        public double Spacing0 { get; set; }

        [DataMember(Name = "spacing1")]
        public double Spacing1 { get; set; }

        [DataMember(Name = "num0")]
        public int Num0 { get; set; }

        [DataMember(Name = "num1")]
        public int Num1 { get; set; }

        [DataMember(Name = "points")]
        public List<double> Points { get; set; }

        public Heightmap()
        {
            Points = new List<double>();
        }
    }

    public class HeightmapViewModel : FluxRoutableViewModel<HeightmapViewModel>
    {
        public Optional<Heightmap> _Heightmap;
        [RemoteOutput(true)]
        public Optional<Heightmap> Heightmap
        {
            get => _Heightmap;
            set => this.RaiseAndSetIfChanged(ref _Heightmap, value);
        }

        private readonly ObservableAsPropertyHelper<Optional<FLUX_Temp>> _PlateTemperature;
        [RemoteOutput(true, typeof(FluxTemperatureConverter))]
        public Optional<FLUX_Temp> PlateTemperature => _PlateTemperature.Value;

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ProbeHeightmapCommand { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ProbeHeightmapSettingsCommand { get; }

        //private ushort _WidthProbePoints = 2;
        //[RemoteInput(min: 2, max: 10)]
        //public ushort WidthProbePoints
        //{
        //    get => _WidthProbePoints;
        //    set => this.RaiseAndSetIfChanged(ref _WidthProbePoints, value);
        //}

        //private ushort _DepthProbePoints = 2;
        //[RemoteInput(min: 2, max: 10)]
        //public ushort DepthProbePoints 
        //{
        //    get => _DepthProbePoints;
        //    set => this.RaiseAndSetIfChanged(ref _DepthProbePoints, value);
        //}

        public HeightmapViewModel(FluxViewModel flux) : base(flux)
        {
            var can_probe = Flux.StatusProvider
               .WhenAnyValue(s => s.StatusEvaluation)
               .Select(s => s.CanSafeCycle);

            ProbeHeightmapCommand = ReactiveCommandRC.CreateFromTask(ProbeHeightmapAsync, this, can_probe);
            ProbeHeightmapSettingsCommand = ReactiveCommandRC.CreateFromTask(ProbeHeightmapSettingsAsync, this);

            _PlateTemperature = Flux.ConnectionProvider
                .ObserveVariable(c => c.TEMP_PLATE, "main.plate")
                .ObservableOrDefault()
                .ToPropertyRC(this, v => v.PlateTemperature);
        }

        private Task ProbeHeightmapSettingsAsync()
        {
            return Flux.ShowModalDialogAsync(f => f.Temperatures);
        }

        private async Task ProbeHeightmapAsync()
        {
            var printer = Flux.SettingsProvider.Printer;
            if (!printer.HasValue)
                return;

            var printer_size = printer.Value.MachineSize;
            if (!printer_size.HasValue)
                return;

            //var min0 = printer_size.Value.border / 2;
            //var max0 = printer_size.Value.width - printer_size.Value.border / 2;
            //var spacing_width = (max0 - min0) / (WidthProbePoints - 1);

            //var min1 = printer_size.Value.border / 2;
            //var max1 = printer_size.Value.depth - printer_size.Value.border / 2;
            //var spacing_depth = (max1 - min1) / (DepthProbePoints - 1);

            using var put_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var wait_cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await Flux.ConnectionProvider.ExecuteParamacroAsync(_ => new[]
            {
                "T-1",
                "G28 Z0",
                //$"M557 X{min0:0.00}:{max0:0.00} Y{min1:0.00}:{max1:0.00} S{spacing_width:0.00}:{spacing_depth:0.00}".Replace(",", "."),
                "G29 S0 P\"heightmap.csv\"",
            }, put_cts.Token, true, wait_cts.Token, false);

            await UpdateHeightmap();
        }

        private async Task UpdateHeightmap()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var heightmap_csv = await Flux.ConnectionProvider.GetFileAsync("/sys", "heightmap.csv", cts.Token);
                if (!heightmap_csv.HasValue)
                    return;

                var line_nr = 0;
                var heightmap = new Heightmap();
                foreach (var line in heightmap_csv.Value.ReadLines())
                {
                    switch (line_nr++)
                    {
                        case 0:
                            continue;
                        case 1:
                            continue;
                        case 2:
                            var parts = line.Split(',');
                            heightmap.Axis0 = parts[0][0];
                            heightmap.Axis1 = parts[1][0];
                            heightmap.Min0 = double.Parse(parts[2], CultureInfo.InvariantCulture);
                            heightmap.Max0 = double.Parse(parts[3], CultureInfo.InvariantCulture);
                            heightmap.Min1 = double.Parse(parts[4], CultureInfo.InvariantCulture);
                            heightmap.Max1 = double.Parse(parts[5], CultureInfo.InvariantCulture);
                            heightmap.Radius = double.Parse(parts[6], CultureInfo.InvariantCulture);
                            heightmap.Spacing0 = double.Parse(parts[7], CultureInfo.InvariantCulture);
                            heightmap.Spacing1 = double.Parse(parts[8], CultureInfo.InvariantCulture);
                            heightmap.Num0 = int.Parse(parts[9]);
                            heightmap.Num1 = int.Parse(parts[10]);
                            break;
                        default:
                            parts = line.Split(',');
                            foreach (var part in parts)
                                heightmap.Points.Add(double.Parse(part, CultureInfo.InvariantCulture));
                            break;
                    }
                }

                Heightmap = heightmap;
            }
            catch (Exception)
            {
            }
        }

        public override Task OnNavigatedFromAsync(Optional<IFluxRoutableViewModel> from)
        {
            return UpdateHeightmap();
        }
    }
}
