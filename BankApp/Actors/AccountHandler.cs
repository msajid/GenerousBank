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
        public MetadataCreated State { get; set; }

        [JsonProperty("balance")]
        public int Balance { get; set; }

        public AccountHandler(CosmosClient cosmosClient)
        {
            this.cosmosClient = cosmosClient;
        }
        public async Task CreateAccount(Account account)
        {
            ValidateMatchingAccountNumberAndEntityKey(account?.AccountNumber);

            var accountCreatedEvent = AccountCreated.Create(account, State.Payload.LastSequence + 1);

            MetadataCreated newState = MetadataCreated.Clone(State);

            newState.Payload.LastSequence = accountCreatedEvent.Version;

            var batchOptions = new TransactionalBatchItemRequestOptions()
            {
                IfMatchEtag = State.Etag
            };

            Container container = cosmosClient.GetContainer("BankDB", "GenerousBank");
            var batch = container.CreateTransactionalBatch(new PartitionKey(Entity.Current.EntityKey))
                                 .ReplaceItem<MetadataCreated>(newState.Id, newState, batchOptions)
                                 .UpsertItem<AccountCreated>(accountCreatedEvent);

            var transactionResult = await batch.ExecuteAsync().ConfigureAwait(false);

            if (transactionResult.IsSuccessStatusCode)
            {
                State = transactionResult.GetOperationResultAtIndex<MetadataCreated>(0).Resource;
            }
            else
            {
                throw new Exception(transactionResult.ErrorMessage);
            }

        }

        public async Task PerformDeposit(Deposit deposit)
        {
            ValidateMatchingAccountNumberAndEntityKey(deposit?.AccountNumber);

            var depositPerformedEvent = DepositPerformed.Create(deposit, State.Payload.LastSequence + 1);

            MetadataCreated newState = MetadataCreated.Clone(State);
            newState.Payload.LastSequence = depositPerformedEvent.Version;


            var batchOptions = new TransactionalBatchItemRequestOptions()
            {
                IfMatchEtag = State.Etag
            };

            Container container = cosmosClient.GetContainer("BankDB", "GenerousBank");
            var batch = container.CreateTransactionalBatch(new PartitionKey(Entity.Current.EntityKey))
                                 .ReplaceItem<MetadataCreated>(newState.Id, newState, batchOptions)
                                 .UpsertItem<DepositPerformed>(depositPerformedEvent);

            TransactionalBatchResponse transactionResult = await batch.ExecuteAsync()
                                                                      .ConfigureAwait(false);

            if (transactionResult.IsSuccessStatusCode)
            {
                State = transactionResult.GetOperationResultAtIndex<MetadataCreated>(0).Resource;
                var depositPersisted = transactionResult.GetOperationResultAtIndex<DepositPerformed>(1).Resource;
                Balance += depositPersisted.Payload.Amount;
            }
            else
            {
                throw new Exception(transactionResult.ErrorMessage);
            }
        }


        public async Task PerformWithdraw(Withdraw withdraw)
        {

            ValidateMatchingAccountNumberAndEntityKey(withdraw?.AccountNumber);

            var withdrawPerformedEvent = WithdrawPerformed.Create(withdraw, State.Payload.LastSequence + 1);

            MetadataCreated newState = MetadataCreated.Clone(State);
            newState.Payload.LastSequence = withdrawPerformedEvent.Version;

            var batchOptions = new TransactionalBatchItemRequestOptions()
            {
                IfMatchEtag = State.Etag
            };

            Container container = cosmosClient.GetContainer("BankDB", "GenerousBank");
            var batch = container.CreateTransactionalBatch(new PartitionKey(Entity.Current.EntityKey))
                                 .ReplaceItem<MetadataCreated>(newState.Id, newState, batchOptions)
                                 .UpsertItem<WithdrawPerformed>(withdrawPerformedEvent);

            TransactionalBatchResponse transactionResult = await batch.ExecuteAsync()
                                                                      .ConfigureAwait(false);

            if (transactionResult.IsSuccessStatusCode)
            {
                State = transactionResult.GetOperationResultAtIndex<MetadataCreated>(0).Resource;
                var withdrawPersisted = transactionResult.GetOperationResultAtIndex<WithdrawPerformed>(1).Resource;
                Balance -= withdrawPersisted.Payload.Amount;
            }
            else
            {
                throw new Exception(transactionResult.ErrorMessage);
            }
        }

        public async Task CreateSnapshot()
        {
            Snapshot snapshot = new Snapshot() { Balance = Balance, AccountNumber = State.Payload.AccountNumber };
            var snapshotCreatedEvent = SnapshotCreated.Create(snapshot, State.Payload.LastSequence + 1);

            MetadataCreated newState = MetadataCreated.Clone(State);
            newState.Payload.LastSequence = snapshotCreatedEvent.Version;

            var batchOptions = new TransactionalBatchItemRequestOptions()
            {
                IfMatchEtag = State.Etag
            };

            Container container = cosmosClient.GetContainer("BankDB", "GenerousBank");
            var batch = container.CreateTransactionalBatch(new PartitionKey(Entity.Current.EntityKey))
                                 .ReplaceItem<MetadataCreated>(newState.Id, newState, batchOptions)
                                 .UpsertItem<SnapshotCreated>(snapshotCreatedEvent);

            TransactionalBatchResponse transactionResult = await batch.ExecuteAsync()
                                                                      .ConfigureAwait(false);

            if (transactionResult.IsSuccessStatusCode)
            {
                State = transactionResult.GetOperationResultAtIndex<MetadataCreated>(0).Resource;
                var snapShotpersisted = transactionResult.GetOperationResultAtIndex<SnapshotCreated>(1).Resource;
                Balance = snapShotpersisted.Payload.Balance;
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
                        Metadata metadata = new Metadata()
                        {
                            AccountNumber = Entity.Current.EntityKey,
                            LastSequence = 0
                        };

                        var metadatCreatedEvent = MetadataCreated.Create(metadata, 0);

                        Account account = new Account()
                        {
                            AccountNumber = Entity.Current.EntityKey
                        };

                        var accountCreatedEvent = AccountCreated.Create(account, 1);

                        var batch = container.CreateTransactionalBatch(new PartitionKey(Entity.Current.EntityKey))
                                          .UpsertItem(metadatCreatedEvent)
                                          .UpsertItem(accountCreatedEvent);

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
                    else
                    {
                        throw;
                    }
                }

                ctx.SetState(new AccountHandler(cosmosClient) { State = metadataCreated  });
            }
            return ctx.DispatchAsync<AccountHandler>();
        }

    }
}
