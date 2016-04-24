package net.nihlen.bf2.dependency;

import dagger.Component;
import net.nihlen.bf2.data.Database;
import net.nihlen.bf2.servers.BF2SocketServer;
import net.nihlen.bf2.servers.WebSocketServer;

import javax.inject.Singleton;

/**
 * Dagger2 Component
 *
 * Created by Alex on 2015-11-10.
 */
@Singleton
@Component(modules = {BF2WebAdminModule.class})
public interface BF2WebAdminComponent {

    WebSocketServer webSocketServer();
    BF2SocketServer bf2SocketServer();
    Database database();
}
