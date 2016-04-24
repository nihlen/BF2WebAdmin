package net.nihlen.bf2;

import dagger.internal.Factory;
import javax.annotation.Generated;
import javax.inject.Provider;
import net.nihlen.bf2.servers.BF2SocketServer;
import net.nihlen.bf2.servers.WebSocketServer;

@Generated("dagger.internal.codegen.ComponentProcessor")
public final class BF2WebAdmin_Factory implements Factory<BF2WebAdmin> {
  private final Provider<BF2SocketServer> bf2SocketServerProvider;
  private final Provider<WebSocketServer> webSocketServerProvider;

  public BF2WebAdmin_Factory(Provider<BF2SocketServer> bf2SocketServerProvider, Provider<WebSocketServer> webSocketServerProvider) {  
    assert bf2SocketServerProvider != null;
    this.bf2SocketServerProvider = bf2SocketServerProvider;
    assert webSocketServerProvider != null;
    this.webSocketServerProvider = webSocketServerProvider;
  }

  @Override
  public BF2WebAdmin get() {  
    return new BF2WebAdmin(bf2SocketServerProvider.get(), webSocketServerProvider.get());
  }

  public static Factory<BF2WebAdmin> create(Provider<BF2SocketServer> bf2SocketServerProvider, Provider<WebSocketServer> webSocketServerProvider) {  
    return new BF2WebAdmin_Factory(bf2SocketServerProvider, webSocketServerProvider);
  }
}

