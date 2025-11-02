using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using DotnetAgents.AgentApi.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using IntelAgent;

IConfigurationRoot config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();
string? model = config["ModelName"];
string? key = config["OpenAIKey"];

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddSingleton<IAgent>(sp =>
{
    if (string.IsNullOrEmpty(key))
    {
        throw new InvalidOperationException("OpenAI API key is not configured. Please set the 'OpenAIKey' in user secrets.");
    }

    if (string.IsNullOrEmpty(model))
    {
        throw new InvalidOperationException("Model name is not configured. Please set the 'ModelName' in user secrets.");
    }

    return new Agent(key, model);
});

builder.Services.AddSingleton<IAgentService, AgentService>(sp =>
{
    var agent = sp.GetRequiredService<IAgent>();
    return new AgentService(agent);
});

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DotnetAgents API",
        Version = "v1",
        Description = "API for interacting with AI agents",
        Contact = new OpenApiContact
        {
            Name = "Intel Agency",
            Url = new Uri("https://github.com/intel-agency/DotnetAgents")
        }
    });

    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// builder.Services.ConfigureHttpJsonOptions(options =>
// {
//     options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
// });

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DotnetAgents API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "DotnetAgents API Documentation";
    });

    app.UseReDoc(c =>
    {
        c.SpecUrl = "/swagger/v1/swagger.json";
        c.RoutePrefix = "redoc";
        c.DocumentTitle = "DotnetAgents API Documentation";
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// [JsonSerializable(typeof(Todo[]))]
// internal partial class AppJsonSerializerContext : JsonSerializerContext
// {

// }
