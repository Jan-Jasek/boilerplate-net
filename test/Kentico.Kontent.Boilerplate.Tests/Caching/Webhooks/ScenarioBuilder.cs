﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Kentico.Kontent.Delivery;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RichardSzalay.MockHttp;

namespace Kentico.Kontent.Boilerplate.Tests.Caching.Webhooks
{
    public class ScenarioBuilder
    {
        private readonly string _projectId = Guid.NewGuid().ToString();
        private readonly string _baseUrl;

        private readonly MemoryCache _memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        private readonly Dictionary<string, int> _requestCounter = new Dictionary<string, int>();

        private readonly List<(string key, Action<MockHttpMessageHandler> configure)> _configurations = new List<(string key, Action<MockHttpMessageHandler> configure)>();

        public ScenarioBuilder()
        {
            _baseUrl = $"https://deliver.kontent.ai/{_projectId}/";
        }

        public ScenarioBuilder WithResponse(string relativeUrl, object responseObject)
        {
            var url = $"{_baseUrl}{relativeUrl.TrimStart('/')}";

            void ConfigureMock(MockHttpMessageHandler mockHttp) => mockHttp
                .When(url)
                .Respond("application/json", _ => CreateStreamAndCount(relativeUrl, responseObject));

            var existingIndex = _configurations.FindIndex(x => x.key == url);
            if (existingIndex >= 0)
            {
                _configurations[existingIndex] = (url, ConfigureMock);
            }
            else
            {
                _configurations.Add((url, ConfigureMock));
            }

            return this;
        }

        public ScenarioBuilder WithResponse(string relativeUrl, IEnumerable<KeyValuePair<string, string>> requestHeaders, object responseObject, IEnumerable<KeyValuePair<string, string>> responseHeaders)
        {
            var url = $"{_baseUrl}{relativeUrl.TrimStart('/')}";
            var key = url + (requestHeaders == null
                ? ""
                : $"|{string.Join(";", requestHeaders.Select(p => $"{p.Key}:{p.Value}"))}");

            void ConfigureMock(MockHttpMessageHandler mockHttp) => mockHttp
                .When(url)
                .WithHeaders(requestHeaders ?? new List<KeyValuePair<string, string>>())
                .Respond(responseHeaders ?? new List<KeyValuePair<string, string>>(), "application/json", _ => CreateStreamAndCount(relativeUrl, responseObject));

            var existingIndex = _configurations.FindIndex(x => x.key == key);
            if (existingIndex >= 0)
            {
                _configurations[existingIndex] = (key, ConfigureMock);
            }
            else
            {
                _configurations.Add((key, ConfigureMock));
            }

            return this;
        }

        private Stream CreateStreamAndCount(string url, object responseObject)
        {
            _requestCounter[url] = _requestCounter.GetValueOrDefault(url) + 1;
            var bytes = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(responseObject));
            return new MemoryStream(bytes);
        }

        public Scenario Build()
        {
            var mockHttp = new MockHttpMessageHandler();
            foreach (var (_, configure) in _configurations)
            {
                configure?.Invoke(mockHttp);
            }
            return new Scenario(_memoryCache, mockHttp.ToHttpClient(), new DeliveryOptions { ProjectId = _projectId }, _requestCounter);
        }
    }
}