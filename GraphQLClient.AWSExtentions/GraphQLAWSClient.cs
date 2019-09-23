namespace GraphQLClient.AWSExtentions
{
    using Amazon;
    using Amazon.Runtime;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Aws4RequestSigner;
    using GraphQL.Client;
    using GraphQL.Common.Request;
    using GraphQL.Common.Response;
    using Newtonsoft.Json;

    public class GraphQLAWSClient : IGraphQLAWSClient, IDisposable
    {
        private const string SessionTokenHeader = "X-Amz-Security-Token";

        private readonly string region;
        private readonly string serviceName;
        private readonly string accessKey;
        private readonly string secretKey;
        private readonly string sessionToken;
        private readonly AWSCredentials awsCredentials;
        private readonly HttpClient httpClient;
        private readonly GraphQLClientOptions options;

        public GraphQLAWSClient(GraphQLClientOptions options, AWSOptions awsOptions, RegionEndpoint region, string serviceName)
        {
            this.options = options;
            this.accessKey = awsOptions.AccessKey;
            this.secretKey = awsOptions.SecretKey;
            this.sessionToken = awsOptions.SessionToken;
            this.serviceName = serviceName;
            this.region = region.SystemName;
            this.httpClient = new HttpClient(this.options.HttpMessageHandler);
        }

        public GraphQLAWSClient(Uri endPoint, AWSOptions awsOptions, RegionEndpoint region, string serviceName)
            : this(new GraphQLClientOptions {EndPoint = endPoint}, awsOptions, region, serviceName)
        {
        }

        public GraphQLAWSClient(
            GraphQLClientOptions options,
            AWSCredentials awsCredentials,
            RegionEndpoint region,
            string serviceName)
        {
            this.options = options;
            this.awsCredentials = awsCredentials;
            this.region = region.SystemName;
            this.serviceName = serviceName;
            this.httpClient = new HttpClient(this.options.HttpMessageHandler);
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
            AWS4RequestSigner signer;
            var credentials = awsCredentials?.GetCredentials();

            signer = credentials == null
                ? new AWS4RequestSigner(accessKey, secretKey)
                : new AWS4RequestSigner(credentials.AccessKey, credentials.SecretKey);

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

                    if (credentials != null)
                    {
                        if (!string.IsNullOrEmpty(credentials.Token))
                        {
                            httpRequest.Headers.Add(SessionTokenHeader, credentials.Token);
                        }
                    }
                    else if (!string.IsNullOrEmpty(sessionToken))
                    {
                        httpRequest.Headers.Add(SessionTokenHeader, sessionToken);
                    }

                    await signer.Sign(httpRequest, serviceName, region);

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
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                var message = await httpResponseMessage.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Server return unexpected error with code: {httpResponseMessage.StatusCode} ({message})");
            }

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

                        return jsonSerializer.Deserialize<GraphQLResponse>(jsonTextReader);
                    }
                }
            }
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }
    }
}