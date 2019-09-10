namespace GraphQLClient.AWSExtentions
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Aws4RequestSigner;
    using GraphQL.Client;
    using GraphQL.Client.Exceptions;
    using GraphQL.Common.Request;
    using GraphQL.Common.Response;
    using Newtonsoft.Json;

    public class GraphQLAWSClient : IGraphQLAWSClient, IDisposable
    {
        private readonly AWSOptions awsOptions;
        private readonly HttpClient httpClient;
        private readonly GraphQLClientOptions options;

        public GraphQLAWSClient(GraphQLClientOptions options, AWSOptions awsOptions)
        {
            this.options = options;
            this.awsOptions = awsOptions;
            this.httpClient = new HttpClient(this.options.HttpMessageHandler);
        }

        public GraphQLAWSClient(Uri endPoint, AWSOptions awsOptions)
            : this(new GraphQLClientOptions {EndPoint = endPoint}, awsOptions)
        {
        }

        public async Task<GraphQLResponse> PostSignedRequestAsync(GraphQLRequest request,
            IDictionary<string, string> headers)
        {
            return await this.PostSignedRequestAsync(request, headers, CancellationToken.None);
        }

        public async Task<GraphQLResponse> PostSignedRequestAsync(
            GraphQLRequest request,
            IDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            GraphQLResponse graphQlResponse;

            var signer = new AWS4RequestSigner(awsOptions.AccessKey, awsOptions.SecretKey);

            using (StringContent httpContent =
                new StringContent(JsonConvert.SerializeObject(request, this.options.JsonSerializerSettings)))
            {
                httpContent.Headers.ContentType = this.options.MediaType;

                using (var httpRequest = new HttpRequestMessage
                    {Content = httpContent, Method = HttpMethod.Post, RequestUri = options.EndPoint})
                {
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            httpRequest.Headers.Add(header.Key, header.Value);
                        }
                    }

                    await signer.Sign(httpRequest, awsOptions.Service, awsOptions.Region);

                    using (HttpResponseMessage httpResponseMessage =
                        await this.httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false))
                    {
                        graphQlResponse = await this.ReadHttpResponseMessageAsync(httpResponseMessage)
                            .ConfigureAwait(false);
                    }
                }
            }

            return graphQlResponse;
        }

        private async Task<GraphQLResponse> ReadHttpResponseMessageAsync(
            HttpResponseMessage httpResponseMessage)
        {
            GraphQLResponse graphQlResponse;
            using (Stream stream = await httpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                using (StreamReader streamReader = new StreamReader(stream))
                {
                    using (JsonTextReader jsonTextReader = new JsonTextReader((TextReader) streamReader))
                    {
                        JsonSerializer jsonSerializer = new JsonSerializer
                        {
                            ContractResolver = this.options.JsonSerializerSettings.ContractResolver
                        };
                        try
                        {
                            graphQlResponse = jsonSerializer.Deserialize<GraphQLResponse>(jsonTextReader);
                        }
                        catch (JsonReaderException ex)
                        {
                            if (httpResponseMessage.IsSuccessStatusCode)
                            {
                                throw ex;
                            }

                            throw new GraphQLHttpException(httpResponseMessage);
                        }
                    }
                }
            }

            return graphQlResponse;
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }
    }
}