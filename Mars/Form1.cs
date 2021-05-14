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

            chart1.Series.Clear();
            chart1.Palette = ChartColorPalette.EarthTones;

            chart1.ChartAreas[0].Name = "Price chart";
            chart1.Legends[0].DockedToChartArea = chart1.ChartAreas[0].Name;
            chart1.Legends[0].IsDockedInsideChartArea = false;

            chart1.ChartAreas[0].InnerPlotPosition.Auto = true;
            chart1.ChartAreas[0].AxisY.IsStartedFromZero = false;
            chart1.ChartAreas[0].AxisY2.Enabled = AxisEnabled.True;
            chart1.ChartAreas[0].AxisY2.IsStartedFromZero = false;
            chart1.ChartAreas[0].AxisY2.MajorGrid.Enabled = false;
            chart1.Legends[0].Docking = Docking.Top;
            chart1.ChartAreas[0].AxisX.LabelStyle.Format = "dd-MMM HH:mm:ss";
            chart1.ChartAreas[0].AxisX.LabelStyle.Angle = 45;

            chart1.ChartAreas.Add("Strategy chart");
            chart1.ChartAreas[1].AlignWithChartArea = chart1.ChartAreas[0].Name;
            chart1.Legends.Add("Strategy legend");
            chart1.Legends[1].DockedToChartArea = chart1.ChartAreas[1].Name;
            chart1.Legends[1].IsDockedInsideChartArea = false;
            chart1.ChartAreas[1].InnerPlotPosition.Auto = true;
            chart1.ChartAreas[1].AxisY.IsStartedFromZero = false;
            chart1.Legends[1].Docking = Docking.Top;
            chart1.ChartAreas[1].AxisX.LabelStyle.Format = "dd-MMM HH:mm:ss";
            chart1.ChartAreas[1].AxisX.LabelStyle.Angle = 45;
            chart1.ChartAreas[1].AxisY2.Enabled = AxisEnabled.True;
            chart1.ChartAreas[1].AxisY2.MajorGrid.Enabled = false;
            chart1.Titles.Add("Strategy PnL");
            chart1.Titles[0].DockedToChartArea = chart1.ChartAreas[1].Name;
            chart1.Titles[0].IsDockedInsideChartArea = false;

            chart1.ChartAreas.Add("Vol chart");
            chart1.ChartAreas[2].AlignWithChartArea = chart1.ChartAreas[0].Name;
            chart1.Legends.Add("Vol legend");
            chart1.Legends[2].DockedToChartArea = chart1.ChartAreas[2].Name;
            chart1.Legends[2].IsDockedInsideChartArea = false;
            chart1.ChartAreas[2].InnerPlotPosition.Auto = true;
            chart1.ChartAreas[2].AxisY.IsStartedFromZero = false;
            chart1.Legends[2].Docking = Docking.Top;
            chart1.ChartAreas[2].AxisX.LabelStyle.Format = "dd-MMM HH:mm:ss";
            chart1.ChartAreas[2].AxisX.LabelStyle.Angle = 45;
            chart1.Titles.Add("Vols (% pa)");
            chart1.Titles[1].DockedToChartArea = chart1.ChartAreas[2].Name;
            chart1.Titles[1].IsDockedInsideChartArea = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Text = "Mars : " + comboBox1.Text;

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
            strategies[0] = new Strategy(comboBox1.Text, db, double.Parse(textBox4.Text), double.Parse(textBox5.Text)/100, double.Parse(textBox6.Text));

            tt = new System.Threading.Timer(new System.Threading.TimerCallback(UpdateMarketData), null, 0, 30 * 1000);
        }

        // todo(2) - time stamping of all the market data is crap.
        private void UpdateMarketData(object state)
        {
            db.UpdateData();
            DateTime now = DateTime.Now;

            bool updated = false;
            foreach (var s in strategies)
                updated = s.UpdateStrategy();

            // Chart 1 -- token price & future price
            List<Tuple<string, DateTime, double, bool>> data = new List<Tuple<string, DateTime, double, bool>>();
            data.Add(new Tuple<string, DateTime, double, bool>("Token Price", now, Math.Round(db.TokenPrice, 2), false));
            foreach (var a in strategies[0].StrategyPortfolio.AssetSizes.Keys)
            {
                if (db.Instruments[a].Kind == Org.OpenAPITools.Model.Instrument.KindEnum.Future)
                    data.Add(new Tuple<string, DateTime, double, bool>(a, now, Math.Round(db[a].CashMid, 2), false));
                else
                    data.Add(new Tuple<string, DateTime, double, bool>(a, now, Math.Round(db[a].CashMid, 2), true));
            }
            AddManyDataPoints(chart1, 0, data, false, SeriesChartType.Line);

            // Chart 2 -- strategy portfolio value & delta
            List<Tuple<string, DateTime, double, bool>> data3 = new List<Tuple<string, DateTime, double, bool>>();
            data3.Add(new Tuple<string, DateTime, double, bool>("Portfolio Delta", now, strategies[0].StrategyPortfolio.CurrentPortfolioDelta, true));
            data3.Add(new Tuple<string, DateTime, double, bool>("Portfolio Value", now, strategies[0].StrategyPortfolio.CurrentPortfolioValue, false));
            AddManyDataPoints(chart1, 1, data3, false, SeriesChartType.Line);

            // Chart 3 -- historical & implied volatilities
            List<Tuple<string, DateTime, double, bool>> data2 = new List<Tuple<string, DateTime, double, bool>>();
            data2.Add(new Tuple<string, DateTime, double, bool>("Implied Vols", db.ImpliedVolatilityCandles.Last().Key, Math.Round(db.ImpliedVolatilityCandles.Last().Value.Close, 2), false));
            data2.Add(new Tuple<string, DateTime, double, bool>("HistoricalVols", db.HistoricalVolatilities.Last().Key, Math.Round(db.HistoricalVolatilities.Last().Value, 2), false));
            AddManyDataPoints(chart1, 2, data2, false, SeriesChartType.Line);

            if (updated)
            {
                data3.Clear();
                data3.Add(new Tuple<string, DateTime, double, bool>("Traded", now, strategies[0].StrategyPortfolio.CurrentPortfolioDelta, true));
                AddManyDataPoints(chart1, 1, data3, false, SeriesChartType.Point);
            }

            string label4Text = string.Format("Portfolio Value: {0:N2}{1}Portfolio Cash: {2:N2}{3}Portfolio Delta: {4:F4}{5}Portfolio Gamma: {6:F5}{7}PortfolioTheta: {8:F3}{9}Total Commissions: {10:N2}",
                                              strategies[0].StrategyPortfolio.CurrentPortfolioValue,
                                              Environment.NewLine,
                                              strategies[0].StrategyPortfolio.CurrentCash,
                                              Environment.NewLine,
                                              strategies[0].StrategyPortfolio.CurrentPortfolioDelta,
                                              Environment.NewLine,
                                              strategies[0].StrategyPortfolio.CurrentPortfolioGamma,
                                              Environment.NewLine,
                                              strategies[0].StrategyPortfolio.CurrentPortfolioTheta,
                                              Environment.NewLine,
                                              strategies[0].StrategyPortfolio.TotalCommissions);

            string label5Text = "Positions: " + Environment.NewLine;
            string label6Text = "Markets: " + Environment.NewLine;
            foreach (var a in strategies[0].StrategyPortfolio.AssetSizes)
            {
                double currentMid = db[a.Key].CashMid;
                double currentMtM = a.Value * (currentMid - strategies[0].StrategyPortfolio.AssetPrices[a.Key]);
                label5Text += Environment.NewLine + string.Format("{0}: {1:F3}@{2:F3} = {3:N2} ({4:N2})",
                                                                  a.Key, a.Value, strategies[0].StrategyPortfolio.AssetPrices[a.Key],
                                                                  a.Value * strategies[0].StrategyPortfolio.AssetPrices[a.Key], currentMtM);

                label6Text += Environment.NewLine + string.Format("{0}: {1:F3} / {2:F3} ({3:F3} / {4:F3})",
                                                                  a.Key, db[a.Key].CashBid, db[a.Key].CashAsk, db[a.Key].Bid, db[a.Key].Ask);
            }

            label4.Invoke(new MethodInvoker(delegate
            {
                label4.Text = label4Text;
            }));

            label5.Invoke(new MethodInvoker(delegate
            {
                label5.Text = label5Text;
            }));

            label6.Invoke(new MethodInvoker(delegate
            {
                label6.Text = label6Text;
            }));
        }


        // Add many points to a single series in one go
        private static void AddManyDataPoints(Chart chart, int chartAreaIndex, string seriesName, IEnumerable<Tuple<DateTime, double>> data, bool useMarker, bool useY2,
                                              SeriesChartType seriesChartType = SeriesChartType.FastLine)
        {
            int n = data.Count();
            List<Tuple<string, DateTime, double, bool>> newData = new List<Tuple<string, DateTime, double, bool>>(n);
            int i = 0;
            foreach (var d in data)
            {
                newData.Add(new Tuple<string, DateTime, double, bool>(seriesName, d.Item1, d.Item2, useY2));
                i++;
            }

            AddManyDataPoints(chart, chartAreaIndex, newData, useMarker, seriesChartType);
        }

        // Add many points to different series in one go
        private static void AddManyDataPoints<T>(Chart chart, int chartAreaIndex, IEnumerable<Tuple<string, T, double, bool>> data, bool useMarker,
                                                 SeriesChartType seriesChartType = SeriesChartType.FastLine)
        {
            chart.Invoke(new MethodInvoker(delegate
            {
                foreach (var d in data)
                {
                    if (chart.Series.FindByName(d.Item1) == null)
                    {
                        Series ss = chart.Series.Add(d.Item1);
                        ss.ChartArea = chart.ChartAreas[chartAreaIndex].Name;
                        ss.Legend = chart.Legends[chartAreaIndex].Name;

                        if (typeof(T) == typeof(DateTime))
                            ss.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.DateTime;

                        ss.ChartType = seriesChartType;

                        if (d.Item4)
                        {
                            ss.ToolTip = d.Item1 + ": (#VALY2, #VALX)";
                            ss.YAxisType = AxisType.Secondary;
                        }
                        else
                        {
                            ss.ToolTip = d.Item1 + ": (#VALY, #VALX)";
                        }
                    }

                    DataPoint pp = new DataPoint();

                    if (useMarker)
                        pp.MarkerStyle = MarkerStyle.Circle;

                    pp.SetValueXY(d.Item2, d.Item3, 2);
                    chart.Series[d.Item1].Points.Add(pp);
                }
            }));
        }

    }
}
