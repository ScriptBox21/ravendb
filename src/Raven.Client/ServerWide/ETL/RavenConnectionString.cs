﻿using System.Collections.Generic;
using Sparrow.Json.Parsing;

namespace Raven.Client.ServerWide.ETL
{
    public class RavenConnectionString : ConnectionString
    {
        public string Database { get; set; }
        public string[] TopologyDiscoveryUrls;

        public override ConnectionStringType Type => ConnectionStringType.Raven;

        protected override void ValidateImpl(ref List<string> errors)
        {
            if (string.IsNullOrEmpty(Database))
                errors.Add($"{nameof(Database)} cannot be empty");

            if (TopologyDiscoveryUrls == null || TopologyDiscoveryUrls.Length == 0)
                errors.Add($"{nameof(TopologyDiscoveryUrls)} cannot be empty");

            if(TopologyDiscoveryUrls == null)
                return;

            for (int i = 0; i < TopologyDiscoveryUrls.Length; i++)
            {
                if (TopologyDiscoveryUrls[i] == null)
                {
                    errors.Add($"Url number {i+1} in {nameof(TopologyDiscoveryUrls)} cannot be empty");
                    continue;
                }
                TopologyDiscoveryUrls[i] = TopologyDiscoveryUrls[i].Trim();
            }
        }

        public override DynamicJsonValue ToJson()
        {
            var json = base.ToJson();
            json[nameof(Database)] = Database;
            json[nameof(TopologyDiscoveryUrls)] = new DynamicJsonArray(TopologyDiscoveryUrls);
            return json;
        }
    }
}
