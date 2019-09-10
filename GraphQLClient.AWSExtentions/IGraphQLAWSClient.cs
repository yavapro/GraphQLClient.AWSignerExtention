namespace GraphQLClient.AWSExtentions
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using GraphQL.Common.Request;
    using GraphQL.Common.Response;

    public interface IGraphQLAWSClient
    {
        Task<GraphQLResponse> PostSignedRequestAsync(GraphQLRequest request, IDictionary<string, string> headers);

        Task<GraphQLResponse> PostSignedRequestAsync(GraphQLRequest request, IDictionary<string, string> headers,
            CancellationToken cancellationToken);
    }
}