using NotesStorage;
using NotesStorage.Managers;
using NotesStorage.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSingleton<NotesManager>();
builder.Services.AddSingleton<IdentityCache<User>>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseMiddleware<Authentication>();

app.UseAuthorization();

app.MapControllers();

app.Run();