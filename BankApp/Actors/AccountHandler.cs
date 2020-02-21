using BankApp.Events;
using BankApp.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankApp.Actors
{
    [JsonObject(MemberSerialization.OptIn)]
    public class AccountHandler
    {
        [JsonIgnore]
        private readonly CosmosClient cosmosClient;

        [JsonProperty("marker")]
        public MarkerCreated Marker { get; set; }

        [JsonProperty("balance")]
        public int Balance { get; set; }

        public AccountHandler(CosmosClient cosmosClient)
        {
            this.cosmosClient = cosmosClient;
        }
        public async Task PerformDeposit(Deposit deposit)
        {
            if (Marker != null)
            {
                var depositPerformedEvent = DepositPerformed.Create(deposit, Marker.Payload.LastSequence + 1, Entity.Current.EntityKey);
                var transactionPersisted = await PerformTransactionAsync<DepositPerformed, Deposit>(depositPerformedEvent).ConfigureAwait(false);
                Balance += transactionPersisted.Payload.Amount;
            }
        }


        public async Task PerformWithdraw(Withdraw withdraw)
        {
            if (Marker != null)
            {
                var withdrawPerformedEvent = WithdrawPerformed.Create(withdraw, Marker.Payload.LastSequence + 1, Entity.Current.EntityKey);
                var transactionPersisted = await PerformTransactionAsync<WithdrawPerformed, Withdraw>(withdrawPerformedEvent).ConfigureAwait(false);
                Balance -= transactionPersisted.Payload.Amount;
            }

        }

        public async Task CreateSnapshot()
        {
            if (Marker != null)
            {
                Snapshot snapshot = new Snapshot() { Balance = Balance };
                var snapshotCreatedEvent = SnapshotCreated.Create(snapshot, Marker.Payload.LastSequence + 1, Entity.Current.EntityKey);
                var transactionPersisted = await PerformTransactionAsync<SnapshotCreated, Snapshot>(snapshotCreatedEvent).ConfigureAwait(false);
                Balance = transactionPersisted.Payload.Balance;
            }

        }


        [FunctionName(nameof(AccountHandler))]
        public Task Run([EntityTrigger] IDurableEntityContext ctx)
        {
            if (!ctx.HasState)
            {
                Container container = cosmosClient.GetContainer("BankDB", "GenerousBank");
                var marker = CreateIfNotExistsAndReadMarker(container).GetAwaiter().GetResult();
                int balance = ReplayFromSnapshot(container).GetAwaiter().GetResult();

                ctx.SetState(new AccountHandler(cosmosClient) { Marker = marker, Balance = balance });
            }
            return ctx.DispatchAsync<AccountHandler>();
        }

        private async Task<TEvent> PerformTransactionAsync<TEvent, TPayload>(TEvent parameter) where TEvent : ITransaction<TPayload>
        {
            Marker.Payload.LastSequence = parameter.Version;

            var batchOptions = new TransactionalBatchItemRequestOptions()
            {
                IfMatchEtag = Marker.Etag
            };

            Container container = cosmosClient.GetContainer("BankDB", "GenerousBank");
            var batch = container.CreateTransactionalBatch(new PartitionKey(Entity.Current.EntityKey))
                                 .ReplaceItem<MarkerCreated>(Marker.Id, Marker, batchOptions)
                                 .UpsertItem<TEvent>(parameter);

            TransactionalBatchResponse transactionResult = await batch.ExecuteAsync()
                                                                      .ConfigureAwait(false);

            if (transactionResult.IsSuccessStatusCode)
            {
                Marker = transactionResult.GetOperationResultAtIndex<MarkerCreated>(0).Resource;
                var transactionPersisted = transactionResult.GetOperationResultAtIndex<TEvent>(1).Resource;
                return transactionPersisted;
            }
            else
            {
                throw new Exception(transactionResult.ErrorMessage);
            }
        }

        private async Task<MarkerCreated> CreateIfNotExistsAndReadMarker(Container container)
        {
            MarkerCreated markerCreatedEvent;
            try
            {
                markerCreatedEvent = await container.ReadItemAsync<MarkerCreated>("_metadata", new PartitionKey(Entity.Current.EntityKey)).ConfigureAwait(false);
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Marker marker = new Marker()
                    {
                        LastSequence = 0
                    };
                    markerCreatedEvent = await container.CreateItemAsync(MarkerCreated.Create(marker, 0, Entity.Current.EntityKey), new PartitionKey(Entity.Current.EntityKey)).ConfigureAwait(false);
                }
                else
                {
                    throw;
                }
            }
            return markerCreatedEvent;

        }

        private async Task<int> ReplayFromSnapshot(Container container)
        {
            int balance = 0;

            QueryDefinition lastestSnapshotQuery = new QueryDefinition("SELECT TOP 1 * FROM c where c.eventType = @eventType and c.partitionKey = @partitionKey order by c.id desc")
                                                           .WithParameter("@eventType", "SnapshotCreated")
                                                           .WithParameter("@partitionKey", Entity.Current.EntityKey);

            FeedIterator<SnapshotCreated> setIteratorSnapshot = container.GetItemQueryIterator<SnapshotCreated>(lastestSnapshotQuery, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(Entity.Current.EntityKey), MaxItemCount = 1 });
            while (setIteratorSnapshot.HasMoreResults)
            {
                FeedResponse<SnapshotCreated> snapshotItemFeed = await setIteratorSnapshot.ReadNextAsync().ConfigureAwait(false);
                var snapshot = snapshotItemFeed.Resource.FirstOrDefault();
                if (snapshot != null)
                {
                    balance = snapshot.Payload.Balance;

                    QueryDefinition everythingAfterSnapshotQuery = new QueryDefinition("SELECT * FROM c where c.version > @snapshot and c.partitionKey = @partitionKey order by c.id")
                                                                  .WithParameter("@snapshot", snapshot.Version)
                                                                  .WithParameter("@partitionKey", Entity.Current.EntityKey);

                    FeedIterator<dynamic> setIteratorReplay = container.GetItemQueryIterator<dynamic>(everythingAfterSnapshotQuery, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(Entity.Current.EntityKey) });
                    while (setIteratorReplay.HasMoreResults)
                    {
                        foreach (FeedResponse<dynamic> replayItemFeed in await setIteratorReplay.ReadNextAsync().ConfigureAwait(false))
                        {
                            var replayItem = replayItemFeed.Resource.FirstOrDefault();
                            if (replayItem != null)
                            {
                                if (replayItem.eventType.ToString() == "DepositPerformed")
                                {
                                    balance += (int)replayItem.payload.amount;
                                }
                                else if (replayItem.eventType.ToString() == "WithdrawPerformed")
                                {
                                    balance -= (int)replayItem.payload.amount;
                                }
                            }
                        }
                    }
                }
            }

            return balance;
        }

    }
}
