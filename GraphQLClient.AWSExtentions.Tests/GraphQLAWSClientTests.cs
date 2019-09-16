namespace GraphQLClient.AWSExtentions.Tests
{
    using GraphQL.Client;
    using GraphQL.Common.Request;
    using Xunit;
    using System;
    using FluentAssertions;

    public class GraphQLAWSClientTests
    {
        [Fact]
        public async void Test()
        {
            var graphQLOptions = new GraphQLClientOptions
            {
                EndPoint = new Uri("")
            };

            var awsOptions = new AWSOptions()
            {
                AccessKey = "",
                SecretKey = "",
                SessionToken = "",
                Service = "",
                Region = ""
            };

            var client = new GraphQLAWSClient(graphQLOptions, awsOptions);

            var request = new GraphQLRequest()
            {
                Query = ""
            };

            var response = await client.PostSignedRequestAsync(request, null);

            response.Should().NotBeNull();
        }
    }
}