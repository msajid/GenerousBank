using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;


[assembly: FunctionsStartup(typeof(BankApp.Startup))]

namespace BankApp
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            CosmosClient client = new CosmosClientBuilder("https://localhost:8081", "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==")
                                        .WithApplicationName("GenerousBank")
                                        .WithSerializerOptions(new CosmosSerializationOptions() 
                                                                    { 
                                                                        IgnoreNullValues = true, 
                                                                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase 
                                                                    } )
                                        .Build();
          
            Database database = client.CreateDatabaseIfNotExistsAsync("BankDB").GetAwaiter().GetResult();
            Container container = database.CreateContainerIfNotExistsAsync(
                "GenerousBank",
                "/partitionKey",
                400).GetAwaiter().GetResult();                                                                          

            builder.Services.AddSingleton(client);
        }
    }
}

