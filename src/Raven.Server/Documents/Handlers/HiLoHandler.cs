// -----------------------------------------------------------------------
//  <copyright file="HiLoHandler.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Globalization;
using System.Threading.Tasks;
using Raven.Server.Exceptions;
using Raven.Server.Routing;
using Raven.Server.ServerWide;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Server.Documents.Handlers
{
    public class HiLoHandler : DatabaseRequestHandler
    {
        private const string ravenKeyGeneratorsHilo = "Raven/Hilo/";
        private const string ravenKeyServerPrefix = "Raven/ServerPrefixForHilo";

        private static long CalculateCapacity(string lastSizeStr, string lastRangeAtStr)
        {
            long lastSize;
            DateTime lastRangeAt;
            if (long.TryParse(lastSizeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out lastSize) == false ||
                DateTime.TryParseExact(lastRangeAtStr, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                    out lastRangeAt) == false)
                return 32;

            var span = DateTime.UtcNow - lastRangeAt;

            if (span.TotalSeconds < 30)
            {
                return Math.Min(Math.Max(32, Math.Max(lastSize, lastSize * 2)), 1024 * 1024);
            }
            if (span.TotalSeconds > 60)
            {
                return Math.Max(lastSize / 2, 32);
            }

            return Math.Max(32, lastSize);
        }

        [RavenAction("/databases/*/hilo/next", "GET",
             "/databases/{databaseName:string}/hilo/next?tag={collectionName:string}&lastBatchSize={size:long|optional}&lastRangeAt={date:System.DateTime|optional}&identityPartsSeparator={separator:string|optional}&lastMax={max:long|optional} "
         )]

        public async Task GetNextHiLo()
        {
            DocumentsOperationContext context;

            using (ContextPool.AllocateOperationContext(out context))
            {
                var tag = GetQueryStringValueAndAssertIfSingleAndNotEmpty("tag");
                var lastSize = GetStringQueryString("lastBatchSize", false);
                var lastRangeAt = GetStringQueryString("lastRangeAt", false);
                var identityPartsSeparator = GetStringQueryString("identityPartsSeparator", false) ?? "/";
                var lastMaxSt = GetStringQueryString("lastMax", false);

                var capacity = CalculateCapacity(lastSize, lastRangeAt);

                long lastMax;
                if (long.TryParse(lastMaxSt, NumberStyles.Any, CultureInfo.InvariantCulture, out lastMax) == false)
                    lastMax = 0;

                var cmd = new MergedNextHiLoCommand
                {
                    Database = Database,
                    Key = tag,
                    Capacity = capacity,
                    Separator = identityPartsSeparator,
                    LastRangeMax = lastMax
                };

                await Database.TxMerger.Enqueue(cmd);

                HttpContext.Response.StatusCode = 201;

                using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
                {
                    context.Write(writer, new DynamicJsonValue
                    {
                        ["Prefix"] = cmd.Prefix,
                        ["Low"] = cmd.OldMax + 1,
                        ["High"] = cmd.OldMax + capacity,
                        ["LastSize"] = capacity,
                        ["LastRangeAt"] = cmd.LastRangeAt.ToString("o")
                    });
                }
            }
        }

        private class MergedNextHiLoCommand : TransactionOperationsMerger.MergedTransactionCommand
        {
            public string Key;
            public DocumentDatabase Database;
            public long Capacity;
            public string Separator;
            public long LastRangeMax;
            public string Prefix;
            public long OldMax;
            public DateTime LastRangeAt;

            public override void Execute(DocumentsOperationContext context, RavenTransaction tx)
            {
                var hiLoDocumentKey = ravenKeyGeneratorsHilo + Key;
                var prefix = Key + Separator;

                long oldMax = 0;
                var newDoc = new DynamicJsonValue();
                BlittableJsonReaderObject hiloDocReader = null, serverPrefixDocReader = null;
                try
                {
                    try
                    {
                        serverPrefixDocReader = Database.DocumentsStorage.Get(context, ravenKeyServerPrefix)?.Data;
                        hiloDocReader = Database.DocumentsStorage.Get(context, hiLoDocumentKey)?.Data;
                    }
                    catch (DocumentConflictException e)
                    {
                        // resolving the conflict by selecting the document with the highest number
                        long highestMax = 0;
                        foreach (var conflict in e.Conflicts)
                        {
                            long tmpMax;
                            if (conflict.Doc.TryGet("Max", out tmpMax) && tmpMax > highestMax)
                            {
                                highestMax = tmpMax;
                                hiloDocReader = conflict.Doc;
                            }
                        }
                    }

                    string serverPrefix;
                    if (serverPrefixDocReader != null &&
                        serverPrefixDocReader.TryGet("ServerPrefix", out serverPrefix))
                        prefix += serverPrefix;

                    if (hiloDocReader != null)
                    {
                        hiloDocReader.TryGet("Max", out oldMax);
                        var prop = new BlittableJsonReaderObject.PropertyDetails();
                        for (var i = 0; i < hiloDocReader.Count; i++)
                        {
                            hiloDocReader.GetPropertyByIndex(0, ref prop);
                            if (prop.Name == "Max")
                                continue;
                            newDoc[prop.Name] = prop.Value;
                        }
                    }
                }

                finally
                {
                    serverPrefixDocReader?.Dispose();
                    hiloDocReader?.Dispose();
                }
                oldMax = Math.Max(oldMax, LastRangeMax);

                newDoc["Max"] = oldMax + Capacity;

                using (
                    var freshHilo = context.ReadObject(newDoc, hiLoDocumentKey,
                        BlittableJsonDocumentBuilder.UsageMode.ToDisk))
                {
                    Database.DocumentsStorage.Put(context, hiLoDocumentKey, null, freshHilo);
                }

                OldMax = oldMax;
                Prefix = prefix;
                LastRangeAt = DateTime.UtcNow;
            }
        }

        [RavenAction("/databases/*/hilo/return", "GET",
            "/databases/{databaseName:string}/hilo/return?tag={collectionName:string}&end={lastGivenHigh:string}&last={lastIdUsed:string}")]
        public async Task HiLoReturn()
        {
            DocumentsOperationContext context;
            using (ContextPool.AllocateOperationContext(out context))
            {
                var tag = GetQueryStringValueAndAssertIfSingleAndNotEmpty("tag");
                var end = GetLongQueryString("end", required: true) ?? -1;
                var last = GetLongQueryString("last", required: true) ?? -1;

                var cmd = new MergedHiLoReturnCommand
                {
                    Database = Database,
                    Key = tag,
                    End = end,
                    Last = last
                };

                await Database.TxMerger.Enqueue(cmd);

                HttpContext.Response.StatusCode = 200;
            }
        }

        private class MergedHiLoReturnCommand : TransactionOperationsMerger.MergedTransactionCommand
        {
            public string Key;
            public DocumentDatabase Database;
            public long End;
            public long Last;

            public override void Execute(DocumentsOperationContext context, RavenTransaction tx)
            {
                var hiLoDocumentKey = ravenKeyGeneratorsHilo + Key;

                var document = Database.DocumentsStorage.Get(context, hiLoDocumentKey);

                if (document == null)
                    return;

                long oldMax;

                document.Data.TryGet("Max", out oldMax);

                if (oldMax != End || Last > oldMax)
                    return;

                document.Data.Modifications = new DynamicJsonValue()
                {
                    ["Max"] = Last,
                };

                using (var hiloReader = context.ReadObject(document.Data, hiLoDocumentKey, BlittableJsonDocumentBuilder.UsageMode.ToDisk))
                {
                    Database.DocumentsStorage.Put(context, hiLoDocumentKey, null, hiloReader);
                }
            }
        }

    }
}