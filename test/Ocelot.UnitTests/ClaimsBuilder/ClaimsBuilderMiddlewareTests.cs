﻿using Ocelot.Infrastructure.RequestData;

namespace Ocelot.UnitTests.ClaimsBuilder
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using Ocelot.ClaimsBuilder;
    using Ocelot.ClaimsBuilder.Middleware;
    using Ocelot.Configuration;
    using Ocelot.Configuration.Builder;
    using Ocelot.DownstreamRouteFinder;
    using Ocelot.DownstreamRouteFinder.UrlMatcher;
    using Responses;
    using TestStack.BDDfy;
    using Xunit;

    public class ClaimsBuilderMiddlewareTests : IDisposable
    {
        private readonly Mock<IRequestScopedDataRepository> _scopedRepository;
        private readonly Mock<IAddClaimsToRequest> _addHeaders;
        private readonly string _url;
        private readonly TestServer _server;
        private readonly HttpClient _client;
        private Response<DownstreamRoute> _downstreamRoute;
        private HttpResponseMessage _result;

        public ClaimsBuilderMiddlewareTests()
        {
            _url = "http://localhost:51879";
            _scopedRepository = new Mock<IRequestScopedDataRepository>();
            _addHeaders = new Mock<IAddClaimsToRequest>();
            var builder = new WebHostBuilder()
              .ConfigureServices(x =>
              {
                  x.AddSingleton(_addHeaders.Object);
                  x.AddSingleton(_scopedRepository.Object);
              })
              .UseUrls(_url)
              .UseKestrel()
              .UseContentRoot(Directory.GetCurrentDirectory())
              .UseIISIntegration()
              .UseUrls(_url)
              .Configure(app =>
              {
                  app.UseClaimsBuilderMiddleware();
              });

            _server = new TestServer(builder);
            _client = _server.CreateClient();
        }

        [Fact]
        public void happy_path()
        {
            var downstreamRoute = new DownstreamRoute(new List<TemplateVariableNameAndValue>(),
                new ReRouteBuilder()
                    .WithDownstreamTemplate("any old string")
                    .WithClaimsToClaims(new List<ClaimToThing>
                    {
                        new ClaimToThing("sub", "UserType", "|", 0)
                    })
                    .Build());

            this.Given(x => x.GivenTheDownStreamRouteIs(downstreamRoute))
                .And(x => x.GivenTheAddClaimsToRequestReturns())
                .When(x => x.WhenICallTheMiddleware())
                .Then(x => x.ThenTheClaimsToRequestIsCalledCorrectly())
                .BDDfy();
        }

        private void GivenTheAddClaimsToRequestReturns()
        {
            _addHeaders
                .Setup(x => x.SetClaimsOnContext(It.IsAny<List<ClaimToThing>>(),
                It.IsAny<HttpContext>()))
                .Returns(new OkResponse());
        }

        private void ThenTheClaimsToRequestIsCalledCorrectly()
        {
            _addHeaders
                .Verify(x => x.SetClaimsOnContext(It.IsAny<List<ClaimToThing>>(),
                It.IsAny<HttpContext>()), Times.Once);
        }

        private void WhenICallTheMiddleware()
        {
            _result = _client.GetAsync(_url).Result;
        }

        private void GivenTheDownStreamRouteIs(DownstreamRoute downstreamRoute)
        {
            _downstreamRoute = new OkResponse<DownstreamRoute>(downstreamRoute);
            _scopedRepository
                .Setup(x => x.Get<DownstreamRoute>(It.IsAny<string>()))
                .Returns(_downstreamRoute);
        }

        public void Dispose()
        {
            _client.Dispose();
            _server.Dispose();
        }
    }
}
