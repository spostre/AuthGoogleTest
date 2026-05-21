using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotesController : ControllerBase
{
    private readonly INoteService _noteService;
    private readonly IUserService _userService;

    public NotesController(INoteService noteService, IUserService userService)
    {
        _noteService = noteService;
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotes()
    {
        var userId = await ResolveCurrentUserIdAsync();
        if (userId == null)
        {
            return Unauthorized("Debes completar tu registro para ver tus notas.");
        }

        try
        {
            var notes = await _noteService.GetNotesByUserIdAsync(userId.Value);
            return Ok(notes);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateNote([FromBody] CreateNoteRequest request)
    {
        if (string.IsNullOrEmpty(request.Titulo) || string.IsNullOrEmpty(request.Contenido))
        {
            return BadRequest("Titulo y Contenido son requeridos.");
        }

        var userId = await ResolveCurrentUserIdAsync();
        if (userId == null)
        {
            return Unauthorized("Debes completar tu registro para crear notas.");
        }

        try
        {
            var nota = await _noteService.CreateNoteAsync(userId.Value, request.Titulo, request.Contenido);
            return Ok(nota);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateNote(int id, [FromBody] UpdateNoteRequest request)
    {
        if (string.IsNullOrEmpty(request.Titulo) || string.IsNullOrEmpty(request.Contenido))
        {
            return BadRequest("Titulo y Contenido son requeridos.");
        }

        var userId = await ResolveCurrentUserIdAsync();
        if (userId == null)
        {
            return Unauthorized();
        }

        var nota = await _noteService.UpdateNoteAsync(id, userId.Value, request.Titulo, request.Contenido);
        if (nota == null)
        {
            return NotFound("La nota no existe o no te pertenece.");
        }

        return Ok(nota);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteNote(int id)
    {
        var userId = await ResolveCurrentUserIdAsync();
        if (userId == null)
        {
            return Unauthorized();
        }

        var deleted = await _noteService.DeleteNoteAsync(id, userId.Value);
        if (!deleted)
        {
            return NotFound("La nota no existe o no te pertenece.");
        }

        return Ok(new { deleted = true });
    }

    private async Task<int?> ResolveCurrentUserIdAsync()
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(subject))
        {
            return null;
        }

        return await _userService.ResolveUserIdFromSubjectAsync(subject);
    }
}

public class CreateNoteRequest
{
    public string Titulo { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
}

public class UpdateNoteRequest
{
    public string Titulo { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
}
