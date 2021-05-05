using System;
using System.Collections.Generic;

namespace Mars
{
    internal class Portfolio
    {
        double InitialPortfolioValue { get; set; }
        double CurrentPortfolioValue { get; set; }

        Dictionary<string, double> AssetSizes { get; set; }        // values = sizes
        Dictionary<string, double> AssetPrices { get; set; }        // values = prices

        public Portfolio(double initialPortfolioValue)
        {
            InitialPortfolioValue = initialPortfolioValue;

            AssetSizes = new Dictionary<string, double>();
            AssetPrices = new Dictionary<string, double>();
        }

        // todo - think about maker
        public void UpdatePortfolioPosition(string instrumentName, double tradeSize, double tradePrice, double commissionInPercentage)
        {
            if (!AssetSizes.ContainsKey(instrumentName))
            {
                AssetSizes[instrumentName] = tradeSize;
                AssetPrices[instrumentName] = tradePrice;

                // todo - haven't factored in cap on commission here
                CurrentPortfolioValue -= Math.Abs(tradeSize) * tradePrice * commissionInPercentage;

                // todo - haven't factored in delivery fee
            }
            else
            {
                double existingSize = AssetSizes[instrumentName];
                double existingPrice = AssetPrices[instrumentName];

                if (existingSize + tradeSize == 0)
                {
                    CurrentPortfolioValue -= Math.Abs(tradeSize) * tradePrice * commissionInPercentage;
                    CurrentPortfolioValue += tradeSize * (tradePrice - AssetPrices[instrumentName]);

                    AssetSizes.Remove(instrumentName);
                    AssetPrices.Remove(instrumentName);
                }
                else if (Math.Sign(existingSize) != Math.Sign (existingSize + tradeSize))
                {
                    CurrentPortfolioValue -= Math.Abs(tradeSize) * tradePrice * commissionInPercentage;
                    CurrentPortfolioValue += existingSize * (tradePrice - AssetPrices[instrumentName]);

                    AssetSizes[instrumentName] = existingSize + tradeSize;
                    AssetPrices[instrumentName] = tradePrice;
                }
                else
                {
                    CurrentPortfolioValue -= Math.Abs(tradeSize) * tradePrice * commissionInPercentage;

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
    }
}