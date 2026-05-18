using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NotesController : ControllerBase
    {
        private readonly INoteService _noteService;

        public NotesController(INoteService noteService)
        {
            _noteService = noteService;
        }

        [HttpGet("{googleId}")]
        public async Task<IActionResult> GetNotes(string googleId)
        {
            var authenticatedGoogleId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (authenticatedGoogleId != googleId)
            {
                return Forbid("No tienes permiso para ver las notas de otro usuario.");
            }

            try
            {
                var notes = await _noteService.GetNotesByGoogleIdAsync(googleId);
                return Ok(notes);
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateNote([FromBody] CreateNoteRequest request)
        {
            if (string.IsNullOrEmpty(request.GoogleId) || string.IsNullOrEmpty(request.Titulo) || string.IsNullOrEmpty(request.Contenido))
            {
                return BadRequest("Todos los campos (GoogleId, Titulo, Contenido) son requeridos.");
            }

            var authenticatedGoogleId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (authenticatedGoogleId != request.GoogleId)
            {
                return Forbid("No tienes permiso para crear notas a nombre de otro usuario.");
            }

            try
            {
                var nota = await _noteService.CreateNoteAsync(request.GoogleId, request.Titulo, request.Contenido);
                return Ok(nota);
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }

    public class CreateNoteRequest
    {
        public string GoogleId { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
    }
}
