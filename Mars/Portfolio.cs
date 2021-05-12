using System;
using System.Collections.Generic;

namespace Mars
{
    internal class Portfolio
    {
        DeribitClient MarketDataClient;

        double InitialPortfolioValue { get; set; }

        public Dictionary<string, double> AssetSizes { get; set; }        // values = sizes
        public Dictionary<string, double> AssetPrices { get; set; }        // values = prices
        public double CurrentCash { get; set; }

        public double TotalCommissions { get; set; }

        public Portfolio(DeribitClient client, double initialPortfolioValue)
        {
            MarketDataClient = client;

            InitialPortfolioValue = initialPortfolioValue;

            AssetSizes = new Dictionary<string, double>();
            AssetPrices = new Dictionary<string, double>();
            CurrentCash = initialPortfolioValue;
        }

        // todo(2) - think about maker
        // todo - cash calculations are all suspect
        public void UpdatePortfolioPosition(string instrumentName, double tradeSize, double tradePrice, double commissionInPercentage)
        {
            // todo(2) - haven't factored in cap on commission here
            double commissions = Math.Abs(tradeSize) * tradePrice * commissionInPercentage;
            CurrentCash -= commissions;
            TotalCommissions += commissions;

            if (!AssetSizes.ContainsKey(instrumentName))
            {
                AssetSizes[instrumentName] = tradeSize;
                AssetPrices[instrumentName] = tradePrice;

                CurrentCash -= tradeSize * tradePrice;

                // todo(2) - haven't factored in delivery fee
            }
            else
            {
                double existingSize = AssetSizes[instrumentName];
                double existingPrice = AssetPrices[instrumentName];

                if (existingSize + tradeSize == 0)
                {
                    CurrentCash += tradeSize * (tradePrice - AssetPrices[instrumentName]);

                    AssetSizes.Remove(instrumentName);
                    AssetPrices.Remove(instrumentName);
                }
                else if (Math.Sign(existingSize) != Math.Sign (tradeSize))
                {
                    CurrentCash += tradeSize * (tradePrice - AssetPrices[instrumentName]);

                    AssetSizes[instrumentName] = existingSize + tradeSize;
                    AssetPrices[instrumentName] = tradePrice;
                }
                else
                {
                    AssetSizes[instrumentName] = existingSize + tradeSize;
                    AssetPrices[instrumentName] = (existingSize * AssetPrices[instrumentName] + tradeSize * tradePrice) / (existingSize + tradeSize);
                }
            }

        }

        // todo - check these
        public double CurrentPortfolioValue
        {
            get
            {
                double value = CurrentCash;
                foreach (var a in AssetSizes)
                {
                    if (MarketDataClient.Instruments[a.Key].Kind == Org.OpenAPITools.Model.Instrument.KindEnum.Option)
                        value += a.Value * (MarketDataClient[a.Key] as OptionMarket).CashMid;
                    else
                        value += a.Value * ((MarketDataClient[a.Key] as Market).Mid - AssetPrices[a.Key]);
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
                    if (MarketDataClient.Instruments[a.Key].Kind == Org.OpenAPITools.Model.Instrument.KindEnum.Option)
                        delta += a.Value * (MarketDataClient[a.Key] as OptionMarket).Delta;
                    else
                        delta += a.Value;
                }

                return delta;
            }
        }

        public double CurrentPortfolioGamma
        {
            get
            {
                double gamma = 0;
                foreach (var a in AssetSizes)
                {
                    if (MarketDataClient.Instruments[a.Key].Kind == Org.OpenAPITools.Model.Instrument.KindEnum.Option)
                        gamma += a.Value * (MarketDataClient[a.Key] as OptionMarket).Gamma;
                }

                return gamma;
            }
        }

        public double CurrentPortfolioTheta
        {
            get
            {
                double theta = 0;
                foreach (var a in AssetSizes)
                {
                    if (MarketDataClient.Instruments[a.Key].Kind == Org.OpenAPITools.Model.Instrument.KindEnum.Option)
                        theta += a.Value * (MarketDataClient[a.Key] as OptionMarket).Theta;
                }

                return theta;
            }
        }
    }
}