# BF2 Web Admin

A Battlefield 2 mod server with administration through a web interface or Discord. The aim is to be a replacement for some of the BF2CC features and also create new behavior through modules written in C# for a better dev experience.

The communication between webadmin and the ModManager module on the Battlefield 2 server  is done over an open socket, but does not require any additional open ports on the game server.

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
* A Battlefield 2 server with ModManager and the [mm_webadmin.py](https://github.com/nihlen/bf2-docker#bf2hub-pb-mm-webadmin) module

## Usage

On your host you need to create `docker-compose.yml` and `appsecrets.json`. See the examples below and replace the IP and password values with your own.

Build a Battlefield 2 server image with the required ModManager module:
```sh
docker build -t bf2:bf2hub-pb-mm-webadmin https://github.com/nihlen/bf2-docker.git#master:images/bf2hub-pb-mm-webadmin
```

Then start up webadmin and the Battlefield 2 servers by running this command in the same directory as the `docker-compose.yml` and `appsecrets.json` files.
```sh
docker-compose up -d --remove-orphans
```

If you need to update the game server image you can rerun the build command, and if you need to update the webadmin image you can run
```sh
docker-compose pull
```
and then rerun the docker-compose up-command.

### Docker Compose Example

This is an example of webadmin and two Battlefield 2 servers running on the same host.

If you want to display the country of the players when joining then you need a free GeoIP database file from MaxMind and place `GeoLite2-Country.mmdb` in the same directory as `appsecrets.json`. Then bind mount it the same way in the volume section of the bf2-webadmin service `- "./GeoLite2-Country.mmdb:/app/GeoLite2-Country.mmdb"`.

docker-compose.yml
```yaml
version: "3.3"
services:
  bf2-webadmin:
    container_name: bf2-webadmin
    image: "nihlen/bf2-webadmin:latest"
    restart: on-failure
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:80
    ports:
      - "80:80"
      - "4300:4300"
    volumes:
      - "./bf2/webadmin/Data:/app/Data"
      - "./bf2/webadmin/Logs:/app/Logs"
      - "./bf2/webadmin/ServerLogs:/app/ServerLogs"
      - "./appsecrets.json:/app/Configuration/appsecrets.json"

  bf2-server-1:
    container_name: bf2-server-1
    image: "bf2:bf2hub-pb-mm-webadmin"
    restart: on-failure
    environment:
      - ENV_SERVER_NAME=Testserver 1
      - ENV_MAX_PLAYERS=16
      - ENV_SERVER_PORT=16567
      - ENV_GAMESPY_PORT=29900
      - ENV_DEMOS_URL=http://www.example.com/
      - ENV_RCON_PASSWORD=rconpw123
      - ENV_BF2WEBADMIN_HOST=bf2-webadmin
      - ENV_BF2WEBADMIN_PORT=4300
      - ENV_BF2WEBADMIN_TIMER_INTERVAL=300
      - ENV_API_KEY=apikey123
    volumes:
      - "./bf2/server-1/server:/home/bf2/srv"
      - "./bf2/server-1/volume:/volume"
    ports:
      - "8000:80/tcp"
      - "4711:4711/tcp"
      - "4712:4712/tcp"
      - "16567:16567/udp"
      - "27901:27901/udp"
      - "29900:29900/udp"

  bf2-server-2:
    container_name: bf2-server-2
    image: "bf2:bf2hub-pb-mm-webadmin"
    restart: on-failure
    environment:
      - ENV_SERVER_NAME=Testserver 2
      - ENV_MAX_PLAYERS=16
      - ENV_SERVER_PORT=16569
      - ENV_GAMESPY_PORT=29901
      - ENV_DEMOS_URL=http://www.example.com/
      - ENV_RCON_PASSWORD=rconpw123
      - ENV_BF2WEBADMIN_HOST=bf2-webadmin
      - ENV_BF2WEBADMIN_PORT=4300
      - ENV_BF2WEBADMIN_TIMER_INTERVAL=300
      - ENV_API_KEY=apikey123
    volumes:
      - "./bf2/server-2/server:/home/bf2/srv"
      - "./bf2/server-2/volume:/volume"
    ports:
      - "8001:80/tcp"
      - "4721:4711/tcp"
      - "4722:4712/tcp"
      - "16569:16569/udp"
      - "27911:27901/udp"
      - "29901:29901/udp"

```

### Appsecrets Example

`ServerSettings:IpAddress` public IP of the host

Note: The ports, aliases and RCON passwords need to match between the game server (docker-compose file) and the appsettings.

appsecrets.json
```json
{
  "ConnectionStrings": {
    "BF2DB": "Data Source=./Data/BF2WebAdmin.sqlite;Cache=Shared"
  },
  "ServerSettings": {
    "IpAddress": "YOUR_PUBLIC_IP_ADDRESS",
    "Port": 4300,
    "StartFakeGameServer": false,
    "ServerLogDirectory": "ServerLogs",
    "PrintSendLog": false,
    "PrintRecvLog": false,
    "ForceHttps": false,
    "GameServers": [
      {
        "IpAddress": "bf2-server-1",
        "GamePort": 16567,
        "QueryPort": 29900,
        "RconPort": 4711,
        "RconPassword": "rconpw123",
        "DiscordBot": {
          "Token": "",
          "AdminChannel": "",
          "NotificationChannel": "",
          "MatchResultChannel": ""
        }
      },
      {
        "IpAddress": "bf2-server-2",
        "GamePort": 16569,
        "QueryPort": 29901,
        "RconPort": 4711,
        "RconPassword": "rconpw123",
        "DiscordBot": {
          "Token": "",
          "AdminChannel": "",
          "NotificationChannel": "",
          "MatchResultChannel": ""
        }
      }
    ]
  },
  "Discord": {
    "Webhooks": [
      {
        "WebhookId": "",
        "WebhookToken": "",
        "ServerGroupFilter": "",
        "MessageTypeFilter": "player"
      }
    ]
  },
  "Seq": {
    "ServerUrl": "",
    "ApiKey": ""
  },
  "Authentication": {
    "Admins": [
      {
        "Username": "admin",
        "Password": "test"
      }
    ]
  },
  "RabbitMQ": {
    "Host": "",
    "Port": 5673,
    "VirtualHost": "",
    "UserName": "",
    "Password": ""
  },
  "Twitter": {
    "ConsumerKey": "",
    "ConsumerSecret": "",
    "AccessToken": "",
    "AccessTokenSecret": ""
  },
  "Mashape": {
    "Key": ""
  },
  "Geoip": {
    "DatabasePath": "GeoLite2-Country.mmdb"
  },
  "Logging": {
    "IncludeScopes": false,
    "LogLevel": {
      "Default": "Information",
      "System": "Information",
      "Microsoft": "Information"
    }
  }
}
```