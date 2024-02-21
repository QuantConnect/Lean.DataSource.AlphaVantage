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

namespace QuantConnect.DataSource.AlphaVantage.Enums
{
    /// <summary>
    /// Represents different plans with associated request limits and monthly prices.
    /// premium plans <see href="https://www.alphavantage.co/premium/"/>
    /// </summary>
    public enum AlphaVantagePricePlan
    {
        /// <summary>
        /// 25 requests per day + free access
        /// </summary>
        Free = 0,

        /// <summary>
        /// 30 API requests per minute + 15-minute delayed US market data
        /// </summary>
        Plan30 = 1,

        /// <summary>
        /// 75 API requests per minute + 15-minute delayed US market data
        /// </summary>
        Plan75 = 2,

        /// <summary>
        /// 150 API requests per minute + Realtime US market data
        /// </summary>
        Plan150 = 3,

        /// <summary>
        /// 300 API requests per minute + Realtime US market data
        /// </summary>
        Plan300 = 4,

        /// <summary>
        /// 600 API requests per minute + Realtime US market data
        /// </summary>
        Plan600 = 5,

        /// <summary>
        /// 1200 API requests per minute + Realtime US market data
        /// </summary>
        Plan1200 = 6,
    }
}
