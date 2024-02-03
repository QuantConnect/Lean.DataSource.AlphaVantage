/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Linq;
using NUnit.Framework;
using QuantConnect.Util;
using QuantConnect.Securities;
using QuantConnect.Data.Market;
using System.Collections.Generic;

namespace QuantConnect.AlphaVantage.Tests
{
    [TestFixture]
    public class AlphaVantageDataDownloaderTests
    {
        private AlphaVantageDataDownloader _downloader;
        private MarketHoursDatabase _marketHoursDatabase;

        [SetUp]
        public void SetUp()
        {
            _downloader = new();
            _marketHoursDatabase = MarketHoursDatabase.FromDataFolder();
        }

        [TearDown]
        public void TearDown()
        {
            _downloader.Dispose();
        }

        public static IEnumerable<TestCaseData> DownloaderValidCaseData
        {
            get
            {
                var symbol = Symbol.Create("AAPL", SecurityType.Equity, Market.USA);

                yield return new TestCaseData(symbol, Resolution.Minute, new DateTime(2024, 1, 1, 5, 30, 0), new DateTime(2024, 2, 1, 20, 0, 0), TickType.Trade);
                yield return new TestCaseData(symbol, Resolution.Minute, new DateTime(2024, 1, 8, 9, 30, 0), new DateTime(2024, 1, 12, 16, 0, 0), TickType.Trade);
                yield return new TestCaseData(symbol, Resolution.Minute, new DateTime(2020, 1, 8, 9, 30, 0), new DateTime(2021, 1, 12, 16, 0, 0), TickType.Trade);
                yield return new TestCaseData(symbol, Resolution.Minute, new DateTime(2015, 2, 2, 9, 30, 0), new DateTime(2015, 3, 1, 16, 0, 0), TickType.Trade);
                yield return new TestCaseData(symbol, Resolution.Hour, new DateTime(2023, 11, 8, 9, 30, 0), new DateTime(2024, 2, 2, 16, 0, 0), TickType.Trade);
                yield return new TestCaseData(symbol, Resolution.Daily, new DateTime(2023, 1, 8, 9, 30, 0), new DateTime(2024, 2, 2, 16, 0, 0), TickType.Trade);
            }
        }

        [TestCaseSource(nameof(DownloaderValidCaseData))]
        public void DownloadDataWithDifferentValidParameters(Symbol symbol, Resolution resolution, DateTime startUtc, DateTime endUtc, TickType tickType)
        {
            var downloadParameters = new DataDownloaderGetParameters(symbol, resolution, startUtc, endUtc, tickType);

            var baseData = _downloader.Get(downloadParameters).ToList();

            Assert.IsNotEmpty(baseData);
            Assert.IsTrue(baseData.First().Time >= ConvertUtcTimeToSymbolExchange(symbol, startUtc));
            Assert.IsTrue(baseData.Last().Time <= ConvertUtcTimeToSymbolExchange(symbol, endUtc));

            foreach (var data in baseData)
            {
                Assert.IsTrue(data.DataType == MarketDataType.TradeBar);
                var tradeBar = data as TradeBar;
                Assert.Greater(tradeBar.Open, 0m);
                Assert.Greater(tradeBar.High, 0m);
                Assert.Greater(tradeBar.Low, 0m);
                Assert.Greater(tradeBar.Close, 0m);
                Assert.IsTrue(tradeBar.Period.ToHigherResolutionEquivalent(true) == resolution);
            }
        }

        public static IEnumerable<TestCaseData> DownloaderInvalidCaseData
        {
            get
            {
                var symbol = Symbol.Create("AAPL", SecurityType.Equity, Market.USA);

                var startUtc = new DateTime(2024, 1, 1);
                var endUtc = new DateTime(2024, 2, 1);

                yield return new TestCaseData(symbol, Resolution.Minute, startUtc, endUtc, TickType.Quote, false)
                    .SetDescription($"Not supported {nameof(TickType.Quote)} -> empty result");
                yield return new TestCaseData(symbol, Resolution.Minute, startUtc, endUtc, TickType.OpenInterest, false)
                    .SetDescription($"Not supported {nameof(TickType.OpenInterest)} -> empty result");
                yield return new TestCaseData(symbol, Resolution.Tick, startUtc, endUtc, TickType.Trade, true)
                    .SetDescription($"Not supported {nameof(Resolution.Tick)} -> throw Exception");
                yield return new TestCaseData(symbol, Resolution.Second, startUtc, endUtc, TickType.Trade, true)
                    .SetDescription($"Not supported {nameof(Resolution.Second)} -> throw Exception");
                yield return new TestCaseData(symbol, Resolution.Minute, endUtc, startUtc, TickType.Trade, false)
                    .SetDescription("startDateTime > endDateTime -> empty result");
            }
        }

        [TestCaseSource(nameof(DownloaderInvalidCaseData))]
        public void DownloadDataWithDifferentInvalidParameters(Symbol symbol, Resolution resolution, DateTime start, DateTime end, TickType tickType, bool isThrowException)
        {
            var downloadParameters = new DataDownloaderGetParameters(symbol, resolution, start, end, tickType);

            if (isThrowException)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => _downloader.Get(downloadParameters).ToList());
                return;
            }

            var baseData = _downloader.Get(downloadParameters).ToList();

            Assert.IsEmpty(baseData);
        }

        private DateTime ConvertUtcTimeToSymbolExchange(Symbol symbol, DateTime dateTimeUtc)
        {
            var exchangeTimeZone = _marketHoursDatabase.GetExchangeHours(symbol.ID.Market, symbol, symbol.SecurityType).TimeZone;
            return dateTimeUtc.ConvertFromUtc(exchangeTimeZone);
        }
    }
}
