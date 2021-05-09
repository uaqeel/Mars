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

        public Market this[string i]
        {
            get => Markets[i];
        }

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
            Markets = new Dictionary<string, Market>();

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

        internal void AddContracts(List<Instrument> contracts)
        {
            foreach (var i in contracts)
            {
                bool isOption = i.Kind == Instrument.KindEnum.Option;
                if (!Markets.ContainsKey(i.InstrumentName))
                {
                    if (isOption)
                        Markets[i.InstrumentName] = new OptionMarket(JObject.Parse(ApiClient.PublicGetOrderBookGet(i.InstrumentName, 1).ToString())["result"] as JObject);
                    else
                        Markets[i.InstrumentName] = new Market(JObject.Parse(ApiClient.PublicGetOrderBookGet(i.InstrumentName, 1).ToString())["result"] as JObject);
                }
            }
        }

        // todo - async this thing
        internal void UpdateData()
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

            Dictionary<string, object> retValues = new Dictionary<string, object>();
            Parallel.ForEach(Markets, market =>
            {
                retValues.Add(market.Key, ApiClient.PublicGetOrderBookGet(market.Key, 1));
            });

            foreach (var m in retValues)
            {
                bool isOption = Instruments[m.Key].Kind == Instrument.KindEnum.Option;

                if (isOption)
                    Markets[m.Key] = new OptionMarket(JObject.Parse(retValues[m.Key].ToString())["result"] as JObject);
                else
                    Markets[m.Key] = new Market(JObject.Parse(retValues[m.Key].ToString())["result"] as JObject);
            }
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

        public double Mid
        {
            get
            {
                return 0.5 * (Bid + Ask);
            }
        }

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
            UnderlyingRefPrice = (double)JsonObject["underlying_price"];
            Delta = (double)JsonObject["greeks"]["delta"];
            Gamma = (double)JsonObject["greeks"]["gamma"];
            Theta = (double)JsonObject["greeks"]["theta"];
            MarkVol = (double)JsonObject["mark_iv"];
        }
    }
}
