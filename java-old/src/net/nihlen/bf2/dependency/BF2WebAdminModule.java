package net.nihlen.bf2.dependency;

import dagger.Module;
import dagger.Provides;
import net.nihlen.bf2.data.Database;
import net.nihlen.bf2.data.FakeDatabase;
import net.nihlen.bf2.servers.BF2SocketServer;
import net.nihlen.bf2.servers.WebSocketServer;

import javax.inject.Singleton;

/**
 * Dagger2 Module
 *
 * Created by Alex on 2015-11-15.
 */
@Module
public class BF2WebAdminModule {

    @Provides
    @Singleton
    Database provideDatabase() {
        return new FakeDatabase();
    }

    @Provides
    @Singleton
    WebSocketServer provideWebSocketServer() {
        return new WebSocketServer(new FakeDatabase());
    }

    @Provides
    @Singleton
    BF2SocketServer provideBF2SocketServer() {
        return new BF2SocketServer();
    }





    /*@Provides
    @Singleton
    Database provideDatabase() {
        return new FakeDatabase();
    }

    @Provides
    @Singleton
    WebSocketServer provideWebSocketServer(Database database) {
        return new WebSocketServer(database);
    }

    @Provides
    @Singleton
    BF2SocketServer provideBF2SocketServer() {
        return new BF2SocketServer();
    }*/

}
