# BF2 Web Admin

A Battlefield 2 mod server with administration through a web interface or Discord. The aim is to be a replacement for some of the BF2CC features and also create new behavior through modules written in C# for a better dev experience.

The communication between webadmin and the ModManager module on the Battlefield 2 server  is done over an open socket, but does not require any additional open ports on the game server.

![Server page screenshot](https://static.nihlen.net/bf2/media/bf2webadmin-ss-1.png)

## Features
* Administration through the web UI
  * Restart server, restart map, toggle pause
  * Switch team, kill, warn, kick and ban
  * Map selection
  * Interactive chat
  * Live minimap
  * Ingame and custom commands available through chat
* Discord integration (optional)
  * channel for player join/leave messages
  * channel integration with ingame chat and administration through commands
  * bot showing game status and players online in the sidebar
* Custom modules can be enabled based on the server group
  * Heli 2v2 module with score tracking and QoL improvements
  * Chopper Mayhem module with base protection
  * Map module for server-side modding of maps by adding buildings ingame and saving/loading your work
  * Log module to log chat and game events to file
* Other random ingame commands 

Modifying settings in `serversettings.con`, `modnanager.con` or `maplist.con` is not currently supported since these values are built into our container images and do not need to change. However, there is some work started to enable this. See `bf2tool.php` in the [bf2-docker](https://github.com/nihlen/bf2-docker) repository.

## Prerequisites

* A host running Linux or Windows. The Docker setup is tested on Linux containers
* A Battlefield 2 server with ModManager and the [mm_webadmin.py](https://github.com/nihlen/bf2-docker/tree/master/images/bf2hub-pb-mm-webadmin) module

## Usage

On your host you need to create `docker-compose.yml` and `appsecrets.json`. See the examples below and replace the IP and password values with your own.

Build a Battlefield 2 server image with the required ModManager module:
```sh
docker build -t bf2:bf2hub-pb-mm-webadmin https://github.com/nihlen/bf2-docker.git#master:images/bf2hub-pb-mm-webadmin
```

Or build the container image locally with the .NET SDK:
```sh
dotnet publish "src/BF2WebAdmin.Server/BF2WebAdmin.Server.csproj" -c Release -t:PublishContainer -p:RuntimeIdentifier=linux-x64
```

Then start up webadmin and the Battlefield 2 servers by running this command in the same directory as the `docker-compose.yml` and `appsecrets.json` files.
```sh
docker compose up -d --remove-orphans
```

If you need to update the game server image you can rerun the build command, and if you need to update the webadmin image you can run
```sh
docker compose pull
```
and then rerun the docker compose up-command.

### Windows build

To build a self-contained app for Windows, use `dotnet publish`, requires [.NET SDK 9](https://dotnet.microsoft.com/en-us/download/dotnet/9.0).

```sh
dotnet publish .\src\BF2WebAdmin.Server\BF2WebAdmin.Server.csproj -c Release -o .\publish
```

Create `Configuration/appsecrets.json` using the template further down, then run `BF2WebAdmin.Server.exe` and navigate to http://localhost:5000.

## Examples

See the [docker compose example](examples/docker-compose/README.md)
