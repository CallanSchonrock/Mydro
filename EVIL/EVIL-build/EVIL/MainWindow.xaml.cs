using OxyPlot.Series;
using OxyPlot;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OxyPlot.Axes;
using System.Collections.ObjectModel;
using System.Globalization;
using System.ComponentModel;
using System;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using OxyPlot.Annotations;

namespace EVIL
{

    public partial class MainWindow : Window
    {
        public PlotModel PlotModel { get; private set; }
        public PlotModel PlotPDF { get; private set; }
        public PlotModel PlotLinRegression { get; private set; }

        public ObservableCollection<Point> Points { get; set; }

        public List<double> Probabilities = new List<double> { 0.999, 0.995, 0.99, 0.98, 0.95, 0.9, 0.8, 0.75, 0.6321, 0.5, 0.3935, 0.3, 0.2, 0.1, 0.05, 0.02, 0.01, 0.005, 0.002, 0.001, 0.0005 };
        List<double> DesignValues = new List<double>();
        LogPearsonFit logDist = new LogPearsonFit();
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            Points = new ObservableCollection<Point>();

            PlotModel = new PlotModel {  };

            PlotLinRegression = new PlotModel { };

            // Create and configure the X axis as logarithmic
            var xAxis = new LogarithmicAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Exceedance Probability",
                LabelFormatter = x => FormatToPercentage(x, 1),
                StartPosition = 1,
                EndPosition = 0.0001,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Base=10
            };
            PlotModel.Axes.Add(xAxis);

            // Create and configure the Y axis as logarithmic
            var yAxis = new LogarithmicAxis
            {
                Position = AxisPosition.Left,
                Title = "Value",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Base=10
            };
            PlotModel.Axes.Add(yAxis);

            PlotLinRegression = new PlotModel { };
            var linRegressXAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Expected Value",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
            };
            PlotLinRegression.Axes.Add(linRegressXAxis);

            var linRegressYAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Observed Value",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
            };
            PlotLinRegression.Axes.Add(linRegressYAxis);

            UpdatePlot(0);
            plotView.InvalidatePlot(true);
            linRegressionPlotView.InvalidatePlot(true);
        }

        private static string FormatToPercentage(double value, int significantFigures)
        {
            double percentage = value * 100;
            if (percentage == 0)
                return "0 %";
            int digitsBeforeDecimal = (int)Math.Floor(Math.Log10(Math.Abs(percentage)) + 1);
            int decimalPlaces = significantFigures - digitsBeforeDecimal;
            string format = decimalPlaces > 0 ? $"F{decimalPlaces}" : "F0";
            string formattedValue = percentage.ToString(format);
            return formattedValue + " %";
        }

        private void PlotButton_Click(object sender, RoutedEventArgs e)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            UpdateDistribution();
            stopwatch.Stop();
            Debug.WriteLine($"UpdateDistribution took: {stopwatch.ElapsedMilliseconds} ms");
            stopwatch.Restart(); // Restart the stopwatch for the next function
            UpdateEmpiricalProbabilities();
            stopwatch.Stop();
            Debug.WriteLine($"UpdateEmpiricalProbabilities took: {stopwatch.ElapsedMilliseconds} ms");
            stopwatch.Restart(); // Restart the stopwatch for the next function
            UpdateFfaProbabilities();
            stopwatch.Stop();
            Debug.WriteLine($"UpdateFfaProbabilities took: {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Restart(); // Restart the stopwatch for the next function

            List<double> y_true = new List<double>();
            List<double> y_pred = new List<double>();
            foreach (var point in Points)
            {
                y_true.Add(point.Value);
                y_pred.Add(point.ExpectedValue);
            }
            double rSquared = Get_R_Squared(y_true, y_pred);
            stopwatch.Stop();
            Debug.WriteLine($"Get_R_Squared took: {stopwatch.ElapsedMilliseconds} ms");
            stopwatch.Restart(); // Restart the stopwatch for the next function
            UpdatePlot(rSquared);
            stopwatch.Stop();
            Debug.WriteLine($"UpdatePlot took: {stopwatch.ElapsedMilliseconds} ms");
            stopwatch.Restart(); // Restart the stopwatch for the next function
            AdjustPlotViewLimits();
            stopwatch.Stop();
            Debug.WriteLine($"AdjustPlotViewLimits took: {stopwatch.ElapsedMilliseconds} ms");
        }

        private void UpdateDistribution()
        {
            List<double> values = Points.Select(p => p.Value).ToList();
            logDist.Fit(values);
            DesignValues = logDist.InverseCdf(Probabilities);
        }

        private void UpdateEmpiricalProbabilities()
        {
            // Step 1: Extract and sort values
            List<double> values = Points.Select(p => p.Value).ToList();
            List<double> sortedValues = values.ToList();
            sortedValues.Sort();

            // Step 2: Calculate empirical probabilities
            int totalPoints = values.Count;
            for (int i = 0; i < totalPoints; i++)
            {
                double empiricalProbability = 1 - (double) sortedValues.IndexOf(values[i]) / totalPoints;
                Points[i].EmpiricalProbability = empiricalProbability;
            }
        }

        private void UpdateFfaProbabilities()
        {
            List<double> values = Points.Select(p => p.Value).ToList();
            List<double> ffaProbabilities = logDist.PearsonTypeIIICdf(values);

            for (int i = 0; i < Points.Count; i++)
            {
                Points[i].FfaProbability = 1 - ffaProbabilities[i];
                Points[i].ExpectedValue = logDist.InverseCdf(new List<double>() { Points[i].EmpiricalProbability } ) [0];
            }
 
        }

        private void UpdatePlot(double r2)
        {
            var scatterSeries = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerStroke = OxyColors.Black,     // Outline color
                MarkerFill = OxyColors.Red      // Fill color
            };

            // Add points from the ObservableCollection
            foreach (var point in Points)
            {
                scatterSeries.Points.Add(new ScatterPoint(point.EmpiricalProbability, point.Value));
            }

            var lineSeries = new LineSeries
            {
                Color = OxyColors.Blue, // Line color
                StrokeThickness = 2     // Line thickness
            };

            if (DesignValues.Count > 0)
            {
                for (int i = 0; i < DesignValues.Count; i++)
                {
                    lineSeries.Points.Add(new DataPoint(Probabilities[i], DesignValues[i]));
                }
            }
            
            // Update the PlotModel
            PlotModel.Series.Clear();
            PlotModel.Series.Add(scatterSeries);
            PlotModel.Series.Add(lineSeries);
            plotView.InvalidatePlot(true);

            var observedLinRegress = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerStroke = OxyColors.Black,     // Outline color
                MarkerFill = OxyColors.Red      // Fill color
            };

            // Add points from the ObservableCollection
            foreach (var point in Points)
            {
                observedLinRegress.Points.Add(new ScatterPoint(point.Value, point.ExpectedValue));
            }

            var yx = new LineSeries
            {
                Color = OxyColors.Blue, // Line color
                StrokeThickness = 2     // Line thickness
            };

            foreach (var point in Points)
            {
                yx.Points.Add(new DataPoint(point.Value, point.Value));
            }

            PlotLinRegression.Series.Clear();
            PlotLinRegression.Series.Add(observedLinRegress);
            PlotLinRegression.Series.Add(yx);

            double min;
            double max;
            try
            {
                min = Math.Min(Points.Min(p => p.Value), Points.Min(p => p.ExpectedValue));
                max = Math.Max(Points.Max(p => p.Value), Points.Max(p => p.ExpectedValue));
            }
            catch 
            {
                min = -999;
                max = -999;
            }

            var textAnnotation = new TextAnnotation
            {
                Text = $"R² = {Math.Round(r2,5)}",
                TextPosition = new DataPoint(min, max), // Adjust this to set the position
                FontSize = 12,
                FontWeight = OxyPlot.FontWeights.Bold,
                TextColor = OxyColors.Black,
                Background = OxyColors.White,
                Padding = new OxyThickness(5),
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Left,
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Top
            };

            PlotLinRegression.Annotations.Add(textAnnotation);
            linRegressionPlotView.InvalidatePlot(true);
        }

        public double Get_R_Squared(List<double> yTrue, List<double> yPred, List<double> weights = null)
        {
            // Initialize weights to 1.0 for each element if weights is null
            if (weights == null)
            {
                weights = Enumerable.Repeat(1.0, yTrue.Count).ToList();
            }

            // Calculate the weighted mean of yTrue
            double weightedSumYTrue = yTrue.Zip(weights, (yt, w) => yt * w).Sum();
            double totalWeight = weights.Sum();
            double meanYTrue = weightedSumYTrue / totalWeight;

            // Calculate the weighted numerator
            double weightedNum = 0;
            for (int i = 0; i < yTrue.Count; i++)
            {
                weightedNum += weights[i] * Math.Pow(yTrue[i] - yPred[i], 2);
            }

            // Calculate the weighted denominator
            double weightedDenom = 0;
            for (int i = 0; i < yTrue.Count; i++)
            {
                weightedDenom += weights[i] * Math.Pow(yTrue[i] - meanYTrue, 2);
            }

            // Calculate and return the R-squared value
            return 1 - (weightedNum / weightedDenom);
        }

        private void AdjustPlotViewLimits()
        {
            // Get the current axes from the plot model
            var xAxis = PlotModel.Axes[0] as LogarithmicAxis;
            var yAxis = PlotModel.Axes[1] as LogarithmicAxis;

            if (xAxis != null && yAxis != null)
            {                
                xAxis.Minimum = 0.0001;
                xAxis.Maximum = 2;

                double yMin = Points.Min(p => p.Value);
                double yMax = Points.Max(p => p.Value);
                double yRange = yMax - yMin;
                yAxis.Minimum = yMin/10;
                yAxis.Maximum = yMax*10;

                plotView.InvalidatePlot(true);
            }

            // Get the current axes from the plot model
            var linRegressXAxis = PlotLinRegression.Axes[0] as LinearAxis;
            var linRegressYAxis = PlotLinRegression.Axes[1] as LinearAxis;

            if (linRegressXAxis != null && linRegressYAxis != null)
            {
                // Adjust y-axis limits with buffer
                double min = Math.Min(Points.Min(p => p.Value), Points.Min(p => p.ExpectedValue));
                double max = Math.Max(Points.Max(p => p.Value), Points.Max(p => p.ExpectedValue));
                double range = max - min;
                linRegressXAxis.Minimum = min - range / 10;
                linRegressYAxis.Minimum = min - range / 10;
                linRegressXAxis.Maximum = max + range / 10;
                linRegressYAxis.Maximum = max + range / 10;
                linRegressionPlotView.InvalidatePlot(true);
            }
        }

        private void PasteCommandHandler(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string clipboardText = Clipboard.GetText();
                    string[] lines = clipboardText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string line in lines)
                    {
                        string[] values = line.Split('\t');

                        if (values.Length == 2 && double.TryParse(values[0], out double index) && double.TryParse(values[1], out double value))
                        {
                            Points.Add(new Point { Index = index, Value = value });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error pasting data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class Point : INotifyPropertyChanged
    {
        private double _index;
        public double Index
        {
            get { return _index; }
            set
            {
                _index = value;
                OnPropertyChanged(nameof(Index));
            }
        }

        private double _value;
        public double Value
        {
            get { return _value; }
            set
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }

        private double _empiricalProbability;
        public double EmpiricalProbability
        {
            get { return _empiricalProbability; }
            set
            {
                _empiricalProbability = value;
                OnPropertyChanged(nameof(EmpiricalProbability));
            }
        }

        private double _ffaProbability;
        public double FfaProbability
        {
            get { return _ffaProbability; }
            set
            {
                _ffaProbability = value;
                OnPropertyChanged(nameof(FfaProbability));
            }
        }

        private double _expectedValue;
        public double ExpectedValue
        {
            get { return _expectedValue; }
            set
            {
                _expectedValue = value;
                OnPropertyChanged(nameof(ExpectedValue));
            }
        }

        // Implement INotifyPropertyChanged for updating UI
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }

    public class LogPearsonFit
    {
        private List<double> dataset;
        public double scale;
        public double shape;
        public double loc;
        GammaFunctions GammaFunctions = new GammaFunctions();

        public double Equation(double scale, double P)
        {
            double approx_P = (2 * Math.Log(1 - scale) - Math.Log(1 - 2 * scale)) / (Math.Log(1 - scale) + scale);
            return approx_P - P;
        }

        public double Solver(double P, double tol = 0.000001, int maxIter = 1000)
        {
            double low = 0;
            double high = 0.5;

            for (int i = 0; i < maxIter; i++)
            {
                double mid = (low + high) / 2;

                if (Equation(mid, P) < 0) { high = mid; }
                else { low = mid; }
                if (high - low < tol){ return mid; }
            }

            return (low + high) / 2; // Return the best estimate after maxIter iterations
        }

        public void Fit(List<double> dataset)
        {

            this.dataset = dataset;
            int n = this.dataset.Count;
            double mean = this.dataset.Sum() / n;
            double variance = this.dataset.Sum(x => Math.Pow(x - mean, 2)) / (n - 1);

            List<double> logData = this.dataset.Select(x => Math.Log(x)).ToList();
            double logmean = logData.Average();

            double P = Math.Log((variance + Math.Pow(mean, 2)) / Math.Pow(mean, 2)) / (logmean - Math.Log(mean));
            Debug.WriteLine(P);
            this.scale = Solver(P);
            this.shape = (logmean - Math.Log(mean)) / (this.scale + Math.Log(1 - this.scale));
            this.loc = logmean - (this.scale * this.shape);
            Debug.WriteLine(this.scale);
            Debug.WriteLine(this.shape);
            Debug.WriteLine(this.loc);
        }

        public List<double> PearsonTypeIIICdf(List<double> xs)
        {
            List<double> cdf = new List<double>();

            foreach (double x in xs)
            {
                double cdfValue = GammaFunctions.IncompleteGamma(this.shape, (Math.Log(x) - this.loc) / this.scale) /
                                   GammaFunctions.IncompleteGamma(this.shape, this.shape * 5 + (Math.Log(x) - this.loc) / this.scale);
                cdf.Add(cdfValue);
            }

            return cdf;
        }

        public double TargetCdfFunction(double x, double targetProb)
        {
            double prob = PearsonTypeIIICdf(new List<double> { x })[0];
            return prob - targetProb;
        }

        public List<double> InverseCdf(List<double> targetAeps, int maxIter = 1000)
        {
            List<double> values = new List<double>();

            foreach (double targetAep in targetAeps)
            {
                double targetProb = 1 - targetAep;
                double tol = targetProb / 100;
                double low = 0;
                double high = 1e9; // Define the search range
                double mid = (low + high) / 2;

                for (int i = 0; i < maxIter; i++)
                {
                    mid = (low + high) / 2;
                    if (TargetCdfFunction(mid, targetProb) > 0)
                    {
                        high = mid;
                    }
                    else
                    {
                        low = mid;
                    }
                    if (high - low < tol)
                    {
                        break;
                    }
                }
                values.Add(mid);
            }
            return values;
        }
    }

    public class GammaFunctions
    {
        public double IncompleteGamma(double a, double x, int numIntervals = 5000)
        {
            double[] tValues = GenerateLinspace(0, x, numIntervals);
            double dt = tValues[1] - tValues[0];

            double[] integrandValues = new double[numIntervals];
            for (int i = 0; i < numIntervals; i++)
            {
                integrandValues[i] = Math.Pow(tValues[i], a - 1) * Math.Exp(-tValues[i]);
            }

            double integral = (1.0 / 3.0) * dt * (integrandValues[0] + integrandValues[numIntervals - 1]);
            integral += (2.0 / 3.0) * dt * SumElements(integrandValues, 2, numIntervals - 1, 2);
            integral += (4.0 / 3.0) * dt * SumElements(integrandValues, 1, numIntervals - 1, 2);

            return integral;
        }

        private double[] GenerateLinspace(double start, double end, int numIntervals)
        {
            double[] values = new double[numIntervals];
            double step = (end - start) / (numIntervals - 1);

            for (int i = 0; i < numIntervals; i++)
            {
                values[i] = start + i * step;
            }

            return values;
        }

        private double SumElements(double[] array, int start, int end, int step)
        {
            double sum = 0.0;
            for (int i = start; i <= end; i += step)
            {
                sum += array[i];
            }
            return sum;
        }
    }
}