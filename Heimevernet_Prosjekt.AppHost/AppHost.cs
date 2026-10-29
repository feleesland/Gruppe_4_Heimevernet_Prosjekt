var builder = DistributedApplication.CreateBuilder(args);

// MariaDB container, started automatically by Aspire
var mariadb = builder.AddMySql("mariadb")
    .WithImage("mariadb", "lts")
    .WithDataVolume();          // keeps the data when you restart

var db = mariadb.AddDatabase("heimevernetdb");

builder.AddProject<Projects.Heimevernet_Prosjekt>("heimevernet-prosjekt")
    .WithReference(db)          // gives the website the connection string
    .WaitFor(db);               // waits until the database is ready

builder.Build().Run();



