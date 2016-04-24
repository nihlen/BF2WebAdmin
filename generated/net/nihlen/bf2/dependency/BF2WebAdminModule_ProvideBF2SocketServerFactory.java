package net.nihlen.bf2.dependency;

import dagger.internal.Factory;
import javax.annotation.Generated;
import net.nihlen.bf2.servers.BF2SocketServer;

@Generated("dagger.internal.codegen.ComponentProcessor")
public final class BF2WebAdminModule_ProvideBF2SocketServerFactory implements Factory<BF2SocketServer> {
  private final BF2WebAdminModule module;

  public BF2WebAdminModule_ProvideBF2SocketServerFactory(BF2WebAdminModule module) {  
    assert module != null;
    this.module = module;
  }

  @Override
  public BF2SocketServer get() {  
    BF2SocketServer provided = module.provideBF2SocketServer();
    if (provided == null) {
      throw new NullPointerException("Cannot return null from a non-@Nullable @Provides method");
    }
    return provided;
  }

  public static Factory<BF2SocketServer> create(BF2WebAdminModule module) {  
    return new BF2WebAdminModule_ProvideBF2SocketServerFactory(module);
  }
}

