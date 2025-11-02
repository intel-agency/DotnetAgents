using DotnetAgents.AgentApi.Controllers;
using DotnetAgents.AgentApi.Services;
using IntelAgent;


var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// IConfigurationRoot config = new ConfigurationBuilder()
//     .AddUserSecrets<Program>()
//     .Build();
// string? model = config["ModelName"];
// string? key = config["OpenAIKey"];


// builder.Services.AddSingleton<IAgent>(sp =>
// {
//     if (string.IsNullOrEmpty(key))
//     {
//         throw new InvalidOperationException("OpenAI API key is not configured. Please set the 'OpenAIKey' in user secrets.");
//     }

//     if (string.IsNullOrEmpty(model))
//     {
//         throw new InvalidOperationException("Model name is not configured. Please set the 'ModelName' in user secrets.");
//     }

//     return new Agent(key, model);
// });

// builder.Services.AddSingleton<IAgentService, AgentService>(sp =>
// {
//     var agent = sp.GetRequiredService<IAgent>();
//     return new AgentService(agent);
// });

builder.Services.AddSingleton<IAgentService, AgentService>();
builder.Services.AddSingleton<IAgentController, AgentController>();
builder.Services.AddControllers();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapControllers();

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
