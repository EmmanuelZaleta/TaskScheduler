using Microsoft.OpenApi.Models;
using Serilog;
using YCC.SapAutomation.Infrastructure.Sql;
using YCC.SapAutomation.WebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/webapi-.log", rollingInterval: RollingInterval.Day));

// Add services to the container
builder.Services.AddControllers()
    .AddNewtonsoftJson();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Task Scheduler API",
        Version = "v1",
        Description = "API para gestión de tareas automatizadas con soporte de carga de archivos ZIP"
    });
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://localhost:4201")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Register SQL connection factory
builder.Services.AddSingleton<ISqlConnectionFactory>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Sql")
        ?? throw new InvalidOperationException("SQL connection string not found");
    return new SqlConnectionFactory(connectionString);
});

// Register application services
builder.Services.AddScoped<IJobManagementService, JobManagementService>();
builder.Services.AddSingleton<IFileStorageService, FileStorageService>();

// Configure file storage options
builder.Services.Configure<FileStorageOptions>(options =>
{
    options.BaseStoragePath = builder.Configuration.GetValue<string>("FileStorage:BasePath") ?? "JobFiles";
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Task Scheduler API v1");
        c.RoutePrefix = string.Empty; // Swagger en la raíz
    });
}

app.UseSerilogRequestLogging();

app.UseCors("AllowAngularApp");

app.UseAuthorization();

app.MapControllers();

app.Logger.LogInformation("Task Scheduler Web API starting...");

app.Run();
