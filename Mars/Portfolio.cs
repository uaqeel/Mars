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

        public double CurrentPortfolioValue
        {
            get
            {
                double value = CurrentCash;
                foreach (var a in AssetSizes)
                {
                    value += a.Value * (MarketDataClient[a.Key] as OptionMarket).Mid;
                }

                return value;
            }
        }

        public double CurrentPortfolioDelta
        {
            get
            {
                double delta = 0;
                foreach (var a in AssetSizes)
                {
                    delta += a.Value * (MarketDataClient[a.Key] as OptionMarket).Delta;
                }

                return delta;
            }
        }

        double CurrentPortfolioGamma
        {
            get
            {
                double gamma = 0;
                foreach (var a in AssetSizes)
                {
                    gamma += a.Value * (MarketDataClient[a.Key] as OptionMarket).Gamma;
                }

                return gamma;
            }
        }

        double CurrentPortfolioTheta
        {
            get
            {
                double theta = 0;
                foreach (var a in AssetSizes)
                {
                    theta += a.Value * (MarketDataClient[a.Key] as OptionMarket).Theta;
                }

                return theta;
            }
        }
    }
}