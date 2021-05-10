using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace Mars
{
    public partial class Form1 : Form
    {
        DeribitClient db;
        Strategy[] strategies;
        System.Threading.Timer tt;

        public Form1()
        {
            InitializeComponent();
            comboBox1.SelectedIndex = 0;

            //chart1.ChartAreas[0].InnerPlotPosition.Auto = true;
            chart1.ChartAreas[0].AxisY.IsStartedFromZero = false;
            chart1.Legends[0].Docking = Docking.Top;
            chart1.ChartAreas[0].AxisX.LabelStyle.Format = "dd-MMM HH:mm:ss";
            chart1.ChartAreas[0].AxisX.LabelStyle.Angle = 45;
  
            //chart2.ChartAreas[0].InnerPlotPosition.Auto = true;
            chart2.ChartAreas[0].AxisY.IsStartedFromZero = false;
            chart2.Legends[0].Docking = Docking.Top;
            chart2.ChartAreas[0].AxisX.LabelStyle.Format = "dd-MMM HH:mm:ss";
            chart2.ChartAreas[0].AxisX.LabelStyle.Angle = 45;
            chart2.Titles.Add("Vols (% pa)");

            //chart3.ChartAreas[0].InnerPlotPosition.Auto = true;
            chart3.ChartAreas[0].AxisY.IsStartedFromZero = false;
            chart3.Legends[0].Enabled = false;
            chart3.ChartAreas[0].AxisX.LabelStyle.Angle = 45;
            chart3.Titles.Add("Strategy PnL");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            db = new DeribitClient(comboBox1.Text, textBox3.Text);

            listBox1.DataSource = db.Instruments.Where(x => x.Value.Kind == Org.OpenAPITools.Model.Instrument.KindEnum.Future)
                                                .OrderBy(x => x.Value.ExpirationTimestamp)
                                                .Select(x => x.Value.InstrumentName)
                                                .ToList();

            listBox2.DataSource = db.Instruments.Where(x => x.Value.Kind == Org.OpenAPITools.Model.Instrument.KindEnum.Option && x.Value.ExpirationTimestamp.HasValue)
                                                .Select(x => DateTimeOffset.FromUnixTimeMilliseconds(x.Value.ExpirationTimestamp ?? 0).ToString("dd-MMM-yy"))
                                                .Distinct().OrderBy(x => x).ToList();

            listBox3.DataSource = db.Instruments.Where(x => x.Value.Kind == Org.OpenAPITools.Model.Instrument.KindEnum.Option)
                                                .OrderBy(x => x.Value.ExpirationTimestamp)
                                                .Select(x => x.Value.InstrumentName)
                                                .ToList();

            strategies = new Strategy[1];
            strategies[0] = new Strategy(comboBox1.Text, db, double.Parse(textBox4.Text), double.Parse(textBox5.Text)/100, 0.1);

            tt = new System.Threading.Timer(new System.Threading.TimerCallback(UpdateMarketData), null, 0, 30 * 1000);
        }

        // todo - time stamping of all the market data is crap.
        private void UpdateMarketData(object state)
        {
            db.UpdateData();
            DateTime now = DateTime.Now;

            // todo - run rebalancing

            // Chart 1 -- token price & future price
            List<Tuple<string, DateTime, double>> data = new List<Tuple<string, DateTime, double>>();
            data.Add(new Tuple<string, DateTime, double>("Token Price", now, db.TokenPrice));
            data.Add(new Tuple<string, DateTime, double>("Future Price", now, 0));      // todo
            AddManyDataPoints(chart1, data, false, SeriesChartType.Line);

            // Chart 2 -- historical & implied volatilities
            List<Tuple<string, DateTime, double>> data2 = new List<Tuple<string, DateTime, double>>();
            data2.Add(new Tuple<string, DateTime, double>("Implied Vols", db.ImpliedVolatilityCandles.Last().Key, db.ImpliedVolatilityCandles.Last().Value.Close));
            data2.Add(new Tuple<string, DateTime, double>("HistoricalVols", db.HistoricalVolatilities.Last().Key, db.HistoricalVolatilities.Last().Value));
            AddManyDataPoints(chart2, data2, false, SeriesChartType.Line);

            // Chart 3 -- strategy portfolio value & delta
            List<Tuple<string, DateTime, double>> data3 = new List<Tuple<string, DateTime, double>>();
            data3.Add(new Tuple<string, DateTime, double>("Portfolio Value", now, strategies[0].StrategyPortfolio.CurrentPortfolioValue));
            data3.Add(new Tuple<string, DateTime, double>("Portfolio Delta", now, strategies[0].StrategyPortfolio.CurrentPortfolioDelta));
            AddManyDataPoints(chart3, data3, false, SeriesChartType.Line);
        }


        // Add many points to a single series in one go
        private static void AddManyDataPoints(Chart chart, string seriesName, IEnumerable<Tuple<DateTime, double>> data, bool useMarker, SeriesChartType seriesChartType = SeriesChartType.FastLine)
        {
            int n = data.Count();
            List<Tuple<string, DateTime, double>> newData = new List<Tuple<string, DateTime, double>>(n);
            int i = 0;
            foreach (var d in data)
            {
                newData.Add(new Tuple<string, DateTime, double>(seriesName, d.Item1, d.Item2));
                i++;
            }

            AddManyDataPoints(chart, newData, useMarker, seriesChartType);
        }

        // Add many points to different series in one go
        private static void AddManyDataPoints<T>(Chart chart, IEnumerable<Tuple<string, T, double>> data, bool useMarker, SeriesChartType seriesChartType = SeriesChartType.FastLine)
        {
            chart.Invoke(new MethodInvoker(delegate
            {
                foreach (var d in data)
                {
                    if (chart.Series.FindByName(d.Item1) == null)
                    {
                        Series ss = chart.Series.Add(d.Item1);
                        ss.ChartArea = chart.ChartAreas[0].Name;

                        if (typeof(T) == typeof(DateTime))
                            ss.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Time;

                        ss.ToolTip = d.Item1 + ": (#VALY2, #VALX)";

                        ss.ChartType = seriesChartType;
                    }

                    DataPoint pp = new DataPoint();

                    if (useMarker)
                        pp.MarkerStyle = MarkerStyle.Circle;

                    pp.SetValueXY(d.Item2, Math.Round(d.Item3, 2));
                    chart.Series[d.Item1].Points.Add(pp);
                }
            }));
        }

    }
}
