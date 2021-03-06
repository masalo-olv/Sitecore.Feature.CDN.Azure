﻿// MIT License
// 
// Copyright (c) 2017 Kyle Kingsbury
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
namespace Sitecore.Feature.CDN
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Data.Items;
    using Foundation.CDN;
    using Microsoft.Azure.Management.Cdn;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Microsoft.Rest;

    public class AzureDeliveryService : IDeliveryService
    {
        /// <summary>
        /// Gets the Path Service that generates Cdn Urls
        /// </summary>
        private readonly IPathService pathService;

        /// <summary>
        /// Gets the Azure specific settings
        /// </summary>
        private readonly IAzureSettings azureSettings;

        /// <summary>
        /// Private backing field for <see cref="CdnClient"/>
        /// </summary>
        private CdnManagementClient cdnClient;

        /// <summary>
        /// Gets the Azure Cdn Client for purge requests
        /// <para>Lazily instantiates the object</para>
        /// </summary>
        public virtual CdnManagementClient CdnClient
        {
            get
            {
                if (this.cdnClient == null)
                {
                    var credentials = this.GetServiceCredentials(
                        this.azureSettings.Authority, 
                        this.azureSettings.ClientId,
                        this.azureSettings.ClientSecret);

                    this.cdnClient = new CdnManagementClient(credentials)
                    {
                        SubscriptionId = this.azureSettings.SubscriptionId
                    };
                }

                return this.cdnClient;
            }
        }

        public AzureDeliveryService(IPathService pathService, IAzureSettings azureSettings)
        {
            if (String.IsNullOrEmpty(azureSettings.Authority)
                || String.IsNullOrEmpty(azureSettings.ClientId)
                || String.IsNullOrEmpty(azureSettings.ClientSecret)
                || String.IsNullOrEmpty(azureSettings.EndpointName)
                || String.IsNullOrEmpty(azureSettings.ProfileName)
                || String.IsNullOrEmpty(azureSettings.ResourceGroupName)
                || String.IsNullOrEmpty(azureSettings.SubscriptionId))
            {
                throw new ArgumentException($"{nameof(azureSettings)} is not fully instantiated.");
            }

            this.pathService = pathService;
            this.azureSettings = azureSettings;
        }

        /// <summary>
        /// Purges the Items from the Azure CDN
        /// </summary>
        /// <param name="items">The items that have require purging</param>
        public virtual void Purge(IEnumerable<Item> items)
        {
            var list = items as Item[] ?? items.ToArray();

            if (!list.Any())
            {
                return;
            }

            var urls = list.SelectMany(item => this.pathService.GeneratePaths(item))
                           .Distinct()
                           .ToArray();

            if (urls.Contains("/"))
            {
                var temp = urls.ToList();

                temp.Remove("/");

                urls = temp.ToArray();
            }

            this.CdnClient.Endpoints.PurgeContent(
                this.azureSettings.ResourceGroupName,
                this.azureSettings.ProfileName,
                this.azureSettings.EndpointName,
                urls);
        }

        /// <summary>
        /// Gets an Service Client Credentials
        /// </summary>
        /// <param name="authority">The Authority</param>
        /// <param name="clientId">The Application or Client Id</param>
        /// <param name="clientSecret">The Application Secrect Key</param>
        /// <returns>Service Credentials to sign requests</returns>
        protected virtual ServiceClientCredentials GetServiceCredentials(string authority, string clientId, string clientSecret)
        {
            var authContext = new AuthenticationContext(authority);
            var credential = new ClientCredential(clientId, clientSecret);
            var token = authContext.AcquireTokenAsync("https://management.core.windows.net/", credential).Result.AccessToken;

            return new TokenCredentials(token);
        }
    }
}