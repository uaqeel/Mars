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
    internal class Strategy
    {
        string Token { get; set; }
        Instrument.BaseCurrencyEnum tokenEnum;

        DeribitClient MarketDataClient { get; set; }
        Portfolio StrategyPortfolio {get; set; }
        double MaxInvestedPercentage { get; set; }
        double DeltaLimit { get; set; }


        public Strategy(string token, DeribitClient dbClient, double initialPortfolioValue, double maxInvestedPercentage = 0.50, double deltaLimit = 0.5)
        {
            Token = token;
            tokenEnum = (Instrument.BaseCurrencyEnum) Enum.Parse(typeof(Instrument.BaseCurrencyEnum), Token);
            MarketDataClient = dbClient;
            StrategyPortfolio = new Portfolio(dbClient, initialPortfolioValue);
            MaxInvestedPercentage = maxInvestedPercentage;
            DeltaLimit = deltaLimit;

            // Decide which options to track and buy them.
            Tuple<Instrument, Instrument> options = SelectOptions();
            double putAsk = MarketDataClient[options.Item1.InstrumentName].Ask;
            double putDelta = (MarketDataClient[options.Item1.InstrumentName] as OptionMarket).Delta;
            double putGamma = (MarketDataClient[options.Item1.InstrumentName] as OptionMarket).Gamma;
            double callAsk = MarketDataClient[options.Item2.InstrumentName].Ask;
            double callDelta = (MarketDataClient[options.Item2.InstrumentName] as OptionMarket).Delta;
            double callGamma = (MarketDataClient[options.Item2.InstrumentName] as OptionMarket).Gamma;
            double putCallRatio = callDelta / -putDelta;

            // With these sizes, strategy will be delta-neutral to start.
            double putSize = (initialPortfolioValue * maxInvestedPercentage) / (putCallRatio * putAsk + callAsk);
            double callSize = putSize / putCallRatio;

            StrategyPortfolio.UpdatePortfolioPosition(options.Item1.InstrumentName, putSize, putAsk, MarketDataClient.TakerCommissions[options.Item1.InstrumentName]);
            StrategyPortfolio.UpdatePortfolioPosition(options.Item2.InstrumentName, callSize, callAsk, MarketDataClient.TakerCommissions[options.Item2.InstrumentName]);
        }

        // Find the options that are closest to ATM and tradable and set up the initial position.
        // For now, just taker commissions + just longest maturity of options.
        public Tuple<Instrument, Instrument> SelectOptions()
        {
            var tokenOptions = from o in MarketDataClient.Instruments.Values
                               where o.BaseCurrency == tokenEnum && o.Kind == Instrument.KindEnum.Option
                               select o;

            var longestMaturity = (from o in tokenOptions
                                   orderby Math.Abs((DateTimeOffset.FromUnixTimeMilliseconds(o.ExpirationTimestamp ?? 0) - DateTime.Now.AddDays(120)).TotalSeconds)
                                   select o.ExpirationTimestamp ?? 0).First();

            var closestStrikes = (from o in tokenOptions
                                  where o.ExpirationTimestamp == longestMaturity
                                  orderby Math.Abs((decimal)MarketDataClient.TokenPrice - o.Strike ?? 0)
                                  select o).Take(20);

            MarketDataClient.AddContracts(closestStrikes.ToList());

            // todo - required?
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

            return new Tuple<Instrument, Instrument>(selectedPut, selectedCall);
        }
    }
}