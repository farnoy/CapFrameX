﻿using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Data;
using CapFrameX.Statistics;
using LiveCharts;
using MathNet.Numerics.Statistics;
using OxyPlot;
using OxyPlot.Axes;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LiveCharts.Wpf;
using CapFrameX.Data.Session.Contracts;
using OxyPlot.Series;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.PlotBuilder;
using CapFrameX.Extensions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;
using DialogResult = System.Windows.Forms.DialogResult;
using CapFrameX.Statistics.NetStandard.Contracts;
using OxyPlot.Legends;

namespace CapFrameX.ViewModel
{
    public class SynchronizationViewModel : BindableBase, INavigationAware
    {
        private readonly IStatisticProvider _frametimeStatisticProvider;
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppConfiguration _appConfiguration;
        private static ILogger<SynchronizationViewModel> _logger;

        private PlotModel _synchronizationModel;
        private PlotModel _inputLagModel;
        private SeriesCollection _displayTimesHistogramCollection;
        private SeriesCollection _inputLagHistogramCollection;
        private SeriesCollection _droppedFramesStatisticCollection;
        private SeriesCollection _inputLagStatisticCollection;
        private string[] _droppedFramesLabels;
        private string[] _displayTimesHistogramLabels;
        private string[] _inputLagHistogramLabels;
        private bool _useUpdateSession;
        private ISession _session;
        private ISession _previousSession;
        private IFileRecordInfo _recordInfo;
        private string _currentGameName;
        private string _syncRangePercentage = "0%";
        private string _syncRangeLower;
        private string _syncRangeUpper;
        private int _inputLagBarMaxValue = 100;
        private Func<double, string> _inputLagParameterFormatter;
        private string[] _inputLagParameterLabels;

        /// <summary>
        /// https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings
        /// </summary>
        public Func<double, string> HistogramFormatter { get; } =
            value => value.ToString("N", CultureInfo.InvariantCulture);

        /// <summary>
        /// https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings
        /// </summary>
        public Func<ChartPoint, string> PieChartPointLabel { get; } =
            chartPoint => string.Format(CultureInfo.InvariantCulture, "{0} ({1:P})", chartPoint.Y, chartPoint.Participation);

        public PlotModel SynchronizationModel
        {
            get { return _synchronizationModel; }
            set
            {
                _synchronizationModel = value;
                RaisePropertyChanged();
            }
        }
        public PlotModel InputLagModel
        {
            get { return _inputLagModel; }
            set
            {
                _inputLagModel = value;
                RaisePropertyChanged();
            }
        }

        public SeriesCollection DisplayTimesHistogramCollection
        {
            get { return _displayTimesHistogramCollection; }
            set
            {
                _displayTimesHistogramCollection = value;
                RaisePropertyChanged();
            }
        }

        public SeriesCollection InputLagHistogramCollection
        {
            get { return _inputLagHistogramCollection; }
            set
            {
                _inputLagHistogramCollection = value;
                RaisePropertyChanged();
            }
        }

        public SeriesCollection InputLagStatisticCollection
        {
            get { return _inputLagStatisticCollection; }
            set
            {
                _inputLagStatisticCollection = value;
                RaisePropertyChanged();
            }
        }

        public SeriesCollection DroppedFramesStatisticCollection
        {
            get { return _droppedFramesStatisticCollection; }
            set
            {
                _droppedFramesStatisticCollection = value;
                RaisePropertyChanged();
            }
        }

        public string[] DroppedFramesLabels
        {
            get { return _droppedFramesLabels; }
            set
            {
                _droppedFramesLabels = value;
                RaisePropertyChanged();
            }
        }

        public string[] DisplayTimesHistogramLabels
        {
            get { return _displayTimesHistogramLabels; }
            set
            {
                _displayTimesHistogramLabels = value;
                RaisePropertyChanged();
            }
        }

        public string[] InputLagHistogramLabels
        {
            get { return _inputLagHistogramLabels; }
            set
            {
                _inputLagHistogramLabels = value;
                RaisePropertyChanged();
            }
        }

        public string CurrentGameName
        {
            get { return _currentGameName; }
            set
            {
                _currentGameName = value;
                RaisePropertyChanged();
            }
        }

        public string SyncRangeLower
        {
            get { return _syncRangeLower; }
            set
            {
                _syncRangeLower = value;
                _appConfiguration.SyncRangeLower = value;
                RaisePropertyChanged();
                OnSyncRangeChanged();
            }
        }

        public string SyncRangeUpper
        {
            get { return _syncRangeUpper; }
            set
            {
                _syncRangeUpper = value;
                _appConfiguration.SyncRangeUpper = value;
                RaisePropertyChanged();
                OnSyncRangeChanged();
            }
        }

        public string SyncRangePercentage
        {
            get { return _syncRangePercentage; }
            set
            {
                _syncRangePercentage = value;
                RaisePropertyChanged();
            }
        }

        public int InputLagBarMaxValue
        {
            get { return _inputLagBarMaxValue; }
            set
            {
                _inputLagBarMaxValue = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings
        /// </summary>
        public Func<double, string> InputLagParameterFormatter
        {
            get { return _inputLagParameterFormatter; }
            set
            {
                _inputLagParameterFormatter = value;
                RaisePropertyChanged();
            }
        }

        public string[] InputLagParameterLabels
        {
            get { return _inputLagParameterLabels; }
            set
            {
                _inputLagParameterLabels = value;
                RaisePropertyChanged();
            }
        }

        public int InputLagOffset
        {
            get { return _appConfiguration.InputLagOffset; }
            set
            {
                _appConfiguration.InputLagOffset = value;
                UpdateCharts();
                RaisePropertyChanged();
            }
        }

        public ICommand CopyUntilDisplayedTimesValuesCommand { get; }

        public ICommand CopyDisplayTimesHistogramDataCommand { get; }

        public ICommand CopyInputLagHistogramDataCommand { get; }

        public ICommand CopyInputLagStatisticalParameterCommand { get; }

        public ICommand SaveInputLagPlotAsSVG{ get; }

        public ICommand SaveDisplayTimesPlotAsSVG { get; }

        public ICommand SaveInputLagPlotAsPNG { get; }

        public ICommand SaveDisplayTimesPlotAsPNG { get; }


        public SynchronizationViewModel(IStatisticProvider frametimeStatisticProvider,
                                        IEventAggregator eventAggregator,
                                        IAppConfiguration appConfiguration,
                                        ILogger<SynchronizationViewModel> logger)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            _frametimeStatisticProvider = frametimeStatisticProvider;
            _eventAggregator = eventAggregator;
            _appConfiguration = appConfiguration;
            _logger = logger;

            CopyUntilDisplayedTimesValuesCommand = new DelegateCommand(OnCopyUntilDisplayedTimesValues);
            CopyDisplayTimesHistogramDataCommand = new DelegateCommand(CopDisplayTimesHistogramData);
            CopyInputLagHistogramDataCommand = new DelegateCommand(CopyInputLagHistogramData);
            CopyInputLagStatisticalParameterCommand = new DelegateCommand(CopyInputLagStatisticalParameter);
            SaveInputLagPlotAsSVG = new DelegateCommand(() => OnSavePlotAsImage("inputlag", "svg"));
            SaveDisplayTimesPlotAsSVG = new DelegateCommand(() => OnSavePlotAsImage("displaytimes", "svg"));
            SaveInputLagPlotAsPNG = new DelegateCommand(() => OnSavePlotAsImage("inputlag", "png"));
            SaveDisplayTimesPlotAsPNG = new DelegateCommand(() => OnSavePlotAsImage("displaytimes", "png"));

            InputLagParameterFormatter = value => value.ToString(string.Format("F{0}",
                _appConfiguration.FpsValuesRoundingDigits), CultureInfo.InvariantCulture);

            _syncRangeLower = _appConfiguration.SyncRangeLower;
            _syncRangeUpper = _appConfiguration.SyncRangeUpper;

            SubscribeToUpdateSession();

            SynchronizationModel = new PlotModel
            {
                PlotMargins = new OxyThickness(40, 10, 0, 40),
                PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204),
            };

            InputLagModel = new PlotModel
            {
                PlotMargins = new OxyThickness(40, 10, 0, 40),
                PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204),
            };

            stopwatch.Stop();
            _logger.LogInformation(this.GetType().Name + " {initializationTime}s initialization time", Math.Round(stopwatch.ElapsedMilliseconds * 1E-03, 1));
        }

        private void SubscribeToUpdateSession()
        {
            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>()
                            .Subscribe(msg =>
                            {
                                if (msg == null)
                                    return;

                                _session = msg.CurrentSession;
                                _recordInfo = msg.RecordInfo;

                                if (_useUpdateSession)
                                {
                                    if (msg.RecordInfo != null && msg.RecordInfo.GameName != null)
                                    {
                                        // Do update actions
                                        CurrentGameName = msg.RecordInfo.GameName;
                                        UpdateCharts();
                                        SyncRangePercentage = GetSyncRangePercentageString();
                                    }
                                }
                            });

            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.ThemeChanged>>()
                  .Subscribe(msg =>
                  {
                      UpdateCharts();
                  });
        }

        private void OnCopyUntilDisplayedTimesValues()
        {
            if (_session == null)
                return;

            StringBuilder builder = new StringBuilder();
            var currentSessionDisplayTimes = _session.Runs.SelectMany(r => r.CaptureData.MsUntilDisplayed).ToArray();
            foreach (var dcTime in currentSessionDisplayTimes)
            {
                builder.Append(dcTime + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void CopDisplayTimesHistogramData()
        {
            if (DisplayTimesHistogramCollection == null || DisplayTimesHistogramLabels == null)
                return;

            StringBuilder builder = new StringBuilder();
            var chartValues = DisplayTimesHistogramCollection.First().Values;

            foreach (var bin in DisplayTimesHistogramLabels.Select((value, i) => new { i, value }))
            {
                builder.Append(bin.value.ToString(CultureInfo.InvariantCulture) + "\t" + chartValues[bin.i]
                    .ToString() + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void CopyInputLagHistogramData()
        {
            throw new NotImplementedException();
        }

        private void CopyInputLagStatisticalParameter()
        {
            throw new NotImplementedException();
        }

        private void OnSyncRangeChanged()
            => SyncRangePercentage = GetSyncRangePercentageString();

        private void UpdateCharts()
        {
            if (_session == null)
                return;

            // Do not run on background thread, leads to errors on analysis page
            var inputLagTimes = _session.CalculateInputLagTimes(EInputLagType.Expected).Select(val => val += InputLagOffset).ToList();
            var upperBoundInputLagTimes = _session.CalculateInputLagTimes(EInputLagType.UpperBound).Select(val => val += InputLagOffset).ToList();
            var lowerBoundInputLagTimes = _session.CalculateInputLagTimes(EInputLagType.LowerBound).Select(val => val += InputLagOffset).ToList();
            var frametimes = _session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToList();
            var displayTimes = _session.Runs.SelectMany(r => r.CaptureData.MsUntilDisplayed).ToList();
            var appMissed = _session.Runs.SelectMany(r => r.CaptureData.Dropped).ToList();

            SetFrameDisplayTimesChart(frametimes, displayTimes);
            SetFrameInputLagChart(frametimes, upperBoundInputLagTimes, lowerBoundInputLagTimes);
            Task.Factory.StartNew(() => SetDisplayTimesHistogramChart(displayTimes));
            Task.Factory.StartNew(() => SetInputLagHistogramChart(inputLagTimes.Where(t => !double.IsNaN(t)).ToList()));
            Task.Factory.StartNew(() => SetInputLagStatisticChart(
                inputLagTimes.Where(t => !double.IsNaN(t)).ToList(),
                upperBoundInputLagTimes.Where(t => !double.IsNaN(t)).ToList(), 
                lowerBoundInputLagTimes.Where(t => !double.IsNaN(t)).ToList())
            );
            Task.Factory.StartNew(() => SetDroppedFramesChart(appMissed));
        }

        private void SetFrameDisplayTimesChart(IList<double> frametimes, IList<double> displaytimes)
        {
            if (frametimes == null || !frametimes.Any())
                return;

            if (displaytimes == null || !displaytimes.Any())
                return;

            var yMin = Math.Min(frametimes.Min(), displaytimes.Min());
            var yMax = Math.Max(frametimes.Max(), displaytimes.Max());

            var frametimeSeries = new Statistics.PlotBuilder.LineSeries
            {
                Title = "Frametimes",
                StrokeThickness = 1,
                LegendStrokeThickness = 4,
                FontSize = 13,
                Color = Constants.FrametimeStroke,
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            var untilDisplayedTimesSeries = new Statistics.PlotBuilder.LineSeries
            {
                Title = "Until displayed times",
                StrokeThickness = 1,
                LegendStrokeThickness = 4,
                FontSize = 13,
                Color = OxyColor.FromArgb(150, 241, 125, 32),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            frametimeSeries.Points.AddRange(frametimes.Select((x, i) => new DataPoint(i, x)));
            untilDisplayedTimesSeries.Points.AddRange(displaytimes.Select((x, i) => new DataPoint(i, x)));

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                var tmp = new PlotModel
                {
                    PlotMargins = new OxyThickness(40, 10, 40, 40),
                    PlotAreaBorderColor = _appConfiguration.UseDarkMode ? OxyColor.FromArgb(100, 204, 204, 204) : OxyColor.FromArgb(50, 30, 30, 30),
                    TextColor = _appConfiguration.UseDarkMode ? OxyColors.White : OxyColors.Black
                };

                tmp.Legends.Add(new Legend()
                {
                    LegendPosition = LegendPosition.TopCenter,
                    LegendOrientation = LegendOrientation.Horizontal
                });

                tmp.Series.Add(frametimeSeries);
                tmp.Series.Add(untilDisplayedTimesSeries);

                //Axes
                //X
                tmp.Axes.Add(new LinearAxis()
                {
                    Key = "xAxis",
                    Position = OxyPlot.Axes.AxisPosition.Bottom,
                    Title = "Samples",
                    FontSize = 13,
                    AxisTitleDistance = 10,
                    Minimum = 0,
                    Maximum = frametimes.Count,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineThickness = 1,
                    MajorGridlineColor = _appConfiguration.UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30),
                    MinorTickSize = 0,
                    MajorTickSize = 0
                });

                //Y
                tmp.Axes.Add(new LinearAxis()
                {
                    Key = "yAxis",
                    Position = OxyPlot.Axes.AxisPosition.Left,
                    Title = "Frametime + until displayed time [ms]",
                    FontSize = 13,
                    AxisTitleDistance = 10,
                    Minimum = yMin - (yMax - yMin) / 6,
                    Maximum = yMax + (yMax - yMin) / 6,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineThickness = 1,
                    MajorGridlineColor = _appConfiguration.UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30),
                    AbsoluteMinimum = 0,
                    MinorTickSize = 0,
                    MajorTickSize = 0
                });

                SynchronizationModel = tmp;
            }));
        }

        private void SetDisplayTimesHistogramChart(IList<double> displaytimes)
        {
            var histogramValues = new ChartValues<double>();
            var bins = new List<double>();

            if (!(displaytimes == null || !displaytimes.Any()))
            {
                var discreteDistribution = _frametimeStatisticProvider.GetDiscreteDistribution(displaytimes);
                if (discreteDistribution.Any())
                {
                    var histogram = new Histogram(displaytimes, discreteDistribution.Length);

                    for (int i = 0; i < discreteDistribution.Length; i++)
                    {
                        var count = discreteDistribution[i].Count;
                        var avg = count > 0 ?
                                  discreteDistribution[i].Average() :
                                  (histogram[i].UpperBound + histogram[i].LowerBound) / 2;

                        bins.Add(avg);
                        histogramValues.Add(count);
                    }
                }
            }

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                DisplayTimesHistogramCollection = new SeriesCollection
                {
                    new LiveCharts.Wpf.ColumnSeries
                    {
                        Title = "Until displayed time distribution",
                        FontSize = 12,
                        Values = histogramValues,
                        Fill = new SolidColorBrush(Color.FromRgb(241, 125, 32)),
                        DataLabels = true,
                    }
                };

                DisplayTimesHistogramLabels = bins.Select(bin => Math.Round(bin, 2)
                    .ToString(CultureInfo.InvariantCulture)).ToArray();
            }));
        }

        private void SetInputLagHistogramChart(IList<double> inputLagTimes)
        {
            var bins = new List<double>();
            var histogramValues = new ChartValues<double>();

            if (!(inputLagTimes == null || !inputLagTimes.Any()))
            {
                var discreteDistribution = _frametimeStatisticProvider.GetDiscreteDistribution(inputLagTimes);
                if (discreteDistribution.Any())
                {
                    var histogram = new Histogram(inputLagTimes, discreteDistribution.Length);

                    for (int i = 0; i < discreteDistribution.Length; i++)
                    {
                        var count = discreteDistribution[i].Count;
                        var avg = count > 0 ?
                                  discreteDistribution[i].Average() :
                                  (histogram[i].UpperBound + histogram[i].LowerBound) / 2;

                        bins.Add(avg);
                        histogramValues.Add(count);
                    }
                }
            }

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                InputLagHistogramCollection = new SeriesCollection
                {
                    new LiveCharts.Wpf.ColumnSeries
                    {
                        Title = "Input lag time distribution",
                        FontSize = 12,
                        Values = histogramValues,
                        Fill = new SolidColorBrush(Color.FromRgb(241, 125, 32)),
                        DataLabels = true,
                    }
                };

                InputLagHistogramLabels = bins.Select(bin => Math.Round(bin, 2)
                    .ToString(CultureInfo.InvariantCulture)).ToArray();
            }));
        }

        private void SetInputLagStatisticChart(IList<double> inputLagTimes, IList<double> upperBoundInputLagTimes, IList<double> lowerBoundInputLagTimes)
        {
            double upperBound = 0;
            double expected = 0;
            double lowerBound = 0;

            if (!(inputLagTimes == null || !inputLagTimes.Any()))
            {
                upperBound = upperBoundInputLagTimes.Average();
                expected = inputLagTimes.Average();
                lowerBound = lowerBoundInputLagTimes.Average();
            }

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                IChartValues values = new ChartValues<double>
                {
                    upperBound,
                    expected,
                    lowerBound
                };

                InputLagStatisticCollection = new SeriesCollection
                {
                    new RowSeries
                    {
                        Title = "Average input lag",
                        Fill = new SolidColorBrush(Color.FromRgb(241, 125, 32)),
                        Values = values,
                        DataLabels = true,
                        FontSize = 12,
                        MaxRowHeigth = 25
                    }
                };

                double maxOffset = (values as IList<double>).Max() * 0.15;
                InputLagBarMaxValue = (int)((values as IList<double>).Max() + maxOffset);

                InputLagParameterLabels = new List<string>
                {
                    "Upper bound",
                    "Expected",
                    "Lower bound"
                }.ToArray();
            }));
        }

        private void SetDroppedFramesChart(List<bool> appMissed)
        {
            if (!appMissed.Any())
                return;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                DroppedFramesStatisticCollection = new SeriesCollection()
                {
                    new LiveCharts.Wpf.PieSeries
                    {
                        Title = "Synced frames",
                        Values = new ChartValues<int>(){ appMissed.Count(flag => flag == false) },
                        DataLabels = true,
                        StrokeThickness = 0,
                        LabelPosition = PieLabelPosition.InsideSlice,
                        LabelPoint = PieChartPointLabel,
                        FontSize = 13
                    },
                    new LiveCharts.Wpf.PieSeries
                    {
                        Title = "Dropped frames",
                        Values = new ChartValues<int>(){ appMissed.Count(flag => flag == true) },
                        DataLabels = true,
                        StrokeThickness = 0,
                        LabelPosition = PieLabelPosition.InsideSlice,
                        LabelPoint = PieChartPointLabel,
                        FontSize = 13
                    }
                };
            }));
        }

        private string GetSyncRangePercentageString()
        {
            if (string.IsNullOrWhiteSpace(SyncRangeLower) ||
                string.IsNullOrWhiteSpace(SyncRangeUpper))
                return "NaN";

            int lowerValue = Convert.ToInt32(SyncRangeLower);
            int upperValue = Convert.ToInt32(SyncRangeUpper);
            var percentage = _session.GetSyncRangePercentage(lowerValue, upperValue);

            return (Math.Round(percentage * 100, 0))
                .ToString() + "%";
        }

        private void SetFrameInputLagChart(IList<double> frametimes, IList<double> upperBoundInputlagtimes, IList<double> lowerBoundInputlagtimes)
        {
            if (frametimes.IsNullOrEmpty() ||
                upperBoundInputlagtimes.IsNullOrEmpty() ||
                lowerBoundInputlagtimes.IsNullOrEmpty() ||
                upperBoundInputlagtimes.All(t => double.IsNaN(t)) ||
                lowerBoundInputlagtimes.All(t => double.IsNaN(t)))
            {

                InputLagModel = new PlotModel
                {
                    PlotMargins = new OxyThickness(40, 10, 0, 40),
                    PlotAreaBorderColor = _appConfiguration.UseDarkMode ? OxyColor.FromArgb(100, 204, 204, 204) : OxyColor.FromArgb(50, 30, 30, 30)
                };

                InputLagModel.Legends.Add(new Legend()
                {
                    LegendPosition = LegendPosition.TopCenter,
                    LegendOrientation = LegendOrientation.Horizontal
                });

                return;
            }

            var appMissed = _session.Runs.SelectMany(r => r.CaptureData.Dropped).ToList();
            var filteredFrametimes = frametimes.Where((ft, i) => appMissed[i] != true).ToList();

            var yMin = Math.Min(filteredFrametimes.Min(), lowerBoundInputlagtimes.Min());
            var yMax = Math.Max(filteredFrametimes.Max(), upperBoundInputlagtimes.Max());

            var frametimeSeries = new Statistics.PlotBuilder.LineSeries
            {
                Title = "Frametimes",
                StrokeThickness = 1,
                LegendStrokeThickness = 4,
                Color = Constants.FrametimeStroke,
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            var upperBoundInputLagSeries = new Statistics.PlotBuilder.LineSeries
            {
                Title = "Input lag high",
                StrokeThickness = 1,
                LegendStrokeThickness = 4,
                Color = OxyColor.FromRgb(255, 150, 150),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            var lowerBoundInputLagSeries = new Statistics.PlotBuilder.LineSeries
            {
                Title = "Input lag low",
                StrokeThickness = 1,
                LegendStrokeThickness = 4,
                Color = OxyColor.FromRgb(200, 140, 140),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            var areaSeries = new AreaSeries()
            {
                StrokeThickness = 1,
                Color = OxyColors.Transparent,
                Fill = OxyColor.FromArgb(64, 225, 145, 145)
            };

            frametimeSeries.Points.AddRange(filteredFrametimes.Select((x, i) => new DataPoint(i, x)));
            upperBoundInputLagSeries.Points.AddRange(upperBoundInputlagtimes.Select((x, i) => new DataPoint(i, x)));
            lowerBoundInputLagSeries.Points.AddRange(lowerBoundInputlagtimes.Select((x, i) => new DataPoint(i, x)));
            areaSeries.Points.AddRange(lowerBoundInputlagtimes.Select((x, i) => new DataPoint(i, x)));
            areaSeries.Points2.AddRange(upperBoundInputlagtimes.Select((x, i) => new DataPoint(i, x)));

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                var tmp = new PlotModel
                {
                    PlotMargins = new OxyThickness(40, 10, 40, 40),
                    PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204),
                    TextColor = _appConfiguration.UseDarkMode ? OxyColors.White : OxyColors.Black
                };

                tmp.Legends.Add(new Legend()
                {
                    LegendPosition = LegendPosition.TopCenter,
                    LegendOrientation = LegendOrientation.Horizontal
                });

                tmp.Series.Add(frametimeSeries);
                tmp.Series.Add(upperBoundInputLagSeries);
                tmp.Series.Add(lowerBoundInputLagSeries);
                tmp.Series.Add(areaSeries);

                //Axes
                //X
                tmp.Axes.Add(new LinearAxis()
                {
                    Key = "xAxis",
                    Position = OxyPlot.Axes.AxisPosition.Bottom,
                    Title = "Samples",
                    FontSize = 13,
                    AxisTitleDistance = 10,
                    Minimum = 0,
                    Maximum = filteredFrametimes.Count(),
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineThickness = 1,
                    MajorGridlineColor = _appConfiguration.UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30),
                    MinorTickSize = 0,
                    MajorTickSize = 0
                });

                //Y
                tmp.Axes.Add(new LinearAxis()
                {
                    Key = "yAxis",
                    Position = OxyPlot.Axes.AxisPosition.Left,
                    Title = "Frametime + input lag [ms]",
                    FontSize = 13,
                    AxisTitleDistance = 10,
                    Minimum = yMin - (yMax - yMin) / 6,
                    Maximum = yMax + (yMax - yMin) / 6,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineThickness = 1,
                    MajorGridlineColor = _appConfiguration.UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30),
                    AbsoluteMinimum = 0,
                    MinorTickSize = 0,
                    MajorTickSize = 0
                });

                InputLagModel = tmp;
            }));
        }



        protected void OnSavePlotAsImage(string plotType, string fileFormat)
        {
            var filename = $"{CurrentGameName}_SynchronizationChartExport_{plotType}";


            if (fileFormat == "svg")
            {
                ImageExport.SavePlotAsSVG(plotType == "inputlag" ? InputLagModel : SynchronizationModel, filename, _appConfiguration.HorizontalGraphExportRes, _appConfiguration.VerticalGraphExportRes);
            }
            else if (fileFormat == "png")
            {
                ImageExport.SavePlotAsPNG(plotType == "inputlag" ? InputLagModel : SynchronizationModel, filename, _appConfiguration.HorizontalGraphExportRes, _appConfiguration.VerticalGraphExportRes, _appConfiguration.UseDarkMode);
            }
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _useUpdateSession = true;

            if (_session != null && _recordInfo != null && _session?.Hash != _previousSession?.Hash)
            {
                CurrentGameName = _recordInfo.GameName;
                UpdateCharts();
                SyncRangePercentage = GetSyncRangePercentageString();
            }
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _previousSession = _session;
            _useUpdateSession = false;
        }
    }
}
