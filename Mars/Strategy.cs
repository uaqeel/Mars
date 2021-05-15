using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Org.OpenAPITools.Client;
using Org.OpenAPITools.Api;
using Org.OpenAPITools.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mars
{
    internal class Strategy
    {
        public string Token { get; set; }
        Instrument.BaseCurrencyEnum tokenEnum;

        DeribitClient MarketDataClient { get; set; }
        
        public Portfolio StrategyPortfolio {get; set; }
        double MaxInvestedPercentage { get; set; }
        double DeltaLimit { get; set; }

        private Instrument tradedFuture;
        private Instrument tradedPut;
        private Instrument tradedCall;


        public Strategy(string token, DeribitClient dbClient, double initialPortfolioValue, double maxInvestedPercentage = 0.50, double deltaLimit = 0.5)
        {
            Token = token;
            tokenEnum = (Instrument.BaseCurrencyEnum) Enum.Parse(typeof(Instrument.BaseCurrencyEnum), Token);
            MarketDataClient = dbClient;
            StrategyPortfolio = new Portfolio(dbClient, initialPortfolioValue);
            MaxInvestedPercentage = maxInvestedPercentage;
            DeltaLimit = deltaLimit;

            // Decide which options to track and buy them.
            Tuple<Instrument, Instrument, Instrument> contracts = SelectContracts();
            double putAsk = (MarketDataClient[contracts.Item2.InstrumentName] as OptionMarket).CashAsk;
            double putDelta = (MarketDataClient[contracts.Item2.InstrumentName] as OptionMarket).Delta;
            double putGamma = (MarketDataClient[contracts.Item2.InstrumentName] as OptionMarket).Gamma;
            double callAsk = (MarketDataClient[contracts.Item3.InstrumentName] as OptionMarket).CashAsk;
            double callDelta = (MarketDataClient[contracts.Item3.InstrumentName] as OptionMarket).Delta;
            double callGamma = (MarketDataClient[contracts.Item3.InstrumentName] as OptionMarket).Gamma;
            double putCallRatio = callDelta / -putDelta;

            // With these sizes, strategy will be delta-neutral to start.
            double numUnits = (initialPortfolioValue * maxInvestedPercentage) / (putCallRatio * putAsk + callAsk);
            double putSize = Math.Round(putCallRatio * numUnits, 2);
            double callSize = Math.Round(numUnits, 2);

            StrategyPortfolio.UpdatePortfolioPosition(contracts.Item2.InstrumentName, putSize, putAsk, MarketDataClient.TakerCommissions[contracts.Item2.InstrumentName]);
            StrategyPortfolio.UpdatePortfolioPosition(contracts.Item3.InstrumentName, callSize, callAsk, MarketDataClient.TakerCommissions[contracts.Item3.InstrumentName]);

            tradedFuture = contracts.Item1;
            tradedPut = contracts.Item2;
            tradedCall = contracts.Item3;

            Trace.Listeners.Add(new TextWriterTraceListener("c:\\temp\\mars_output" + token + ".txt"));
            Trace.AutoFlush = true;
            Trace.WriteLine("***************************");
            Trace.WriteLine("Timestamp,PortfolioValue,PortfolioCash,TotalCommissions,PortfolioDelta,Future Size,Future Price,Put Size,Put Price,Call Size,Call Price,Future,Put,Call");
        }

        public bool UpdateStrategy()
        {
            bool ret = false;

            double delta = StrategyPortfolio.CurrentPortfolioDelta;

            Trace.WriteLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13}",
                                        DateTime.Now.ToString(),
                                        StrategyPortfolio.CurrentPortfolioValue,
                                        StrategyPortfolio.CurrentCash,
                                        StrategyPortfolio.TotalCommissions,
                                        StrategyPortfolio.CurrentPortfolioDelta,
                                        StrategyPortfolio.AssetSizes.ContainsKey(tradedFuture.InstrumentName) ? StrategyPortfolio.AssetSizes[tradedFuture.InstrumentName] : 0,
                                        StrategyPortfolio.AssetPrices.ContainsKey(tradedFuture.InstrumentName) ? StrategyPortfolio.AssetPrices[tradedFuture.InstrumentName] : 0,
                                        StrategyPortfolio.AssetSizes.ContainsKey(tradedPut.InstrumentName) ? StrategyPortfolio.AssetSizes[tradedPut.InstrumentName] : 0,
                                        StrategyPortfolio.AssetPrices.ContainsKey(tradedPut.InstrumentName) ? StrategyPortfolio.AssetPrices[tradedPut.InstrumentName] : 0,
                                        StrategyPortfolio.AssetSizes.ContainsKey(tradedCall.InstrumentName) ? StrategyPortfolio.AssetSizes[tradedCall.InstrumentName] : 0,
                                        StrategyPortfolio.AssetPrices.ContainsKey(tradedCall.InstrumentName) ? StrategyPortfolio.AssetPrices[tradedCall.InstrumentName] : 0,
                                        MarketDataClient[tradedFuture.InstrumentName].CashMid,
                                        MarketDataClient[tradedPut.InstrumentName].CashMid,
                                        MarketDataClient[tradedCall.InstrumentName].CashMid));

            if (Math.Abs(delta) > DeltaLimit)
            {
                double quantityToTrade = Math.Sign(delta) * (0.5 * DeltaLimit - Math.Abs(delta));         // todo - should i do this scaled to portfolio size?
                double priceToTrade = Math.Sign(quantityToTrade) > 0 ? MarketDataClient[tradedFuture.InstrumentName].Ask : MarketDataClient[tradedFuture.InstrumentName].Bid;

                StrategyPortfolio.UpdatePortfolioPosition(tradedFuture.InstrumentName, quantityToTrade, priceToTrade, MarketDataClient.TakerCommissions[tradedFuture.InstrumentName]);
                ret = true;
            }

            Trace.WriteLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13}",
                                        DateTime.Now.ToString(),
                                        StrategyPortfolio.CurrentPortfolioValue,
                                        StrategyPortfolio.CurrentCash,
                                        StrategyPortfolio.TotalCommissions,
                                        StrategyPortfolio.CurrentPortfolioDelta,
                                        StrategyPortfolio.AssetSizes.ContainsKey(tradedFuture.InstrumentName) ? StrategyPortfolio.AssetSizes[tradedFuture.InstrumentName] : 0,
                                        StrategyPortfolio.AssetPrices.ContainsKey(tradedFuture.InstrumentName) ? StrategyPortfolio.AssetPrices[tradedFuture.InstrumentName] : 0,
                                        StrategyPortfolio.AssetSizes.ContainsKey(tradedPut.InstrumentName) ? StrategyPortfolio.AssetSizes[tradedPut.InstrumentName] : 0,
                                        StrategyPortfolio.AssetPrices.ContainsKey(tradedPut.InstrumentName) ? StrategyPortfolio.AssetPrices[tradedPut.InstrumentName] : 0,
                                        StrategyPortfolio.AssetSizes.ContainsKey(tradedCall.InstrumentName) ? StrategyPortfolio.AssetSizes[tradedCall.InstrumentName] : 0,
                                        StrategyPortfolio.AssetPrices.ContainsKey(tradedCall.InstrumentName) ? StrategyPortfolio.AssetPrices[tradedCall.InstrumentName] : 0,
                                        MarketDataClient[tradedFuture.InstrumentName].CashMid,
                                        MarketDataClient[tradedPut.InstrumentName].CashMid,
                                        MarketDataClient[tradedCall.InstrumentName].CashMid));

            Trace.Flush();

            return ret;
        }

        // Find the options that are closest to ATM and tradable and set up the initial position.
        // For now, just taker commissions + just longest maturity of options.
        public Tuple<Instrument, Instrument, Instrument> SelectContracts()
        {
            var tokenFutures = from o in MarketDataClient.Instruments.Values
                               where o.BaseCurrency == tokenEnum && o.Kind == Instrument.KindEnum.Future
                               select o;

            var tokenOptions = from o in MarketDataClient.Instruments.Values
                               where o.BaseCurrency == tokenEnum && o.Kind == Instrument.KindEnum.Option
                               select o;

            var selectedMaturity = (from o in tokenOptions
                                    orderby Math.Abs((DateTimeOffset.FromUnixTimeMilliseconds(o.ExpirationTimestamp ?? 0) - DateTime.Now.AddDays(40)).TotalSeconds)
                                    select o.ExpirationTimestamp ?? 0).First();

            var closestStrikes = (from o in tokenOptions
                                  where o.ExpirationTimestamp == selectedMaturity
                                  orderby Math.Abs((decimal)MarketDataClient.TokenPrice - o.Strike ?? 0)
                                  select o).Take(20);

            MarketDataClient.AddContracts(closestStrikes.ToList());

            // todo - required? can't do this until market data has been retrieved
            closestStrikes = from o in closestStrikes
                             orderby (MarketDataClient[o.InstrumentName] as OptionMarket).Gamma descending
                             select o;

            // In reality we just need the first two options in closestStrikes -- a call and a put -- that have ask prices
            Instrument selectedPut = null, selectedCall = null;
            foreach (var i in closestStrikes)
            {
                if (selectedCall == null && i.OptionType == Instrument.OptionTypeEnum.Call && MarketDataClient[i.InstrumentName].Ask != 0)
                {
                    selectedCall = i;
                }

                if (selectedPut == null && i.OptionType == Instrument.OptionTypeEnum.Put && MarketDataClient[i.InstrumentName].Ask != 0)
                {
                    selectedPut = i;
                }

                if (selectedPut != null && selectedCall != null)
                    break;
            }

            Instrument selectedFuture = (from f in tokenFutures
                                         orderby Math.Abs((DateTimeOffset.FromUnixTimeMilliseconds(f.ExpirationTimestamp ?? 0) - DateTime.Now.AddDays(30)).TotalSeconds)
                                         select f).First();

            List<Instrument> futs = new List<Instrument>();
            futs.Add(selectedFuture);
            MarketDataClient.AddContracts(futs);

            return new Tuple<Instrument, Instrument, Instrument>(selectedFuture, selectedPut, selectedCall);
        }
    }
}