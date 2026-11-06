var builder = DistributedApplication.CreateBuilder(args);

// MariaDB container, started automatically by Aspire
var mariadb = builder.AddMySql("mariadb")
    .WithImage("mariadb", "lts")
    .WithDataVolume();          // keeps the data when you restart

var db = mariadb.AddDatabase("heimevernetdb");

var launchProfileName = string.Equals(
    System.Environment.GetEnvironmentVariable("CI"),
    "true",
    StringComparison.OrdinalIgnoreCase)
    ? "http"
    : "https";

builder.AddProject<Projects.Heimevernet_Prosjekt>("heimevernet-prosjekt", launchProfileName: launchProfileName)
    .WithReference(db)          // gives the website the connection string
    .WaitFor(db);               // waits until the database is ready

builder.Build().Run();



