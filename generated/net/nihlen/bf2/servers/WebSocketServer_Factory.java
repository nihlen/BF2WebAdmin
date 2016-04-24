package net.nihlen.bf2.servers;

import dagger.MembersInjector;
import dagger.internal.Factory;
import javax.annotation.Generated;
import javax.inject.Provider;
import net.nihlen.bf2.data.Database;

@Generated("dagger.internal.codegen.ComponentProcessor")
public final class WebSocketServer_Factory implements Factory<WebSocketServer> {
  private final MembersInjector<WebSocketServer> membersInjector;
  private final Provider<Database> databaseProvider;

  public WebSocketServer_Factory(MembersInjector<WebSocketServer> membersInjector, Provider<Database> databaseProvider) {  
    assert membersInjector != null;
    this.membersInjector = membersInjector;
    assert databaseProvider != null;
    this.databaseProvider = databaseProvider;
  }

  @Override
  public WebSocketServer get() {  
    WebSocketServer instance = new WebSocketServer(databaseProvider.get());
    membersInjector.injectMembers(instance);
    return instance;
  }

  public static Factory<WebSocketServer> create(MembersInjector<WebSocketServer> membersInjector, Provider<Database> databaseProvider) {  
    return new WebSocketServer_Factory(membersInjector, databaseProvider);
  }
}

