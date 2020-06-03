namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.Extensions;
    using Infrastructure.WebApi;
    using Microsoft.Extensions.Caching.Memory;
    using Raven.Abstractions.Data;
    using Raven.Client;

    public class GetAllErrorsController : ApiController
    {
        internal GetAllErrorsController(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        [Route("errors")]
        [HttpGet]
        public async Task<HttpResponseMessage> ErrorsGet()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Advanced
                    .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .Statistics(out var stats)
                    .FilterByStatusWhere(Request)
                    .FilterByLastModifiedRange(Request)
                    .FilterByQueueAddress(Request)
                    .Sort(Request)
                    .Paging(Request)
                    .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                    .SelectFields<FailedMessageView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return Negotiator
                    .FromModel(Request, results)
                    .WithPagingLinksAndTotalCount(stats.TotalResults, Request)
                    .WithEtag(stats);
            }
        }

        [Route("errors")]
        [HttpHead]
        public HttpResponseMessage ErrorsHead()
        {
            var requestString = GetRequestString("errors", Request);

            return cache.GetOrCreate(requestString, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);

                return Task.Run(() => GetErrors()).GetAwaiter().GetResult();
            });
        }

        string GetRequestString(string url, HttpRequestMessage request)
        {
            var status = request.GetQueryStringValue<string>("status");
            var modified = request.GetQueryStringValue<string>("modified");
            var queueAddress = request.GetQueryStringValue<string>("queueaddress");

            var statusString = string.IsNullOrWhiteSpace(status) ? string.Empty : status;
            var modifiedString = string.IsNullOrWhiteSpace(modified) ? string.Empty : modified;
            var queueAddressString = string.IsNullOrWhiteSpace(queueAddress) ? string.Empty : queueAddress;

            return $"{url}{statusString}{modifiedString}{queueAddress}";
        }

        async Task<HttpResponseMessage> GetErrors()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var queryResult = await session.Advanced
                    .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .FilterByStatusWhere(Request)
                    .FilterByLastModifiedRange(Request)
                    .FilterByQueueAddress(Request)
                    .QueryResultAsync()
                    .ConfigureAwait(false);

                var response = Request.CreateResponse(HttpStatusCode.OK);

                return response
                    .WithTotalCount(queryResult.TotalResults)
                    .WithEtag(queryResult.IndexEtag);
            }
        }

        [Route("endpoints/{endpointname}/errors")]
        [HttpGet]
        public async Task<HttpResponseMessage> ErrorsByEndpointName(string endpointName)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Advanced
                    .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .Statistics(out var stats)
                    .FilterByStatusWhere(Request)
                    .AndAlso()
                    .WhereEquals("ReceivingEndpointName", endpointName)
                    .FilterByLastModifiedRange(Request)
                    .Sort(Request)
                    .Paging(Request)
                    .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                    .SelectFields<FailedMessageView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return Negotiator
                    .FromModel(Request, results)
                    .WithPagingLinksAndTotalCount(stats.TotalResults, Request)
                    .WithEtag(stats);
            }
        }

        [Route("errors/summary")]
        [HttpGet]
        public async Task<HttpResponseMessage> ErrorsSummary()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var facetResults = await session.Query<FailedMessage, FailedMessageFacetsIndex>()
                    .ToFacetsAsync(new List<Facet>
                    {
                        new Facet
                        {
                            Name = "Name",
                            DisplayName = "Endpoints"
                        },
                        new Facet
                        {
                            Name = "Host",
                            DisplayName = "Hosts"
                        },
                        new Facet
                        {
                            Name = "MessageType",
                            DisplayName = "Message types"
                        }
                    })
                    .ConfigureAwait(false);

                return Negotiator.FromModel(Request, facetResults.Results);
            }
        }

        readonly IDocumentStore documentStore;
        static MemoryCache cache = new MemoryCache(new MemoryCacheOptions());
    }
}