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

            SelectOptionSet();
        }

        // Find the options that are closest to ATM and tradable and set up the initial position.
        // For now, just taker commissions + just longest maturity of options.
        public void SelectOptionSet()
        {
            var tokenOptions = from o in MarketDataClient.Instruments.Values
                               where o.BaseCurrency == tokenEnum && o.Kind == Instrument.KindEnum.Option
                               select o;

            var longestMaturity = (from o in tokenOptions
                                   orderby o.ExpirationTimestamp
                                   select o.ExpirationTimestamp).Last();

            var closestStrikes = (from o in tokenOptions
                                  where o.ExpirationTimestamp == longestMaturity
                                  orderby  Math.Abs((decimal)MarketDataClient.TokenPrice - o.Strike ?? 0)
                                  select o.Strike ?? 0).Take(10);

            tokenOptions = from o in tokenOptions
                           where o.ExpirationTimestamp == longestMaturity && closestStrikes.Contains(o.Strike ?? 0)
                           select o;
        }
    }
}