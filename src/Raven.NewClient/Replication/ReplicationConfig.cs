// -----------------------------------------------------------------------
//  <copyright file="ReplicationConfig.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

namespace Raven.NewClient.Client.Replication
{
    /// <summary>
    /// Data class for replication config document, available on a destination server
    /// </summary>
    public class ReplicationConfig
    {
        public StraightforwardConflictResolution DocumentConflictResolution { get; set; }
    }

    public enum StraightforwardConflictResolution
    {
        None,
        /// <summary>
        /// Always resolve in favor of the latest version based on the last modified time
        /// </summary>
        ResolveToLatest
    }
}
