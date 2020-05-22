﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAzure.Storage;
using StarWars5e.Models;
using StarWars5e.Models.Enums;
using StarWars5e.Parser.Managers;
using Wolnik.Azure.TableStorage.Repository;

namespace StarWars5e.Parser
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appSettings.json", true, true)
                .AddUserSecrets<Program>()
                .Build();

            var tableStorage = new AzureTableStorage(config["StorageAccountConnectionString"]);
            
            var storageAccount = CloudStorageAccount.Parse(config["StorageAccountConnectionString"]);
            var globalSearchTermRepository = new GlobalSearchTermRepository();

            var serviceProvider = new ServiceCollection()
                .AddSingleton<ITableStorage>(tableStorage)
                .AddSingleton(storageAccount)
                .AddSingleton(globalSearchTermRepository)
                .BuildServiceProvider();

            var starshipManager = new StarshipsOfTheGalaxyManager(serviceProvider.GetService<ITableStorage>(),
                serviceProvider.GetService<CloudStorageAccount>(),
                serviceProvider.GetService<GlobalSearchTermRepository>());
            var monsterManualManager = new MonsterManualManager(serviceProvider.GetService<ITableStorage>(),
                serviceProvider.GetService<GlobalSearchTermRepository>());
            var extendedContentSpeciesManager = new ExpandedContentSpeciesManager(
                serviceProvider.GetService<ITableStorage>(), serviceProvider.GetService<GlobalSearchTermRepository>());
            var extendedContentBackgroundManager = new ExpandedContentBackgroundsManager(
                serviceProvider.GetService<ITableStorage>(), serviceProvider.GetService<GlobalSearchTermRepository>());
            var extendedContentEquipmentManager = new ExpandedContentEquipmentManager(
                serviceProvider.GetService<ITableStorage>(), serviceProvider.GetService<GlobalSearchTermRepository>());
            var extendedContentArchetypesManager = new ExpandedContentArchetypesManager(
                serviceProvider.GetService<ITableStorage>(), serviceProvider.GetService<GlobalSearchTermRepository>());
            var extendedContentVariantRulesManager =
                new ExpandedContentVariantRulesManager(serviceProvider.GetService<CloudStorageAccount>());
            var extendedContentCustomizationOptionsManager = new ExpandedContentCustomizationOptionsManager(
                serviceProvider.GetService<ITableStorage>(),
                serviceProvider.GetService<GlobalSearchTermRepository>());
            var extendedContentForcePowersManager = new ExpandedContentForcePowersManager(
                serviceProvider.GetService<ITableStorage>(), serviceProvider.GetService<GlobalSearchTermRepository>());
            var playerHandbookManager = new PlayerHandbookManager(serviceProvider.GetService<ITableStorage>(),
                serviceProvider.GetService<CloudStorageAccount>(),
                serviceProvider.GetService<GlobalSearchTermRepository>());
            var referenceTableManager = new ReferenceTableManager(serviceProvider.GetService<ITableStorage>());
            var searchManager = new SearchManager(serviceProvider.GetService<ITableStorage>(),
                serviceProvider.GetService<GlobalSearchTermRepository>());
            var wretchedHivesManager = new WretchedHivesManager(serviceProvider.GetService<ITableStorage>(),
                serviceProvider.GetService<CloudStorageAccount>(),
                serviceProvider.GetService<GlobalSearchTermRepository>());
            var creditsManager = new CreditsManager(serviceProvider.GetService<CloudStorageAccount>());
            var extendedContentEnhancedItemManager = new ExpandedContentEnhancedItemsManager(
                serviceProvider.GetService<ITableStorage>(), serviceProvider.GetService<GlobalSearchTermRepository>());

            var referenceTables = await referenceTableManager.Parse();
            await starshipManager.Parse(referenceTables);
            await monsterManualManager.Parse();
            await extendedContentSpeciesManager.Parse();
            await extendedContentBackgroundManager.Parse();
            await extendedContentEquipmentManager.Parse();
            await extendedContentArchetypesManager.Parse();
            await extendedContentVariantRulesManager.Parse();
            await extendedContentCustomizationOptionsManager.Parse();
            await extendedContentForcePowersManager.Parse();
            await wretchedHivesManager.Parse();
            await playerHandbookManager.Parse();
            await creditsManager.Parse();
            await extendedContentEnhancedItemManager.Parse();

            try
            {
                await searchManager.Upload();
            }
            catch (StorageException)
            {
                var searchTerms = serviceProvider.GetService<GlobalSearchTermRepository>().SearchTerms;
                var dupes = searchTerms
                    .GroupBy(i => i.RowKey)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);

                foreach (var dupe in dupes)
                {
                    Console.WriteLine($"Dupe: {dupe}");
                }

                var nonConformingNames = searchTerms.Where(s => Regex.IsMatch(s.RowKey, @"[\\]|[/]|[#]|[?] "));
                foreach (var nonConformingName in nonConformingNames)
                {
                    Console.WriteLine($"Non-conforming: {nonConformingName}");
                }

                Console.WriteLine("Failed to upload search terms.");
            }

            try
            {
                var tableStorageRepository = serviceProvider.GetService<ITableStorage>();

                var currentVersion =
                    (await tableStorageRepository.GetAsync<DataVersion>("dataVersion", ContentType.Core.ToString(),
                        "MASTERVERSION")).Version;

                var dataNames = new List<string>
                {
                    "MASTERVERSION", "archetypes", "armorProperties", "backgrounds", "classes", "credits", "enhancedItems", "equipment",
                    "feats", "features", "monsters", "powers", "referenceTables", "species", "starshipBaseSizes",
                    "starshipDeployments", "starshipEquipment", "starshipModifications", "starshipVentures", "weaponProperties",
                    "player-handbook-rules", "starships-rules", "variant-rules", "wretched-hives-rules",
                    "characterAdvancementLU", "conditionsLU", "featureDataLU", "featureLevelLU", "skillsLU"
                };
                var dataVersions = dataNames.Select(d => new DataVersion
                {
                    LastUpdated = DateTime.Now,
                    Name = d,
                    PartitionKey = ContentType.Core.ToString(),
                    RowKey = d,
                    Version = currentVersion + 1
                });

                await tableStorageRepository.AddBatchAsync<DataVersion>("dataVersion", dataVersions,
                    new BatchOperationOptions {BatchInsertMethod = BatchInsertMethod.InsertOrReplace});
            }
            catch (StorageException)
            {
                Console.WriteLine("Failed to update data versions.");
            }
        }
    }
}
