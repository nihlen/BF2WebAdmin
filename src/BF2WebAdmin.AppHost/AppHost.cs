// Local development: the server and the web UI's Vite dev server, which proxies /api and /hubs to the server.
// Publishing (aspire publish / aspire deploy): one image, the server with the web UI's build in its wwwroot.
var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("compose");

var server = builder.AddProject<Projects.BF2WebAdmin_Server>("server")
    .WithExternalHttpEndpoints();

if (builder.ExecutionContext.IsRunMode)
{
    // The admin channel listener of the dev server. Not the default 4310: the localtest's play WebAdmin listens there,
    // and the server stops when it cannot bind. Point a dev game server's adminConnect at this port, or change it with
    // AdminChannelPort in this project's appsettings or user secrets.
    server.WithEnvironment("ServerSettings__AdminChannel__Port", builder.Configuration["AdminChannelPort"] ?? "4314");
}
else
{
    // The admin channel listener (ServerSettings:AdminChannel:Port, 4310 by default), dialed by the game servers' module.
    // Only published: in development the server listens on it directly.
    server.WithEndpoint("admin", endpoint =>
    {
        endpoint.Port = 4310;
        endpoint.TargetPort = 4310;
        endpoint.UriScheme = "tcp";
        endpoint.IsExternal = true;
    });
}

var web = builder.AddViteApp("web", "../BF2WebAdmin.Web")
    // Read by vite.config.ts for its proxy
    .WithEnvironment("SERVER_HTTP", server.GetEndpoint("http"))
    .WithReference(server)
    .WaitFor(server);

server.PublishWithContainerFiles(web, "./wwwroot");

builder.Build().Run();
