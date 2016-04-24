package net.nihlen.bf2.dependency;

import dagger.internal.Factory;
import javax.annotation.Generated;
import net.nihlen.bf2.servers.WebSocketServer;

@Generated("dagger.internal.codegen.ComponentProcessor")
public final class BF2WebAdminModule_ProvideWebSocketServerFactory implements Factory<WebSocketServer> {
  private final BF2WebAdminModule module;

  public BF2WebAdminModule_ProvideWebSocketServerFactory(BF2WebAdminModule module) {  
    assert module != null;
    this.module = module;
  }

  @Override
  public WebSocketServer get() {  
    WebSocketServer provided = module.provideWebSocketServer();
    if (provided == null) {
      throw new NullPointerException("Cannot return null from a non-@Nullable @Provides method");
    }
    return provided;
  }

  public static Factory<WebSocketServer> create(BF2WebAdminModule module) {  
    return new BF2WebAdminModule_ProvideWebSocketServerFactory(module);
  }
}

