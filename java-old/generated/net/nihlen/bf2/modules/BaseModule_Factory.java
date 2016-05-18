package net.nihlen.bf2.modules;

import dagger.internal.Factory;
import javax.annotation.Generated;
import javax.inject.Provider;
import net.nihlen.bf2.data.Database;
import net.nihlen.bf2.objects.GameServer;

@Generated("dagger.internal.codegen.ComponentProcessor")
public final class BaseModule_Factory implements Factory<BaseModule> {
  private final Provider<GameServer> serverProvider;
  private final Provider<Database> databaseProvider;

  public BaseModule_Factory(Provider<GameServer> serverProvider, Provider<Database> databaseProvider) {  
    assert serverProvider != null;
    this.serverProvider = serverProvider;
    assert databaseProvider != null;
    this.databaseProvider = databaseProvider;
  }

  @Override
  public BaseModule get() {  
    return new BaseModule(serverProvider.get(), databaseProvider.get());
  }

  public static Factory<BaseModule> create(Provider<GameServer> serverProvider, Provider<Database> databaseProvider) {  
    return new BaseModule_Factory(serverProvider, databaseProvider);
  }
}

