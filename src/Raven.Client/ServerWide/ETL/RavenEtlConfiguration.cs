﻿using System;
using Sparrow.Json.Parsing;

namespace Raven.Client.ServerWide.ETL
{
    public class RavenEtlConfiguration : EtlConfiguration<RavenConnectionString>
    {
        private string _destination;

        public int? LoadRequestTimeoutInSec { get; set; }

        public override EtlType EtlType => EtlType.Raven;

        public override string GetDestination()
        {
            return _destination ?? (_destination = $"{Connection.Database}@{string.Join(",",Connection.TopologyDiscoveryUrls)}");
        }

        public override bool UsingEncryptedCommunicationChannel()
        {
            foreach (var url in Connection.TopologyDiscoveryUrls)
            {
                if (url.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public override DynamicJsonValue ToJson()
        {
            var result = base.ToJson();

            result[nameof(LoadRequestTimeoutInSec)] = LoadRequestTimeoutInSec;

            return result;
        }
    }
}
