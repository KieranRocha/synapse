using CADCompanion.Server.Services;
using CADCompanion.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CADCompanion.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MachinesController : ControllerBase
    {
        private readonly IMachineService _machineService;
        private readonly ILogger<MachinesController> _logger;

        public MachinesController(IMachineService machineService, ILogger<MachinesController> logger)
        {
            _machineService = machineService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MachineSummaryDto>>> GetAllMachines()
        {
            try
            {
                var machines = await _machineService.GetAllMachinesAsync();
                return Ok(machines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all machines");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MachineDto>> GetMachineById(int id)
        {
            try
            {
                var machine = await _machineService.GetMachineByIdAsync(id);
                if (machine == null)
                {
                    return NotFound();
                }
                return Ok(machine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting machine with id {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<MachineDto>> CreateMachine([FromBody] CreateMachineDto createMachineDto)
        {
            if (createMachineDto == null)
            {
                return BadRequest("Machine data is null.");
            }

            try
            {
                var newMachine = await _machineService.CreateMachineAsync(createMachineDto);
                return CreatedAtAction(nameof(GetMachineById), new { id = newMachine.Id }, newMachine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating new machine");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}