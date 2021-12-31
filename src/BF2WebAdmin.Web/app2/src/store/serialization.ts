// import { IMessage } from "./protos/protos";

export interface ISerializer<TData> {
  serialize(data: any): TData;
  deserialize<T>(data: TData): T;
}

export class JsonSerializer implements ISerializer<string> {
  public serialize(data: any) {
    return JSON.stringify(data);
  }

  public deserialize<T>(data: string): T {
    if (typeof data !== "string") {
      return data;
    }
    return JSON.parse(data);
  }
}

// export class ProtobufSerializer implements ISerializer<Uint8Array> {
//   public serialize(data: any) {
//     return IMessage.encode(data).finish();
//   }

//   public deserialize<T>(data: Uint8Array): T {
//     return (IMessage.decode(data) as any) as T; // ?? Cannot convert to T
//   }
// }
