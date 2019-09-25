﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

using Kentico.Kontent.Boilerplate.Controllers;
using Kentico.Kontent.Boilerplate.Helpers;
using Kentico.Kontent.Boilerplate.Models;
using Kentico.Kontent.Boilerplate.Resolvers;
using Kentico.Kontent.Boilerplate.Services;
using Kentico.Kontent.Delivery;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RichardSzalay.MockHttp;
using Xunit;

namespace Kentico.Kontent.Boilerplate.Tests
{
    public class HomeControllerTests
    {
        readonly string guid = string.Empty;
        readonly string baseUrl = string.Empty;
        readonly MockHttpMessageHandler mockHttp;

        public HomeControllerTests()
        {
            guid = Guid.NewGuid().ToString();
            baseUrl = $"https://deliver.kontent.ai/{guid}";
            mockHttp = new MockHttpMessageHandler();
        }

        [Fact]
        public void IndexReturnsArticle()
        {
            var cachedDeliveryClient = GetCachedDeliveryClient();

            var controller = new HomeController(cachedDeliveryClient);
            var result = Task.Run(() => controller.Index()).Result;

            Assert.Equal(typeof(ReadOnlyCollection<Article>), result.Model.GetType());
        }

        private IDeliveryClient GetCachedDeliveryClient()
        {
            mockHttp.When($"{baseUrl}/items")
                .WithQueryString(new[] { new KeyValuePair<string, string>("system.type", Article.Codename), new KeyValuePair<string, string>("limit", "3"), new KeyValuePair<string, string>("depth", "0"), new KeyValuePair<string, string>("order", "elements.post_date[asc]") })
                .Respond("application/json", File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "Fixtures\\CachedDeliveryClient\\articles.json")));

            var httpClient = mockHttp.ToHttpClient();

            var projectOptions = Options.Create(new ProjectOptions
            {
                CacheTimeoutSeconds = 60,
                DeliveryOptions = new DeliveryOptions
                {
                    ProjectId = guid
                }
            });

            var memoryCacheOptions = Options.Create(new MemoryCacheOptions
            {
                Clock = new TestClock(),
                ExpirationScanFrequency = new TimeSpan(0, 0, 5)
            });

            var cacheManager = new ReactiveCacheManager(projectOptions, new MemoryCache(memoryCacheOptions), new DependentFormatResolver(), new WebhookListener());

            return new CachedDeliveryClient(projectOptions, cacheManager, DeliveryClientBuilder.WithOptions(o => projectOptions.Value.DeliveryOptions).WithTypeProvider(new Models.CustomTypeProvider()).WithHttpClient(httpClient).Build());
        }
    }
}
