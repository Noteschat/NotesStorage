using Microsoft.AspNetCore.Mvc;
using NotesStorage.Managers;
using System.Text.Json;

namespace NotesStorage.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class StorageController : Controller
    {
        private readonly NotesManager _notes;

        public StorageController(NotesManager notes)
        {
            _notes = notes;
        }

        [HttpGet("{chatId}")]
        public async Task<dynamic> GetAll(string chatId)
        {
            if (!HttpContext.Items.TryGetValue("user", out var userObj) ||
            userObj is not User user)
            {
                return StatusCode(500, new { cause = "invalid user" });
            }

            var result = await _notes.GetAll(chatId);
            return result.Match<ActionResult>(
                chats => {
                    var res = chats.Select(chat => new AllNotesResponse
                    {
                        Id = chat.Id,
                        Name = chat.Name,
                    }).ToList();
                    return StatusCode(200, new { notes = res });
                },
                error =>
                {
                    switch (error)
                    {
                        case NotesError.Unauthorized:
                            return StatusCode(401, new { cause = "not logged in" });
                        case NotesError.WrongFormatInDatabase:
                            return StatusCode(500, new { cause = "conversion failed" });
                        default:
                            return StatusCode(500, new { cause = "retrieval failed" });
                    }
                }
            );
        }

        [HttpPost("{chatId}")]
        public async Task<dynamic> CreateNew(string chatId)
        {
            if (!HttpContext.Items.TryGetValue("user", out var userObj) ||
            userObj is not User user)
            {
                return StatusCode(500, new { cause = "invalid user" });
            }

            string requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
            NewNoteBody data;

            try
            {
                data = JsonSerializer.Deserialize<NewNoteBody>(requestBody);
            }
            catch
            {
                return StatusCode(400, new { cause = "missing properties" });
            }

            var result = await _notes.CreateNew(chatId, data);

            return result.Match<ActionResult>(
                id => StatusCode(200, new { id }),
                error =>
                {
                    switch (error)
                    {
                        case NotesError.Unauthorized:
                            return StatusCode(403, new { cause = "not logged in" });
                        default:
                            return StatusCode(500, new { cause = "retrieval failed" });
                    }
                }
            );
        }

        [HttpGet("{chatId}/{id}")]
        public async Task<dynamic> GetSpecific(string chatId, string id)
        {
            if (!HttpContext.Items.TryGetValue("user", out var userObj) ||
            userObj is not User user)
            {
                return StatusCode(500, new { cause = "invalid user" });
            }

            var result = await _notes.FindOne(chatId, id);

            return result.Match(
                note => StatusCode(200, note),
                error =>
                {
                    switch (error)
                    {
                        case NotesError.Unauthorized:
                            return StatusCode(403, new { cause = "not logged in" });
                        case NotesError.NotFound:
                            return StatusCode(404, new { cause = "chat not found" });
                        default:
                            return StatusCode(500, new { cause = "retrieval failed" });
                    }
                }
            );
        }

        [HttpPut("{chatId}/{id}")]
        public async Task<dynamic> ChangeNote(string chatId, string id)
        {
           if (!HttpContext.Items.TryGetValue("user", out var userObj) ||
           userObj is not User user)
           {
               return StatusCode(500, new { cause = "invalid user" });
           }

           string requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
           ChangeNoteBody data;

           try
           {
               data = JsonSerializer.Deserialize<ChangeNoteBody>(requestBody);
           }
           catch
           {
               return StatusCode(400, new { cause = "wrong format" });
           }

           var result = await _notes.ChangeOne(chatId, id, data);
           return result.Match(
               chat => StatusCode(200, chat),
               error =>
               {
                   switch (error)
                   {
                       case NotesError.Unauthorized:
                           return StatusCode(403, new { cause = "not logged in" });
                       case NotesError.NotFound:
                           return StatusCode(404, new { cause = "chat not found" });
                       default:
                           return StatusCode(500, new { cause = "retrieval failed" });
                   }
               }
           );
        }

        [HttpDelete("{chatId}/{id}")]
        public async Task<dynamic> DeleteSpecific(string chatId, string id)
        {
            if (!HttpContext.Items.TryGetValue("user", out var userObj) ||
            userObj is not User user)
            {
                return StatusCode(500, new { cause = "invalid user" });
            }

            switch (await _notes.DeleteOne(chatId, id))
            {
                case NotesError.None:
                    return StatusCode(200);
                default:
                    return StatusCode(500, new { cause = "deletion failed" });
            }
        }
    }
}
