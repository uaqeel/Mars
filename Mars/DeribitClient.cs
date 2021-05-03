using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Org.OpenAPITools.Client;
using Org.OpenAPITools.Api;
using Org.OpenAPITools.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace Mars
{
    public class DeribitClient
    {
        public string Token { get; set; }
        double tokenPrice;
        public double TokenPrice { get { return tokenPrice; } }

        Configuration Config;
        PublicApi ApiClient;

        // Dictionary of all tradable instruments
        public Dictionary<string, Instrument> Instruments { get; set; }
        public Dictionary<string, double> MakerCommissions { get; set; }
        public Dictionary<string, double> TakerCommissions { get; set; }

        public Dictionary<string, JObject> Futures { get; set; }
        public Dictionary<string, JObject> Options { get; set; }
        public SortedDictionary<DateTime, double> HistoricalVolatilities { get; set; }
        public SortedDictionary<DateTime, Candle> ImpliedVolatilityCandles { get; set; }
        public Dictionary<string, Market> Markets { get; set; }

        public DeribitClient(string token, string basePath)
        {
            Token = token;

            Config = new Configuration();
            Config.BasePath = basePath;
            ApiClient = new PublicApi(Config);
            Instruments = new Dictionary<string, Instrument>();
            MakerCommissions = new Dictionary<string, double>();
            TakerCommissions = new Dictionary<string, double>();
            HistoricalVolatilities = new SortedDictionary<DateTime, double>();
            ImpliedVolatilityCandles = new SortedDictionary<DateTime, Candle>();

            Initialise();

        }

        public void Initialise()
        {
            var result = JObject.Parse(ApiClient.PublicGetInstrumentsGet(Token).ToString())["result"];
            foreach (var i in result)
            {
                Instrument ii = JsonConvert.DeserializeObject<Instrument>(i.ToString());

                Instruments.Add(ii.InstrumentName, ii);
                MakerCommissions.Add(ii.InstrumentName, double.Parse(i["maker_commission"].ToString()));
                TakerCommissions.Add(ii.InstrumentName, double.Parse(i["taker_commission"].ToString()));
            }

            var indexPrice = JObject.Parse(ApiClient.PublicGetIndexGet(Token).ToString());
            tokenPrice = (double)(indexPrice["result"][Token]);
        }

        internal void UpdateData(List<string> futures, List<string> options)
        {
            var indexPrice = JObject.Parse(ApiClient.PublicGetIndexGet(Token).ToString());
            tokenPrice = (double)(indexPrice["result"][Token]);

            var historicalVol = JObject.Parse(ApiClient.PublicGetHistoricalVolatilityGet(Token).ToString());
            foreach (var i in historicalVol["result"])
            {
                JArray ja = (JArray)i;
                HistoricalVolatilities[DateTimeOffset.FromUnixTimeMilliseconds((long)ja[0]).DateTime] = (double)ja[1];
            }

            var impliedVols = JObject.Parse(ApiClient.PublicGetVolatilityIndexDataGet(Token, HistoricalVolatilities.First().Key, HistoricalVolatilities.Last().Key.AddHours(1)).ToString());
            foreach (var i in impliedVols["result"]["data"])
            {
                JArray ja = (JArray)i;
                ImpliedVolatilityCandles[DateTimeOffset.FromUnixTimeMilliseconds((long)ja[0]).DateTime] = new Candle((double)ja[1],
                                                                                                                     (double)ja[2],
                                                                                                                     (double)ja[3],
                                                                                                                     (double)ja[4]);
            }

            Parallel.ForEach(futures, future =>
            {
                Market mm = new Market(JObject.Parse(ApiClient.PublicGetOrderBookGet(future, 1).ToString())["result"] as JObject);
                Markets.Add(future, mm);
            });

            Parallel.ForEach(options, option =>
            {
                OptionMarket mm = new OptionMarket(JObject.Parse(ApiClient.PublicGetOrderBookGet(option, 1).ToString())["result"] as JObject);
                Markets.Add(option, mm);
            });


        }
    }

    public class Candle
    {
        public Candle(double o, double h, double l, double c)
        {
            Open = o;
            High = h;
            Low = l;
            Close = c;
        }

        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
    }

    public class Market
    {
        public DateTime Timestamp { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double Mark { get; set; }
        public double IndexRefPrice { get; set; }

        public Market(long timestamp, double index_price, double mark_price, double best_bid_price, double best_ask_price)
        {
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
            Bid = best_bid_price;
            Ask = best_ask_price;
            Mark = mark_price;
            IndexRefPrice = index_price;
        }

        public Market(JObject JsonObject)
        {
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)JsonObject["timestamp"]).DateTime;
            Bid = (double)JsonObject["best_bid_price"];
            Ask = (double)JsonObject["best_ask_price"];
            Mark = (double)JsonObject["mark_price"];
            IndexRefPrice = (double)JsonObject["index_price"];
        }
    }

    public class OptionMarket : Market
    {
        public double UnderlyingRefPrice { get; set; }
        public double Delta { get; set; }
        public double Gamma { get; set; }
        public double Theta { get; set; }
        public double MarkVol { get; set; }

        public OptionMarket(long timestamp, double index_price, double mark_price, double underlying_price, double mark_iv, double best_bid_price, double bid_iv,
                            double best_ask_price, double ask_iv, double interest_rate, double delta, double gamma, double rho, double vega)
                            : base(timestamp, index_price, mark_price, best_bid_price, best_ask_price)
        {
            UnderlyingRefPrice = underlying_price;
            Delta = delta;
            Gamma = gamma;
            Theta = Theta;
            MarkVol = mark_iv;
        }

        public OptionMarket(JObject JsonObject) : base(JsonObject)
        {
            UnderlyingRefPrice = (double)JsonObject["greeks"]["underlying_price"];
            Delta = (double)JsonObject["greeks"]["delta"];
            Gamma = (double)JsonObject["greeks"]["gamma"];
            Theta = (double)JsonObject["greeks"]["theta"];
            MarkVol = (double)JsonObject["mark_iv"];
        }
    }
}
