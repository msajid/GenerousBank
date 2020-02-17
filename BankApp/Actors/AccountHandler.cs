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
using System.Text;
using System.Threading.Tasks;

namespace BankApp.Actors
{
    [JsonObject(MemberSerialization.OptIn)]
    public class AccountHandler
    {
        [JsonIgnore]
        private readonly CosmosClient cosmosClient;

        [JsonProperty("state")]
        public State State { get; set; }

        public AccountHandler(CosmosClient cosmosClient)
        {
            this.cosmosClient = cosmosClient;
        }
        public async Task CreateAccount(Account account)
        {
            ValidateMatchingAccountNumberAndEntityKey(account?.AccountNumber);

            var accountCreatedEvent = AccountCreated.Create(account, State.Metadata.Payload.LastSequence + 1);

            State.Metadata.Payload.LastSequence = accountCreatedEvent.Version;

            var batchOptions = new TransactionalBatchItemRequestOptions()
            {
                IfMatchEtag = State.Metadata.Etag
            };

            Container container = cosmosClient.GetContainer("BankDB", "GenerousBank");
            var batch = container.CreateTransactionalBatch(new PartitionKey(Entity.Current.EntityKey))
                                 .ReplaceItem<MetadataCreated>(State.Metadata.Id, State.Metadata, batchOptions)
                                 .UpsertItem<AccountCreated>(accountCreatedEvent);

            var transactionResult = await batch.ExecuteAsync().ConfigureAwait(false);

            if (transactionResult.IsSuccessStatusCode)
            {
                State.Metadata = transactionResult.GetOperationResultAtIndex<MetadataCreated>(0).Resource;
                State.Account = transactionResult.GetOperationResultAtIndex<AccountCreated>(1).Resource;
            }
            else
            {
                throw new Exception(transactionResult.ErrorMessage);
            }

        }

        public async Task PerformDeposit(Deposit deposit)
        {
            ValidateMatchingAccountNumberAndEntityKey(deposit?.AccountNumber);

            var depositPerformedEvent = DepositPerformed.Create(deposit, State.Metadata.Payload.LastSequence + 1);

            State.Metadata.Payload.LastSequence = depositPerformedEvent.Version;

            var batchOptions = new TransactionalBatchItemRequestOptions()
            {
                IfMatchEtag = State.Metadata.Etag
            };

            Container container = cosmosClient.GetContainer("BankDB", "GenerousBank");
            var batch = container.CreateTransactionalBatch(new PartitionKey(Entity.Current.EntityKey))
                                 .ReplaceItem<MetadataCreated>(State.Metadata.Id, State.Metadata, batchOptions)
                                 .UpsertItem<DepositPerformed>(depositPerformedEvent);

            TransactionalBatchResponse transactionResult = await batch.ExecuteAsync()
                                                                      .ConfigureAwait(false);

            if (transactionResult.IsSuccessStatusCode)
            {
                State.Metadata = transactionResult.GetOperationResultAtIndex<MetadataCreated>(0).Resource;
                var depositPersisted = transactionResult.GetOperationResultAtIndex<DepositPerformed>(1).Resource;
                State.Balance += depositPersisted.Payload.Amount;
            }
            else
            {
                throw new Exception(transactionResult.ErrorMessage);
            }
        }


        public async Task PerformWithdraw(Withdraw withdraw)
        {

            ValidateMatchingAccountNumberAndEntityKey(withdraw?.AccountNumber);

            var withdrawPerformedEvent = WithdrawPerformed.Create(withdraw, State.Metadata.Payload.LastSequence + 1);

            State.Metadata.Payload.LastSequence = withdrawPerformedEvent.Version;

            var batchOptions = new TransactionalBatchItemRequestOptions()
            {
                IfMatchEtag = State.Metadata.Etag
            };

            Container container = cosmosClient.GetContainer("BankDB", "GenerousBank");
            var batch = container.CreateTransactionalBatch(new PartitionKey(Entity.Current.EntityKey))
                                 .ReplaceItem<MetadataCreated>(State.Metadata.Id, State.Metadata, batchOptions)
                                 .UpsertItem<WithdrawPerformed>(withdrawPerformedEvent);

            TransactionalBatchResponse transactionResult = await batch.ExecuteAsync()
                                                                      .ConfigureAwait(false);

            if (transactionResult.IsSuccessStatusCode)
            {
                State.Metadata = transactionResult.GetOperationResultAtIndex<MetadataCreated>(0).Resource;
                var withdrawPersisted = transactionResult.GetOperationResultAtIndex<WithdrawPerformed>(1).Resource;
                State.Balance -= withdrawPersisted.Payload.Amount;
            }
            else
            {
                throw new Exception(transactionResult.ErrorMessage);
            }
        }




        private static void ValidateMatchingAccountNumberAndEntityKey(string accountNumber)
        {
            if (Entity.Current.EntityKey != accountNumber)
            {
                throw new InvalidOperationException("AccountNumber doesn't match the Entity Key");
            }
        }
        public async Task CreateSnapshot()
        {

            Container container = cosmosClient.GetContainer("BankDB", "GenerousBank");
            MetadataCreated metadataCreated = await container.ReadItemAsync<MetadataCreated>("_metadata", new PartitionKey(Entity.Current.EntityKey)).ConfigureAwait(false);

            Snapshot newSnapshot = null;// ReplayFromLastSnapshot(metadataCreated.Payload.LastSnapshot);

            var snapshotCreatedEvent = new SnapshotCreated()
            {
                Payload = newSnapshot,
                EventType = "SnapshotCreated",
                Version = metadataCreated.Payload.LastSnapshot + 1,
                Timestamp = DateTime.UtcNow,
                Id = Guid.NewGuid().ToString(),
                PartitionKey = Entity.Current.EntityKey

            };

            metadataCreated.Payload.LastSnapshot = snapshotCreatedEvent.Version;

            var batch = container.CreateTransactionalBatch(new PartitionKey(Entity.Current.EntityKey))
                                 .ReplaceItem<MetadataCreated>(metadataCreated.Id, metadataCreated)
                                 .CreateItem<SnapshotCreated>(snapshotCreatedEvent);

            TransactionalBatchResponse transactionResult = await batch.ExecuteAsync()
                                                                      .ConfigureAwait(false);

            if (!transactionResult.IsSuccessStatusCode)
            {
                throw new Exception(transactionResult.ErrorMessage);
            }
        }

        //private async Task<Snapshot> ReplayFromLastSnapshot(int lastSnapshot)
        //{

        //    if (lastSnapshot == 0)
        //    {
        //        Container container = cosmosClient.GetContainer("BankDB", "GenerousBank");
        //        QueryDefinition deposits = new QueryDefinition("SELECT VALUE SUM(c.payload.amount) FROM c where c.eventType = @criteria")
        //                                                    .WithParameter("@criteria", "DepositPerformed");


        //        FeedIterator<JObject> setIterator = container.GetItemQueryIterator<JObject>(deposits, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(Entity.Current.EntityKey) });
        //        while (setIterator.HasMoreResults)
        //        {
        //            int amount = 0;
        //            foreach (JObject item in await setIterator.ReadNextAsync().ConfigureAwait(false))
        //            {
        //                amount += item["payload"]["amount"];
        //            }
        //        }

        //    }
        //    else
        //    {
        //    }
        //}

        [FunctionName(nameof(AccountHandler))]
        public Task Run([EntityTrigger] IDurableEntityContext ctx)
        {
            if (!ctx.HasState)
            {
                MetadataCreated metadataCreated = null;
                AccountCreated accountCreated = null;

                Container container = cosmosClient.GetContainer("BankDB", "GenerousBank");

                try
                {
                    var batch = container.CreateTransactionalBatch(new PartitionKey(Entity.Current.EntityKey))
                                         .ReadItem("_metadata")
                                         .ReadItem("1");

                    TransactionalBatchResponse transactionResult = batch.ExecuteAsync()
                                                                        .GetAwaiter()
                                                                        .GetResult();

                    if (transactionResult.IsSuccessStatusCode)
                    {
                        metadataCreated = transactionResult.GetOperationResultAtIndex<MetadataCreated>(0).Resource;
                        accountCreated = transactionResult.GetOperationResultAtIndex<AccountCreated>(1).Resource;
                    }
                    else
                    {
                        throw new CosmosException("batch failed", transactionResult.StatusCode, 404, "1", 1);
                    }


                }
                catch (CosmosException ex)
                {
                    if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        var metadata = new MetadataCreated()
                        {
                            PartitionKey = Entity.Current.EntityKey,
                            Timestamp = DateTime.UtcNow,
                            Version = 0,
                            Payload = new Metada()
                            {
                                AccountNumber = Entity.Current.EntityKey,
                                LastSequence = 0,
                                LastSnapshot = 0
                            }
                        };

                        var result = container.CreateItemAsync(metadata, new PartitionKey(Entity.Current.EntityKey)).GetAwaiter().GetResult();
                        metadataCreated = result.Resource;
                    }
                    else
                    {
                        throw;
                    }
                }

                ctx.SetState(new AccountHandler(cosmosClient) { State = new State() { Account = accountCreated, Balance = 0, Metadata = metadataCreated } });
            }
            return ctx.DispatchAsync<AccountHandler>();
        }

    }
}
