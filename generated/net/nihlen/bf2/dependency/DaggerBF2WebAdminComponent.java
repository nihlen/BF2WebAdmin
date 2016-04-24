package net.nihlen.bf2.dependency;

import dagger.internal.ScopedProvider;
import javax.annotation.Generated;
import javax.inject.Provider;
import net.nihlen.bf2.data.Database;
import net.nihlen.bf2.servers.BF2SocketServer;
import net.nihlen.bf2.servers.WebSocketServer;

@Generated("dagger.internal.codegen.ComponentProcessor")
public final class DaggerBF2WebAdminComponent implements BF2WebAdminComponent {
  private Provider<WebSocketServer> provideWebSocketServerProvider;
  private Provider<BF2SocketServer> provideBF2SocketServerProvider;
  private Provider<Database> provideDatabaseProvider;

  private DaggerBF2WebAdminComponent(Builder builder) {  
    assert builder != null;
    initialize(builder);
  }

  public static Builder builder() {  
    return new Builder();
  }

  public static BF2WebAdminComponent create() {  
    return builder().build();
  }

  private void initialize(final Builder builder) {  
    this.provideWebSocketServerProvider = ScopedProvider.create(BF2WebAdminModule_ProvideWebSocketServerFactory.create(builder.bF2WebAdminModule));
    this.provideBF2SocketServerProvider = ScopedProvider.create(BF2WebAdminModule_ProvideBF2SocketServerFactory.create(builder.bF2WebAdminModule));
    this.provideDatabaseProvider = ScopedProvider.create(BF2WebAdminModule_ProvideDatabaseFactory.create(builder.bF2WebAdminModule));
  }

  @Override
  public WebSocketServer webSocketServer() {  
    return provideWebSocketServerProvider.get();
  }

  @Override
  public BF2SocketServer bf2SocketServer() {  
    return provideBF2SocketServerProvider.get();
  }

  @Override
  public Database database() {  
    return provideDatabaseProvider.get();
  }

  public static final class Builder {
    private BF2WebAdminModule bF2WebAdminModule;
  
    private Builder() {  
    }
  
    public BF2WebAdminComponent build() {  
      if (bF2WebAdminModule == null) {
        this.bF2WebAdminModule = new BF2WebAdminModule();
      }
      return new DaggerBF2WebAdminComponent(this);
    }
  
    public Builder bF2WebAdminModule(BF2WebAdminModule bF2WebAdminModule) {  
      if (bF2WebAdminModule == null) {
        throw new NullPointerException("bF2WebAdminModule");
      }
      this.bF2WebAdminModule = bF2WebAdminModule;
      return this;
    }
  }
}

