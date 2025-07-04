using MongoDB.Driver;
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

        public async Task<Either<List<Note>, NotesError>> GetAll(string chatId)
        {
            IAsyncCursor<DBNote> result;
            try
            {
                result = await _notes.FindAsync(note => note.ChatId == chatId);
            }
            catch
            {
                return new Either<List<Note>, NotesError>(NotesError.NoDatabaseConnection);
            }

            try
            {
                var list = result.ToList().Select(note => new Note
                {
                    Id = note.Id,
                    Name = note.Name,
                    ChatId = note.ChatId,
                    Content = note.Content
                }).ToList();
                return new Either<List<Note>, NotesError>(list);
            }
            catch
            {
                return new Either<List<Note>, NotesError>(NotesError.WrongFormatInDatabase);
            }
        }

        public async Task<Either<string, NotesError>> CreateNew(string chatId, NewNoteBody body)
        {
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

        public async Task<Either<Note, NotesError>> FindOne(string chatId, string id)
        {
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
                    Content = list[0].Content
                };
                return new Either<Note, NotesError>(note);
            }
            else
            {
                return new Either<Note, NotesError>(NotesError.NotFound);
            }
        }

        public async Task<Either<Note, NotesError>> ChangeOne(string chatId, string id, ChangeNoteBody body)
        {
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

               return new Either<Note, NotesError>(new Note
               {
                   Id = note.Id,
                   Name = body.Name,
                   Content = body.Content,
                   ChatId = note.ChatId
               });
           }
           else
           {
               return new Either<Note, NotesError>(NotesError.NotFound);
           }
        }

        public async Task<NotesError> DeleteOne(string chatId, string id)
        {
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
    }

    public struct AllNotesResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public struct NewNoteBody
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("content")]
        public string Content { get; set; }
    }

    public struct ChangeNoteBody
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("content")]
        public string Content { get; set; }
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
