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
            StrategyPortfolio = new Portfolio(initialPortfolioValue);
            MaxInvestedPercentage = maxInvestedPercentage;
            DeltaLimit = deltaLimit;

            // Decide which options to track and buy them.
            Tuple<Instrument, Instrument> options = SelectOptions();
            double putAsk = MarketDataClient[options.Item1.InstrumentName].Ask;
            double putDelta = (MarketDataClient[options.Item1.InstrumentName] as OptionMarket).Delta;
            double callAsk = MarketDataClient[options.Item2.InstrumentName].Ask;
            double callDelta = (MarketDataClient[options.Item2.InstrumentName] as OptionMarket).Delta;
            double putCallRatio = callDelta / putDelta;

            double putSize = initialPortfolioValue * maxInvestedPercentage / (putCallRatio * putAsk + callAsk);
            double callSize = putSize / putCallRatio;

            //StrategyPortfolio.UpdatePortfolioPosition(options.Item1.InstrumentName, )
        }

        // Find the options that are closest to ATM and tradable and set up the initial position.
        // For now, just taker commissions + just longest maturity of options.
        public Tuple<Instrument, Instrument> SelectOptions()
        {
            var tokenOptions = from o in MarketDataClient.Instruments.Values
                               where o.BaseCurrency == tokenEnum && o.Kind == Instrument.KindEnum.Option
                               select o;

            var longestMaturity = (from o in tokenOptions
                                   orderby o.ExpirationTimestamp
                                   select o.ExpirationTimestamp).Last();

            var closestStrikes = (from o in tokenOptions
                                  where o.ExpirationTimestamp == longestMaturity
                                  orderby Math.Abs((decimal)MarketDataClient.TokenPrice - o.Strike ?? 0)
                                  select o).Take(20);

            MarketDataClient.AddContracts(closestStrikes.ToList());

            // In reality we just need the first two options in closestStrikes -- a call and a put
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