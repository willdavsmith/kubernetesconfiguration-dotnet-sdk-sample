using Azure.Core;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rest.Azure;
using Microsoft.Azure.Management.KubernetesConfiguration.Extensions;
using Microsoft.Azure.Management.KubernetesConfiguration.Extensions.Models;
using System;
using System.Linq;

namespace kubernetesconfiguration
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Under the hood this creates a SourceControlConfigurationClient. Please see ExtensionsClient definition for details.
            ExtensionsClient client = new ExtensionsClient();

            var clusterName = Environment.GetEnvironmentVariable("AZURE_RESOURCE_NAME");
            var location = Environment.GetEnvironmentVariable("AZURE_RESOURCE_LOCATION");
            var resourceGroup = Environment.GetEnvironmentVariable("AZURE_RESOURCE_GROUP");

            // Set up cluster info with environment variables
            ClusterInfo cluster = new ClusterInfo(
                name: clusterName,
                type: ClusterInfo.ClusterType.connectedClusters,
                location: location,
                resourceGroup: resourceGroup
            );
            client.Cluster = cluster;

            // Set up extension info. Sample pre-registered extension provided - update with your extension.
            Extension extension = new Extension(
                name: "openservicemesh",
                type: "Extensions",
                extensionType: "microsoft.openservicemesh",
                autoUpgradeMinorVersion: false,
                releaseTrain: "staging",
                version: "0.1.0",
                scope: new Scope(
                    cluster: new ScopeCluster(
                        releaseNamespace: "arc-osm-system"
                    )
                )
             );
            client.Extension = extension;

            var extensions1 = client.ListExtensions();
            Console.WriteLine("Found {0} extensions before creation", extensions1.Count());

            client.CreateExtension();

            var extensions2 = client.ListExtensions();
            Console.WriteLine("Found {0} extensions after creation", extensions2.Count());

            client.DeleteExtension();

            var extensions3 = client.ListExtensions();
            Console.WriteLine("Found {0} extensions after deletion", extensions3.Count());
        }
    }
    public class ClusterInfo
    {
        public readonly string ResourceGroup;
        public readonly string Name;
        public readonly string RpName;
        public readonly string Type;
        public readonly string Location;

        public enum ClusterType
        {
            connectedClusters,
            managedClusters
        }

        public const string ArcClusterRP = "Microsoft.Kubernetes";
        public const string AksClusterRP = "Microsoft.ContainerServices";

        public ClusterInfo(string name, ClusterType type, string location, string resourceGroup)
        {
            this.Name = name;
            this.Type = type.ToString();
            this.RpName = type == ClusterType.connectedClusters ? ArcClusterRP : AksClusterRP;
            this.Location = location;
            this.ResourceGroup = resourceGroup;
        }
    }

    // Auth provider. Adapt to the needs of your service.
    public class TokenCredentialTokenProvider : Microsoft.Rest.ITokenProvider
    {
        readonly TokenCredential _tokenCredential;
        readonly string[] _scopes;

        public TokenCredentialTokenProvider(TokenCredential tokenCredential, string[] scopes)
        {
            _tokenCredential = tokenCredential;
            _scopes = scopes;
        }

        public async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
        {
            var accessToken = await _tokenCredential.GetTokenAsync(new TokenRequestContext(_scopes), cancellationToken);
            return new AuthenticationHeaderValue("Bearer", accessToken.Token);
        }
    }

    // Sample extension client
    public class ExtensionsClient
    {
        public SourceControlConfigurationClient SourceControlConfigurationClient { get; set; }

        public Extension Extension { get; set; }

        public const string ApiVersion = "2020-09-01";
        public const string ConfigurationType = "Extensions";

        public ClusterInfo Cluster { get; set; }

        public ExtensionsClient()
        {
            // Set up authorization. Adapt to the needs of your service.
            string subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");

            var tokenCredentials = new Azure.Identity.DefaultAzureCredential(new Azure.Identity.DefaultAzureCredentialOptions
            {
                AuthorityHost = Azure.Identity.AzureAuthorityHosts.AzurePublicCloud
            });

            var restTokenProvider = new TokenCredentialTokenProvider(tokenCredentials,
                new string[] { "https://management.core.windows.net/.default" }
            );

            var restTokenCredentials = new Microsoft.Rest.TokenCredentials(restTokenProvider);

            var client = new SourceControlConfigurationClient(restTokenCredentials)
            {
                SubscriptionId = subscriptionId
            };

            SourceControlConfigurationClient = client;
        }

        public Extension CreateExtension()
        {
            return SourceControlConfigurationClient.Extensions.Create(
                resourceGroupName: Cluster.ResourceGroup,
                clusterRp: Cluster.RpName,
                clusterResourceName: Cluster.Type,
                clusterName: Cluster.Name,
                extensionName: Extension.Name,
                extension: Extension
            );
        }

        public Extension GetExtension()
        {
            return SourceControlConfigurationClient.Extensions.Get(
                resourceGroupName: Cluster.ResourceGroup,
                clusterRp: Cluster.RpName,
                clusterResourceName: Cluster.Type,
                clusterName: Cluster.Name,
                extensionName: Extension.Name
            );
        }

        public void DeleteExtension()
        {
            SourceControlConfigurationClient.Extensions.Delete(
                resourceGroupName: Cluster.ResourceGroup,
                clusterRp: Cluster.RpName,
                clusterResourceName: Cluster.Type,
                clusterName: Cluster.Name,
                extensionName: Extension.Name,
                forceDelete: true
            );
        }

        public IPage<Extension> ListExtensions()
        {
            return SourceControlConfigurationClient.Extensions.List(
                resourceGroupName: Cluster.ResourceGroup,
                clusterRp: Cluster.RpName,
                clusterResourceName: Cluster.Type,
                clusterName: Cluster.Name
            );
        }
    }
}
