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
            _marketHoursDatabase = MarketHoursDatabase.FromDataFolder();
            _downloader = new();
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

                var startUtc = new DateTime(2024, 1, 1);
                var endUtc = new DateTime(2024, 2, 1);

                yield return new TestCaseData(symbol, Resolution.Minute, startUtc, endUtc, TickType.Trade);
            }
        }

        [TestCaseSource(nameof(DownloaderValidCaseData))]
        public void DownloadDataWithDifferentValidParameters(Symbol symbol, Resolution resolution, DateTime start, DateTime end, TickType tickType)
        {
            var amountBars = GetAmountBarsByResolutionAndRangeDate(symbol, resolution, start, end);

            var downloadParameters = new DataDownloaderGetParameters(symbol, resolution, start, end, tickType);

            var baseData = _downloader.Get(downloadParameters).ToList();

            Assert.IsNotEmpty(baseData);
            Assert.GreaterOrEqual(baseData.Count, amountBars);

            foreach (var data in baseData)
            {
                Assert.IsTrue(data.DataType == MarketDataType.TradeBar);
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

        private double GetAmountBarsByResolutionAndRangeDate(Symbol symbol, Resolution resolution, DateTime startDate, DateTime endDate)
        {
            var exchangeHours = _marketHoursDatabase.GetExchangeHours(Market.USA, symbol, symbol.SecurityType);

            int amountOpenDaysInRange = 0;
            foreach (DateTime day in EachDay(startDate, endDate))
            {
                if (exchangeHours.IsDateOpen(day))
                {
                    amountOpenDaysInRange++;
                }
            }

            return resolution switch
            {
                Resolution.Minute => amountOpenDaysInRange * exchangeHours.RegularMarketDuration.TotalMinutes,
            };
        }

        public IEnumerable<DateTime> EachDay(DateTime from, DateTime thru)
        {
            for (var day = from.Date; day.Date <= thru.Date; day = day.AddDays(1))
                yield return day;
        }
    }
}
