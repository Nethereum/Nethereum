using NethereumDapp.LoadGenerator;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<LoadGeneratorService>();

builder.Build().Run();
