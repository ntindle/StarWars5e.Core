﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.WindowsAzure.Storage.Table;
using StarWars5e.Api.Interfaces;
using StarWars5e.Models.Class;
using StarWars5e.Models.Search;
using Wolnik.Azure.TableStorage.Repository;

namespace StarWars5e.Api.Controllers
{
    [Route("api/archetype")]
    [ApiController]
    public class ArchetypeController : ControllerBase
    {
        private readonly ITableStorage _tableStorage;
        private readonly IArchetypeManager _archetypeManager;

        public ArchetypeController(ITableStorage tableStorage, IArchetypeManager archetypeManager)
        {
            _tableStorage = tableStorage;
            _archetypeManager = archetypeManager;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Archetype>>> Get()
        {
            var archetypes = await _tableStorage.GetAllAsync<Archetype>("archetypes");
            return Ok(archetypes);
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<PagedSearchResult<Archetype>>>> Get([FromQuery] ArchetypeSearch archetypeSearch)
        {
            var archetypes = await _archetypeManager.SearchArchetypes(archetypeSearch);

            return Ok(archetypes);
        }

        [HttpPost]
        public async Task Post([FromBody] Archetype archetype)
        {
            await _tableStorage.AddOrUpdateAsync("archetypes", archetype);
        }

        [HttpDelete("{name}")]
        public async Task Delete(string name)
        {
            var query = new TableQuery<Archetype>();
            query.Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, name));

            var archetypes = await _tableStorage.QueryAsync("archetypes", query);
            foreach (var archetype in archetypes)
            {
                await _tableStorage.DeleteAsync("archetypes", archetype);
            }
        }
    }
}