# Docker Compose Example

This is an example of webadmin and two Battlefield 2 servers (OpenBF2 with the server extension) running on the same
host. The README's "Admin channel" section explains each setting.

If you want to display the country of the players when joining then you need a free GeoIP database file from MaxMind and place `GeoLite2-Country.mmdb` in the same directory as `appsecrets.json`. Then bind mount it the same way in the volume section of the bf2-webadmin service `- "./GeoLite2-Country.mmdb:/app/GeoLite2-Country.mmdb"`.

`ServerSettings:IpAddress` public IP of the host

Note: The ports and server ids need to match between the game servers (docker-compose file) and `appsecrets.json`.

## Admin channel

1. Create the certificate and put the base64 value in `.env` next to `docker-compose.yml`:

   ```sh
   docker run --rm nihlen/bf2-webadmin:latest --admin-cert-create
   ```

   ```
   ADMIN_CERT=MIIE...
   ```

   Keep the printed `adminCertSha256 <pin>` for the game servers. Keep `.env` out of version control.
2. Give each server in `appsecrets.json` its own `AdminKey` (`openssl rand -hex 32`). `ServerId` is what the game
   server sends as `adminServerId`.
3. On each game server, `serverext.cfg`:

   ```
   adminConnect bf2-webadmin:4310
   adminServerId bf2-server-1:16567
   adminKey <the AdminKey of bf2-server-1>
   adminCertSha256 <the pin>
   ```

WebAdmin listens on the default admin-channel port 4310. The game servers use restart policy `unless-stopped`:
`.restartserver` quits the server with exit code 0.

The WebAdmin image runs as user 1654 (`app`), so the mounted `bf2/webadmin` directories must be writable by it.

## Build container images

Build the BF2 webadmin image needed for the example. Run it in the root folder of this repo.
```sh
dotnet publish "src/BF2WebAdmin.Server/BF2WebAdmin.Server.csproj" -c Release -t:PublishContainer -p:RuntimeIdentifier=linux-x64
```
