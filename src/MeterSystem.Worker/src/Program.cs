var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<MeterReadingWorker>();

var host = builder.Build();
host.Run();
