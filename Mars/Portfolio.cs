using System;
using System.Collections.Generic;

namespace Mars
{
    internal class Portfolio
    {
        DeribitClient MarketDataClient;

        double InitialPortfolioValue { get; set; }

        Dictionary<string, double> AssetSizes { get; set; }        // values = sizes
        Dictionary<string, double> AssetPrices { get; set; }        // values = prices
        double CurrentCash { get; set; }

        public Portfolio(DeribitClient client, double initialPortfolioValue)
        {
            MarketDataClient = client;

            InitialPortfolioValue = initialPortfolioValue;

            AssetSizes = new Dictionary<string, double>();
            AssetPrices = new Dictionary<string, double>();
            CurrentCash = initialPortfolioValue;
        }

        // todo - think about maker
        // todo - cash calculations are all suspect
        public void UpdatePortfolioPosition(string instrumentName, double tradeSize, double tradePrice, double commissionInPercentage)
        {
            if (!AssetSizes.ContainsKey(instrumentName))
            {
                AssetSizes[instrumentName] = tradeSize;
                AssetPrices[instrumentName] = tradePrice;

                // todo - haven't factored in cap on commission here
                CurrentCash -= tradeSize * tradePrice;
                CurrentCash -= Math.Abs(tradeSize) * tradePrice * commissionInPercentage;

                // todo - haven't factored in delivery fee
            }
            else
            {
                double existingSize = AssetSizes[instrumentName];
                double existingPrice = AssetPrices[instrumentName];

                if (existingSize + tradeSize == 0)
                {
                    CurrentCash -= Math.Abs(tradeSize) * tradePrice * commissionInPercentage;
                    CurrentCash += tradeSize * (tradePrice - AssetPrices[instrumentName]);

                    AssetSizes.Remove(instrumentName);
                    AssetPrices.Remove(instrumentName);
                }
                else if (Math.Sign(existingSize) != Math.Sign (existingSize + tradeSize))
                {
                    CurrentCash -= Math.Abs(tradeSize) * tradePrice * commissionInPercentage;
                    CurrentCash += existingSize * (tradePrice - AssetPrices[instrumentName]);

                    AssetSizes[instrumentName] = existingSize + tradeSize;
                    AssetPrices[instrumentName] = tradePrice;
                }
                else
                {
                    CurrentCash-= Math.Abs(tradeSize) * tradePrice * commissionInPercentage;

                    AssetSizes[instrumentName] = existingSize + tradeSize;
                    AssetPrices[instrumentName] = (existingSize * AssetPrices[instrumentName] + tradeSize * tradePrice) / (existingSize + tradeSize);
                }
            }

        }

        public double MarkToMarket(Dictionary<string, Market> markets)
        {
            double mtm = 0;
            foreach (var i in AssetSizes)
            {
                double mid = 0.5 * (markets[i.Key].Ask + markets[i.Key].Bid);
                mtm += i.Value * (mid - AssetPrices[i.Key]);
            }

            return mtm;
        }

        // todo
        double CurrentPortfolioValue
        {
            get
            {
                return 0;
            }
        }

        double CurrentPortfolioDelta
        {
            get
            {
                return 0;
            }
        }

        double CurrentPortfolioGamma
        {
            get
            {
                return 0;
            }
        }

        double CurrentPortfolioTheta
        {
            get
            {
                return 0;
            }
        }
    }
}