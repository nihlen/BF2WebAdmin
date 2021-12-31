import * as $protobuf from "protobufjs";

/** Properties of a ChatEvent. */
export interface IChatEvent {

    /** ChatEvent Message */
    Message?: (IMessageDto|null);
}

/** Represents a ChatEvent. */
export class ChatEvent implements IChatEvent {

    /**
     * Constructs a new ChatEvent.
     * @param [properties] Properties to set
     */
    constructor(properties?: IChatEvent);

    /** ChatEvent Message. */
    public Message?: (IMessageDto|null);

    /**
     * Creates a new ChatEvent instance using the specified properties.
     * @param [properties] Properties to set
     * @returns ChatEvent instance
     */
    public static create(properties?: IChatEvent): ChatEvent;

    /**
     * Encodes the specified ChatEvent message. Does not implicitly {@link ChatEvent.verify|verify} messages.
     * @param message ChatEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IChatEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified ChatEvent message, length delimited. Does not implicitly {@link ChatEvent.verify|verify} messages.
     * @param message ChatEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IChatEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a ChatEvent message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns ChatEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): ChatEvent;

    /**
     * Decodes a ChatEvent message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns ChatEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): ChatEvent;

    /**
     * Verifies a ChatEvent message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a ChatEvent message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns ChatEvent
     */
    public static fromObject(object: { [k: string]: any }): ChatEvent;

    /**
     * Creates a plain object from a ChatEvent message. Also converts values to other types if specified.
     * @param message ChatEvent
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: ChatEvent, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this ChatEvent to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a GameStateEvent. */
export interface IGameStateEvent {

    /** GameStateEvent State */
    State?: (string|null);
}

/** Represents a GameStateEvent. */
export class GameStateEvent implements IGameStateEvent {

    /**
     * Constructs a new GameStateEvent.
     * @param [properties] Properties to set
     */
    constructor(properties?: IGameStateEvent);

    /** GameStateEvent State. */
    public State: string;

    /**
     * Creates a new GameStateEvent instance using the specified properties.
     * @param [properties] Properties to set
     * @returns GameStateEvent instance
     */
    public static create(properties?: IGameStateEvent): GameStateEvent;

    /**
     * Encodes the specified GameStateEvent message. Does not implicitly {@link GameStateEvent.verify|verify} messages.
     * @param message GameStateEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IGameStateEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified GameStateEvent message, length delimited. Does not implicitly {@link GameStateEvent.verify|verify} messages.
     * @param message GameStateEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IGameStateEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a GameStateEvent message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns GameStateEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): GameStateEvent;

    /**
     * Decodes a GameStateEvent message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns GameStateEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): GameStateEvent;

    /**
     * Verifies a GameStateEvent message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a GameStateEvent message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns GameStateEvent
     */
    public static fromObject(object: { [k: string]: any }): GameStateEvent;

    /**
     * Creates a plain object from a GameStateEvent message. Also converts values to other types if specified.
     * @param message GameStateEvent
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: GameStateEvent, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this GameStateEvent to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a IMessage. */
export interface IIMessage {

    /** IMessage Payload */
    Payload?: (IIMessagePayload|null);

    /** IMessage ServerId */
    ServerId?: (string|null);

    /** IMessage Type */
    Type?: (string|null);
}

/** Represents a IMessage. */
export class IMessage implements IIMessage {

    /**
     * Constructs a new IMessage.
     * @param [properties] Properties to set
     */
    constructor(properties?: IIMessage);

    /** IMessage Payload. */
    public Payload?: (IIMessagePayload|null);

    /** IMessage ServerId. */
    public ServerId: string;

    /** IMessage Type. */
    public Type: string;

    /**
     * Creates a new IMessage instance using the specified properties.
     * @param [properties] Properties to set
     * @returns IMessage instance
     */
    public static create(properties?: IIMessage): IMessage;

    /**
     * Encodes the specified IMessage message. Does not implicitly {@link IMessage.verify|verify} messages.
     * @param message IMessage message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IIMessage, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified IMessage message, length delimited. Does not implicitly {@link IMessage.verify|verify} messages.
     * @param message IMessage message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IIMessage, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a IMessage message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns IMessage
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): IMessage;

    /**
     * Decodes a IMessage message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns IMessage
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): IMessage;

    /**
     * Verifies a IMessage message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a IMessage message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns IMessage
     */
    public static fromObject(object: { [k: string]: any }): IMessage;

    /**
     * Creates a plain object from a IMessage message. Also converts values to other types if specified.
     * @param message IMessage
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: IMessage, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this IMessage to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a IMessagePayload. */
export interface IIMessagePayload {

    /** IMessagePayload PlayerJoinEvent */
    PlayerJoinEvent?: (IPlayerJoinEvent|null);

    /** IMessagePayload PlayerLeftEvent */
    PlayerLeftEvent?: (IPlayerLeftEvent|null);

    /** IMessagePayload PlayerPositionEvent */
    PlayerPositionEvent?: (IPlayerPositionEvent|null);

    /** IMessagePayload PlayerVehicleEvent */
    PlayerVehicleEvent?: (IPlayerVehicleEvent|null);

    /** IMessagePayload PlayerKillEvent */
    PlayerKillEvent?: (IPlayerKillEvent|null);

    /** IMessagePayload PlayerDeathEvent */
    PlayerDeathEvent?: (IPlayerDeathEvent|null);

    /** IMessagePayload PlayerSpawnEvent */
    PlayerSpawnEvent?: (IPlayerSpawnEvent|null);

    /** IMessagePayload PlayerTeamEvent */
    PlayerTeamEvent?: (IPlayerTeamEvent|null);

    /** IMessagePayload PlayerScoreEvent */
    PlayerScoreEvent?: (IPlayerScoreEvent|null);

    /** IMessagePayload ProjectilePositionEvent */
    ProjectilePositionEvent?: (IProjectilePositionEvent|null);

    /** IMessagePayload ChatEvent */
    ChatEvent?: (IChatEvent|null);

    /** IMessagePayload ServerUpdateEvent */
    ServerUpdateEvent?: (IServerUpdateEvent|null);

    /** IMessagePayload GameStateEvent */
    GameStateEvent?: (IGameStateEvent|null);

    /** IMessagePayload MapChangeEvent */
    MapChangeEvent?: (IMapChangeEvent|null);

    /** IMessagePayload UserConnectAction */
    UserConnectAction?: (IUserConnectAction|null);

    /** IMessagePayload UserDisconnectAction */
    UserDisconnectAction?: (IUserDisconnectAction|null);
}

/** Represents a IMessagePayload. */
export class IMessagePayload implements IIMessagePayload {

    /**
     * Constructs a new IMessagePayload.
     * @param [properties] Properties to set
     */
    constructor(properties?: IIMessagePayload);

    /** IMessagePayload PlayerJoinEvent. */
    public PlayerJoinEvent?: (IPlayerJoinEvent|null);

    /** IMessagePayload PlayerLeftEvent. */
    public PlayerLeftEvent?: (IPlayerLeftEvent|null);

    /** IMessagePayload PlayerPositionEvent. */
    public PlayerPositionEvent?: (IPlayerPositionEvent|null);

    /** IMessagePayload PlayerVehicleEvent. */
    public PlayerVehicleEvent?: (IPlayerVehicleEvent|null);

    /** IMessagePayload PlayerKillEvent. */
    public PlayerKillEvent?: (IPlayerKillEvent|null);

    /** IMessagePayload PlayerDeathEvent. */
    public PlayerDeathEvent?: (IPlayerDeathEvent|null);

    /** IMessagePayload PlayerSpawnEvent. */
    public PlayerSpawnEvent?: (IPlayerSpawnEvent|null);

    /** IMessagePayload PlayerTeamEvent. */
    public PlayerTeamEvent?: (IPlayerTeamEvent|null);

    /** IMessagePayload PlayerScoreEvent. */
    public PlayerScoreEvent?: (IPlayerScoreEvent|null);

    /** IMessagePayload ProjectilePositionEvent. */
    public ProjectilePositionEvent?: (IProjectilePositionEvent|null);

    /** IMessagePayload ChatEvent. */
    public ChatEvent?: (IChatEvent|null);

    /** IMessagePayload ServerUpdateEvent. */
    public ServerUpdateEvent?: (IServerUpdateEvent|null);

    /** IMessagePayload GameStateEvent. */
    public GameStateEvent?: (IGameStateEvent|null);

    /** IMessagePayload MapChangeEvent. */
    public MapChangeEvent?: (IMapChangeEvent|null);

    /** IMessagePayload UserConnectAction. */
    public UserConnectAction?: (IUserConnectAction|null);

    /** IMessagePayload UserDisconnectAction. */
    public UserDisconnectAction?: (IUserDisconnectAction|null);

    /**
     * Creates a new IMessagePayload instance using the specified properties.
     * @param [properties] Properties to set
     * @returns IMessagePayload instance
     */
    public static create(properties?: IIMessagePayload): IMessagePayload;

    /**
     * Encodes the specified IMessagePayload message. Does not implicitly {@link IMessagePayload.verify|verify} messages.
     * @param message IMessagePayload message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IIMessagePayload, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified IMessagePayload message, length delimited. Does not implicitly {@link IMessagePayload.verify|verify} messages.
     * @param message IMessagePayload message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IIMessagePayload, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a IMessagePayload message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns IMessagePayload
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): IMessagePayload;

    /**
     * Decodes a IMessagePayload message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns IMessagePayload
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): IMessagePayload;

    /**
     * Verifies a IMessagePayload message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a IMessagePayload message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns IMessagePayload
     */
    public static fromObject(object: { [k: string]: any }): IMessagePayload;

    /**
     * Creates a plain object from a IMessagePayload message. Also converts values to other types if specified.
     * @param message IMessagePayload
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: IMessagePayload, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this IMessagePayload to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a MapChangeEvent. */
export interface IMapChangeEvent {

    /** MapChangeEvent Map */
    Map?: (string|null);

    /** MapChangeEvent Size */
    Size?: (number|null);
}

/** Represents a MapChangeEvent. */
export class MapChangeEvent implements IMapChangeEvent {

    /**
     * Constructs a new MapChangeEvent.
     * @param [properties] Properties to set
     */
    constructor(properties?: IMapChangeEvent);

    /** MapChangeEvent Map. */
    public Map: string;

    /** MapChangeEvent Size. */
    public Size: number;

    /**
     * Creates a new MapChangeEvent instance using the specified properties.
     * @param [properties] Properties to set
     * @returns MapChangeEvent instance
     */
    public static create(properties?: IMapChangeEvent): MapChangeEvent;

    /**
     * Encodes the specified MapChangeEvent message. Does not implicitly {@link MapChangeEvent.verify|verify} messages.
     * @param message MapChangeEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IMapChangeEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified MapChangeEvent message, length delimited. Does not implicitly {@link MapChangeEvent.verify|verify} messages.
     * @param message MapChangeEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IMapChangeEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a MapChangeEvent message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns MapChangeEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): MapChangeEvent;

    /**
     * Decodes a MapChangeEvent message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns MapChangeEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): MapChangeEvent;

    /**
     * Verifies a MapChangeEvent message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a MapChangeEvent message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns MapChangeEvent
     */
    public static fromObject(object: { [k: string]: any }): MapChangeEvent;

    /**
     * Creates a plain object from a MapChangeEvent message. Also converts values to other types if specified.
     * @param message MapChangeEvent
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: MapChangeEvent, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this MapChangeEvent to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a MessageDto. */
export interface IMessageDto {

    /** MessageDto Channel */
    Channel?: (string|null);

    /** MessageDto Flags */
    Flags?: (string|null);

    /** MessageDto PlayerId */
    PlayerId?: (number|null);

    /** MessageDto Text */
    Text?: (string|null);

    /** MessageDto Time */
    Time?: (string|null);

    /** MessageDto Type */
    Type?: (string|null);
}

/** Represents a MessageDto. */
export class MessageDto implements IMessageDto {

    /**
     * Constructs a new MessageDto.
     * @param [properties] Properties to set
     */
    constructor(properties?: IMessageDto);

    /** MessageDto Channel. */
    public Channel: string;

    /** MessageDto Flags. */
    public Flags: string;

    /** MessageDto PlayerId. */
    public PlayerId: number;

    /** MessageDto Text. */
    public Text: string;

    /** MessageDto Time. */
    public Time: string;

    /** MessageDto Type. */
    public Type: string;

    /**
     * Creates a new MessageDto instance using the specified properties.
     * @param [properties] Properties to set
     * @returns MessageDto instance
     */
    public static create(properties?: IMessageDto): MessageDto;

    /**
     * Encodes the specified MessageDto message. Does not implicitly {@link MessageDto.verify|verify} messages.
     * @param message MessageDto message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IMessageDto, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified MessageDto message, length delimited. Does not implicitly {@link MessageDto.verify|verify} messages.
     * @param message MessageDto message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IMessageDto, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a MessageDto message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns MessageDto
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): MessageDto;

    /**
     * Decodes a MessageDto message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns MessageDto
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): MessageDto;

    /**
     * Verifies a MessageDto message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a MessageDto message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns MessageDto
     */
    public static fromObject(object: { [k: string]: any }): MessageDto;

    /**
     * Creates a plain object from a MessageDto message. Also converts values to other types if specified.
     * @param message MessageDto
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: MessageDto, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this MessageDto to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a PlayerDeathEvent. */
export interface IPlayerDeathEvent {

    /** PlayerDeathEvent PlayerId */
    PlayerId?: (number|null);

    /** PlayerDeathEvent Position */
    Position?: (IVector3|null);
}

/** Represents a PlayerDeathEvent. */
export class PlayerDeathEvent implements IPlayerDeathEvent {

    /**
     * Constructs a new PlayerDeathEvent.
     * @param [properties] Properties to set
     */
    constructor(properties?: IPlayerDeathEvent);

    /** PlayerDeathEvent PlayerId. */
    public PlayerId: number;

    /** PlayerDeathEvent Position. */
    public Position?: (IVector3|null);

    /**
     * Creates a new PlayerDeathEvent instance using the specified properties.
     * @param [properties] Properties to set
     * @returns PlayerDeathEvent instance
     */
    public static create(properties?: IPlayerDeathEvent): PlayerDeathEvent;

    /**
     * Encodes the specified PlayerDeathEvent message. Does not implicitly {@link PlayerDeathEvent.verify|verify} messages.
     * @param message PlayerDeathEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IPlayerDeathEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified PlayerDeathEvent message, length delimited. Does not implicitly {@link PlayerDeathEvent.verify|verify} messages.
     * @param message PlayerDeathEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IPlayerDeathEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a PlayerDeathEvent message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns PlayerDeathEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): PlayerDeathEvent;

    /**
     * Decodes a PlayerDeathEvent message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns PlayerDeathEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): PlayerDeathEvent;

    /**
     * Verifies a PlayerDeathEvent message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a PlayerDeathEvent message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns PlayerDeathEvent
     */
    public static fromObject(object: { [k: string]: any }): PlayerDeathEvent;

    /**
     * Creates a plain object from a PlayerDeathEvent message. Also converts values to other types if specified.
     * @param message PlayerDeathEvent
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: PlayerDeathEvent, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this PlayerDeathEvent to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a PlayerDto. */
export interface IPlayerDto {

    /** PlayerDto Country */
    Country?: (string|null);

    /** PlayerDto Hash */
    Hash?: (string|null);

    /** PlayerDto Index */
    Index?: (number|null);

    /** PlayerDto IpAddress */
    IpAddress?: (string|null);

    /** PlayerDto IsAlive */
    IsAlive?: (boolean|null);

    /** PlayerDto Name */
    Name?: (string|null);

    /** PlayerDto Rank */
    Rank?: (number|null);

    /** PlayerDto Score */
    Score?: (IScoreDto|null);

    /** PlayerDto Team */
    Team?: (number|null);
}

/** Represents a PlayerDto. */
export class PlayerDto implements IPlayerDto {

    /**
     * Constructs a new PlayerDto.
     * @param [properties] Properties to set
     */
    constructor(properties?: IPlayerDto);

    /** PlayerDto Country. */
    public Country: string;

    /** PlayerDto Hash. */
    public Hash: string;

    /** PlayerDto Index. */
    public Index: number;

    /** PlayerDto IpAddress. */
    public IpAddress: string;

    /** PlayerDto IsAlive. */
    public IsAlive: boolean;

    /** PlayerDto Name. */
    public Name: string;

    /** PlayerDto Rank. */
    public Rank: number;

    /** PlayerDto Score. */
    public Score?: (IScoreDto|null);

    /** PlayerDto Team. */
    public Team: number;

    /**
     * Creates a new PlayerDto instance using the specified properties.
     * @param [properties] Properties to set
     * @returns PlayerDto instance
     */
    public static create(properties?: IPlayerDto): PlayerDto;

    /**
     * Encodes the specified PlayerDto message. Does not implicitly {@link PlayerDto.verify|verify} messages.
     * @param message PlayerDto message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IPlayerDto, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified PlayerDto message, length delimited. Does not implicitly {@link PlayerDto.verify|verify} messages.
     * @param message PlayerDto message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IPlayerDto, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a PlayerDto message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns PlayerDto
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): PlayerDto;

    /**
     * Decodes a PlayerDto message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns PlayerDto
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): PlayerDto;

    /**
     * Verifies a PlayerDto message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a PlayerDto message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns PlayerDto
     */
    public static fromObject(object: { [k: string]: any }): PlayerDto;

    /**
     * Creates a plain object from a PlayerDto message. Also converts values to other types if specified.
     * @param message PlayerDto
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: PlayerDto, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this PlayerDto to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a PlayerJoinEvent. */
export interface IPlayerJoinEvent {

    /** PlayerJoinEvent Player */
    Player?: (IPlayerDto|null);
}

/** Represents a PlayerJoinEvent. */
export class PlayerJoinEvent implements IPlayerJoinEvent {

    /**
     * Constructs a new PlayerJoinEvent.
     * @param [properties] Properties to set
     */
    constructor(properties?: IPlayerJoinEvent);

    /** PlayerJoinEvent Player. */
    public Player?: (IPlayerDto|null);

    /**
     * Creates a new PlayerJoinEvent instance using the specified properties.
     * @param [properties] Properties to set
     * @returns PlayerJoinEvent instance
     */
    public static create(properties?: IPlayerJoinEvent): PlayerJoinEvent;

    /**
     * Encodes the specified PlayerJoinEvent message. Does not implicitly {@link PlayerJoinEvent.verify|verify} messages.
     * @param message PlayerJoinEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IPlayerJoinEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified PlayerJoinEvent message, length delimited. Does not implicitly {@link PlayerJoinEvent.verify|verify} messages.
     * @param message PlayerJoinEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IPlayerJoinEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a PlayerJoinEvent message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns PlayerJoinEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): PlayerJoinEvent;

    /**
     * Decodes a PlayerJoinEvent message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns PlayerJoinEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): PlayerJoinEvent;

    /**
     * Verifies a PlayerJoinEvent message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a PlayerJoinEvent message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns PlayerJoinEvent
     */
    public static fromObject(object: { [k: string]: any }): PlayerJoinEvent;

    /**
     * Creates a plain object from a PlayerJoinEvent message. Also converts values to other types if specified.
     * @param message PlayerJoinEvent
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: PlayerJoinEvent, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this PlayerJoinEvent to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a PlayerKillEvent. */
export interface IPlayerKillEvent {

    /** PlayerKillEvent AttackerId */
    AttackerId?: (number|null);

    /** PlayerKillEvent AttackerPosition */
    AttackerPosition?: (IVector3|null);

    /** PlayerKillEvent VictimId */
    VictimId?: (number|null);

    /** PlayerKillEvent VictimPosition */
    VictimPosition?: (IVector3|null);

    /** PlayerKillEvent Weapon */
    Weapon?: (string|null);
}

/** Represents a PlayerKillEvent. */
export class PlayerKillEvent implements IPlayerKillEvent {

    /**
     * Constructs a new PlayerKillEvent.
     * @param [properties] Properties to set
     */
    constructor(properties?: IPlayerKillEvent);

    /** PlayerKillEvent AttackerId. */
    public AttackerId: number;

    /** PlayerKillEvent AttackerPosition. */
    public AttackerPosition?: (IVector3|null);

    /** PlayerKillEvent VictimId. */
    public VictimId: number;

    /** PlayerKillEvent VictimPosition. */
    public VictimPosition?: (IVector3|null);

    /** PlayerKillEvent Weapon. */
    public Weapon: string;

    /**
     * Creates a new PlayerKillEvent instance using the specified properties.
     * @param [properties] Properties to set
     * @returns PlayerKillEvent instance
     */
    public static create(properties?: IPlayerKillEvent): PlayerKillEvent;

    /**
     * Encodes the specified PlayerKillEvent message. Does not implicitly {@link PlayerKillEvent.verify|verify} messages.
     * @param message PlayerKillEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IPlayerKillEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified PlayerKillEvent message, length delimited. Does not implicitly {@link PlayerKillEvent.verify|verify} messages.
     * @param message PlayerKillEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IPlayerKillEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a PlayerKillEvent message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns PlayerKillEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): PlayerKillEvent;

    /**
     * Decodes a PlayerKillEvent message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns PlayerKillEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): PlayerKillEvent;

    /**
     * Verifies a PlayerKillEvent message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a PlayerKillEvent message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns PlayerKillEvent
     */
    public static fromObject(object: { [k: string]: any }): PlayerKillEvent;

    /**
     * Creates a plain object from a PlayerKillEvent message. Also converts values to other types if specified.
     * @param message PlayerKillEvent
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: PlayerKillEvent, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this PlayerKillEvent to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a PlayerLeftEvent. */
export interface IPlayerLeftEvent {

    /** PlayerLeftEvent PlayerId */
    PlayerId?: (number|null);
}

/** Represents a PlayerLeftEvent. */
export class PlayerLeftEvent implements IPlayerLeftEvent {

    /**
     * Constructs a new PlayerLeftEvent.
     * @param [properties] Properties to set
     */
    constructor(properties?: IPlayerLeftEvent);

    /** PlayerLeftEvent PlayerId. */
    public PlayerId: number;

    /**
     * Creates a new PlayerLeftEvent instance using the specified properties.
     * @param [properties] Properties to set
     * @returns PlayerLeftEvent instance
     */
    public static create(properties?: IPlayerLeftEvent): PlayerLeftEvent;

    /**
     * Encodes the specified PlayerLeftEvent message. Does not implicitly {@link PlayerLeftEvent.verify|verify} messages.
     * @param message PlayerLeftEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IPlayerLeftEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified PlayerLeftEvent message, length delimited. Does not implicitly {@link PlayerLeftEvent.verify|verify} messages.
     * @param message PlayerLeftEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IPlayerLeftEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a PlayerLeftEvent message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns PlayerLeftEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): PlayerLeftEvent;

    /**
     * Decodes a PlayerLeftEvent message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns PlayerLeftEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): PlayerLeftEvent;

    /**
     * Verifies a PlayerLeftEvent message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a PlayerLeftEvent message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns PlayerLeftEvent
     */
    public static fromObject(object: { [k: string]: any }): PlayerLeftEvent;

    /**
     * Creates a plain object from a PlayerLeftEvent message. Also converts values to other types if specified.
     * @param message PlayerLeftEvent
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: PlayerLeftEvent, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this PlayerLeftEvent to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a PlayerPositionEvent. */
export interface IPlayerPositionEvent {

    /** PlayerPositionEvent Ping */
    Ping?: (number|null);

    /** PlayerPositionEvent PlayerId */
    PlayerId?: (number|null);

    /** PlayerPositionEvent Position */
    Position?: (IVector3|null);

    /** PlayerPositionEvent Rotation */
    Rotation?: (IVector3|null);
}

/** Represents a PlayerPositionEvent. */
export class PlayerPositionEvent implements IPlayerPositionEvent {

    /**
     * Constructs a new PlayerPositionEvent.
     * @param [properties] Properties to set
     */
    constructor(properties?: IPlayerPositionEvent);

    /** PlayerPositionEvent Ping. */
    public Ping: number;

    /** PlayerPositionEvent PlayerId. */
    public PlayerId: number;

    /** PlayerPositionEvent Position. */
    public Position?: (IVector3|null);

    /** PlayerPositionEvent Rotation. */
    public Rotation?: (IVector3|null);

    /**
     * Creates a new PlayerPositionEvent instance using the specified properties.
     * @param [properties] Properties to set
     * @returns PlayerPositionEvent instance
     */
    public static create(properties?: IPlayerPositionEvent): PlayerPositionEvent;

    /**
     * Encodes the specified PlayerPositionEvent message. Does not implicitly {@link PlayerPositionEvent.verify|verify} messages.
     * @param message PlayerPositionEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IPlayerPositionEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified PlayerPositionEvent message, length delimited. Does not implicitly {@link PlayerPositionEvent.verify|verify} messages.
     * @param message PlayerPositionEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IPlayerPositionEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a PlayerPositionEvent message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns PlayerPositionEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): PlayerPositionEvent;

    /**
     * Decodes a PlayerPositionEvent message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns PlayerPositionEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): PlayerPositionEvent;

    /**
     * Verifies a PlayerPositionEvent message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a PlayerPositionEvent message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns PlayerPositionEvent
     */
    public static fromObject(object: { [k: string]: any }): PlayerPositionEvent;

    /**
     * Creates a plain object from a PlayerPositionEvent message. Also converts values to other types if specified.
     * @param message PlayerPositionEvent
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: PlayerPositionEvent, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this PlayerPositionEvent to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a PlayerScoreEvent. */
export interface IPlayerScoreEvent {

    /** PlayerScoreEvent Deaths */
    Deaths?: (number|null);

    /** PlayerScoreEvent Kills */
    Kills?: (number|null);

    /** PlayerScoreEvent PlayerId */
    PlayerId?: (number|null);

    /** PlayerScoreEvent TeamScore */
    TeamScore?: (number|null);

    /** PlayerScoreEvent TotalScore */
    TotalScore?: (number|null);
}

/** Represents a PlayerScoreEvent. */
export class PlayerScoreEvent implements IPlayerScoreEvent {

    /**
     * Constructs a new PlayerScoreEvent.
     * @param [properties] Properties to set
     */
    constructor(properties?: IPlayerScoreEvent);

    /** PlayerScoreEvent Deaths. */
    public Deaths: number;

    /** PlayerScoreEvent Kills. */
    public Kills: number;

    /** PlayerScoreEvent PlayerId. */
    public PlayerId: number;

    /** PlayerScoreEvent TeamScore. */
    public TeamScore: number;

    /** PlayerScoreEvent TotalScore. */
    public TotalScore: number;

    /**
     * Creates a new PlayerScoreEvent instance using the specified properties.
     * @param [properties] Properties to set
     * @returns PlayerScoreEvent instance
     */
    public static create(properties?: IPlayerScoreEvent): PlayerScoreEvent;

    /**
     * Encodes the specified PlayerScoreEvent message. Does not implicitly {@link PlayerScoreEvent.verify|verify} messages.
     * @param message PlayerScoreEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IPlayerScoreEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified PlayerScoreEvent message, length delimited. Does not implicitly {@link PlayerScoreEvent.verify|verify} messages.
     * @param message PlayerScoreEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IPlayerScoreEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a PlayerScoreEvent message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns PlayerScoreEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): PlayerScoreEvent;

    /**
     * Decodes a PlayerScoreEvent message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns PlayerScoreEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): PlayerScoreEvent;

    /**
     * Verifies a PlayerScoreEvent message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a PlayerScoreEvent message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns PlayerScoreEvent
     */
    public static fromObject(object: { [k: string]: any }): PlayerScoreEvent;

    /**
     * Creates a plain object from a PlayerScoreEvent message. Also converts values to other types if specified.
     * @param message PlayerScoreEvent
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: PlayerScoreEvent, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this PlayerScoreEvent to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a PlayerSpawnEvent. */
export interface IPlayerSpawnEvent {

    /** PlayerSpawnEvent PlayerId */
    PlayerId?: (number|null);

    /** PlayerSpawnEvent Position */
    Position?: (IVector3|null);

    /** PlayerSpawnEvent Rotation */
    Rotation?: (IVector3|null);
}

/** Represents a PlayerSpawnEvent. */
export class PlayerSpawnEvent implements IPlayerSpawnEvent {

    /**
     * Constructs a new PlayerSpawnEvent.
     * @param [properties] Properties to set
     */
    constructor(properties?: IPlayerSpawnEvent);

    /** PlayerSpawnEvent PlayerId. */
    public PlayerId: number;

    /** PlayerSpawnEvent Position. */
    public Position?: (IVector3|null);

    /** PlayerSpawnEvent Rotation. */
    public Rotation?: (IVector3|null);

    /**
     * Creates a new PlayerSpawnEvent instance using the specified properties.
     * @param [properties] Properties to set
     * @returns PlayerSpawnEvent instance
     */
    public static create(properties?: IPlayerSpawnEvent): PlayerSpawnEvent;

    /**
     * Encodes the specified PlayerSpawnEvent message. Does not implicitly {@link PlayerSpawnEvent.verify|verify} messages.
     * @param message PlayerSpawnEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IPlayerSpawnEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified PlayerSpawnEvent message, length delimited. Does not implicitly {@link PlayerSpawnEvent.verify|verify} messages.
     * @param message PlayerSpawnEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IPlayerSpawnEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a PlayerSpawnEvent message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns PlayerSpawnEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): PlayerSpawnEvent;

    /**
     * Decodes a PlayerSpawnEvent message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns PlayerSpawnEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): PlayerSpawnEvent;

    /**
     * Verifies a PlayerSpawnEvent message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a PlayerSpawnEvent message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns PlayerSpawnEvent
     */
    public static fromObject(object: { [k: string]: any }): PlayerSpawnEvent;

    /**
     * Creates a plain object from a PlayerSpawnEvent message. Also converts values to other types if specified.
     * @param message PlayerSpawnEvent
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: PlayerSpawnEvent, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this PlayerSpawnEvent to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a PlayerTeamEvent. */
export interface IPlayerTeamEvent {

    /** PlayerTeamEvent PlayerId */
    PlayerId?: (number|null);

    /** PlayerTeamEvent TeamId */
    TeamId?: (number|null);
}

/** Represents a PlayerTeamEvent. */
export class PlayerTeamEvent implements IPlayerTeamEvent {

    /**
     * Constructs a new PlayerTeamEvent.
     * @param [properties] Properties to set
     */
    constructor(properties?: IPlayerTeamEvent);

    /** PlayerTeamEvent PlayerId. */
    public PlayerId: number;

    /** PlayerTeamEvent TeamId. */
    public TeamId: number;

    /**
     * Creates a new PlayerTeamEvent instance using the specified properties.
     * @param [properties] Properties to set
     * @returns PlayerTeamEvent instance
     */
    public static create(properties?: IPlayerTeamEvent): PlayerTeamEvent;

    /**
     * Encodes the specified PlayerTeamEvent message. Does not implicitly {@link PlayerTeamEvent.verify|verify} messages.
     * @param message PlayerTeamEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IPlayerTeamEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified PlayerTeamEvent message, length delimited. Does not implicitly {@link PlayerTeamEvent.verify|verify} messages.
     * @param message PlayerTeamEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IPlayerTeamEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a PlayerTeamEvent message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns PlayerTeamEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): PlayerTeamEvent;

    /**
     * Decodes a PlayerTeamEvent message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns PlayerTeamEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): PlayerTeamEvent;

    /**
     * Verifies a PlayerTeamEvent message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a PlayerTeamEvent message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns PlayerTeamEvent
     */
    public static fromObject(object: { [k: string]: any }): PlayerTeamEvent;

    /**
     * Creates a plain object from a PlayerTeamEvent message. Also converts values to other types if specified.
     * @param message PlayerTeamEvent
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: PlayerTeamEvent, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this PlayerTeamEvent to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a PlayerVehicleEvent. */
export interface IPlayerVehicleEvent {

    /** PlayerVehicleEvent PlayerId */
    PlayerId?: (number|null);

    /** PlayerVehicleEvent Vehicle */
    Vehicle?: (IVehicleDto|null);
}

/** Represents a PlayerVehicleEvent. */
export class PlayerVehicleEvent implements IPlayerVehicleEvent {

    /**
     * Constructs a new PlayerVehicleEvent.
     * @param [properties] Properties to set
     */
    constructor(properties?: IPlayerVehicleEvent);

    /** PlayerVehicleEvent PlayerId. */
    public PlayerId: number;

    /** PlayerVehicleEvent Vehicle. */
    public Vehicle?: (IVehicleDto|null);

    /**
     * Creates a new PlayerVehicleEvent instance using the specified properties.
     * @param [properties] Properties to set
     * @returns PlayerVehicleEvent instance
     */
    public static create(properties?: IPlayerVehicleEvent): PlayerVehicleEvent;

    /**
     * Encodes the specified PlayerVehicleEvent message. Does not implicitly {@link PlayerVehicleEvent.verify|verify} messages.
     * @param message PlayerVehicleEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IPlayerVehicleEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified PlayerVehicleEvent message, length delimited. Does not implicitly {@link PlayerVehicleEvent.verify|verify} messages.
     * @param message PlayerVehicleEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IPlayerVehicleEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a PlayerVehicleEvent message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns PlayerVehicleEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): PlayerVehicleEvent;

    /**
     * Decodes a PlayerVehicleEvent message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns PlayerVehicleEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): PlayerVehicleEvent;

    /**
     * Verifies a PlayerVehicleEvent message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a PlayerVehicleEvent message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns PlayerVehicleEvent
     */
    public static fromObject(object: { [k: string]: any }): PlayerVehicleEvent;

    /**
     * Creates a plain object from a PlayerVehicleEvent message. Also converts values to other types if specified.
     * @param message PlayerVehicleEvent
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: PlayerVehicleEvent, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this PlayerVehicleEvent to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a ProjectilePositionEvent. */
export interface IProjectilePositionEvent {

    /** ProjectilePositionEvent Position */
    Position?: (IVector3|null);

    /** ProjectilePositionEvent ProjectileId */
    ProjectileId?: (number|null);

    /** ProjectilePositionEvent Rotation */
    Rotation?: (IVector3|null);

    /** ProjectilePositionEvent Template */
    Template?: (string|null);
}

/** Represents a ProjectilePositionEvent. */
export class ProjectilePositionEvent implements IProjectilePositionEvent {

    /**
     * Constructs a new ProjectilePositionEvent.
     * @param [properties] Properties to set
     */
    constructor(properties?: IProjectilePositionEvent);

    /** ProjectilePositionEvent Position. */
    public Position?: (IVector3|null);

    /** ProjectilePositionEvent ProjectileId. */
    public ProjectileId: number;

    /** ProjectilePositionEvent Rotation. */
    public Rotation?: (IVector3|null);

    /** ProjectilePositionEvent Template. */
    public Template: string;

    /**
     * Creates a new ProjectilePositionEvent instance using the specified properties.
     * @param [properties] Properties to set
     * @returns ProjectilePositionEvent instance
     */
    public static create(properties?: IProjectilePositionEvent): ProjectilePositionEvent;

    /**
     * Encodes the specified ProjectilePositionEvent message. Does not implicitly {@link ProjectilePositionEvent.verify|verify} messages.
     * @param message ProjectilePositionEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IProjectilePositionEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified ProjectilePositionEvent message, length delimited. Does not implicitly {@link ProjectilePositionEvent.verify|verify} messages.
     * @param message ProjectilePositionEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IProjectilePositionEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a ProjectilePositionEvent message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns ProjectilePositionEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): ProjectilePositionEvent;

    /**
     * Decodes a ProjectilePositionEvent message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns ProjectilePositionEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): ProjectilePositionEvent;

    /**
     * Verifies a ProjectilePositionEvent message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a ProjectilePositionEvent message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns ProjectilePositionEvent
     */
    public static fromObject(object: { [k: string]: any }): ProjectilePositionEvent;

    /**
     * Creates a plain object from a ProjectilePositionEvent message. Also converts values to other types if specified.
     * @param message ProjectilePositionEvent
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: ProjectilePositionEvent, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this ProjectilePositionEvent to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a ScoreDto. */
export interface IScoreDto {

    /** ScoreDto Deaths */
    Deaths?: (number|null);

    /** ScoreDto Kills */
    Kills?: (number|null);

    /** ScoreDto Ping */
    Ping?: (number|null);

    /** ScoreDto Team */
    Team?: (number|null);

    /** ScoreDto Total */
    Total?: (number|null);
}

/** Represents a ScoreDto. */
export class ScoreDto implements IScoreDto {

    /**
     * Constructs a new ScoreDto.
     * @param [properties] Properties to set
     */
    constructor(properties?: IScoreDto);

    /** ScoreDto Deaths. */
    public Deaths: number;

    /** ScoreDto Kills. */
    public Kills: number;

    /** ScoreDto Ping. */
    public Ping: number;

    /** ScoreDto Team. */
    public Team: number;

    /** ScoreDto Total. */
    public Total: number;

    /**
     * Creates a new ScoreDto instance using the specified properties.
     * @param [properties] Properties to set
     * @returns ScoreDto instance
     */
    public static create(properties?: IScoreDto): ScoreDto;

    /**
     * Encodes the specified ScoreDto message. Does not implicitly {@link ScoreDto.verify|verify} messages.
     * @param message ScoreDto message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IScoreDto, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified ScoreDto message, length delimited. Does not implicitly {@link ScoreDto.verify|verify} messages.
     * @param message ScoreDto message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IScoreDto, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a ScoreDto message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns ScoreDto
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): ScoreDto;

    /**
     * Decodes a ScoreDto message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns ScoreDto
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): ScoreDto;

    /**
     * Verifies a ScoreDto message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a ScoreDto message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns ScoreDto
     */
    public static fromObject(object: { [k: string]: any }): ScoreDto;

    /**
     * Creates a plain object from a ScoreDto message. Also converts values to other types if specified.
     * @param message ScoreDto
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: ScoreDto, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this ScoreDto to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a ServerUpdateEvent. */
export interface IServerUpdateEvent {

    /** ServerUpdateEvent GamePort */
    GamePort?: (number|null);

    /** ServerUpdateEvent Id */
    Id?: (string|null);

    /** ServerUpdateEvent IpAddress */
    IpAddress?: (string|null);

    /** ServerUpdateEvent Map */
    Map?: (string|null);

    /** ServerUpdateEvent MaxPlayers */
    MaxPlayers?: (number|null);

    /** ServerUpdateEvent Name */
    Name?: (string|null);

    /** ServerUpdateEvent Players */
    Players?: (number|null);

    /** ServerUpdateEvent QueryPort */
    QueryPort?: (number|null);
}

/** Represents a ServerUpdateEvent. */
export class ServerUpdateEvent implements IServerUpdateEvent {

    /**
     * Constructs a new ServerUpdateEvent.
     * @param [properties] Properties to set
     */
    constructor(properties?: IServerUpdateEvent);

    /** ServerUpdateEvent GamePort. */
    public GamePort: number;

    /** ServerUpdateEvent Id. */
    public Id: string;

    /** ServerUpdateEvent IpAddress. */
    public IpAddress: string;

    /** ServerUpdateEvent Map. */
    public Map: string;

    /** ServerUpdateEvent MaxPlayers. */
    public MaxPlayers: number;

    /** ServerUpdateEvent Name. */
    public Name: string;

    /** ServerUpdateEvent Players. */
    public Players: number;

    /** ServerUpdateEvent QueryPort. */
    public QueryPort: number;

    /**
     * Creates a new ServerUpdateEvent instance using the specified properties.
     * @param [properties] Properties to set
     * @returns ServerUpdateEvent instance
     */
    public static create(properties?: IServerUpdateEvent): ServerUpdateEvent;

    /**
     * Encodes the specified ServerUpdateEvent message. Does not implicitly {@link ServerUpdateEvent.verify|verify} messages.
     * @param message ServerUpdateEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IServerUpdateEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified ServerUpdateEvent message, length delimited. Does not implicitly {@link ServerUpdateEvent.verify|verify} messages.
     * @param message ServerUpdateEvent message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IServerUpdateEvent, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a ServerUpdateEvent message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns ServerUpdateEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): ServerUpdateEvent;

    /**
     * Decodes a ServerUpdateEvent message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns ServerUpdateEvent
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): ServerUpdateEvent;

    /**
     * Verifies a ServerUpdateEvent message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a ServerUpdateEvent message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns ServerUpdateEvent
     */
    public static fromObject(object: { [k: string]: any }): ServerUpdateEvent;

    /**
     * Creates a plain object from a ServerUpdateEvent message. Also converts values to other types if specified.
     * @param message ServerUpdateEvent
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: ServerUpdateEvent, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this ServerUpdateEvent to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a UserConnectAction. */
export interface IUserConnectAction {

    /** UserConnectAction Id */
    Id?: (string|null);
}

/** Represents a UserConnectAction. */
export class UserConnectAction implements IUserConnectAction {

    /**
     * Constructs a new UserConnectAction.
     * @param [properties] Properties to set
     */
    constructor(properties?: IUserConnectAction);

    /** UserConnectAction Id. */
    public Id: string;

    /**
     * Creates a new UserConnectAction instance using the specified properties.
     * @param [properties] Properties to set
     * @returns UserConnectAction instance
     */
    public static create(properties?: IUserConnectAction): UserConnectAction;

    /**
     * Encodes the specified UserConnectAction message. Does not implicitly {@link UserConnectAction.verify|verify} messages.
     * @param message UserConnectAction message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IUserConnectAction, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified UserConnectAction message, length delimited. Does not implicitly {@link UserConnectAction.verify|verify} messages.
     * @param message UserConnectAction message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IUserConnectAction, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a UserConnectAction message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns UserConnectAction
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): UserConnectAction;

    /**
     * Decodes a UserConnectAction message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns UserConnectAction
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): UserConnectAction;

    /**
     * Verifies a UserConnectAction message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a UserConnectAction message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns UserConnectAction
     */
    public static fromObject(object: { [k: string]: any }): UserConnectAction;

    /**
     * Creates a plain object from a UserConnectAction message. Also converts values to other types if specified.
     * @param message UserConnectAction
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: UserConnectAction, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this UserConnectAction to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a UserDisconnectAction. */
export interface IUserDisconnectAction {

    /** UserDisconnectAction Id */
    Id?: (string|null);
}

/** Represents a UserDisconnectAction. */
export class UserDisconnectAction implements IUserDisconnectAction {

    /**
     * Constructs a new UserDisconnectAction.
     * @param [properties] Properties to set
     */
    constructor(properties?: IUserDisconnectAction);

    /** UserDisconnectAction Id. */
    public Id: string;

    /**
     * Creates a new UserDisconnectAction instance using the specified properties.
     * @param [properties] Properties to set
     * @returns UserDisconnectAction instance
     */
    public static create(properties?: IUserDisconnectAction): UserDisconnectAction;

    /**
     * Encodes the specified UserDisconnectAction message. Does not implicitly {@link UserDisconnectAction.verify|verify} messages.
     * @param message UserDisconnectAction message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IUserDisconnectAction, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified UserDisconnectAction message, length delimited. Does not implicitly {@link UserDisconnectAction.verify|verify} messages.
     * @param message UserDisconnectAction message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IUserDisconnectAction, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a UserDisconnectAction message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns UserDisconnectAction
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): UserDisconnectAction;

    /**
     * Decodes a UserDisconnectAction message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns UserDisconnectAction
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): UserDisconnectAction;

    /**
     * Verifies a UserDisconnectAction message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a UserDisconnectAction message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns UserDisconnectAction
     */
    public static fromObject(object: { [k: string]: any }): UserDisconnectAction;

    /**
     * Creates a plain object from a UserDisconnectAction message. Also converts values to other types if specified.
     * @param message UserDisconnectAction
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: UserDisconnectAction, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this UserDisconnectAction to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a Vector3. */
export interface IVector3 {

    /** Vector3 X */
    X?: (number|null);

    /** Vector3 Y */
    Y?: (number|null);

    /** Vector3 Z */
    Z?: (number|null);
}

/** Represents a Vector3. */
export class Vector3 implements IVector3 {

    /**
     * Constructs a new Vector3.
     * @param [properties] Properties to set
     */
    constructor(properties?: IVector3);

    /** Vector3 X. */
    public X: number;

    /** Vector3 Y. */
    public Y: number;

    /** Vector3 Z. */
    public Z: number;

    /**
     * Creates a new Vector3 instance using the specified properties.
     * @param [properties] Properties to set
     * @returns Vector3 instance
     */
    public static create(properties?: IVector3): Vector3;

    /**
     * Encodes the specified Vector3 message. Does not implicitly {@link Vector3.verify|verify} messages.
     * @param message Vector3 message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IVector3, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified Vector3 message, length delimited. Does not implicitly {@link Vector3.verify|verify} messages.
     * @param message Vector3 message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IVector3, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a Vector3 message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns Vector3
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): Vector3;

    /**
     * Decodes a Vector3 message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns Vector3
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): Vector3;

    /**
     * Verifies a Vector3 message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a Vector3 message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns Vector3
     */
    public static fromObject(object: { [k: string]: any }): Vector3;

    /**
     * Creates a plain object from a Vector3 message. Also converts values to other types if specified.
     * @param message Vector3
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: Vector3, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this Vector3 to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}

/** Properties of a VehicleDto. */
export interface IVehicleDto {

    /** VehicleDto RootVehicleId */
    RootVehicleId?: (number|null);

    /** VehicleDto RootVehicleTemplate */
    RootVehicleTemplate?: (string|null);
}

/** Represents a VehicleDto. */
export class VehicleDto implements IVehicleDto {

    /**
     * Constructs a new VehicleDto.
     * @param [properties] Properties to set
     */
    constructor(properties?: IVehicleDto);

    /** VehicleDto RootVehicleId. */
    public RootVehicleId: number;

    /** VehicleDto RootVehicleTemplate. */
    public RootVehicleTemplate: string;

    /**
     * Creates a new VehicleDto instance using the specified properties.
     * @param [properties] Properties to set
     * @returns VehicleDto instance
     */
    public static create(properties?: IVehicleDto): VehicleDto;

    /**
     * Encodes the specified VehicleDto message. Does not implicitly {@link VehicleDto.verify|verify} messages.
     * @param message VehicleDto message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encode(message: IVehicleDto, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Encodes the specified VehicleDto message, length delimited. Does not implicitly {@link VehicleDto.verify|verify} messages.
     * @param message VehicleDto message or plain object to encode
     * @param [writer] Writer to encode to
     * @returns Writer
     */
    public static encodeDelimited(message: IVehicleDto, writer?: $protobuf.Writer): $protobuf.Writer;

    /**
     * Decodes a VehicleDto message from the specified reader or buffer.
     * @param reader Reader or buffer to decode from
     * @param [length] Message length if known beforehand
     * @returns VehicleDto
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decode(reader: ($protobuf.Reader|Uint8Array), length?: number): VehicleDto;

    /**
     * Decodes a VehicleDto message from the specified reader or buffer, length delimited.
     * @param reader Reader or buffer to decode from
     * @returns VehicleDto
     * @throws {Error} If the payload is not a reader or valid buffer
     * @throws {$protobuf.util.ProtocolError} If required fields are missing
     */
    public static decodeDelimited(reader: ($protobuf.Reader|Uint8Array)): VehicleDto;

    /**
     * Verifies a VehicleDto message.
     * @param message Plain object to verify
     * @returns `null` if valid, otherwise the reason why it is not
     */
    public static verify(message: { [k: string]: any }): (string|null);

    /**
     * Creates a VehicleDto message from a plain object. Also converts values to their respective internal types.
     * @param object Plain object
     * @returns VehicleDto
     */
    public static fromObject(object: { [k: string]: any }): VehicleDto;

    /**
     * Creates a plain object from a VehicleDto message. Also converts values to other types if specified.
     * @param message VehicleDto
     * @param [options] Conversion options
     * @returns Plain object
     */
    public static toObject(message: VehicleDto, options?: $protobuf.IConversionOptions): { [k: string]: any };

    /**
     * Converts this VehicleDto to JSON.
     * @returns JSON object
     */
    public toJSON(): { [k: string]: any };
}
