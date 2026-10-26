var builder = DistributedApplication.CreateBuilder(args);



builder.AddProject<Projects.Heimevernet_Prosjekt>("heimevernet-prosjekt");

builder.Build().Run();


