using MongoDB.Driver;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NotesStorage.Managers
{
    public class NotesManager
    {
        public IMongoCollection<DBNote> _notes;

        public NotesManager()
        {
            var client = new MongoClient("mongodb://localhost:27017");
            var database = client.GetDatabase("NotesChat");
            _notes = database.GetCollection<DBNote>("notes");
        }

        public HttpClient generateClient(string sessionId)
        {
            var cookies = new CookieContainer();
            cookies.Add(new Uri("http://localhost/"), new Cookie("sessionId", sessionId));
            var handler = new HttpClientHandler
            {
                CookieContainer = cookies
            };

            return new HttpClient(handler);
        }

        public async Task<bool> checkAccessToChat(User user, string sessionId, string chatId)
        {
            var http = generateClient(sessionId);
            try
            {
                var chatResult = await http.GetAsync($"http://localhost/api/chat/storage/{chatId}");
                var jsonResponse = await chatResult.Content.ReadAsStringAsync();
                var chat = JsonSerializer.Deserialize<Chat>(jsonResponse);

                if (!chat.Users.Contains(user.Id))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        public async Task<Either<AllNotesResult, NotesError>> GetAll(User user, string sessionId, string chatId)
        {
            if (!await checkAccessToChat(user, sessionId, chatId))
            {
                return new Either<AllNotesResult, NotesError>(NotesError.Unauthorized);
            }

            IAsyncCursor<DBNote> result;
            try
            {
                result = await _notes.FindAsync(note => note.ChatId == chatId);
            }
            catch
            {
                return new Either<AllNotesResult, NotesError>(NotesError.NoDatabaseConnection);
            }

            try
            {
                var resList = result.ToList();
                var list = resList.Select(note => new Note
                {
                    Id = note.Id,
                    Name = note.Name,
                    ChatId = note.ChatId,
                    Content = note.Content,
                    Tags = note.Tags
                }).ToList();
                List<string> tags = new List<string>();
                foreach (DBNote note in resList)
                {
                    if (note.Tags == null)
                    {
                        continue;
                    }
                    foreach (string tag in note.Tags)
                    {
                        if (!tags.Contains(tag))
                        {
                            tags.Add(tag);
                        }
                    }
                }
                return new Either<AllNotesResult, NotesError>(new AllNotesResult
                {
                    Notes = list,
                    Tags = tags
                });
            }
            catch
            {
                return new Either<AllNotesResult, NotesError>(NotesError.WrongFormatInDatabase);
            }
        }

        public async Task<Either<string, NotesError>> CreateNew(User user, string sessionId, string chatId, NewNoteBody body)
        {
            if (!await checkAccessToChat(user, sessionId, chatId))
            {
                return new Either<string, NotesError>(NotesError.Unauthorized);
            }

            try
            {
                var id = Guid.NewGuid().ToString();
                var note = new DBNote
                {
                    Id = id,
                    ChatId = chatId,
                    Name = body.Name,
                    Content = body.Content
                };
                await _notes.InsertOneAsync(note);

                return new Either<string, NotesError>(id);
            }
            catch
            {
                return new Either<string, NotesError>(NotesError.NoDatabaseConnection);
            }
        }

        public async Task<Either<Note, NotesError>> FindOne(User user, string sessionId, string chatId, string id)
        {
            if (!await checkAccessToChat(user, sessionId, chatId))
            {
                return new Either<Note, NotesError>(NotesError.Unauthorized);
            }

            IAsyncCursor<DBNote> result;
            try
            {
                result = await _notes.FindAsync(note => note.Id == id && note.ChatId == chatId);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new Either<Note, NotesError>(NotesError.NoDatabaseConnection);
            }

            var list = result.ToList();
            if (list.Count > 0)
            {
                var note = new Note
                {
                    Id = list[0].Id,
                    Name = list[0].Name,
                    ChatId = list[0].ChatId,
                    Content = list[0].Content,
                    Tags = list[0].Tags
                };
                return new Either<Note, NotesError>(note);
            }
            else
            {
                return new Either<Note, NotesError>(NotesError.NotFound);
            }
        }

        public async Task<Either<Note, NotesError>> ChangeOne(User user, string sessionId, string chatId, string id, ChangeNoteBody body)
        {
            if (!await checkAccessToChat(user, sessionId, chatId))
            {
                return new Either<Note, NotesError>(NotesError.Unauthorized);
            }

            IAsyncCursor<DBNote> result;
            try
            {
                result = await _notes.FindAsync(note => note.Id == id && note.ChatId == chatId);
            }
            catch
            {
                return new Either<Note, NotesError>(NotesError.NoDatabaseConnection);
            }

            var list = result.ToList();
            if (list.Count > 0)
            {
                var note = list[0];
                body.Name = body.Name != null && body.Name.Length > 0 ? body.Name : note.Name;
                body.Content = body.Content != null && body.Content.Length > 0 ? body.Content : note.Content;

                UpdateDefinition<DBNote> update;
                if (body.Name != note.Name)
                {
                    update = Builders<DBNote>.Update.Set("Name", body.Name);
                    await _notes.UpdateOneAsync(note => note.Id == id && note.ChatId == chatId, update);
                }
                if (body.Content != note.Content)
                {
                    update = Builders<DBNote>.Update.Set("Content", body.Content);
                    await _notes.UpdateOneAsync(note => note.Id == id && note.ChatId == chatId, update);
                }
                if (body.Tags != note.Tags)
                {
                    update = Builders<DBNote>.Update.Set("Tags", body.Tags);
                    await _notes.UpdateOneAsync(note => note.Id == id && note.ChatId == chatId, update);
                }

                return new Either<Note, NotesError>(new Note
                {
                    Id = note.Id,
                    Name = body.Name,
                    Content = body.Content,
                    ChatId = note.ChatId,
                    Tags = note.Tags
                });
            }
            else
            {
                return new Either<Note, NotesError>(NotesError.NotFound);
            }
        }

        public async Task<NotesError> DeleteOne(User user, string sessionId, string chatId, string id)
        {
            if (!await checkAccessToChat(user, sessionId, chatId))
            {
                return NotesError.Unauthorized;
            }

            try
            {
                var result = await _notes.DeleteOneAsync(note => note.Id == id && note.ChatId == chatId);
                return NotesError.None;
            }
            catch
            {
                return NotesError.NoDatabaseConnection;
            }
        }
    }

    public struct DBNote
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("chatId")]
        public string ChatId { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("content")]
        public string Content { get; set; }
        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }
    }

    public struct Note
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("chatId")]
        public string ChatId { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("content")]
        public string Content { get; set; }
        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }
    }

    public struct AllNotesResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }
    }

    public struct NewNoteBody
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("content")]
        public string Content { get; set; }
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; }
    }

    public struct ChangeNoteBody
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("content")]
        public string Content { get; set; }
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; }
    }

    public struct AllNotesResult
    {
        [JsonPropertyName("notes")]
        public List<Note> Notes { get; set; }
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; }
    }

    public enum NotesError
    {
        None,
        NoDatabaseConnection,
        NotFound,
        WrongFormatInDatabase,
        Unauthorized
    }
}
