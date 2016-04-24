package net.nihlen.bf2.dependency;

import dagger.internal.Factory;
import javax.annotation.Generated;
import net.nihlen.bf2.data.Database;

@Generated("dagger.internal.codegen.ComponentProcessor")
public final class BF2WebAdminModule_ProvideDatabaseFactory implements Factory<Database> {
  private final BF2WebAdminModule module;

  public BF2WebAdminModule_ProvideDatabaseFactory(BF2WebAdminModule module) {  
    assert module != null;
    this.module = module;
  }

  @Override
  public Database get() {  
    Database provided = module.provideDatabase();
    if (provided == null) {
      throw new NullPointerException("Cannot return null from a non-@Nullable @Provides method");
    }
    return provided;
  }

  public static Factory<Database> create(BF2WebAdminModule module) {  
    return new BF2WebAdminModule_ProvideDatabaseFactory(module);
  }
}

