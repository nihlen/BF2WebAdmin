# Docker Compose Example

This is an example of webadmin and two Battlefield 2 servers running on the same host.

If you want to display the country of the players when joining then you need a free GeoIP database file from MaxMind and place `GeoLite2-Country.mmdb` in the same directory as `appsecrets.json`. Then bind mount it the same way in the volume section of the bf2-webadmin service `- "./GeoLite2-Country.mmdb:/app/GeoLite2-Country.mmdb"`.

`ServerSettings:IpAddress` public IP of the host

Note: The ports, aliases and RCON passwords need to match between the game server (docker-compose file) and the appsettings/appsecrets.

## Build container images

Build a BF2 game server image and a BF2 webadmin image needed for the example. Run it in the root folder of this repo.
```sh
docker build -t bf2:bf2hub-pb-mm-webadmin https://github.com/nihlen/bf2-docker.git#master:images/bf2hub-pb-mm-webadmin
dotnet publish "src/BF2WebAdmin.Server/BF2WebAdmin.Server.csproj" -c Release -t:PublishContainer -p:RuntimeIdentifier=linux-x64
```
