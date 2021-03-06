﻿using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using StarWars5e.Models.Enums;
using StarWars5e.Models.Starship;
using StarWars5e.Models.Utils;

namespace StarWars5e.Parser.Processors.SOTG
{
    public class StarshipDeploymentProcessor : BaseProcessor<StarshipDeployment>
    {
        public override Task<List<StarshipDeployment>> FindBlocks(List<string> lines)
        {
            var startingIndex = lines.FindIndex(f => f == Localization.DeploymentsStart);
            var deploymentLines = lines.Skip(startingIndex).ToList();

            return Task.FromResult(CreateDeployments(deploymentLines));
        }

        private List<StarshipDeployment> CreateDeployments(List<string> deploymentChapterLines)
        {
            var deployments = new List<StarshipDeployment>();

            var deploymentDescriptionTableStartIndex = deploymentChapterLines.FindIndex(f => f == Localization.DeploymentsStart);
            var deploymentDescriptionTableEndIndex = deploymentChapterLines.FindIndex(deploymentDescriptionTableStartIndex + 3, string.IsNullOrWhiteSpace);
            var deploymentDescriptionTableLines = deploymentChapterLines.Skip(deploymentDescriptionTableStartIndex + 3)
                .Take(deploymentDescriptionTableEndIndex - (deploymentDescriptionTableStartIndex + 3)).ToList();

            foreach (var deploymentTableLine in deploymentDescriptionTableLines)
            {
                var deploymentTableColumns = deploymentTableLine.Split('|');
                var deployment = new StarshipDeployment
                {
                    PartitionKey = ContentType.Core.ToString(),
                    RowKey = deploymentTableColumns[1],
                    Name = deploymentTableColumns[1],
                    Description = deploymentTableColumns[2].Trim()
                };

                var deploymentStart = deploymentChapterLines.FindIndex(f => f == Localization.GetDeploymentTableStart(deployment.Name));
                var deploymentEnd = deploymentChapterLines.FindIndex(deploymentStart + 1, f => f.StartsWith("## ")) == -1
                    ? deploymentChapterLines.Count
                    : deploymentChapterLines.FindIndex(deploymentStart + 1, f => f.StartsWith("## "));
                var deploymentLines =
                    deploymentChapterLines.Skip(deploymentStart).Take(deploymentEnd - deploymentStart).ToList();

                var deploymentTableStart = deploymentLines.FindIndex(f => f == $"##### The {deployment.Name}");
                var deploymentTableEnd = deploymentLines.FindIndex(deploymentTableStart + 3, string.IsNullOrWhiteSpace);
                var deploymentFeatTableLines = deploymentLines.Skip(deploymentTableStart + 3)
                    .Take(deploymentTableEnd - (deploymentTableStart + 3)).ToList();

                deployment.FeatureText = string.Join("\r\n", deploymentLines.Skip(deploymentTableStart).CleanListOfStrings());

                foreach (var line in deploymentFeatTableLines)
                {
                    var splitFeatTable = line.Split('|');
                    var features = splitFeatTable[2].Split(',');
                    var baseFeats = features.Select(featName => new StarshipFeature
                    {
                        Name = Regex.Replace(featName.Trim(), @"[^\u0000-\u007F]+", string.Empty),
                        Tier = int.Parse(Regex.Match(splitFeatTable[1], @"\d+").Value)
                    }).ToList();
                    deployment.Features.AddRange(baseFeats);
                }

                foreach (var deploymentFeature in deployment.Features)
                {
                    var currentFeatNameLineIndex = deploymentLines.FindIndex(f => f == $"### {deploymentFeature.Name}");
                    var nextFeatNameLineIndex =
                        deploymentLines.FindIndex(currentFeatNameLineIndex + 1, f => f.StartsWith("### ")) == -1
                            ? deploymentLines.Count
                            : deploymentLines.FindIndex(currentFeatNameLineIndex + 1, f => f.StartsWith("### "));
                    var starshipFeatureContentLines = deploymentLines.Skip(currentFeatNameLineIndex + 1)
                        .Take(nextFeatNameLineIndex - (currentFeatNameLineIndex + 1)).CleanListOfStrings();

                    deploymentFeature.Content = string.Join("\r\n", starshipFeatureContentLines);
                }

                deployment.FlavorText = string.Join("\r\n",
                    deploymentLines.Skip(1).Take(deploymentTableStart - 1)
                        .CleanListOfStrings());

                deployments.Add(deployment);
            }

            return deployments;
        }
    } 
}
