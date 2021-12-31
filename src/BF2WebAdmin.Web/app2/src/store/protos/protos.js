export const ignore = true;
// /*eslint-disable block-scoped-var, no-redeclare, no-control-regex, no-prototype-builtins*/
// ("use strict");

// var $protobuf = require("protobufjs/minimal");

// // Common aliases
// var $Reader = $protobuf.Reader,
//   $Writer = $protobuf.Writer,
//   $util = $protobuf.util;

// // Exported root namespace
// var $root = $protobuf.roots["default"] || ($protobuf.roots["default"] = {});

// $root.ChatEvent = (function() {
//   /**
//    * Properties of a ChatEvent.
//    * @exports IChatEvent
//    * @interface IChatEvent
//    * @property {IMessageDto|null} [Message] ChatEvent Message
//    */

//   /**
//    * Constructs a new ChatEvent.
//    * @exports ChatEvent
//    * @classdesc Represents a ChatEvent.
//    * @implements IChatEvent
//    * @constructor
//    * @param {IChatEvent=} [properties] Properties to set
//    */
//   function ChatEvent(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * ChatEvent Message.
//    * @member {IMessageDto|null|undefined} Message
//    * @memberof ChatEvent
//    * @instance
//    */
//   ChatEvent.prototype.Message = null;

//   /**
//    * Creates a new ChatEvent instance using the specified properties.
//    * @function create
//    * @memberof ChatEvent
//    * @static
//    * @param {IChatEvent=} [properties] Properties to set
//    * @returns {ChatEvent} ChatEvent instance
//    */
//   ChatEvent.create = function create(properties) {
//     return new ChatEvent(properties);
//   };

//   /**
//    * Encodes the specified ChatEvent message. Does not implicitly {@link ChatEvent.verify|verify} messages.
//    * @function encode
//    * @memberof ChatEvent
//    * @static
//    * @param {IChatEvent} message ChatEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   ChatEvent.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.Message != null && message.hasOwnProperty("Message"))
//       $root.MessageDto.encode(message.Message, writer.uint32(/* id 1, wireType 2 =*/ 10).fork()).ldelim();
//     return writer;
//   };

//   /**
//    * Encodes the specified ChatEvent message, length delimited. Does not implicitly {@link ChatEvent.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof ChatEvent
//    * @static
//    * @param {IChatEvent} message ChatEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   ChatEvent.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a ChatEvent message from the specified reader or buffer.
//    * @function decode
//    * @memberof ChatEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {ChatEvent} ChatEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   ChatEvent.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.ChatEvent();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.Message = $root.MessageDto.decode(reader, reader.uint32());
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a ChatEvent message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof ChatEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {ChatEvent} ChatEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   ChatEvent.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a ChatEvent message.
//    * @function verify
//    * @memberof ChatEvent
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   ChatEvent.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.Message != null && message.hasOwnProperty("Message")) {
//       var error = $root.MessageDto.verify(message.Message);
//       if (error) return "Message." + error;
//     }
//     return null;
//   };

//   /**
//    * Creates a ChatEvent message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof ChatEvent
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {ChatEvent} ChatEvent
//    */
//   ChatEvent.fromObject = function fromObject(object) {
//     if (object instanceof $root.ChatEvent) return object;
//     var message = new $root.ChatEvent();
//     if (object.Message != null) {
//       if (typeof object.Message !== "object") throw TypeError(".ChatEvent.Message: object expected");
//       message.Message = $root.MessageDto.fromObject(object.Message);
//     }
//     return message;
//   };

//   /**
//    * Creates a plain object from a ChatEvent message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof ChatEvent
//    * @static
//    * @param {ChatEvent} message ChatEvent
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   ChatEvent.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) object.Message = null;
//     if (message.Message != null && message.hasOwnProperty("Message"))
//       object.Message = $root.MessageDto.toObject(message.Message, options);
//     return object;
//   };

//   /**
//    * Converts this ChatEvent to JSON.
//    * @function toJSON
//    * @memberof ChatEvent
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   ChatEvent.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return ChatEvent;
// })();

// $root.GameStateEvent = (function() {
//   /**
//    * Properties of a GameStateEvent.
//    * @exports IGameStateEvent
//    * @interface IGameStateEvent
//    * @property {string|null} [State] GameStateEvent State
//    */

//   /**
//    * Constructs a new GameStateEvent.
//    * @exports GameStateEvent
//    * @classdesc Represents a GameStateEvent.
//    * @implements IGameStateEvent
//    * @constructor
//    * @param {IGameStateEvent=} [properties] Properties to set
//    */
//   function GameStateEvent(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * GameStateEvent State.
//    * @member {string} State
//    * @memberof GameStateEvent
//    * @instance
//    */
//   GameStateEvent.prototype.State = "";

//   /**
//    * Creates a new GameStateEvent instance using the specified properties.
//    * @function create
//    * @memberof GameStateEvent
//    * @static
//    * @param {IGameStateEvent=} [properties] Properties to set
//    * @returns {GameStateEvent} GameStateEvent instance
//    */
//   GameStateEvent.create = function create(properties) {
//     return new GameStateEvent(properties);
//   };

//   /**
//    * Encodes the specified GameStateEvent message. Does not implicitly {@link GameStateEvent.verify|verify} messages.
//    * @function encode
//    * @memberof GameStateEvent
//    * @static
//    * @param {IGameStateEvent} message GameStateEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   GameStateEvent.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.State != null && message.hasOwnProperty("State"))
//       writer.uint32(/* id 1, wireType 2 =*/ 10).string(message.State);
//     return writer;
//   };

//   /**
//    * Encodes the specified GameStateEvent message, length delimited. Does not implicitly {@link GameStateEvent.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof GameStateEvent
//    * @static
//    * @param {IGameStateEvent} message GameStateEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   GameStateEvent.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a GameStateEvent message from the specified reader or buffer.
//    * @function decode
//    * @memberof GameStateEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {GameStateEvent} GameStateEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   GameStateEvent.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.GameStateEvent();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.State = reader.string();
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a GameStateEvent message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof GameStateEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {GameStateEvent} GameStateEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   GameStateEvent.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a GameStateEvent message.
//    * @function verify
//    * @memberof GameStateEvent
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   GameStateEvent.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.State != null && message.hasOwnProperty("State"))
//       if (!$util.isString(message.State)) return "State: string expected";
//     return null;
//   };

//   /**
//    * Creates a GameStateEvent message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof GameStateEvent
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {GameStateEvent} GameStateEvent
//    */
//   GameStateEvent.fromObject = function fromObject(object) {
//     if (object instanceof $root.GameStateEvent) return object;
//     var message = new $root.GameStateEvent();
//     if (object.State != null) message.State = String(object.State);
//     return message;
//   };

//   /**
//    * Creates a plain object from a GameStateEvent message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof GameStateEvent
//    * @static
//    * @param {GameStateEvent} message GameStateEvent
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   GameStateEvent.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) object.State = "";
//     if (message.State != null && message.hasOwnProperty("State")) object.State = message.State;
//     return object;
//   };

//   /**
//    * Converts this GameStateEvent to JSON.
//    * @function toJSON
//    * @memberof GameStateEvent
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   GameStateEvent.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return GameStateEvent;
// })();

// $root.IMessage = (function() {
//   /**
//    * Properties of a IMessage.
//    * @exports IIMessage
//    * @interface IIMessage
//    * @property {IIMessagePayload|null} [Payload] IMessage Payload
//    * @property {string|null} [ServerId] IMessage ServerId
//    * @property {string|null} [Type] IMessage Type
//    */

//   /**
//    * Constructs a new IMessage.
//    * @exports IMessage
//    * @classdesc Represents a IMessage.
//    * @implements IIMessage
//    * @constructor
//    * @param {IIMessage=} [properties] Properties to set
//    */
//   function IMessage(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * IMessage Payload.
//    * @member {IIMessagePayload|null|undefined} Payload
//    * @memberof IMessage
//    * @instance
//    */
//   IMessage.prototype.Payload = null;

//   /**
//    * IMessage ServerId.
//    * @member {string} ServerId
//    * @memberof IMessage
//    * @instance
//    */
//   IMessage.prototype.ServerId = "";

//   /**
//    * IMessage Type.
//    * @member {string} Type
//    * @memberof IMessage
//    * @instance
//    */
//   IMessage.prototype.Type = "";

//   /**
//    * Creates a new IMessage instance using the specified properties.
//    * @function create
//    * @memberof IMessage
//    * @static
//    * @param {IIMessage=} [properties] Properties to set
//    * @returns {IMessage} IMessage instance
//    */
//   IMessage.create = function create(properties) {
//     return new IMessage(properties);
//   };

//   /**
//    * Encodes the specified IMessage message. Does not implicitly {@link IMessage.verify|verify} messages.
//    * @function encode
//    * @memberof IMessage
//    * @static
//    * @param {IIMessage} message IMessage message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   IMessage.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.Payload != null && message.hasOwnProperty("Payload"))
//       $root.IMessagePayload.encode(message.Payload, writer.uint32(/* id 1, wireType 2 =*/ 10).fork()).ldelim();
//     if (message.ServerId != null && message.hasOwnProperty("ServerId"))
//       writer.uint32(/* id 2, wireType 2 =*/ 18).string(message.ServerId);
//     if (message.Type != null && message.hasOwnProperty("Type"))
//       writer.uint32(/* id 3, wireType 2 =*/ 26).string(message.Type);
//     return writer;
//   };

//   /**
//    * Encodes the specified IMessage message, length delimited. Does not implicitly {@link IMessage.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof IMessage
//    * @static
//    * @param {IIMessage} message IMessage message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   IMessage.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a IMessage message from the specified reader or buffer.
//    * @function decode
//    * @memberof IMessage
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {IMessage} IMessage
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   IMessage.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.IMessage();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.Payload = $root.IMessagePayload.decode(reader, reader.uint32());
//           break;
//         case 2:
//           message.ServerId = reader.string();
//           break;
//         case 3:
//           message.Type = reader.string();
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a IMessage message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof IMessage
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {IMessage} IMessage
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   IMessage.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a IMessage message.
//    * @function verify
//    * @memberof IMessage
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   IMessage.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.Payload != null && message.hasOwnProperty("Payload")) {
//       var error = $root.IMessagePayload.verify(message.Payload);
//       if (error) return "Payload." + error;
//     }
//     if (message.ServerId != null && message.hasOwnProperty("ServerId"))
//       if (!$util.isString(message.ServerId)) return "ServerId: string expected";
//     if (message.Type != null && message.hasOwnProperty("Type"))
//       if (!$util.isString(message.Type)) return "Type: string expected";
//     return null;
//   };

//   /**
//    * Creates a IMessage message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof IMessage
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {IMessage} IMessage
//    */
//   IMessage.fromObject = function fromObject(object) {
//     if (object instanceof $root.IMessage) return object;
//     var message = new $root.IMessage();
//     if (object.Payload != null) {
//       if (typeof object.Payload !== "object") throw TypeError(".IMessage.Payload: object expected");
//       message.Payload = $root.IMessagePayload.fromObject(object.Payload);
//     }
//     if (object.ServerId != null) message.ServerId = String(object.ServerId);
//     if (object.Type != null) message.Type = String(object.Type);
//     return message;
//   };

//   /**
//    * Creates a plain object from a IMessage message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof IMessage
//    * @static
//    * @param {IMessage} message IMessage
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   IMessage.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.Payload = null;
//       object.ServerId = "";
//       object.Type = "";
//     }
//     if (message.Payload != null && message.hasOwnProperty("Payload"))
//       object.Payload = $root.IMessagePayload.toObject(message.Payload, options);
//     if (message.ServerId != null && message.hasOwnProperty("ServerId")) object.ServerId = message.ServerId;
//     if (message.Type != null && message.hasOwnProperty("Type")) object.Type = message.Type;
//     return object;
//   };

//   /**
//    * Converts this IMessage to JSON.
//    * @function toJSON
//    * @memberof IMessage
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   IMessage.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return IMessage;
// })();

// $root.IMessagePayload = (function() {
//   /**
//    * Properties of a IMessagePayload.
//    * @exports IIMessagePayload
//    * @interface IIMessagePayload
//    * @property {IPlayerJoinEvent|null} [PlayerJoinEvent] IMessagePayload PlayerJoinEvent
//    * @property {IPlayerLeftEvent|null} [PlayerLeftEvent] IMessagePayload PlayerLeftEvent
//    * @property {IPlayerPositionEvent|null} [PlayerPositionEvent] IMessagePayload PlayerPositionEvent
//    * @property {IPlayerVehicleEvent|null} [PlayerVehicleEvent] IMessagePayload PlayerVehicleEvent
//    * @property {IPlayerKillEvent|null} [PlayerKillEvent] IMessagePayload PlayerKillEvent
//    * @property {IPlayerDeathEvent|null} [PlayerDeathEvent] IMessagePayload PlayerDeathEvent
//    * @property {IPlayerSpawnEvent|null} [PlayerSpawnEvent] IMessagePayload PlayerSpawnEvent
//    * @property {IPlayerTeamEvent|null} [PlayerTeamEvent] IMessagePayload PlayerTeamEvent
//    * @property {IPlayerScoreEvent|null} [PlayerScoreEvent] IMessagePayload PlayerScoreEvent
//    * @property {IProjectilePositionEvent|null} [ProjectilePositionEvent] IMessagePayload ProjectilePositionEvent
//    * @property {IChatEvent|null} [ChatEvent] IMessagePayload ChatEvent
//    * @property {IServerUpdateEvent|null} [ServerUpdateEvent] IMessagePayload ServerUpdateEvent
//    * @property {IGameStateEvent|null} [GameStateEvent] IMessagePayload GameStateEvent
//    * @property {IMapChangeEvent|null} [MapChangeEvent] IMessagePayload MapChangeEvent
//    * @property {IUserConnectAction|null} [UserConnectAction] IMessagePayload UserConnectAction
//    * @property {IUserDisconnectAction|null} [UserDisconnectAction] IMessagePayload UserDisconnectAction
//    */

//   /**
//    * Constructs a new IMessagePayload.
//    * @exports IMessagePayload
//    * @classdesc Represents a IMessagePayload.
//    * @implements IIMessagePayload
//    * @constructor
//    * @param {IIMessagePayload=} [properties] Properties to set
//    */
//   function IMessagePayload(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * IMessagePayload PlayerJoinEvent.
//    * @member {IPlayerJoinEvent|null|undefined} PlayerJoinEvent
//    * @memberof IMessagePayload
//    * @instance
//    */
//   IMessagePayload.prototype.PlayerJoinEvent = null;

//   /**
//    * IMessagePayload PlayerLeftEvent.
//    * @member {IPlayerLeftEvent|null|undefined} PlayerLeftEvent
//    * @memberof IMessagePayload
//    * @instance
//    */
//   IMessagePayload.prototype.PlayerLeftEvent = null;

//   /**
//    * IMessagePayload PlayerPositionEvent.
//    * @member {IPlayerPositionEvent|null|undefined} PlayerPositionEvent
//    * @memberof IMessagePayload
//    * @instance
//    */
//   IMessagePayload.prototype.PlayerPositionEvent = null;

//   /**
//    * IMessagePayload PlayerVehicleEvent.
//    * @member {IPlayerVehicleEvent|null|undefined} PlayerVehicleEvent
//    * @memberof IMessagePayload
//    * @instance
//    */
//   IMessagePayload.prototype.PlayerVehicleEvent = null;

//   /**
//    * IMessagePayload PlayerKillEvent.
//    * @member {IPlayerKillEvent|null|undefined} PlayerKillEvent
//    * @memberof IMessagePayload
//    * @instance
//    */
//   IMessagePayload.prototype.PlayerKillEvent = null;

//   /**
//    * IMessagePayload PlayerDeathEvent.
//    * @member {IPlayerDeathEvent|null|undefined} PlayerDeathEvent
//    * @memberof IMessagePayload
//    * @instance
//    */
//   IMessagePayload.prototype.PlayerDeathEvent = null;

//   /**
//    * IMessagePayload PlayerSpawnEvent.
//    * @member {IPlayerSpawnEvent|null|undefined} PlayerSpawnEvent
//    * @memberof IMessagePayload
//    * @instance
//    */
//   IMessagePayload.prototype.PlayerSpawnEvent = null;

//   /**
//    * IMessagePayload PlayerTeamEvent.
//    * @member {IPlayerTeamEvent|null|undefined} PlayerTeamEvent
//    * @memberof IMessagePayload
//    * @instance
//    */
//   IMessagePayload.prototype.PlayerTeamEvent = null;

//   /**
//    * IMessagePayload PlayerScoreEvent.
//    * @member {IPlayerScoreEvent|null|undefined} PlayerScoreEvent
//    * @memberof IMessagePayload
//    * @instance
//    */
//   IMessagePayload.prototype.PlayerScoreEvent = null;

//   /**
//    * IMessagePayload ProjectilePositionEvent.
//    * @member {IProjectilePositionEvent|null|undefined} ProjectilePositionEvent
//    * @memberof IMessagePayload
//    * @instance
//    */
//   IMessagePayload.prototype.ProjectilePositionEvent = null;

//   /**
//    * IMessagePayload ChatEvent.
//    * @member {IChatEvent|null|undefined} ChatEvent
//    * @memberof IMessagePayload
//    * @instance
//    */
//   IMessagePayload.prototype.ChatEvent = null;

//   /**
//    * IMessagePayload ServerUpdateEvent.
//    * @member {IServerUpdateEvent|null|undefined} ServerUpdateEvent
//    * @memberof IMessagePayload
//    * @instance
//    */
//   IMessagePayload.prototype.ServerUpdateEvent = null;

//   /**
//    * IMessagePayload GameStateEvent.
//    * @member {IGameStateEvent|null|undefined} GameStateEvent
//    * @memberof IMessagePayload
//    * @instance
//    */
//   IMessagePayload.prototype.GameStateEvent = null;

//   /**
//    * IMessagePayload MapChangeEvent.
//    * @member {IMapChangeEvent|null|undefined} MapChangeEvent
//    * @memberof IMessagePayload
//    * @instance
//    */
//   IMessagePayload.prototype.MapChangeEvent = null;

//   /**
//    * IMessagePayload UserConnectAction.
//    * @member {IUserConnectAction|null|undefined} UserConnectAction
//    * @memberof IMessagePayload
//    * @instance
//    */
//   IMessagePayload.prototype.UserConnectAction = null;

//   /**
//    * IMessagePayload UserDisconnectAction.
//    * @member {IUserDisconnectAction|null|undefined} UserDisconnectAction
//    * @memberof IMessagePayload
//    * @instance
//    */
//   IMessagePayload.prototype.UserDisconnectAction = null;

//   /**
//    * Creates a new IMessagePayload instance using the specified properties.
//    * @function create
//    * @memberof IMessagePayload
//    * @static
//    * @param {IIMessagePayload=} [properties] Properties to set
//    * @returns {IMessagePayload} IMessagePayload instance
//    */
//   IMessagePayload.create = function create(properties) {
//     return new IMessagePayload(properties);
//   };

//   /**
//    * Encodes the specified IMessagePayload message. Does not implicitly {@link IMessagePayload.verify|verify} messages.
//    * @function encode
//    * @memberof IMessagePayload
//    * @static
//    * @param {IIMessagePayload} message IMessagePayload message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   IMessagePayload.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.PlayerJoinEvent != null && message.hasOwnProperty("PlayerJoinEvent"))
//       $root.PlayerJoinEvent.encode(message.PlayerJoinEvent, writer.uint32(/* id 1, wireType 2 =*/ 10).fork()).ldelim();
//     if (message.PlayerLeftEvent != null && message.hasOwnProperty("PlayerLeftEvent"))
//       $root.PlayerLeftEvent.encode(message.PlayerLeftEvent, writer.uint32(/* id 2, wireType 2 =*/ 18).fork()).ldelim();
//     if (message.PlayerPositionEvent != null && message.hasOwnProperty("PlayerPositionEvent"))
//       $root.PlayerPositionEvent.encode(
//         message.PlayerPositionEvent,
//         writer.uint32(/* id 3, wireType 2 =*/ 26).fork()
//       ).ldelim();
//     if (message.PlayerVehicleEvent != null && message.hasOwnProperty("PlayerVehicleEvent"))
//       $root.PlayerVehicleEvent.encode(
//         message.PlayerVehicleEvent,
//         writer.uint32(/* id 4, wireType 2 =*/ 34).fork()
//       ).ldelim();
//     if (message.PlayerKillEvent != null && message.hasOwnProperty("PlayerKillEvent"))
//       $root.PlayerKillEvent.encode(message.PlayerKillEvent, writer.uint32(/* id 5, wireType 2 =*/ 42).fork()).ldelim();
//     if (message.PlayerDeathEvent != null && message.hasOwnProperty("PlayerDeathEvent"))
//       $root.PlayerDeathEvent.encode(
//         message.PlayerDeathEvent,
//         writer.uint32(/* id 6, wireType 2 =*/ 50).fork()
//       ).ldelim();
//     if (message.PlayerSpawnEvent != null && message.hasOwnProperty("PlayerSpawnEvent"))
//       $root.PlayerSpawnEvent.encode(
//         message.PlayerSpawnEvent,
//         writer.uint32(/* id 7, wireType 2 =*/ 58).fork()
//       ).ldelim();
//     if (message.PlayerTeamEvent != null && message.hasOwnProperty("PlayerTeamEvent"))
//       $root.PlayerTeamEvent.encode(message.PlayerTeamEvent, writer.uint32(/* id 8, wireType 2 =*/ 66).fork()).ldelim();
//     if (message.PlayerScoreEvent != null && message.hasOwnProperty("PlayerScoreEvent"))
//       $root.PlayerScoreEvent.encode(
//         message.PlayerScoreEvent,
//         writer.uint32(/* id 9, wireType 2 =*/ 74).fork()
//       ).ldelim();
//     if (message.ProjectilePositionEvent != null && message.hasOwnProperty("ProjectilePositionEvent"))
//       $root.ProjectilePositionEvent.encode(
//         message.ProjectilePositionEvent,
//         writer.uint32(/* id 10, wireType 2 =*/ 82).fork()
//       ).ldelim();
//     if (message.ChatEvent != null && message.hasOwnProperty("ChatEvent"))
//       $root.ChatEvent.encode(message.ChatEvent, writer.uint32(/* id 11, wireType 2 =*/ 90).fork()).ldelim();
//     if (message.ServerUpdateEvent != null && message.hasOwnProperty("ServerUpdateEvent"))
//       $root.ServerUpdateEvent.encode(
//         message.ServerUpdateEvent,
//         writer.uint32(/* id 12, wireType 2 =*/ 98).fork()
//       ).ldelim();
//     if (message.GameStateEvent != null && message.hasOwnProperty("GameStateEvent"))
//       $root.GameStateEvent.encode(message.GameStateEvent, writer.uint32(/* id 13, wireType 2 =*/ 106).fork()).ldelim();
//     if (message.MapChangeEvent != null && message.hasOwnProperty("MapChangeEvent"))
//       $root.MapChangeEvent.encode(message.MapChangeEvent, writer.uint32(/* id 14, wireType 2 =*/ 114).fork()).ldelim();
//     if (message.UserConnectAction != null && message.hasOwnProperty("UserConnectAction"))
//       $root.UserConnectAction.encode(
//         message.UserConnectAction,
//         writer.uint32(/* id 101, wireType 2 =*/ 810).fork()
//       ).ldelim();
//     if (message.UserDisconnectAction != null && message.hasOwnProperty("UserDisconnectAction"))
//       $root.UserDisconnectAction.encode(
//         message.UserDisconnectAction,
//         writer.uint32(/* id 102, wireType 2 =*/ 818).fork()
//       ).ldelim();
//     return writer;
//   };

//   /**
//    * Encodes the specified IMessagePayload message, length delimited. Does not implicitly {@link IMessagePayload.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof IMessagePayload
//    * @static
//    * @param {IIMessagePayload} message IMessagePayload message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   IMessagePayload.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a IMessagePayload message from the specified reader or buffer.
//    * @function decode
//    * @memberof IMessagePayload
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {IMessagePayload} IMessagePayload
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   IMessagePayload.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.IMessagePayload();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.PlayerJoinEvent = $root.PlayerJoinEvent.decode(reader, reader.uint32());
//           break;
//         case 2:
//           message.PlayerLeftEvent = $root.PlayerLeftEvent.decode(reader, reader.uint32());
//           break;
//         case 3:
//           message.PlayerPositionEvent = $root.PlayerPositionEvent.decode(reader, reader.uint32());
//           break;
//         case 4:
//           message.PlayerVehicleEvent = $root.PlayerVehicleEvent.decode(reader, reader.uint32());
//           break;
//         case 5:
//           message.PlayerKillEvent = $root.PlayerKillEvent.decode(reader, reader.uint32());
//           break;
//         case 6:
//           message.PlayerDeathEvent = $root.PlayerDeathEvent.decode(reader, reader.uint32());
//           break;
//         case 7:
//           message.PlayerSpawnEvent = $root.PlayerSpawnEvent.decode(reader, reader.uint32());
//           break;
//         case 8:
//           message.PlayerTeamEvent = $root.PlayerTeamEvent.decode(reader, reader.uint32());
//           break;
//         case 9:
//           message.PlayerScoreEvent = $root.PlayerScoreEvent.decode(reader, reader.uint32());
//           break;
//         case 10:
//           message.ProjectilePositionEvent = $root.ProjectilePositionEvent.decode(reader, reader.uint32());
//           break;
//         case 11:
//           message.ChatEvent = $root.ChatEvent.decode(reader, reader.uint32());
//           break;
//         case 12:
//           message.ServerUpdateEvent = $root.ServerUpdateEvent.decode(reader, reader.uint32());
//           break;
//         case 13:
//           message.GameStateEvent = $root.GameStateEvent.decode(reader, reader.uint32());
//           break;
//         case 14:
//           message.MapChangeEvent = $root.MapChangeEvent.decode(reader, reader.uint32());
//           break;
//         case 101:
//           message.UserConnectAction = $root.UserConnectAction.decode(reader, reader.uint32());
//           break;
//         case 102:
//           message.UserDisconnectAction = $root.UserDisconnectAction.decode(reader, reader.uint32());
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a IMessagePayload message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof IMessagePayload
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {IMessagePayload} IMessagePayload
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   IMessagePayload.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a IMessagePayload message.
//    * @function verify
//    * @memberof IMessagePayload
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   IMessagePayload.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.PlayerJoinEvent != null && message.hasOwnProperty("PlayerJoinEvent")) {
//       var error = $root.PlayerJoinEvent.verify(message.PlayerJoinEvent);
//       if (error) return "PlayerJoinEvent." + error;
//     }
//     if (message.PlayerLeftEvent != null && message.hasOwnProperty("PlayerLeftEvent")) {
//       var error = $root.PlayerLeftEvent.verify(message.PlayerLeftEvent);
//       if (error) return "PlayerLeftEvent." + error;
//     }
//     if (message.PlayerPositionEvent != null && message.hasOwnProperty("PlayerPositionEvent")) {
//       var error = $root.PlayerPositionEvent.verify(message.PlayerPositionEvent);
//       if (error) return "PlayerPositionEvent." + error;
//     }
//     if (message.PlayerVehicleEvent != null && message.hasOwnProperty("PlayerVehicleEvent")) {
//       var error = $root.PlayerVehicleEvent.verify(message.PlayerVehicleEvent);
//       if (error) return "PlayerVehicleEvent." + error;
//     }
//     if (message.PlayerKillEvent != null && message.hasOwnProperty("PlayerKillEvent")) {
//       var error = $root.PlayerKillEvent.verify(message.PlayerKillEvent);
//       if (error) return "PlayerKillEvent." + error;
//     }
//     if (message.PlayerDeathEvent != null && message.hasOwnProperty("PlayerDeathEvent")) {
//       var error = $root.PlayerDeathEvent.verify(message.PlayerDeathEvent);
//       if (error) return "PlayerDeathEvent." + error;
//     }
//     if (message.PlayerSpawnEvent != null && message.hasOwnProperty("PlayerSpawnEvent")) {
//       var error = $root.PlayerSpawnEvent.verify(message.PlayerSpawnEvent);
//       if (error) return "PlayerSpawnEvent." + error;
//     }
//     if (message.PlayerTeamEvent != null && message.hasOwnProperty("PlayerTeamEvent")) {
//       var error = $root.PlayerTeamEvent.verify(message.PlayerTeamEvent);
//       if (error) return "PlayerTeamEvent." + error;
//     }
//     if (message.PlayerScoreEvent != null && message.hasOwnProperty("PlayerScoreEvent")) {
//       var error = $root.PlayerScoreEvent.verify(message.PlayerScoreEvent);
//       if (error) return "PlayerScoreEvent." + error;
//     }
//     if (message.ProjectilePositionEvent != null && message.hasOwnProperty("ProjectilePositionEvent")) {
//       var error = $root.ProjectilePositionEvent.verify(message.ProjectilePositionEvent);
//       if (error) return "ProjectilePositionEvent." + error;
//     }
//     if (message.ChatEvent != null && message.hasOwnProperty("ChatEvent")) {
//       var error = $root.ChatEvent.verify(message.ChatEvent);
//       if (error) return "ChatEvent." + error;
//     }
//     if (message.ServerUpdateEvent != null && message.hasOwnProperty("ServerUpdateEvent")) {
//       var error = $root.ServerUpdateEvent.verify(message.ServerUpdateEvent);
//       if (error) return "ServerUpdateEvent." + error;
//     }
//     if (message.GameStateEvent != null && message.hasOwnProperty("GameStateEvent")) {
//       var error = $root.GameStateEvent.verify(message.GameStateEvent);
//       if (error) return "GameStateEvent." + error;
//     }
//     if (message.MapChangeEvent != null && message.hasOwnProperty("MapChangeEvent")) {
//       var error = $root.MapChangeEvent.verify(message.MapChangeEvent);
//       if (error) return "MapChangeEvent." + error;
//     }
//     if (message.UserConnectAction != null && message.hasOwnProperty("UserConnectAction")) {
//       var error = $root.UserConnectAction.verify(message.UserConnectAction);
//       if (error) return "UserConnectAction." + error;
//     }
//     if (message.UserDisconnectAction != null && message.hasOwnProperty("UserDisconnectAction")) {
//       var error = $root.UserDisconnectAction.verify(message.UserDisconnectAction);
//       if (error) return "UserDisconnectAction." + error;
//     }
//     return null;
//   };

//   /**
//    * Creates a IMessagePayload message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof IMessagePayload
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {IMessagePayload} IMessagePayload
//    */
//   IMessagePayload.fromObject = function fromObject(object) {
//     if (object instanceof $root.IMessagePayload) return object;
//     var message = new $root.IMessagePayload();
//     if (object.PlayerJoinEvent != null) {
//       if (typeof object.PlayerJoinEvent !== "object")
//         throw TypeError(".IMessagePayload.PlayerJoinEvent: object expected");
//       message.PlayerJoinEvent = $root.PlayerJoinEvent.fromObject(object.PlayerJoinEvent);
//     }
//     if (object.PlayerLeftEvent != null) {
//       if (typeof object.PlayerLeftEvent !== "object")
//         throw TypeError(".IMessagePayload.PlayerLeftEvent: object expected");
//       message.PlayerLeftEvent = $root.PlayerLeftEvent.fromObject(object.PlayerLeftEvent);
//     }
//     if (object.PlayerPositionEvent != null) {
//       if (typeof object.PlayerPositionEvent !== "object")
//         throw TypeError(".IMessagePayload.PlayerPositionEvent: object expected");
//       message.PlayerPositionEvent = $root.PlayerPositionEvent.fromObject(object.PlayerPositionEvent);
//     }
//     if (object.PlayerVehicleEvent != null) {
//       if (typeof object.PlayerVehicleEvent !== "object")
//         throw TypeError(".IMessagePayload.PlayerVehicleEvent: object expected");
//       message.PlayerVehicleEvent = $root.PlayerVehicleEvent.fromObject(object.PlayerVehicleEvent);
//     }
//     if (object.PlayerKillEvent != null) {
//       if (typeof object.PlayerKillEvent !== "object")
//         throw TypeError(".IMessagePayload.PlayerKillEvent: object expected");
//       message.PlayerKillEvent = $root.PlayerKillEvent.fromObject(object.PlayerKillEvent);
//     }
//     if (object.PlayerDeathEvent != null) {
//       if (typeof object.PlayerDeathEvent !== "object")
//         throw TypeError(".IMessagePayload.PlayerDeathEvent: object expected");
//       message.PlayerDeathEvent = $root.PlayerDeathEvent.fromObject(object.PlayerDeathEvent);
//     }
//     if (object.PlayerSpawnEvent != null) {
//       if (typeof object.PlayerSpawnEvent !== "object")
//         throw TypeError(".IMessagePayload.PlayerSpawnEvent: object expected");
//       message.PlayerSpawnEvent = $root.PlayerSpawnEvent.fromObject(object.PlayerSpawnEvent);
//     }
//     if (object.PlayerTeamEvent != null) {
//       if (typeof object.PlayerTeamEvent !== "object")
//         throw TypeError(".IMessagePayload.PlayerTeamEvent: object expected");
//       message.PlayerTeamEvent = $root.PlayerTeamEvent.fromObject(object.PlayerTeamEvent);
//     }
//     if (object.PlayerScoreEvent != null) {
//       if (typeof object.PlayerScoreEvent !== "object")
//         throw TypeError(".IMessagePayload.PlayerScoreEvent: object expected");
//       message.PlayerScoreEvent = $root.PlayerScoreEvent.fromObject(object.PlayerScoreEvent);
//     }
//     if (object.ProjectilePositionEvent != null) {
//       if (typeof object.ProjectilePositionEvent !== "object")
//         throw TypeError(".IMessagePayload.ProjectilePositionEvent: object expected");
//       message.ProjectilePositionEvent = $root.ProjectilePositionEvent.fromObject(object.ProjectilePositionEvent);
//     }
//     if (object.ChatEvent != null) {
//       if (typeof object.ChatEvent !== "object") throw TypeError(".IMessagePayload.ChatEvent: object expected");
//       message.ChatEvent = $root.ChatEvent.fromObject(object.ChatEvent);
//     }
//     if (object.ServerUpdateEvent != null) {
//       if (typeof object.ServerUpdateEvent !== "object")
//         throw TypeError(".IMessagePayload.ServerUpdateEvent: object expected");
//       message.ServerUpdateEvent = $root.ServerUpdateEvent.fromObject(object.ServerUpdateEvent);
//     }
//     if (object.GameStateEvent != null) {
//       if (typeof object.GameStateEvent !== "object")
//         throw TypeError(".IMessagePayload.GameStateEvent: object expected");
//       message.GameStateEvent = $root.GameStateEvent.fromObject(object.GameStateEvent);
//     }
//     if (object.MapChangeEvent != null) {
//       if (typeof object.MapChangeEvent !== "object")
//         throw TypeError(".IMessagePayload.MapChangeEvent: object expected");
//       message.MapChangeEvent = $root.MapChangeEvent.fromObject(object.MapChangeEvent);
//     }
//     if (object.UserConnectAction != null) {
//       if (typeof object.UserConnectAction !== "object")
//         throw TypeError(".IMessagePayload.UserConnectAction: object expected");
//       message.UserConnectAction = $root.UserConnectAction.fromObject(object.UserConnectAction);
//     }
//     if (object.UserDisconnectAction != null) {
//       if (typeof object.UserDisconnectAction !== "object")
//         throw TypeError(".IMessagePayload.UserDisconnectAction: object expected");
//       message.UserDisconnectAction = $root.UserDisconnectAction.fromObject(object.UserDisconnectAction);
//     }
//     return message;
//   };

//   /**
//    * Creates a plain object from a IMessagePayload message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof IMessagePayload
//    * @static
//    * @param {IMessagePayload} message IMessagePayload
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   IMessagePayload.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.PlayerJoinEvent = null;
//       object.PlayerLeftEvent = null;
//       object.PlayerPositionEvent = null;
//       object.PlayerVehicleEvent = null;
//       object.PlayerKillEvent = null;
//       object.PlayerDeathEvent = null;
//       object.PlayerSpawnEvent = null;
//       object.PlayerTeamEvent = null;
//       object.PlayerScoreEvent = null;
//       object.ProjectilePositionEvent = null;
//       object.ChatEvent = null;
//       object.ServerUpdateEvent = null;
//       object.GameStateEvent = null;
//       object.MapChangeEvent = null;
//       object.UserConnectAction = null;
//       object.UserDisconnectAction = null;
//     }
//     if (message.PlayerJoinEvent != null && message.hasOwnProperty("PlayerJoinEvent"))
//       object.PlayerJoinEvent = $root.PlayerJoinEvent.toObject(message.PlayerJoinEvent, options);
//     if (message.PlayerLeftEvent != null && message.hasOwnProperty("PlayerLeftEvent"))
//       object.PlayerLeftEvent = $root.PlayerLeftEvent.toObject(message.PlayerLeftEvent, options);
//     if (message.PlayerPositionEvent != null && message.hasOwnProperty("PlayerPositionEvent"))
//       object.PlayerPositionEvent = $root.PlayerPositionEvent.toObject(message.PlayerPositionEvent, options);
//     if (message.PlayerVehicleEvent != null && message.hasOwnProperty("PlayerVehicleEvent"))
//       object.PlayerVehicleEvent = $root.PlayerVehicleEvent.toObject(message.PlayerVehicleEvent, options);
//     if (message.PlayerKillEvent != null && message.hasOwnProperty("PlayerKillEvent"))
//       object.PlayerKillEvent = $root.PlayerKillEvent.toObject(message.PlayerKillEvent, options);
//     if (message.PlayerDeathEvent != null && message.hasOwnProperty("PlayerDeathEvent"))
//       object.PlayerDeathEvent = $root.PlayerDeathEvent.toObject(message.PlayerDeathEvent, options);
//     if (message.PlayerSpawnEvent != null && message.hasOwnProperty("PlayerSpawnEvent"))
//       object.PlayerSpawnEvent = $root.PlayerSpawnEvent.toObject(message.PlayerSpawnEvent, options);
//     if (message.PlayerTeamEvent != null && message.hasOwnProperty("PlayerTeamEvent"))
//       object.PlayerTeamEvent = $root.PlayerTeamEvent.toObject(message.PlayerTeamEvent, options);
//     if (message.PlayerScoreEvent != null && message.hasOwnProperty("PlayerScoreEvent"))
//       object.PlayerScoreEvent = $root.PlayerScoreEvent.toObject(message.PlayerScoreEvent, options);
//     if (message.ProjectilePositionEvent != null && message.hasOwnProperty("ProjectilePositionEvent"))
//       object.ProjectilePositionEvent = $root.ProjectilePositionEvent.toObject(message.ProjectilePositionEvent, options);
//     if (message.ChatEvent != null && message.hasOwnProperty("ChatEvent"))
//       object.ChatEvent = $root.ChatEvent.toObject(message.ChatEvent, options);
//     if (message.ServerUpdateEvent != null && message.hasOwnProperty("ServerUpdateEvent"))
//       object.ServerUpdateEvent = $root.ServerUpdateEvent.toObject(message.ServerUpdateEvent, options);
//     if (message.GameStateEvent != null && message.hasOwnProperty("GameStateEvent"))
//       object.GameStateEvent = $root.GameStateEvent.toObject(message.GameStateEvent, options);
//     if (message.MapChangeEvent != null && message.hasOwnProperty("MapChangeEvent"))
//       object.MapChangeEvent = $root.MapChangeEvent.toObject(message.MapChangeEvent, options);
//     if (message.UserConnectAction != null && message.hasOwnProperty("UserConnectAction"))
//       object.UserConnectAction = $root.UserConnectAction.toObject(message.UserConnectAction, options);
//     if (message.UserDisconnectAction != null && message.hasOwnProperty("UserDisconnectAction"))
//       object.UserDisconnectAction = $root.UserDisconnectAction.toObject(message.UserDisconnectAction, options);
//     return object;
//   };

//   /**
//    * Converts this IMessagePayload to JSON.
//    * @function toJSON
//    * @memberof IMessagePayload
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   IMessagePayload.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return IMessagePayload;
// })();

// $root.MapChangeEvent = (function() {
//   /**
//    * Properties of a MapChangeEvent.
//    * @exports IMapChangeEvent
//    * @interface IMapChangeEvent
//    * @property {string|null} [Map] MapChangeEvent Map
//    * @property {number|null} [Size] MapChangeEvent Size
//    */

//   /**
//    * Constructs a new MapChangeEvent.
//    * @exports MapChangeEvent
//    * @classdesc Represents a MapChangeEvent.
//    * @implements IMapChangeEvent
//    * @constructor
//    * @param {IMapChangeEvent=} [properties] Properties to set
//    */
//   function MapChangeEvent(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * MapChangeEvent Map.
//    * @member {string} Map
//    * @memberof MapChangeEvent
//    * @instance
//    */
//   MapChangeEvent.prototype.Map = "";

//   /**
//    * MapChangeEvent Size.
//    * @member {number} Size
//    * @memberof MapChangeEvent
//    * @instance
//    */
//   MapChangeEvent.prototype.Size = 0;

//   /**
//    * Creates a new MapChangeEvent instance using the specified properties.
//    * @function create
//    * @memberof MapChangeEvent
//    * @static
//    * @param {IMapChangeEvent=} [properties] Properties to set
//    * @returns {MapChangeEvent} MapChangeEvent instance
//    */
//   MapChangeEvent.create = function create(properties) {
//     return new MapChangeEvent(properties);
//   };

//   /**
//    * Encodes the specified MapChangeEvent message. Does not implicitly {@link MapChangeEvent.verify|verify} messages.
//    * @function encode
//    * @memberof MapChangeEvent
//    * @static
//    * @param {IMapChangeEvent} message MapChangeEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   MapChangeEvent.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.Map != null && message.hasOwnProperty("Map"))
//       writer.uint32(/* id 1, wireType 2 =*/ 10).string(message.Map);
//     if (message.Size != null && message.hasOwnProperty("Size"))
//       writer.uint32(/* id 2, wireType 0 =*/ 16).int32(message.Size);
//     return writer;
//   };

//   /**
//    * Encodes the specified MapChangeEvent message, length delimited. Does not implicitly {@link MapChangeEvent.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof MapChangeEvent
//    * @static
//    * @param {IMapChangeEvent} message MapChangeEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   MapChangeEvent.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a MapChangeEvent message from the specified reader or buffer.
//    * @function decode
//    * @memberof MapChangeEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {MapChangeEvent} MapChangeEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   MapChangeEvent.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.MapChangeEvent();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.Map = reader.string();
//           break;
//         case 2:
//           message.Size = reader.int32();
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a MapChangeEvent message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof MapChangeEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {MapChangeEvent} MapChangeEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   MapChangeEvent.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a MapChangeEvent message.
//    * @function verify
//    * @memberof MapChangeEvent
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   MapChangeEvent.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.Map != null && message.hasOwnProperty("Map"))
//       if (!$util.isString(message.Map)) return "Map: string expected";
//     if (message.Size != null && message.hasOwnProperty("Size"))
//       if (!$util.isInteger(message.Size)) return "Size: integer expected";
//     return null;
//   };

//   /**
//    * Creates a MapChangeEvent message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof MapChangeEvent
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {MapChangeEvent} MapChangeEvent
//    */
//   MapChangeEvent.fromObject = function fromObject(object) {
//     if (object instanceof $root.MapChangeEvent) return object;
//     var message = new $root.MapChangeEvent();
//     if (object.Map != null) message.Map = String(object.Map);
//     if (object.Size != null) message.Size = object.Size | 0;
//     return message;
//   };

//   /**
//    * Creates a plain object from a MapChangeEvent message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof MapChangeEvent
//    * @static
//    * @param {MapChangeEvent} message MapChangeEvent
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   MapChangeEvent.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.Map = "";
//       object.Size = 0;
//     }
//     if (message.Map != null && message.hasOwnProperty("Map")) object.Map = message.Map;
//     if (message.Size != null && message.hasOwnProperty("Size")) object.Size = message.Size;
//     return object;
//   };

//   /**
//    * Converts this MapChangeEvent to JSON.
//    * @function toJSON
//    * @memberof MapChangeEvent
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   MapChangeEvent.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return MapChangeEvent;
// })();

// $root.MessageDto = (function() {
//   /**
//    * Properties of a MessageDto.
//    * @exports IMessageDto
//    * @interface IMessageDto
//    * @property {string|null} [Channel] MessageDto Channel
//    * @property {string|null} [Flags] MessageDto Flags
//    * @property {number|null} [PlayerId] MessageDto PlayerId
//    * @property {string|null} [Text] MessageDto Text
//    * @property {string|null} [Time] MessageDto Time
//    * @property {string|null} [Type] MessageDto Type
//    */

//   /**
//    * Constructs a new MessageDto.
//    * @exports MessageDto
//    * @classdesc Represents a MessageDto.
//    * @implements IMessageDto
//    * @constructor
//    * @param {IMessageDto=} [properties] Properties to set
//    */
//   function MessageDto(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * MessageDto Channel.
//    * @member {string} Channel
//    * @memberof MessageDto
//    * @instance
//    */
//   MessageDto.prototype.Channel = "";

//   /**
//    * MessageDto Flags.
//    * @member {string} Flags
//    * @memberof MessageDto
//    * @instance
//    */
//   MessageDto.prototype.Flags = "";

//   /**
//    * MessageDto PlayerId.
//    * @member {number} PlayerId
//    * @memberof MessageDto
//    * @instance
//    */
//   MessageDto.prototype.PlayerId = 0;

//   /**
//    * MessageDto Text.
//    * @member {string} Text
//    * @memberof MessageDto
//    * @instance
//    */
//   MessageDto.prototype.Text = "";

//   /**
//    * MessageDto Time.
//    * @member {string} Time
//    * @memberof MessageDto
//    * @instance
//    */
//   MessageDto.prototype.Time = "";

//   /**
//    * MessageDto Type.
//    * @member {string} Type
//    * @memberof MessageDto
//    * @instance
//    */
//   MessageDto.prototype.Type = "";

//   /**
//    * Creates a new MessageDto instance using the specified properties.
//    * @function create
//    * @memberof MessageDto
//    * @static
//    * @param {IMessageDto=} [properties] Properties to set
//    * @returns {MessageDto} MessageDto instance
//    */
//   MessageDto.create = function create(properties) {
//     return new MessageDto(properties);
//   };

//   /**
//    * Encodes the specified MessageDto message. Does not implicitly {@link MessageDto.verify|verify} messages.
//    * @function encode
//    * @memberof MessageDto
//    * @static
//    * @param {IMessageDto} message MessageDto message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   MessageDto.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.Channel != null && message.hasOwnProperty("Channel"))
//       writer.uint32(/* id 1, wireType 2 =*/ 10).string(message.Channel);
//     if (message.Flags != null && message.hasOwnProperty("Flags"))
//       writer.uint32(/* id 2, wireType 2 =*/ 18).string(message.Flags);
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId"))
//       writer.uint32(/* id 3, wireType 0 =*/ 24).int32(message.PlayerId);
//     if (message.Text != null && message.hasOwnProperty("Text"))
//       writer.uint32(/* id 4, wireType 2 =*/ 34).string(message.Text);
//     if (message.Time != null && message.hasOwnProperty("Time"))
//       writer.uint32(/* id 5, wireType 2 =*/ 42).string(message.Time);
//     if (message.Type != null && message.hasOwnProperty("Type"))
//       writer.uint32(/* id 6, wireType 2 =*/ 50).string(message.Type);
//     return writer;
//   };

//   /**
//    * Encodes the specified MessageDto message, length delimited. Does not implicitly {@link MessageDto.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof MessageDto
//    * @static
//    * @param {IMessageDto} message MessageDto message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   MessageDto.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a MessageDto message from the specified reader or buffer.
//    * @function decode
//    * @memberof MessageDto
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {MessageDto} MessageDto
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   MessageDto.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.MessageDto();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.Channel = reader.string();
//           break;
//         case 2:
//           message.Flags = reader.string();
//           break;
//         case 3:
//           message.PlayerId = reader.int32();
//           break;
//         case 4:
//           message.Text = reader.string();
//           break;
//         case 5:
//           message.Time = reader.string();
//           break;
//         case 6:
//           message.Type = reader.string();
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a MessageDto message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof MessageDto
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {MessageDto} MessageDto
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   MessageDto.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a MessageDto message.
//    * @function verify
//    * @memberof MessageDto
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   MessageDto.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.Channel != null && message.hasOwnProperty("Channel"))
//       if (!$util.isString(message.Channel)) return "Channel: string expected";
//     if (message.Flags != null && message.hasOwnProperty("Flags"))
//       if (!$util.isString(message.Flags)) return "Flags: string expected";
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId"))
//       if (!$util.isInteger(message.PlayerId)) return "PlayerId: integer expected";
//     if (message.Text != null && message.hasOwnProperty("Text"))
//       if (!$util.isString(message.Text)) return "Text: string expected";
//     if (message.Time != null && message.hasOwnProperty("Time"))
//       if (!$util.isString(message.Time)) return "Time: string expected";
//     if (message.Type != null && message.hasOwnProperty("Type"))
//       if (!$util.isString(message.Type)) return "Type: string expected";
//     return null;
//   };

//   /**
//    * Creates a MessageDto message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof MessageDto
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {MessageDto} MessageDto
//    */
//   MessageDto.fromObject = function fromObject(object) {
//     if (object instanceof $root.MessageDto) return object;
//     var message = new $root.MessageDto();
//     if (object.Channel != null) message.Channel = String(object.Channel);
//     if (object.Flags != null) message.Flags = String(object.Flags);
//     if (object.PlayerId != null) message.PlayerId = object.PlayerId | 0;
//     if (object.Text != null) message.Text = String(object.Text);
//     if (object.Time != null) message.Time = String(object.Time);
//     if (object.Type != null) message.Type = String(object.Type);
//     return message;
//   };

//   /**
//    * Creates a plain object from a MessageDto message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof MessageDto
//    * @static
//    * @param {MessageDto} message MessageDto
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   MessageDto.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.Channel = "";
//       object.Flags = "";
//       object.PlayerId = 0;
//       object.Text = "";
//       object.Time = "";
//       object.Type = "";
//     }
//     if (message.Channel != null && message.hasOwnProperty("Channel")) object.Channel = message.Channel;
//     if (message.Flags != null && message.hasOwnProperty("Flags")) object.Flags = message.Flags;
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId")) object.PlayerId = message.PlayerId;
//     if (message.Text != null && message.hasOwnProperty("Text")) object.Text = message.Text;
//     if (message.Time != null && message.hasOwnProperty("Time")) object.Time = message.Time;
//     if (message.Type != null && message.hasOwnProperty("Type")) object.Type = message.Type;
//     return object;
//   };

//   /**
//    * Converts this MessageDto to JSON.
//    * @function toJSON
//    * @memberof MessageDto
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   MessageDto.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return MessageDto;
// })();

// $root.PlayerDeathEvent = (function() {
//   /**
//    * Properties of a PlayerDeathEvent.
//    * @exports IPlayerDeathEvent
//    * @interface IPlayerDeathEvent
//    * @property {number|null} [PlayerId] PlayerDeathEvent PlayerId
//    * @property {IVector3|null} [Position] PlayerDeathEvent Position
//    */

//   /**
//    * Constructs a new PlayerDeathEvent.
//    * @exports PlayerDeathEvent
//    * @classdesc Represents a PlayerDeathEvent.
//    * @implements IPlayerDeathEvent
//    * @constructor
//    * @param {IPlayerDeathEvent=} [properties] Properties to set
//    */
//   function PlayerDeathEvent(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * PlayerDeathEvent PlayerId.
//    * @member {number} PlayerId
//    * @memberof PlayerDeathEvent
//    * @instance
//    */
//   PlayerDeathEvent.prototype.PlayerId = 0;

//   /**
//    * PlayerDeathEvent Position.
//    * @member {IVector3|null|undefined} Position
//    * @memberof PlayerDeathEvent
//    * @instance
//    */
//   PlayerDeathEvent.prototype.Position = null;

//   /**
//    * Creates a new PlayerDeathEvent instance using the specified properties.
//    * @function create
//    * @memberof PlayerDeathEvent
//    * @static
//    * @param {IPlayerDeathEvent=} [properties] Properties to set
//    * @returns {PlayerDeathEvent} PlayerDeathEvent instance
//    */
//   PlayerDeathEvent.create = function create(properties) {
//     return new PlayerDeathEvent(properties);
//   };

//   /**
//    * Encodes the specified PlayerDeathEvent message. Does not implicitly {@link PlayerDeathEvent.verify|verify} messages.
//    * @function encode
//    * @memberof PlayerDeathEvent
//    * @static
//    * @param {IPlayerDeathEvent} message PlayerDeathEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerDeathEvent.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId"))
//       writer.uint32(/* id 1, wireType 0 =*/ 8).int32(message.PlayerId);
//     if (message.Position != null && message.hasOwnProperty("Position"))
//       $root.Vector3.encode(message.Position, writer.uint32(/* id 2, wireType 2 =*/ 18).fork()).ldelim();
//     return writer;
//   };

//   /**
//    * Encodes the specified PlayerDeathEvent message, length delimited. Does not implicitly {@link PlayerDeathEvent.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof PlayerDeathEvent
//    * @static
//    * @param {IPlayerDeathEvent} message PlayerDeathEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerDeathEvent.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a PlayerDeathEvent message from the specified reader or buffer.
//    * @function decode
//    * @memberof PlayerDeathEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {PlayerDeathEvent} PlayerDeathEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerDeathEvent.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.PlayerDeathEvent();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.PlayerId = reader.int32();
//           break;
//         case 2:
//           message.Position = $root.Vector3.decode(reader, reader.uint32());
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a PlayerDeathEvent message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof PlayerDeathEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {PlayerDeathEvent} PlayerDeathEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerDeathEvent.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a PlayerDeathEvent message.
//    * @function verify
//    * @memberof PlayerDeathEvent
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   PlayerDeathEvent.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId"))
//       if (!$util.isInteger(message.PlayerId)) return "PlayerId: integer expected";
//     if (message.Position != null && message.hasOwnProperty("Position")) {
//       var error = $root.Vector3.verify(message.Position);
//       if (error) return "Position." + error;
//     }
//     return null;
//   };

//   /**
//    * Creates a PlayerDeathEvent message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof PlayerDeathEvent
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {PlayerDeathEvent} PlayerDeathEvent
//    */
//   PlayerDeathEvent.fromObject = function fromObject(object) {
//     if (object instanceof $root.PlayerDeathEvent) return object;
//     var message = new $root.PlayerDeathEvent();
//     if (object.PlayerId != null) message.PlayerId = object.PlayerId | 0;
//     if (object.Position != null) {
//       if (typeof object.Position !== "object") throw TypeError(".PlayerDeathEvent.Position: object expected");
//       message.Position = $root.Vector3.fromObject(object.Position);
//     }
//     return message;
//   };

//   /**
//    * Creates a plain object from a PlayerDeathEvent message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof PlayerDeathEvent
//    * @static
//    * @param {PlayerDeathEvent} message PlayerDeathEvent
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   PlayerDeathEvent.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.PlayerId = 0;
//       object.Position = null;
//     }
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId")) object.PlayerId = message.PlayerId;
//     if (message.Position != null && message.hasOwnProperty("Position"))
//       object.Position = $root.Vector3.toObject(message.Position, options);
//     return object;
//   };

//   /**
//    * Converts this PlayerDeathEvent to JSON.
//    * @function toJSON
//    * @memberof PlayerDeathEvent
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   PlayerDeathEvent.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return PlayerDeathEvent;
// })();

// $root.PlayerDto = (function() {
//   /**
//    * Properties of a PlayerDto.
//    * @exports IPlayerDto
//    * @interface IPlayerDto
//    * @property {string|null} [Country] PlayerDto Country
//    * @property {string|null} [Hash] PlayerDto Hash
//    * @property {number|null} [Index] PlayerDto Index
//    * @property {string|null} [IpAddress] PlayerDto IpAddress
//    * @property {boolean|null} [IsAlive] PlayerDto IsAlive
//    * @property {string|null} [Name] PlayerDto Name
//    * @property {number|null} [Rank] PlayerDto Rank
//    * @property {IScoreDto|null} [Score] PlayerDto Score
//    * @property {number|null} [Team] PlayerDto Team
//    */

//   /**
//    * Constructs a new PlayerDto.
//    * @exports PlayerDto
//    * @classdesc Represents a PlayerDto.
//    * @implements IPlayerDto
//    * @constructor
//    * @param {IPlayerDto=} [properties] Properties to set
//    */
//   function PlayerDto(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * PlayerDto Country.
//    * @member {string} Country
//    * @memberof PlayerDto
//    * @instance
//    */
//   PlayerDto.prototype.Country = "";

//   /**
//    * PlayerDto Hash.
//    * @member {string} Hash
//    * @memberof PlayerDto
//    * @instance
//    */
//   PlayerDto.prototype.Hash = "";

//   /**
//    * PlayerDto Index.
//    * @member {number} Index
//    * @memberof PlayerDto
//    * @instance
//    */
//   PlayerDto.prototype.Index = 0;

//   /**
//    * PlayerDto IpAddress.
//    * @member {string} IpAddress
//    * @memberof PlayerDto
//    * @instance
//    */
//   PlayerDto.prototype.IpAddress = "";

//   /**
//    * PlayerDto IsAlive.
//    * @member {boolean} IsAlive
//    * @memberof PlayerDto
//    * @instance
//    */
//   PlayerDto.prototype.IsAlive = false;

//   /**
//    * PlayerDto Name.
//    * @member {string} Name
//    * @memberof PlayerDto
//    * @instance
//    */
//   PlayerDto.prototype.Name = "";

//   /**
//    * PlayerDto Rank.
//    * @member {number} Rank
//    * @memberof PlayerDto
//    * @instance
//    */
//   PlayerDto.prototype.Rank = 0;

//   /**
//    * PlayerDto Score.
//    * @member {IScoreDto|null|undefined} Score
//    * @memberof PlayerDto
//    * @instance
//    */
//   PlayerDto.prototype.Score = null;

//   /**
//    * PlayerDto Team.
//    * @member {number} Team
//    * @memberof PlayerDto
//    * @instance
//    */
//   PlayerDto.prototype.Team = 0;

//   /**
//    * Creates a new PlayerDto instance using the specified properties.
//    * @function create
//    * @memberof PlayerDto
//    * @static
//    * @param {IPlayerDto=} [properties] Properties to set
//    * @returns {PlayerDto} PlayerDto instance
//    */
//   PlayerDto.create = function create(properties) {
//     return new PlayerDto(properties);
//   };

//   /**
//    * Encodes the specified PlayerDto message. Does not implicitly {@link PlayerDto.verify|verify} messages.
//    * @function encode
//    * @memberof PlayerDto
//    * @static
//    * @param {IPlayerDto} message PlayerDto message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerDto.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.Country != null && message.hasOwnProperty("Country"))
//       writer.uint32(/* id 1, wireType 2 =*/ 10).string(message.Country);
//     if (message.Hash != null && message.hasOwnProperty("Hash"))
//       writer.uint32(/* id 2, wireType 2 =*/ 18).string(message.Hash);
//     if (message.Index != null && message.hasOwnProperty("Index"))
//       writer.uint32(/* id 3, wireType 0 =*/ 24).int32(message.Index);
//     if (message.IpAddress != null && message.hasOwnProperty("IpAddress"))
//       writer.uint32(/* id 4, wireType 2 =*/ 34).string(message.IpAddress);
//     if (message.IsAlive != null && message.hasOwnProperty("IsAlive"))
//       writer.uint32(/* id 5, wireType 0 =*/ 40).bool(message.IsAlive);
//     if (message.Name != null && message.hasOwnProperty("Name"))
//       writer.uint32(/* id 6, wireType 2 =*/ 50).string(message.Name);
//     if (message.Rank != null && message.hasOwnProperty("Rank"))
//       writer.uint32(/* id 7, wireType 0 =*/ 56).int32(message.Rank);
//     if (message.Score != null && message.hasOwnProperty("Score"))
//       $root.ScoreDto.encode(message.Score, writer.uint32(/* id 8, wireType 2 =*/ 66).fork()).ldelim();
//     if (message.Team != null && message.hasOwnProperty("Team"))
//       writer.uint32(/* id 9, wireType 0 =*/ 72).int32(message.Team);
//     return writer;
//   };

//   /**
//    * Encodes the specified PlayerDto message, length delimited. Does not implicitly {@link PlayerDto.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof PlayerDto
//    * @static
//    * @param {IPlayerDto} message PlayerDto message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerDto.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a PlayerDto message from the specified reader or buffer.
//    * @function decode
//    * @memberof PlayerDto
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {PlayerDto} PlayerDto
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerDto.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.PlayerDto();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.Country = reader.string();
//           break;
//         case 2:
//           message.Hash = reader.string();
//           break;
//         case 3:
//           message.Index = reader.int32();
//           break;
//         case 4:
//           message.IpAddress = reader.string();
//           break;
//         case 5:
//           message.IsAlive = reader.bool();
//           break;
//         case 6:
//           message.Name = reader.string();
//           break;
//         case 7:
//           message.Rank = reader.int32();
//           break;
//         case 8:
//           message.Score = $root.ScoreDto.decode(reader, reader.uint32());
//           break;
//         case 9:
//           message.Team = reader.int32();
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a PlayerDto message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof PlayerDto
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {PlayerDto} PlayerDto
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerDto.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a PlayerDto message.
//    * @function verify
//    * @memberof PlayerDto
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   PlayerDto.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.Country != null && message.hasOwnProperty("Country"))
//       if (!$util.isString(message.Country)) return "Country: string expected";
//     if (message.Hash != null && message.hasOwnProperty("Hash"))
//       if (!$util.isString(message.Hash)) return "Hash: string expected";
//     if (message.Index != null && message.hasOwnProperty("Index"))
//       if (!$util.isInteger(message.Index)) return "Index: integer expected";
//     if (message.IpAddress != null && message.hasOwnProperty("IpAddress"))
//       if (!$util.isString(message.IpAddress)) return "IpAddress: string expected";
//     if (message.IsAlive != null && message.hasOwnProperty("IsAlive"))
//       if (typeof message.IsAlive !== "boolean") return "IsAlive: boolean expected";
//     if (message.Name != null && message.hasOwnProperty("Name"))
//       if (!$util.isString(message.Name)) return "Name: string expected";
//     if (message.Rank != null && message.hasOwnProperty("Rank"))
//       if (!$util.isInteger(message.Rank)) return "Rank: integer expected";
//     if (message.Score != null && message.hasOwnProperty("Score")) {
//       var error = $root.ScoreDto.verify(message.Score);
//       if (error) return "Score." + error;
//     }
//     if (message.Team != null && message.hasOwnProperty("Team"))
//       if (!$util.isInteger(message.Team)) return "Team: integer expected";
//     return null;
//   };

//   /**
//    * Creates a PlayerDto message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof PlayerDto
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {PlayerDto} PlayerDto
//    */
//   PlayerDto.fromObject = function fromObject(object) {
//     if (object instanceof $root.PlayerDto) return object;
//     var message = new $root.PlayerDto();
//     if (object.Country != null) message.Country = String(object.Country);
//     if (object.Hash != null) message.Hash = String(object.Hash);
//     if (object.Index != null) message.Index = object.Index | 0;
//     if (object.IpAddress != null) message.IpAddress = String(object.IpAddress);
//     if (object.IsAlive != null) message.IsAlive = Boolean(object.IsAlive);
//     if (object.Name != null) message.Name = String(object.Name);
//     if (object.Rank != null) message.Rank = object.Rank | 0;
//     if (object.Score != null) {
//       if (typeof object.Score !== "object") throw TypeError(".PlayerDto.Score: object expected");
//       message.Score = $root.ScoreDto.fromObject(object.Score);
//     }
//     if (object.Team != null) message.Team = object.Team | 0;
//     return message;
//   };

//   /**
//    * Creates a plain object from a PlayerDto message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof PlayerDto
//    * @static
//    * @param {PlayerDto} message PlayerDto
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   PlayerDto.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.Country = "";
//       object.Hash = "";
//       object.Index = 0;
//       object.IpAddress = "";
//       object.IsAlive = false;
//       object.Name = "";
//       object.Rank = 0;
//       object.Score = null;
//       object.Team = 0;
//     }
//     if (message.Country != null && message.hasOwnProperty("Country")) object.Country = message.Country;
//     if (message.Hash != null && message.hasOwnProperty("Hash")) object.Hash = message.Hash;
//     if (message.Index != null && message.hasOwnProperty("Index")) object.Index = message.Index;
//     if (message.IpAddress != null && message.hasOwnProperty("IpAddress")) object.IpAddress = message.IpAddress;
//     if (message.IsAlive != null && message.hasOwnProperty("IsAlive")) object.IsAlive = message.IsAlive;
//     if (message.Name != null && message.hasOwnProperty("Name")) object.Name = message.Name;
//     if (message.Rank != null && message.hasOwnProperty("Rank")) object.Rank = message.Rank;
//     if (message.Score != null && message.hasOwnProperty("Score"))
//       object.Score = $root.ScoreDto.toObject(message.Score, options);
//     if (message.Team != null && message.hasOwnProperty("Team")) object.Team = message.Team;
//     return object;
//   };

//   /**
//    * Converts this PlayerDto to JSON.
//    * @function toJSON
//    * @memberof PlayerDto
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   PlayerDto.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return PlayerDto;
// })();

// $root.PlayerJoinEvent = (function() {
//   /**
//    * Properties of a PlayerJoinEvent.
//    * @exports IPlayerJoinEvent
//    * @interface IPlayerJoinEvent
//    * @property {IPlayerDto|null} [Player] PlayerJoinEvent Player
//    */

//   /**
//    * Constructs a new PlayerJoinEvent.
//    * @exports PlayerJoinEvent
//    * @classdesc Represents a PlayerJoinEvent.
//    * @implements IPlayerJoinEvent
//    * @constructor
//    * @param {IPlayerJoinEvent=} [properties] Properties to set
//    */
//   function PlayerJoinEvent(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * PlayerJoinEvent Player.
//    * @member {IPlayerDto|null|undefined} Player
//    * @memberof PlayerJoinEvent
//    * @instance
//    */
//   PlayerJoinEvent.prototype.Player = null;

//   /**
//    * Creates a new PlayerJoinEvent instance using the specified properties.
//    * @function create
//    * @memberof PlayerJoinEvent
//    * @static
//    * @param {IPlayerJoinEvent=} [properties] Properties to set
//    * @returns {PlayerJoinEvent} PlayerJoinEvent instance
//    */
//   PlayerJoinEvent.create = function create(properties) {
//     return new PlayerJoinEvent(properties);
//   };

//   /**
//    * Encodes the specified PlayerJoinEvent message. Does not implicitly {@link PlayerJoinEvent.verify|verify} messages.
//    * @function encode
//    * @memberof PlayerJoinEvent
//    * @static
//    * @param {IPlayerJoinEvent} message PlayerJoinEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerJoinEvent.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.Player != null && message.hasOwnProperty("Player"))
//       $root.PlayerDto.encode(message.Player, writer.uint32(/* id 1, wireType 2 =*/ 10).fork()).ldelim();
//     return writer;
//   };

//   /**
//    * Encodes the specified PlayerJoinEvent message, length delimited. Does not implicitly {@link PlayerJoinEvent.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof PlayerJoinEvent
//    * @static
//    * @param {IPlayerJoinEvent} message PlayerJoinEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerJoinEvent.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a PlayerJoinEvent message from the specified reader or buffer.
//    * @function decode
//    * @memberof PlayerJoinEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {PlayerJoinEvent} PlayerJoinEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerJoinEvent.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.PlayerJoinEvent();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.Player = $root.PlayerDto.decode(reader, reader.uint32());
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a PlayerJoinEvent message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof PlayerJoinEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {PlayerJoinEvent} PlayerJoinEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerJoinEvent.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a PlayerJoinEvent message.
//    * @function verify
//    * @memberof PlayerJoinEvent
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   PlayerJoinEvent.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.Player != null && message.hasOwnProperty("Player")) {
//       var error = $root.PlayerDto.verify(message.Player);
//       if (error) return "Player." + error;
//     }
//     return null;
//   };

//   /**
//    * Creates a PlayerJoinEvent message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof PlayerJoinEvent
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {PlayerJoinEvent} PlayerJoinEvent
//    */
//   PlayerJoinEvent.fromObject = function fromObject(object) {
//     if (object instanceof $root.PlayerJoinEvent) return object;
//     var message = new $root.PlayerJoinEvent();
//     if (object.Player != null) {
//       if (typeof object.Player !== "object") throw TypeError(".PlayerJoinEvent.Player: object expected");
//       message.Player = $root.PlayerDto.fromObject(object.Player);
//     }
//     return message;
//   };

//   /**
//    * Creates a plain object from a PlayerJoinEvent message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof PlayerJoinEvent
//    * @static
//    * @param {PlayerJoinEvent} message PlayerJoinEvent
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   PlayerJoinEvent.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) object.Player = null;
//     if (message.Player != null && message.hasOwnProperty("Player"))
//       object.Player = $root.PlayerDto.toObject(message.Player, options);
//     return object;
//   };

//   /**
//    * Converts this PlayerJoinEvent to JSON.
//    * @function toJSON
//    * @memberof PlayerJoinEvent
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   PlayerJoinEvent.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return PlayerJoinEvent;
// })();

// $root.PlayerKillEvent = (function() {
//   /**
//    * Properties of a PlayerKillEvent.
//    * @exports IPlayerKillEvent
//    * @interface IPlayerKillEvent
//    * @property {number|null} [AttackerId] PlayerKillEvent AttackerId
//    * @property {IVector3|null} [AttackerPosition] PlayerKillEvent AttackerPosition
//    * @property {number|null} [VictimId] PlayerKillEvent VictimId
//    * @property {IVector3|null} [VictimPosition] PlayerKillEvent VictimPosition
//    * @property {string|null} [Weapon] PlayerKillEvent Weapon
//    */

//   /**
//    * Constructs a new PlayerKillEvent.
//    * @exports PlayerKillEvent
//    * @classdesc Represents a PlayerKillEvent.
//    * @implements IPlayerKillEvent
//    * @constructor
//    * @param {IPlayerKillEvent=} [properties] Properties to set
//    */
//   function PlayerKillEvent(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * PlayerKillEvent AttackerId.
//    * @member {number} AttackerId
//    * @memberof PlayerKillEvent
//    * @instance
//    */
//   PlayerKillEvent.prototype.AttackerId = 0;

//   /**
//    * PlayerKillEvent AttackerPosition.
//    * @member {IVector3|null|undefined} AttackerPosition
//    * @memberof PlayerKillEvent
//    * @instance
//    */
//   PlayerKillEvent.prototype.AttackerPosition = null;

//   /**
//    * PlayerKillEvent VictimId.
//    * @member {number} VictimId
//    * @memberof PlayerKillEvent
//    * @instance
//    */
//   PlayerKillEvent.prototype.VictimId = 0;

//   /**
//    * PlayerKillEvent VictimPosition.
//    * @member {IVector3|null|undefined} VictimPosition
//    * @memberof PlayerKillEvent
//    * @instance
//    */
//   PlayerKillEvent.prototype.VictimPosition = null;

//   /**
//    * PlayerKillEvent Weapon.
//    * @member {string} Weapon
//    * @memberof PlayerKillEvent
//    * @instance
//    */
//   PlayerKillEvent.prototype.Weapon = "";

//   /**
//    * Creates a new PlayerKillEvent instance using the specified properties.
//    * @function create
//    * @memberof PlayerKillEvent
//    * @static
//    * @param {IPlayerKillEvent=} [properties] Properties to set
//    * @returns {PlayerKillEvent} PlayerKillEvent instance
//    */
//   PlayerKillEvent.create = function create(properties) {
//     return new PlayerKillEvent(properties);
//   };

//   /**
//    * Encodes the specified PlayerKillEvent message. Does not implicitly {@link PlayerKillEvent.verify|verify} messages.
//    * @function encode
//    * @memberof PlayerKillEvent
//    * @static
//    * @param {IPlayerKillEvent} message PlayerKillEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerKillEvent.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.AttackerId != null && message.hasOwnProperty("AttackerId"))
//       writer.uint32(/* id 1, wireType 0 =*/ 8).int32(message.AttackerId);
//     if (message.AttackerPosition != null && message.hasOwnProperty("AttackerPosition"))
//       $root.Vector3.encode(message.AttackerPosition, writer.uint32(/* id 2, wireType 2 =*/ 18).fork()).ldelim();
//     if (message.VictimId != null && message.hasOwnProperty("VictimId"))
//       writer.uint32(/* id 3, wireType 0 =*/ 24).int32(message.VictimId);
//     if (message.VictimPosition != null && message.hasOwnProperty("VictimPosition"))
//       $root.Vector3.encode(message.VictimPosition, writer.uint32(/* id 4, wireType 2 =*/ 34).fork()).ldelim();
//     if (message.Weapon != null && message.hasOwnProperty("Weapon"))
//       writer.uint32(/* id 5, wireType 2 =*/ 42).string(message.Weapon);
//     return writer;
//   };

//   /**
//    * Encodes the specified PlayerKillEvent message, length delimited. Does not implicitly {@link PlayerKillEvent.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof PlayerKillEvent
//    * @static
//    * @param {IPlayerKillEvent} message PlayerKillEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerKillEvent.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a PlayerKillEvent message from the specified reader or buffer.
//    * @function decode
//    * @memberof PlayerKillEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {PlayerKillEvent} PlayerKillEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerKillEvent.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.PlayerKillEvent();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.AttackerId = reader.int32();
//           break;
//         case 2:
//           message.AttackerPosition = $root.Vector3.decode(reader, reader.uint32());
//           break;
//         case 3:
//           message.VictimId = reader.int32();
//           break;
//         case 4:
//           message.VictimPosition = $root.Vector3.decode(reader, reader.uint32());
//           break;
//         case 5:
//           message.Weapon = reader.string();
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a PlayerKillEvent message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof PlayerKillEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {PlayerKillEvent} PlayerKillEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerKillEvent.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a PlayerKillEvent message.
//    * @function verify
//    * @memberof PlayerKillEvent
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   PlayerKillEvent.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.AttackerId != null && message.hasOwnProperty("AttackerId"))
//       if (!$util.isInteger(message.AttackerId)) return "AttackerId: integer expected";
//     if (message.AttackerPosition != null && message.hasOwnProperty("AttackerPosition")) {
//       var error = $root.Vector3.verify(message.AttackerPosition);
//       if (error) return "AttackerPosition." + error;
//     }
//     if (message.VictimId != null && message.hasOwnProperty("VictimId"))
//       if (!$util.isInteger(message.VictimId)) return "VictimId: integer expected";
//     if (message.VictimPosition != null && message.hasOwnProperty("VictimPosition")) {
//       var error = $root.Vector3.verify(message.VictimPosition);
//       if (error) return "VictimPosition." + error;
//     }
//     if (message.Weapon != null && message.hasOwnProperty("Weapon"))
//       if (!$util.isString(message.Weapon)) return "Weapon: string expected";
//     return null;
//   };

//   /**
//    * Creates a PlayerKillEvent message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof PlayerKillEvent
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {PlayerKillEvent} PlayerKillEvent
//    */
//   PlayerKillEvent.fromObject = function fromObject(object) {
//     if (object instanceof $root.PlayerKillEvent) return object;
//     var message = new $root.PlayerKillEvent();
//     if (object.AttackerId != null) message.AttackerId = object.AttackerId | 0;
//     if (object.AttackerPosition != null) {
//       if (typeof object.AttackerPosition !== "object")
//         throw TypeError(".PlayerKillEvent.AttackerPosition: object expected");
//       message.AttackerPosition = $root.Vector3.fromObject(object.AttackerPosition);
//     }
//     if (object.VictimId != null) message.VictimId = object.VictimId | 0;
//     if (object.VictimPosition != null) {
//       if (typeof object.VictimPosition !== "object")
//         throw TypeError(".PlayerKillEvent.VictimPosition: object expected");
//       message.VictimPosition = $root.Vector3.fromObject(object.VictimPosition);
//     }
//     if (object.Weapon != null) message.Weapon = String(object.Weapon);
//     return message;
//   };

//   /**
//    * Creates a plain object from a PlayerKillEvent message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof PlayerKillEvent
//    * @static
//    * @param {PlayerKillEvent} message PlayerKillEvent
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   PlayerKillEvent.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.AttackerId = 0;
//       object.AttackerPosition = null;
//       object.VictimId = 0;
//       object.VictimPosition = null;
//       object.Weapon = "";
//     }
//     if (message.AttackerId != null && message.hasOwnProperty("AttackerId")) object.AttackerId = message.AttackerId;
//     if (message.AttackerPosition != null && message.hasOwnProperty("AttackerPosition"))
//       object.AttackerPosition = $root.Vector3.toObject(message.AttackerPosition, options);
//     if (message.VictimId != null && message.hasOwnProperty("VictimId")) object.VictimId = message.VictimId;
//     if (message.VictimPosition != null && message.hasOwnProperty("VictimPosition"))
//       object.VictimPosition = $root.Vector3.toObject(message.VictimPosition, options);
//     if (message.Weapon != null && message.hasOwnProperty("Weapon")) object.Weapon = message.Weapon;
//     return object;
//   };

//   /**
//    * Converts this PlayerKillEvent to JSON.
//    * @function toJSON
//    * @memberof PlayerKillEvent
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   PlayerKillEvent.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return PlayerKillEvent;
// })();

// $root.PlayerLeftEvent = (function() {
//   /**
//    * Properties of a PlayerLeftEvent.
//    * @exports IPlayerLeftEvent
//    * @interface IPlayerLeftEvent
//    * @property {number|null} [PlayerId] PlayerLeftEvent PlayerId
//    */

//   /**
//    * Constructs a new PlayerLeftEvent.
//    * @exports PlayerLeftEvent
//    * @classdesc Represents a PlayerLeftEvent.
//    * @implements IPlayerLeftEvent
//    * @constructor
//    * @param {IPlayerLeftEvent=} [properties] Properties to set
//    */
//   function PlayerLeftEvent(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * PlayerLeftEvent PlayerId.
//    * @member {number} PlayerId
//    * @memberof PlayerLeftEvent
//    * @instance
//    */
//   PlayerLeftEvent.prototype.PlayerId = 0;

//   /**
//    * Creates a new PlayerLeftEvent instance using the specified properties.
//    * @function create
//    * @memberof PlayerLeftEvent
//    * @static
//    * @param {IPlayerLeftEvent=} [properties] Properties to set
//    * @returns {PlayerLeftEvent} PlayerLeftEvent instance
//    */
//   PlayerLeftEvent.create = function create(properties) {
//     return new PlayerLeftEvent(properties);
//   };

//   /**
//    * Encodes the specified PlayerLeftEvent message. Does not implicitly {@link PlayerLeftEvent.verify|verify} messages.
//    * @function encode
//    * @memberof PlayerLeftEvent
//    * @static
//    * @param {IPlayerLeftEvent} message PlayerLeftEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerLeftEvent.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId"))
//       writer.uint32(/* id 1, wireType 0 =*/ 8).int32(message.PlayerId);
//     return writer;
//   };

//   /**
//    * Encodes the specified PlayerLeftEvent message, length delimited. Does not implicitly {@link PlayerLeftEvent.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof PlayerLeftEvent
//    * @static
//    * @param {IPlayerLeftEvent} message PlayerLeftEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerLeftEvent.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a PlayerLeftEvent message from the specified reader or buffer.
//    * @function decode
//    * @memberof PlayerLeftEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {PlayerLeftEvent} PlayerLeftEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerLeftEvent.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.PlayerLeftEvent();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.PlayerId = reader.int32();
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a PlayerLeftEvent message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof PlayerLeftEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {PlayerLeftEvent} PlayerLeftEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerLeftEvent.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a PlayerLeftEvent message.
//    * @function verify
//    * @memberof PlayerLeftEvent
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   PlayerLeftEvent.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId"))
//       if (!$util.isInteger(message.PlayerId)) return "PlayerId: integer expected";
//     return null;
//   };

//   /**
//    * Creates a PlayerLeftEvent message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof PlayerLeftEvent
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {PlayerLeftEvent} PlayerLeftEvent
//    */
//   PlayerLeftEvent.fromObject = function fromObject(object) {
//     if (object instanceof $root.PlayerLeftEvent) return object;
//     var message = new $root.PlayerLeftEvent();
//     if (object.PlayerId != null) message.PlayerId = object.PlayerId | 0;
//     return message;
//   };

//   /**
//    * Creates a plain object from a PlayerLeftEvent message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof PlayerLeftEvent
//    * @static
//    * @param {PlayerLeftEvent} message PlayerLeftEvent
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   PlayerLeftEvent.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) object.PlayerId = 0;
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId")) object.PlayerId = message.PlayerId;
//     return object;
//   };

//   /**
//    * Converts this PlayerLeftEvent to JSON.
//    * @function toJSON
//    * @memberof PlayerLeftEvent
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   PlayerLeftEvent.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return PlayerLeftEvent;
// })();

// $root.PlayerPositionEvent = (function() {
//   /**
//    * Properties of a PlayerPositionEvent.
//    * @exports IPlayerPositionEvent
//    * @interface IPlayerPositionEvent
//    * @property {number|null} [Ping] PlayerPositionEvent Ping
//    * @property {number|null} [PlayerId] PlayerPositionEvent PlayerId
//    * @property {IVector3|null} [Position] PlayerPositionEvent Position
//    * @property {IVector3|null} [Rotation] PlayerPositionEvent Rotation
//    */

//   /**
//    * Constructs a new PlayerPositionEvent.
//    * @exports PlayerPositionEvent
//    * @classdesc Represents a PlayerPositionEvent.
//    * @implements IPlayerPositionEvent
//    * @constructor
//    * @param {IPlayerPositionEvent=} [properties] Properties to set
//    */
//   function PlayerPositionEvent(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * PlayerPositionEvent Ping.
//    * @member {number} Ping
//    * @memberof PlayerPositionEvent
//    * @instance
//    */
//   PlayerPositionEvent.prototype.Ping = 0;

//   /**
//    * PlayerPositionEvent PlayerId.
//    * @member {number} PlayerId
//    * @memberof PlayerPositionEvent
//    * @instance
//    */
//   PlayerPositionEvent.prototype.PlayerId = 0;

//   /**
//    * PlayerPositionEvent Position.
//    * @member {IVector3|null|undefined} Position
//    * @memberof PlayerPositionEvent
//    * @instance
//    */
//   PlayerPositionEvent.prototype.Position = null;

//   /**
//    * PlayerPositionEvent Rotation.
//    * @member {IVector3|null|undefined} Rotation
//    * @memberof PlayerPositionEvent
//    * @instance
//    */
//   PlayerPositionEvent.prototype.Rotation = null;

//   /**
//    * Creates a new PlayerPositionEvent instance using the specified properties.
//    * @function create
//    * @memberof PlayerPositionEvent
//    * @static
//    * @param {IPlayerPositionEvent=} [properties] Properties to set
//    * @returns {PlayerPositionEvent} PlayerPositionEvent instance
//    */
//   PlayerPositionEvent.create = function create(properties) {
//     return new PlayerPositionEvent(properties);
//   };

//   /**
//    * Encodes the specified PlayerPositionEvent message. Does not implicitly {@link PlayerPositionEvent.verify|verify} messages.
//    * @function encode
//    * @memberof PlayerPositionEvent
//    * @static
//    * @param {IPlayerPositionEvent} message PlayerPositionEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerPositionEvent.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.Ping != null && message.hasOwnProperty("Ping"))
//       writer.uint32(/* id 1, wireType 0 =*/ 8).int32(message.Ping);
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId"))
//       writer.uint32(/* id 2, wireType 0 =*/ 16).int32(message.PlayerId);
//     if (message.Position != null && message.hasOwnProperty("Position"))
//       $root.Vector3.encode(message.Position, writer.uint32(/* id 3, wireType 2 =*/ 26).fork()).ldelim();
//     if (message.Rotation != null && message.hasOwnProperty("Rotation"))
//       $root.Vector3.encode(message.Rotation, writer.uint32(/* id 4, wireType 2 =*/ 34).fork()).ldelim();
//     return writer;
//   };

//   /**
//    * Encodes the specified PlayerPositionEvent message, length delimited. Does not implicitly {@link PlayerPositionEvent.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof PlayerPositionEvent
//    * @static
//    * @param {IPlayerPositionEvent} message PlayerPositionEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerPositionEvent.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a PlayerPositionEvent message from the specified reader or buffer.
//    * @function decode
//    * @memberof PlayerPositionEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {PlayerPositionEvent} PlayerPositionEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerPositionEvent.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.PlayerPositionEvent();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.Ping = reader.int32();
//           break;
//         case 2:
//           message.PlayerId = reader.int32();
//           break;
//         case 3:
//           message.Position = $root.Vector3.decode(reader, reader.uint32());
//           break;
//         case 4:
//           message.Rotation = $root.Vector3.decode(reader, reader.uint32());
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a PlayerPositionEvent message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof PlayerPositionEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {PlayerPositionEvent} PlayerPositionEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerPositionEvent.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a PlayerPositionEvent message.
//    * @function verify
//    * @memberof PlayerPositionEvent
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   PlayerPositionEvent.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.Ping != null && message.hasOwnProperty("Ping"))
//       if (!$util.isInteger(message.Ping)) return "Ping: integer expected";
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId"))
//       if (!$util.isInteger(message.PlayerId)) return "PlayerId: integer expected";
//     if (message.Position != null && message.hasOwnProperty("Position")) {
//       var error = $root.Vector3.verify(message.Position);
//       if (error) return "Position." + error;
//     }
//     if (message.Rotation != null && message.hasOwnProperty("Rotation")) {
//       var error = $root.Vector3.verify(message.Rotation);
//       if (error) return "Rotation." + error;
//     }
//     return null;
//   };

//   /**
//    * Creates a PlayerPositionEvent message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof PlayerPositionEvent
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {PlayerPositionEvent} PlayerPositionEvent
//    */
//   PlayerPositionEvent.fromObject = function fromObject(object) {
//     if (object instanceof $root.PlayerPositionEvent) return object;
//     var message = new $root.PlayerPositionEvent();
//     if (object.Ping != null) message.Ping = object.Ping | 0;
//     if (object.PlayerId != null) message.PlayerId = object.PlayerId | 0;
//     if (object.Position != null) {
//       if (typeof object.Position !== "object") throw TypeError(".PlayerPositionEvent.Position: object expected");
//       message.Position = $root.Vector3.fromObject(object.Position);
//     }
//     if (object.Rotation != null) {
//       if (typeof object.Rotation !== "object") throw TypeError(".PlayerPositionEvent.Rotation: object expected");
//       message.Rotation = $root.Vector3.fromObject(object.Rotation);
//     }
//     return message;
//   };

//   /**
//    * Creates a plain object from a PlayerPositionEvent message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof PlayerPositionEvent
//    * @static
//    * @param {PlayerPositionEvent} message PlayerPositionEvent
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   PlayerPositionEvent.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.Ping = 0;
//       object.PlayerId = 0;
//       object.Position = null;
//       object.Rotation = null;
//     }
//     if (message.Ping != null && message.hasOwnProperty("Ping")) object.Ping = message.Ping;
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId")) object.PlayerId = message.PlayerId;
//     if (message.Position != null && message.hasOwnProperty("Position"))
//       object.Position = $root.Vector3.toObject(message.Position, options);
//     if (message.Rotation != null && message.hasOwnProperty("Rotation"))
//       object.Rotation = $root.Vector3.toObject(message.Rotation, options);
//     return object;
//   };

//   /**
//    * Converts this PlayerPositionEvent to JSON.
//    * @function toJSON
//    * @memberof PlayerPositionEvent
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   PlayerPositionEvent.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return PlayerPositionEvent;
// })();

// $root.PlayerScoreEvent = (function() {
//   /**
//    * Properties of a PlayerScoreEvent.
//    * @exports IPlayerScoreEvent
//    * @interface IPlayerScoreEvent
//    * @property {number|null} [Deaths] PlayerScoreEvent Deaths
//    * @property {number|null} [Kills] PlayerScoreEvent Kills
//    * @property {number|null} [PlayerId] PlayerScoreEvent PlayerId
//    * @property {number|null} [TeamScore] PlayerScoreEvent TeamScore
//    * @property {number|null} [TotalScore] PlayerScoreEvent TotalScore
//    */

//   /**
//    * Constructs a new PlayerScoreEvent.
//    * @exports PlayerScoreEvent
//    * @classdesc Represents a PlayerScoreEvent.
//    * @implements IPlayerScoreEvent
//    * @constructor
//    * @param {IPlayerScoreEvent=} [properties] Properties to set
//    */
//   function PlayerScoreEvent(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * PlayerScoreEvent Deaths.
//    * @member {number} Deaths
//    * @memberof PlayerScoreEvent
//    * @instance
//    */
//   PlayerScoreEvent.prototype.Deaths = 0;

//   /**
//    * PlayerScoreEvent Kills.
//    * @member {number} Kills
//    * @memberof PlayerScoreEvent
//    * @instance
//    */
//   PlayerScoreEvent.prototype.Kills = 0;

//   /**
//    * PlayerScoreEvent PlayerId.
//    * @member {number} PlayerId
//    * @memberof PlayerScoreEvent
//    * @instance
//    */
//   PlayerScoreEvent.prototype.PlayerId = 0;

//   /**
//    * PlayerScoreEvent TeamScore.
//    * @member {number} TeamScore
//    * @memberof PlayerScoreEvent
//    * @instance
//    */
//   PlayerScoreEvent.prototype.TeamScore = 0;

//   /**
//    * PlayerScoreEvent TotalScore.
//    * @member {number} TotalScore
//    * @memberof PlayerScoreEvent
//    * @instance
//    */
//   PlayerScoreEvent.prototype.TotalScore = 0;

//   /**
//    * Creates a new PlayerScoreEvent instance using the specified properties.
//    * @function create
//    * @memberof PlayerScoreEvent
//    * @static
//    * @param {IPlayerScoreEvent=} [properties] Properties to set
//    * @returns {PlayerScoreEvent} PlayerScoreEvent instance
//    */
//   PlayerScoreEvent.create = function create(properties) {
//     return new PlayerScoreEvent(properties);
//   };

//   /**
//    * Encodes the specified PlayerScoreEvent message. Does not implicitly {@link PlayerScoreEvent.verify|verify} messages.
//    * @function encode
//    * @memberof PlayerScoreEvent
//    * @static
//    * @param {IPlayerScoreEvent} message PlayerScoreEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerScoreEvent.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.Deaths != null && message.hasOwnProperty("Deaths"))
//       writer.uint32(/* id 1, wireType 0 =*/ 8).int32(message.Deaths);
//     if (message.Kills != null && message.hasOwnProperty("Kills"))
//       writer.uint32(/* id 2, wireType 0 =*/ 16).int32(message.Kills);
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId"))
//       writer.uint32(/* id 3, wireType 0 =*/ 24).int32(message.PlayerId);
//     if (message.TeamScore != null && message.hasOwnProperty("TeamScore"))
//       writer.uint32(/* id 4, wireType 0 =*/ 32).int32(message.TeamScore);
//     if (message.TotalScore != null && message.hasOwnProperty("TotalScore"))
//       writer.uint32(/* id 5, wireType 0 =*/ 40).int32(message.TotalScore);
//     return writer;
//   };

//   /**
//    * Encodes the specified PlayerScoreEvent message, length delimited. Does not implicitly {@link PlayerScoreEvent.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof PlayerScoreEvent
//    * @static
//    * @param {IPlayerScoreEvent} message PlayerScoreEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerScoreEvent.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a PlayerScoreEvent message from the specified reader or buffer.
//    * @function decode
//    * @memberof PlayerScoreEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {PlayerScoreEvent} PlayerScoreEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerScoreEvent.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.PlayerScoreEvent();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.Deaths = reader.int32();
//           break;
//         case 2:
//           message.Kills = reader.int32();
//           break;
//         case 3:
//           message.PlayerId = reader.int32();
//           break;
//         case 4:
//           message.TeamScore = reader.int32();
//           break;
//         case 5:
//           message.TotalScore = reader.int32();
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a PlayerScoreEvent message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof PlayerScoreEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {PlayerScoreEvent} PlayerScoreEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerScoreEvent.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a PlayerScoreEvent message.
//    * @function verify
//    * @memberof PlayerScoreEvent
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   PlayerScoreEvent.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.Deaths != null && message.hasOwnProperty("Deaths"))
//       if (!$util.isInteger(message.Deaths)) return "Deaths: integer expected";
//     if (message.Kills != null && message.hasOwnProperty("Kills"))
//       if (!$util.isInteger(message.Kills)) return "Kills: integer expected";
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId"))
//       if (!$util.isInteger(message.PlayerId)) return "PlayerId: integer expected";
//     if (message.TeamScore != null && message.hasOwnProperty("TeamScore"))
//       if (!$util.isInteger(message.TeamScore)) return "TeamScore: integer expected";
//     if (message.TotalScore != null && message.hasOwnProperty("TotalScore"))
//       if (!$util.isInteger(message.TotalScore)) return "TotalScore: integer expected";
//     return null;
//   };

//   /**
//    * Creates a PlayerScoreEvent message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof PlayerScoreEvent
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {PlayerScoreEvent} PlayerScoreEvent
//    */
//   PlayerScoreEvent.fromObject = function fromObject(object) {
//     if (object instanceof $root.PlayerScoreEvent) return object;
//     var message = new $root.PlayerScoreEvent();
//     if (object.Deaths != null) message.Deaths = object.Deaths | 0;
//     if (object.Kills != null) message.Kills = object.Kills | 0;
//     if (object.PlayerId != null) message.PlayerId = object.PlayerId | 0;
//     if (object.TeamScore != null) message.TeamScore = object.TeamScore | 0;
//     if (object.TotalScore != null) message.TotalScore = object.TotalScore | 0;
//     return message;
//   };

//   /**
//    * Creates a plain object from a PlayerScoreEvent message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof PlayerScoreEvent
//    * @static
//    * @param {PlayerScoreEvent} message PlayerScoreEvent
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   PlayerScoreEvent.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.Deaths = 0;
//       object.Kills = 0;
//       object.PlayerId = 0;
//       object.TeamScore = 0;
//       object.TotalScore = 0;
//     }
//     if (message.Deaths != null && message.hasOwnProperty("Deaths")) object.Deaths = message.Deaths;
//     if (message.Kills != null && message.hasOwnProperty("Kills")) object.Kills = message.Kills;
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId")) object.PlayerId = message.PlayerId;
//     if (message.TeamScore != null && message.hasOwnProperty("TeamScore")) object.TeamScore = message.TeamScore;
//     if (message.TotalScore != null && message.hasOwnProperty("TotalScore")) object.TotalScore = message.TotalScore;
//     return object;
//   };

//   /**
//    * Converts this PlayerScoreEvent to JSON.
//    * @function toJSON
//    * @memberof PlayerScoreEvent
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   PlayerScoreEvent.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return PlayerScoreEvent;
// })();

// $root.PlayerSpawnEvent = (function() {
//   /**
//    * Properties of a PlayerSpawnEvent.
//    * @exports IPlayerSpawnEvent
//    * @interface IPlayerSpawnEvent
//    * @property {number|null} [PlayerId] PlayerSpawnEvent PlayerId
//    * @property {IVector3|null} [Position] PlayerSpawnEvent Position
//    * @property {IVector3|null} [Rotation] PlayerSpawnEvent Rotation
//    */

//   /**
//    * Constructs a new PlayerSpawnEvent.
//    * @exports PlayerSpawnEvent
//    * @classdesc Represents a PlayerSpawnEvent.
//    * @implements IPlayerSpawnEvent
//    * @constructor
//    * @param {IPlayerSpawnEvent=} [properties] Properties to set
//    */
//   function PlayerSpawnEvent(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * PlayerSpawnEvent PlayerId.
//    * @member {number} PlayerId
//    * @memberof PlayerSpawnEvent
//    * @instance
//    */
//   PlayerSpawnEvent.prototype.PlayerId = 0;

//   /**
//    * PlayerSpawnEvent Position.
//    * @member {IVector3|null|undefined} Position
//    * @memberof PlayerSpawnEvent
//    * @instance
//    */
//   PlayerSpawnEvent.prototype.Position = null;

//   /**
//    * PlayerSpawnEvent Rotation.
//    * @member {IVector3|null|undefined} Rotation
//    * @memberof PlayerSpawnEvent
//    * @instance
//    */
//   PlayerSpawnEvent.prototype.Rotation = null;

//   /**
//    * Creates a new PlayerSpawnEvent instance using the specified properties.
//    * @function create
//    * @memberof PlayerSpawnEvent
//    * @static
//    * @param {IPlayerSpawnEvent=} [properties] Properties to set
//    * @returns {PlayerSpawnEvent} PlayerSpawnEvent instance
//    */
//   PlayerSpawnEvent.create = function create(properties) {
//     return new PlayerSpawnEvent(properties);
//   };

//   /**
//    * Encodes the specified PlayerSpawnEvent message. Does not implicitly {@link PlayerSpawnEvent.verify|verify} messages.
//    * @function encode
//    * @memberof PlayerSpawnEvent
//    * @static
//    * @param {IPlayerSpawnEvent} message PlayerSpawnEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerSpawnEvent.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId"))
//       writer.uint32(/* id 1, wireType 0 =*/ 8).int32(message.PlayerId);
//     if (message.Position != null && message.hasOwnProperty("Position"))
//       $root.Vector3.encode(message.Position, writer.uint32(/* id 2, wireType 2 =*/ 18).fork()).ldelim();
//     if (message.Rotation != null && message.hasOwnProperty("Rotation"))
//       $root.Vector3.encode(message.Rotation, writer.uint32(/* id 3, wireType 2 =*/ 26).fork()).ldelim();
//     return writer;
//   };

//   /**
//    * Encodes the specified PlayerSpawnEvent message, length delimited. Does not implicitly {@link PlayerSpawnEvent.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof PlayerSpawnEvent
//    * @static
//    * @param {IPlayerSpawnEvent} message PlayerSpawnEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerSpawnEvent.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a PlayerSpawnEvent message from the specified reader or buffer.
//    * @function decode
//    * @memberof PlayerSpawnEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {PlayerSpawnEvent} PlayerSpawnEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerSpawnEvent.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.PlayerSpawnEvent();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.PlayerId = reader.int32();
//           break;
//         case 2:
//           message.Position = $root.Vector3.decode(reader, reader.uint32());
//           break;
//         case 3:
//           message.Rotation = $root.Vector3.decode(reader, reader.uint32());
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a PlayerSpawnEvent message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof PlayerSpawnEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {PlayerSpawnEvent} PlayerSpawnEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerSpawnEvent.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a PlayerSpawnEvent message.
//    * @function verify
//    * @memberof PlayerSpawnEvent
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   PlayerSpawnEvent.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId"))
//       if (!$util.isInteger(message.PlayerId)) return "PlayerId: integer expected";
//     if (message.Position != null && message.hasOwnProperty("Position")) {
//       var error = $root.Vector3.verify(message.Position);
//       if (error) return "Position." + error;
//     }
//     if (message.Rotation != null && message.hasOwnProperty("Rotation")) {
//       var error = $root.Vector3.verify(message.Rotation);
//       if (error) return "Rotation." + error;
//     }
//     return null;
//   };

//   /**
//    * Creates a PlayerSpawnEvent message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof PlayerSpawnEvent
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {PlayerSpawnEvent} PlayerSpawnEvent
//    */
//   PlayerSpawnEvent.fromObject = function fromObject(object) {
//     if (object instanceof $root.PlayerSpawnEvent) return object;
//     var message = new $root.PlayerSpawnEvent();
//     if (object.PlayerId != null) message.PlayerId = object.PlayerId | 0;
//     if (object.Position != null) {
//       if (typeof object.Position !== "object") throw TypeError(".PlayerSpawnEvent.Position: object expected");
//       message.Position = $root.Vector3.fromObject(object.Position);
//     }
//     if (object.Rotation != null) {
//       if (typeof object.Rotation !== "object") throw TypeError(".PlayerSpawnEvent.Rotation: object expected");
//       message.Rotation = $root.Vector3.fromObject(object.Rotation);
//     }
//     return message;
//   };

//   /**
//    * Creates a plain object from a PlayerSpawnEvent message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof PlayerSpawnEvent
//    * @static
//    * @param {PlayerSpawnEvent} message PlayerSpawnEvent
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   PlayerSpawnEvent.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.PlayerId = 0;
//       object.Position = null;
//       object.Rotation = null;
//     }
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId")) object.PlayerId = message.PlayerId;
//     if (message.Position != null && message.hasOwnProperty("Position"))
//       object.Position = $root.Vector3.toObject(message.Position, options);
//     if (message.Rotation != null && message.hasOwnProperty("Rotation"))
//       object.Rotation = $root.Vector3.toObject(message.Rotation, options);
//     return object;
//   };

//   /**
//    * Converts this PlayerSpawnEvent to JSON.
//    * @function toJSON
//    * @memberof PlayerSpawnEvent
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   PlayerSpawnEvent.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return PlayerSpawnEvent;
// })();

// $root.PlayerTeamEvent = (function() {
//   /**
//    * Properties of a PlayerTeamEvent.
//    * @exports IPlayerTeamEvent
//    * @interface IPlayerTeamEvent
//    * @property {number|null} [PlayerId] PlayerTeamEvent PlayerId
//    * @property {number|null} [TeamId] PlayerTeamEvent TeamId
//    */

//   /**
//    * Constructs a new PlayerTeamEvent.
//    * @exports PlayerTeamEvent
//    * @classdesc Represents a PlayerTeamEvent.
//    * @implements IPlayerTeamEvent
//    * @constructor
//    * @param {IPlayerTeamEvent=} [properties] Properties to set
//    */
//   function PlayerTeamEvent(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * PlayerTeamEvent PlayerId.
//    * @member {number} PlayerId
//    * @memberof PlayerTeamEvent
//    * @instance
//    */
//   PlayerTeamEvent.prototype.PlayerId = 0;

//   /**
//    * PlayerTeamEvent TeamId.
//    * @member {number} TeamId
//    * @memberof PlayerTeamEvent
//    * @instance
//    */
//   PlayerTeamEvent.prototype.TeamId = 0;

//   /**
//    * Creates a new PlayerTeamEvent instance using the specified properties.
//    * @function create
//    * @memberof PlayerTeamEvent
//    * @static
//    * @param {IPlayerTeamEvent=} [properties] Properties to set
//    * @returns {PlayerTeamEvent} PlayerTeamEvent instance
//    */
//   PlayerTeamEvent.create = function create(properties) {
//     return new PlayerTeamEvent(properties);
//   };

//   /**
//    * Encodes the specified PlayerTeamEvent message. Does not implicitly {@link PlayerTeamEvent.verify|verify} messages.
//    * @function encode
//    * @memberof PlayerTeamEvent
//    * @static
//    * @param {IPlayerTeamEvent} message PlayerTeamEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerTeamEvent.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId"))
//       writer.uint32(/* id 1, wireType 0 =*/ 8).int32(message.PlayerId);
//     if (message.TeamId != null && message.hasOwnProperty("TeamId"))
//       writer.uint32(/* id 2, wireType 0 =*/ 16).int32(message.TeamId);
//     return writer;
//   };

//   /**
//    * Encodes the specified PlayerTeamEvent message, length delimited. Does not implicitly {@link PlayerTeamEvent.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof PlayerTeamEvent
//    * @static
//    * @param {IPlayerTeamEvent} message PlayerTeamEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerTeamEvent.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a PlayerTeamEvent message from the specified reader or buffer.
//    * @function decode
//    * @memberof PlayerTeamEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {PlayerTeamEvent} PlayerTeamEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerTeamEvent.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.PlayerTeamEvent();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.PlayerId = reader.int32();
//           break;
//         case 2:
//           message.TeamId = reader.int32();
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a PlayerTeamEvent message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof PlayerTeamEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {PlayerTeamEvent} PlayerTeamEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerTeamEvent.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a PlayerTeamEvent message.
//    * @function verify
//    * @memberof PlayerTeamEvent
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   PlayerTeamEvent.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId"))
//       if (!$util.isInteger(message.PlayerId)) return "PlayerId: integer expected";
//     if (message.TeamId != null && message.hasOwnProperty("TeamId"))
//       if (!$util.isInteger(message.TeamId)) return "TeamId: integer expected";
//     return null;
//   };

//   /**
//    * Creates a PlayerTeamEvent message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof PlayerTeamEvent
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {PlayerTeamEvent} PlayerTeamEvent
//    */
//   PlayerTeamEvent.fromObject = function fromObject(object) {
//     if (object instanceof $root.PlayerTeamEvent) return object;
//     var message = new $root.PlayerTeamEvent();
//     if (object.PlayerId != null) message.PlayerId = object.PlayerId | 0;
//     if (object.TeamId != null) message.TeamId = object.TeamId | 0;
//     return message;
//   };

//   /**
//    * Creates a plain object from a PlayerTeamEvent message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof PlayerTeamEvent
//    * @static
//    * @param {PlayerTeamEvent} message PlayerTeamEvent
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   PlayerTeamEvent.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.PlayerId = 0;
//       object.TeamId = 0;
//     }
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId")) object.PlayerId = message.PlayerId;
//     if (message.TeamId != null && message.hasOwnProperty("TeamId")) object.TeamId = message.TeamId;
//     return object;
//   };

//   /**
//    * Converts this PlayerTeamEvent to JSON.
//    * @function toJSON
//    * @memberof PlayerTeamEvent
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   PlayerTeamEvent.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return PlayerTeamEvent;
// })();

// $root.PlayerVehicleEvent = (function() {
//   /**
//    * Properties of a PlayerVehicleEvent.
//    * @exports IPlayerVehicleEvent
//    * @interface IPlayerVehicleEvent
//    * @property {number|null} [PlayerId] PlayerVehicleEvent PlayerId
//    * @property {IVehicleDto|null} [Vehicle] PlayerVehicleEvent Vehicle
//    */

//   /**
//    * Constructs a new PlayerVehicleEvent.
//    * @exports PlayerVehicleEvent
//    * @classdesc Represents a PlayerVehicleEvent.
//    * @implements IPlayerVehicleEvent
//    * @constructor
//    * @param {IPlayerVehicleEvent=} [properties] Properties to set
//    */
//   function PlayerVehicleEvent(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * PlayerVehicleEvent PlayerId.
//    * @member {number} PlayerId
//    * @memberof PlayerVehicleEvent
//    * @instance
//    */
//   PlayerVehicleEvent.prototype.PlayerId = 0;

//   /**
//    * PlayerVehicleEvent Vehicle.
//    * @member {IVehicleDto|null|undefined} Vehicle
//    * @memberof PlayerVehicleEvent
//    * @instance
//    */
//   PlayerVehicleEvent.prototype.Vehicle = null;

//   /**
//    * Creates a new PlayerVehicleEvent instance using the specified properties.
//    * @function create
//    * @memberof PlayerVehicleEvent
//    * @static
//    * @param {IPlayerVehicleEvent=} [properties] Properties to set
//    * @returns {PlayerVehicleEvent} PlayerVehicleEvent instance
//    */
//   PlayerVehicleEvent.create = function create(properties) {
//     return new PlayerVehicleEvent(properties);
//   };

//   /**
//    * Encodes the specified PlayerVehicleEvent message. Does not implicitly {@link PlayerVehicleEvent.verify|verify} messages.
//    * @function encode
//    * @memberof PlayerVehicleEvent
//    * @static
//    * @param {IPlayerVehicleEvent} message PlayerVehicleEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerVehicleEvent.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId"))
//       writer.uint32(/* id 1, wireType 0 =*/ 8).int32(message.PlayerId);
//     if (message.Vehicle != null && message.hasOwnProperty("Vehicle"))
//       $root.VehicleDto.encode(message.Vehicle, writer.uint32(/* id 2, wireType 2 =*/ 18).fork()).ldelim();
//     return writer;
//   };

//   /**
//    * Encodes the specified PlayerVehicleEvent message, length delimited. Does not implicitly {@link PlayerVehicleEvent.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof PlayerVehicleEvent
//    * @static
//    * @param {IPlayerVehicleEvent} message PlayerVehicleEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   PlayerVehicleEvent.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a PlayerVehicleEvent message from the specified reader or buffer.
//    * @function decode
//    * @memberof PlayerVehicleEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {PlayerVehicleEvent} PlayerVehicleEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerVehicleEvent.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.PlayerVehicleEvent();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.PlayerId = reader.int32();
//           break;
//         case 2:
//           message.Vehicle = $root.VehicleDto.decode(reader, reader.uint32());
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a PlayerVehicleEvent message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof PlayerVehicleEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {PlayerVehicleEvent} PlayerVehicleEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   PlayerVehicleEvent.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a PlayerVehicleEvent message.
//    * @function verify
//    * @memberof PlayerVehicleEvent
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   PlayerVehicleEvent.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId"))
//       if (!$util.isInteger(message.PlayerId)) return "PlayerId: integer expected";
//     if (message.Vehicle != null && message.hasOwnProperty("Vehicle")) {
//       var error = $root.VehicleDto.verify(message.Vehicle);
//       if (error) return "Vehicle." + error;
//     }
//     return null;
//   };

//   /**
//    * Creates a PlayerVehicleEvent message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof PlayerVehicleEvent
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {PlayerVehicleEvent} PlayerVehicleEvent
//    */
//   PlayerVehicleEvent.fromObject = function fromObject(object) {
//     if (object instanceof $root.PlayerVehicleEvent) return object;
//     var message = new $root.PlayerVehicleEvent();
//     if (object.PlayerId != null) message.PlayerId = object.PlayerId | 0;
//     if (object.Vehicle != null) {
//       if (typeof object.Vehicle !== "object") throw TypeError(".PlayerVehicleEvent.Vehicle: object expected");
//       message.Vehicle = $root.VehicleDto.fromObject(object.Vehicle);
//     }
//     return message;
//   };

//   /**
//    * Creates a plain object from a PlayerVehicleEvent message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof PlayerVehicleEvent
//    * @static
//    * @param {PlayerVehicleEvent} message PlayerVehicleEvent
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   PlayerVehicleEvent.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.PlayerId = 0;
//       object.Vehicle = null;
//     }
//     if (message.PlayerId != null && message.hasOwnProperty("PlayerId")) object.PlayerId = message.PlayerId;
//     if (message.Vehicle != null && message.hasOwnProperty("Vehicle"))
//       object.Vehicle = $root.VehicleDto.toObject(message.Vehicle, options);
//     return object;
//   };

//   /**
//    * Converts this PlayerVehicleEvent to JSON.
//    * @function toJSON
//    * @memberof PlayerVehicleEvent
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   PlayerVehicleEvent.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return PlayerVehicleEvent;
// })();

// $root.ProjectilePositionEvent = (function() {
//   /**
//    * Properties of a ProjectilePositionEvent.
//    * @exports IProjectilePositionEvent
//    * @interface IProjectilePositionEvent
//    * @property {IVector3|null} [Position] ProjectilePositionEvent Position
//    * @property {number|null} [ProjectileId] ProjectilePositionEvent ProjectileId
//    * @property {IVector3|null} [Rotation] ProjectilePositionEvent Rotation
//    * @property {string|null} [Template] ProjectilePositionEvent Template
//    */

//   /**
//    * Constructs a new ProjectilePositionEvent.
//    * @exports ProjectilePositionEvent
//    * @classdesc Represents a ProjectilePositionEvent.
//    * @implements IProjectilePositionEvent
//    * @constructor
//    * @param {IProjectilePositionEvent=} [properties] Properties to set
//    */
//   function ProjectilePositionEvent(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * ProjectilePositionEvent Position.
//    * @member {IVector3|null|undefined} Position
//    * @memberof ProjectilePositionEvent
//    * @instance
//    */
//   ProjectilePositionEvent.prototype.Position = null;

//   /**
//    * ProjectilePositionEvent ProjectileId.
//    * @member {number} ProjectileId
//    * @memberof ProjectilePositionEvent
//    * @instance
//    */
//   ProjectilePositionEvent.prototype.ProjectileId = 0;

//   /**
//    * ProjectilePositionEvent Rotation.
//    * @member {IVector3|null|undefined} Rotation
//    * @memberof ProjectilePositionEvent
//    * @instance
//    */
//   ProjectilePositionEvent.prototype.Rotation = null;

//   /**
//    * ProjectilePositionEvent Template.
//    * @member {string} Template
//    * @memberof ProjectilePositionEvent
//    * @instance
//    */
//   ProjectilePositionEvent.prototype.Template = "";

//   /**
//    * Creates a new ProjectilePositionEvent instance using the specified properties.
//    * @function create
//    * @memberof ProjectilePositionEvent
//    * @static
//    * @param {IProjectilePositionEvent=} [properties] Properties to set
//    * @returns {ProjectilePositionEvent} ProjectilePositionEvent instance
//    */
//   ProjectilePositionEvent.create = function create(properties) {
//     return new ProjectilePositionEvent(properties);
//   };

//   /**
//    * Encodes the specified ProjectilePositionEvent message. Does not implicitly {@link ProjectilePositionEvent.verify|verify} messages.
//    * @function encode
//    * @memberof ProjectilePositionEvent
//    * @static
//    * @param {IProjectilePositionEvent} message ProjectilePositionEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   ProjectilePositionEvent.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.Position != null && message.hasOwnProperty("Position"))
//       $root.Vector3.encode(message.Position, writer.uint32(/* id 1, wireType 2 =*/ 10).fork()).ldelim();
//     if (message.ProjectileId != null && message.hasOwnProperty("ProjectileId"))
//       writer.uint32(/* id 2, wireType 0 =*/ 16).int32(message.ProjectileId);
//     if (message.Rotation != null && message.hasOwnProperty("Rotation"))
//       $root.Vector3.encode(message.Rotation, writer.uint32(/* id 3, wireType 2 =*/ 26).fork()).ldelim();
//     if (message.Template != null && message.hasOwnProperty("Template"))
//       writer.uint32(/* id 4, wireType 2 =*/ 34).string(message.Template);
//     return writer;
//   };

//   /**
//    * Encodes the specified ProjectilePositionEvent message, length delimited. Does not implicitly {@link ProjectilePositionEvent.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof ProjectilePositionEvent
//    * @static
//    * @param {IProjectilePositionEvent} message ProjectilePositionEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   ProjectilePositionEvent.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a ProjectilePositionEvent message from the specified reader or buffer.
//    * @function decode
//    * @memberof ProjectilePositionEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {ProjectilePositionEvent} ProjectilePositionEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   ProjectilePositionEvent.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.ProjectilePositionEvent();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.Position = $root.Vector3.decode(reader, reader.uint32());
//           break;
//         case 2:
//           message.ProjectileId = reader.int32();
//           break;
//         case 3:
//           message.Rotation = $root.Vector3.decode(reader, reader.uint32());
//           break;
//         case 4:
//           message.Template = reader.string();
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a ProjectilePositionEvent message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof ProjectilePositionEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {ProjectilePositionEvent} ProjectilePositionEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   ProjectilePositionEvent.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a ProjectilePositionEvent message.
//    * @function verify
//    * @memberof ProjectilePositionEvent
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   ProjectilePositionEvent.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.Position != null && message.hasOwnProperty("Position")) {
//       var error = $root.Vector3.verify(message.Position);
//       if (error) return "Position." + error;
//     }
//     if (message.ProjectileId != null && message.hasOwnProperty("ProjectileId"))
//       if (!$util.isInteger(message.ProjectileId)) return "ProjectileId: integer expected";
//     if (message.Rotation != null && message.hasOwnProperty("Rotation")) {
//       var error = $root.Vector3.verify(message.Rotation);
//       if (error) return "Rotation." + error;
//     }
//     if (message.Template != null && message.hasOwnProperty("Template"))
//       if (!$util.isString(message.Template)) return "Template: string expected";
//     return null;
//   };

//   /**
//    * Creates a ProjectilePositionEvent message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof ProjectilePositionEvent
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {ProjectilePositionEvent} ProjectilePositionEvent
//    */
//   ProjectilePositionEvent.fromObject = function fromObject(object) {
//     if (object instanceof $root.ProjectilePositionEvent) return object;
//     var message = new $root.ProjectilePositionEvent();
//     if (object.Position != null) {
//       if (typeof object.Position !== "object") throw TypeError(".ProjectilePositionEvent.Position: object expected");
//       message.Position = $root.Vector3.fromObject(object.Position);
//     }
//     if (object.ProjectileId != null) message.ProjectileId = object.ProjectileId | 0;
//     if (object.Rotation != null) {
//       if (typeof object.Rotation !== "object") throw TypeError(".ProjectilePositionEvent.Rotation: object expected");
//       message.Rotation = $root.Vector3.fromObject(object.Rotation);
//     }
//     if (object.Template != null) message.Template = String(object.Template);
//     return message;
//   };

//   /**
//    * Creates a plain object from a ProjectilePositionEvent message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof ProjectilePositionEvent
//    * @static
//    * @param {ProjectilePositionEvent} message ProjectilePositionEvent
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   ProjectilePositionEvent.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.Position = null;
//       object.ProjectileId = 0;
//       object.Rotation = null;
//       object.Template = "";
//     }
//     if (message.Position != null && message.hasOwnProperty("Position"))
//       object.Position = $root.Vector3.toObject(message.Position, options);
//     if (message.ProjectileId != null && message.hasOwnProperty("ProjectileId"))
//       object.ProjectileId = message.ProjectileId;
//     if (message.Rotation != null && message.hasOwnProperty("Rotation"))
//       object.Rotation = $root.Vector3.toObject(message.Rotation, options);
//     if (message.Template != null && message.hasOwnProperty("Template")) object.Template = message.Template;
//     return object;
//   };

//   /**
//    * Converts this ProjectilePositionEvent to JSON.
//    * @function toJSON
//    * @memberof ProjectilePositionEvent
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   ProjectilePositionEvent.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return ProjectilePositionEvent;
// })();

// $root.ScoreDto = (function() {
//   /**
//    * Properties of a ScoreDto.
//    * @exports IScoreDto
//    * @interface IScoreDto
//    * @property {number|null} [Deaths] ScoreDto Deaths
//    * @property {number|null} [Kills] ScoreDto Kills
//    * @property {number|null} [Ping] ScoreDto Ping
//    * @property {number|null} [Team] ScoreDto Team
//    * @property {number|null} [Total] ScoreDto Total
//    */

//   /**
//    * Constructs a new ScoreDto.
//    * @exports ScoreDto
//    * @classdesc Represents a ScoreDto.
//    * @implements IScoreDto
//    * @constructor
//    * @param {IScoreDto=} [properties] Properties to set
//    */
//   function ScoreDto(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * ScoreDto Deaths.
//    * @member {number} Deaths
//    * @memberof ScoreDto
//    * @instance
//    */
//   ScoreDto.prototype.Deaths = 0;

//   /**
//    * ScoreDto Kills.
//    * @member {number} Kills
//    * @memberof ScoreDto
//    * @instance
//    */
//   ScoreDto.prototype.Kills = 0;

//   /**
//    * ScoreDto Ping.
//    * @member {number} Ping
//    * @memberof ScoreDto
//    * @instance
//    */
//   ScoreDto.prototype.Ping = 0;

//   /**
//    * ScoreDto Team.
//    * @member {number} Team
//    * @memberof ScoreDto
//    * @instance
//    */
//   ScoreDto.prototype.Team = 0;

//   /**
//    * ScoreDto Total.
//    * @member {number} Total
//    * @memberof ScoreDto
//    * @instance
//    */
//   ScoreDto.prototype.Total = 0;

//   /**
//    * Creates a new ScoreDto instance using the specified properties.
//    * @function create
//    * @memberof ScoreDto
//    * @static
//    * @param {IScoreDto=} [properties] Properties to set
//    * @returns {ScoreDto} ScoreDto instance
//    */
//   ScoreDto.create = function create(properties) {
//     return new ScoreDto(properties);
//   };

//   /**
//    * Encodes the specified ScoreDto message. Does not implicitly {@link ScoreDto.verify|verify} messages.
//    * @function encode
//    * @memberof ScoreDto
//    * @static
//    * @param {IScoreDto} message ScoreDto message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   ScoreDto.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.Deaths != null && message.hasOwnProperty("Deaths"))
//       writer.uint32(/* id 1, wireType 0 =*/ 8).int32(message.Deaths);
//     if (message.Kills != null && message.hasOwnProperty("Kills"))
//       writer.uint32(/* id 2, wireType 0 =*/ 16).int32(message.Kills);
//     if (message.Ping != null && message.hasOwnProperty("Ping"))
//       writer.uint32(/* id 3, wireType 0 =*/ 24).int32(message.Ping);
//     if (message.Team != null && message.hasOwnProperty("Team"))
//       writer.uint32(/* id 4, wireType 0 =*/ 32).int32(message.Team);
//     if (message.Total != null && message.hasOwnProperty("Total"))
//       writer.uint32(/* id 5, wireType 0 =*/ 40).int32(message.Total);
//     return writer;
//   };

//   /**
//    * Encodes the specified ScoreDto message, length delimited. Does not implicitly {@link ScoreDto.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof ScoreDto
//    * @static
//    * @param {IScoreDto} message ScoreDto message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   ScoreDto.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a ScoreDto message from the specified reader or buffer.
//    * @function decode
//    * @memberof ScoreDto
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {ScoreDto} ScoreDto
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   ScoreDto.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.ScoreDto();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.Deaths = reader.int32();
//           break;
//         case 2:
//           message.Kills = reader.int32();
//           break;
//         case 3:
//           message.Ping = reader.int32();
//           break;
//         case 4:
//           message.Team = reader.int32();
//           break;
//         case 5:
//           message.Total = reader.int32();
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a ScoreDto message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof ScoreDto
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {ScoreDto} ScoreDto
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   ScoreDto.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a ScoreDto message.
//    * @function verify
//    * @memberof ScoreDto
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   ScoreDto.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.Deaths != null && message.hasOwnProperty("Deaths"))
//       if (!$util.isInteger(message.Deaths)) return "Deaths: integer expected";
//     if (message.Kills != null && message.hasOwnProperty("Kills"))
//       if (!$util.isInteger(message.Kills)) return "Kills: integer expected";
//     if (message.Ping != null && message.hasOwnProperty("Ping"))
//       if (!$util.isInteger(message.Ping)) return "Ping: integer expected";
//     if (message.Team != null && message.hasOwnProperty("Team"))
//       if (!$util.isInteger(message.Team)) return "Team: integer expected";
//     if (message.Total != null && message.hasOwnProperty("Total"))
//       if (!$util.isInteger(message.Total)) return "Total: integer expected";
//     return null;
//   };

//   /**
//    * Creates a ScoreDto message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof ScoreDto
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {ScoreDto} ScoreDto
//    */
//   ScoreDto.fromObject = function fromObject(object) {
//     if (object instanceof $root.ScoreDto) return object;
//     var message = new $root.ScoreDto();
//     if (object.Deaths != null) message.Deaths = object.Deaths | 0;
//     if (object.Kills != null) message.Kills = object.Kills | 0;
//     if (object.Ping != null) message.Ping = object.Ping | 0;
//     if (object.Team != null) message.Team = object.Team | 0;
//     if (object.Total != null) message.Total = object.Total | 0;
//     return message;
//   };

//   /**
//    * Creates a plain object from a ScoreDto message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof ScoreDto
//    * @static
//    * @param {ScoreDto} message ScoreDto
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   ScoreDto.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.Deaths = 0;
//       object.Kills = 0;
//       object.Ping = 0;
//       object.Team = 0;
//       object.Total = 0;
//     }
//     if (message.Deaths != null && message.hasOwnProperty("Deaths")) object.Deaths = message.Deaths;
//     if (message.Kills != null && message.hasOwnProperty("Kills")) object.Kills = message.Kills;
//     if (message.Ping != null && message.hasOwnProperty("Ping")) object.Ping = message.Ping;
//     if (message.Team != null && message.hasOwnProperty("Team")) object.Team = message.Team;
//     if (message.Total != null && message.hasOwnProperty("Total")) object.Total = message.Total;
//     return object;
//   };

//   /**
//    * Converts this ScoreDto to JSON.
//    * @function toJSON
//    * @memberof ScoreDto
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   ScoreDto.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return ScoreDto;
// })();

// $root.ServerUpdateEvent = (function() {
//   /**
//    * Properties of a ServerUpdateEvent.
//    * @exports IServerUpdateEvent
//    * @interface IServerUpdateEvent
//    * @property {number|null} [GamePort] ServerUpdateEvent GamePort
//    * @property {string|null} [Id] ServerUpdateEvent Id
//    * @property {string|null} [IpAddress] ServerUpdateEvent IpAddress
//    * @property {string|null} [Map] ServerUpdateEvent Map
//    * @property {number|null} [MaxPlayers] ServerUpdateEvent MaxPlayers
//    * @property {string|null} [Name] ServerUpdateEvent Name
//    * @property {number|null} [Players] ServerUpdateEvent Players
//    * @property {number|null} [QueryPort] ServerUpdateEvent QueryPort
//    */

//   /**
//    * Constructs a new ServerUpdateEvent.
//    * @exports ServerUpdateEvent
//    * @classdesc Represents a ServerUpdateEvent.
//    * @implements IServerUpdateEvent
//    * @constructor
//    * @param {IServerUpdateEvent=} [properties] Properties to set
//    */
//   function ServerUpdateEvent(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * ServerUpdateEvent GamePort.
//    * @member {number} GamePort
//    * @memberof ServerUpdateEvent
//    * @instance
//    */
//   ServerUpdateEvent.prototype.GamePort = 0;

//   /**
//    * ServerUpdateEvent Id.
//    * @member {string} Id
//    * @memberof ServerUpdateEvent
//    * @instance
//    */
//   ServerUpdateEvent.prototype.Id = "";

//   /**
//    * ServerUpdateEvent IpAddress.
//    * @member {string} IpAddress
//    * @memberof ServerUpdateEvent
//    * @instance
//    */
//   ServerUpdateEvent.prototype.IpAddress = "";

//   /**
//    * ServerUpdateEvent Map.
//    * @member {string} Map
//    * @memberof ServerUpdateEvent
//    * @instance
//    */
//   ServerUpdateEvent.prototype.Map = "";

//   /**
//    * ServerUpdateEvent MaxPlayers.
//    * @member {number} MaxPlayers
//    * @memberof ServerUpdateEvent
//    * @instance
//    */
//   ServerUpdateEvent.prototype.MaxPlayers = 0;

//   /**
//    * ServerUpdateEvent Name.
//    * @member {string} Name
//    * @memberof ServerUpdateEvent
//    * @instance
//    */
//   ServerUpdateEvent.prototype.Name = "";

//   /**
//    * ServerUpdateEvent Players.
//    * @member {number} Players
//    * @memberof ServerUpdateEvent
//    * @instance
//    */
//   ServerUpdateEvent.prototype.Players = 0;

//   /**
//    * ServerUpdateEvent QueryPort.
//    * @member {number} QueryPort
//    * @memberof ServerUpdateEvent
//    * @instance
//    */
//   ServerUpdateEvent.prototype.QueryPort = 0;

//   /**
//    * Creates a new ServerUpdateEvent instance using the specified properties.
//    * @function create
//    * @memberof ServerUpdateEvent
//    * @static
//    * @param {IServerUpdateEvent=} [properties] Properties to set
//    * @returns {ServerUpdateEvent} ServerUpdateEvent instance
//    */
//   ServerUpdateEvent.create = function create(properties) {
//     return new ServerUpdateEvent(properties);
//   };

//   /**
//    * Encodes the specified ServerUpdateEvent message. Does not implicitly {@link ServerUpdateEvent.verify|verify} messages.
//    * @function encode
//    * @memberof ServerUpdateEvent
//    * @static
//    * @param {IServerUpdateEvent} message ServerUpdateEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   ServerUpdateEvent.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.GamePort != null && message.hasOwnProperty("GamePort"))
//       writer.uint32(/* id 1, wireType 0 =*/ 8).int32(message.GamePort);
//     if (message.Id != null && message.hasOwnProperty("Id"))
//       writer.uint32(/* id 2, wireType 2 =*/ 18).string(message.Id);
//     if (message.IpAddress != null && message.hasOwnProperty("IpAddress"))
//       writer.uint32(/* id 3, wireType 2 =*/ 26).string(message.IpAddress);
//     if (message.Map != null && message.hasOwnProperty("Map"))
//       writer.uint32(/* id 4, wireType 2 =*/ 34).string(message.Map);
//     if (message.MaxPlayers != null && message.hasOwnProperty("MaxPlayers"))
//       writer.uint32(/* id 5, wireType 0 =*/ 40).int32(message.MaxPlayers);
//     if (message.Name != null && message.hasOwnProperty("Name"))
//       writer.uint32(/* id 6, wireType 2 =*/ 50).string(message.Name);
//     if (message.Players != null && message.hasOwnProperty("Players"))
//       writer.uint32(/* id 7, wireType 0 =*/ 56).int32(message.Players);
//     if (message.QueryPort != null && message.hasOwnProperty("QueryPort"))
//       writer.uint32(/* id 8, wireType 0 =*/ 64).int32(message.QueryPort);
//     return writer;
//   };

//   /**
//    * Encodes the specified ServerUpdateEvent message, length delimited. Does not implicitly {@link ServerUpdateEvent.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof ServerUpdateEvent
//    * @static
//    * @param {IServerUpdateEvent} message ServerUpdateEvent message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   ServerUpdateEvent.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a ServerUpdateEvent message from the specified reader or buffer.
//    * @function decode
//    * @memberof ServerUpdateEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {ServerUpdateEvent} ServerUpdateEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   ServerUpdateEvent.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.ServerUpdateEvent();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.GamePort = reader.int32();
//           break;
//         case 2:
//           message.Id = reader.string();
//           break;
//         case 3:
//           message.IpAddress = reader.string();
//           break;
//         case 4:
//           message.Map = reader.string();
//           break;
//         case 5:
//           message.MaxPlayers = reader.int32();
//           break;
//         case 6:
//           message.Name = reader.string();
//           break;
//         case 7:
//           message.Players = reader.int32();
//           break;
//         case 8:
//           message.QueryPort = reader.int32();
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a ServerUpdateEvent message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof ServerUpdateEvent
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {ServerUpdateEvent} ServerUpdateEvent
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   ServerUpdateEvent.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a ServerUpdateEvent message.
//    * @function verify
//    * @memberof ServerUpdateEvent
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   ServerUpdateEvent.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.GamePort != null && message.hasOwnProperty("GamePort"))
//       if (!$util.isInteger(message.GamePort)) return "GamePort: integer expected";
//     if (message.Id != null && message.hasOwnProperty("Id"))
//       if (!$util.isString(message.Id)) return "Id: string expected";
//     if (message.IpAddress != null && message.hasOwnProperty("IpAddress"))
//       if (!$util.isString(message.IpAddress)) return "IpAddress: string expected";
//     if (message.Map != null && message.hasOwnProperty("Map"))
//       if (!$util.isString(message.Map)) return "Map: string expected";
//     if (message.MaxPlayers != null && message.hasOwnProperty("MaxPlayers"))
//       if (!$util.isInteger(message.MaxPlayers)) return "MaxPlayers: integer expected";
//     if (message.Name != null && message.hasOwnProperty("Name"))
//       if (!$util.isString(message.Name)) return "Name: string expected";
//     if (message.Players != null && message.hasOwnProperty("Players"))
//       if (!$util.isInteger(message.Players)) return "Players: integer expected";
//     if (message.QueryPort != null && message.hasOwnProperty("QueryPort"))
//       if (!$util.isInteger(message.QueryPort)) return "QueryPort: integer expected";
//     return null;
//   };

//   /**
//    * Creates a ServerUpdateEvent message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof ServerUpdateEvent
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {ServerUpdateEvent} ServerUpdateEvent
//    */
//   ServerUpdateEvent.fromObject = function fromObject(object) {
//     if (object instanceof $root.ServerUpdateEvent) return object;
//     var message = new $root.ServerUpdateEvent();
//     if (object.GamePort != null) message.GamePort = object.GamePort | 0;
//     if (object.Id != null) message.Id = String(object.Id);
//     if (object.IpAddress != null) message.IpAddress = String(object.IpAddress);
//     if (object.Map != null) message.Map = String(object.Map);
//     if (object.MaxPlayers != null) message.MaxPlayers = object.MaxPlayers | 0;
//     if (object.Name != null) message.Name = String(object.Name);
//     if (object.Players != null) message.Players = object.Players | 0;
//     if (object.QueryPort != null) message.QueryPort = object.QueryPort | 0;
//     return message;
//   };

//   /**
//    * Creates a plain object from a ServerUpdateEvent message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof ServerUpdateEvent
//    * @static
//    * @param {ServerUpdateEvent} message ServerUpdateEvent
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   ServerUpdateEvent.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.GamePort = 0;
//       object.Id = "";
//       object.IpAddress = "";
//       object.Map = "";
//       object.MaxPlayers = 0;
//       object.Name = "";
//       object.Players = 0;
//       object.QueryPort = 0;
//     }
//     if (message.GamePort != null && message.hasOwnProperty("GamePort")) object.GamePort = message.GamePort;
//     if (message.Id != null && message.hasOwnProperty("Id")) object.Id = message.Id;
//     if (message.IpAddress != null && message.hasOwnProperty("IpAddress")) object.IpAddress = message.IpAddress;
//     if (message.Map != null && message.hasOwnProperty("Map")) object.Map = message.Map;
//     if (message.MaxPlayers != null && message.hasOwnProperty("MaxPlayers")) object.MaxPlayers = message.MaxPlayers;
//     if (message.Name != null && message.hasOwnProperty("Name")) object.Name = message.Name;
//     if (message.Players != null && message.hasOwnProperty("Players")) object.Players = message.Players;
//     if (message.QueryPort != null && message.hasOwnProperty("QueryPort")) object.QueryPort = message.QueryPort;
//     return object;
//   };

//   /**
//    * Converts this ServerUpdateEvent to JSON.
//    * @function toJSON
//    * @memberof ServerUpdateEvent
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   ServerUpdateEvent.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return ServerUpdateEvent;
// })();

// $root.UserConnectAction = (function() {
//   /**
//    * Properties of a UserConnectAction.
//    * @exports IUserConnectAction
//    * @interface IUserConnectAction
//    * @property {string|null} [Id] UserConnectAction Id
//    */

//   /**
//    * Constructs a new UserConnectAction.
//    * @exports UserConnectAction
//    * @classdesc Represents a UserConnectAction.
//    * @implements IUserConnectAction
//    * @constructor
//    * @param {IUserConnectAction=} [properties] Properties to set
//    */
//   function UserConnectAction(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * UserConnectAction Id.
//    * @member {string} Id
//    * @memberof UserConnectAction
//    * @instance
//    */
//   UserConnectAction.prototype.Id = "";

//   /**
//    * Creates a new UserConnectAction instance using the specified properties.
//    * @function create
//    * @memberof UserConnectAction
//    * @static
//    * @param {IUserConnectAction=} [properties] Properties to set
//    * @returns {UserConnectAction} UserConnectAction instance
//    */
//   UserConnectAction.create = function create(properties) {
//     return new UserConnectAction(properties);
//   };

//   /**
//    * Encodes the specified UserConnectAction message. Does not implicitly {@link UserConnectAction.verify|verify} messages.
//    * @function encode
//    * @memberof UserConnectAction
//    * @static
//    * @param {IUserConnectAction} message UserConnectAction message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   UserConnectAction.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.Id != null && message.hasOwnProperty("Id"))
//       writer.uint32(/* id 1, wireType 2 =*/ 10).string(message.Id);
//     return writer;
//   };

//   /**
//    * Encodes the specified UserConnectAction message, length delimited. Does not implicitly {@link UserConnectAction.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof UserConnectAction
//    * @static
//    * @param {IUserConnectAction} message UserConnectAction message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   UserConnectAction.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a UserConnectAction message from the specified reader or buffer.
//    * @function decode
//    * @memberof UserConnectAction
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {UserConnectAction} UserConnectAction
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   UserConnectAction.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.UserConnectAction();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.Id = reader.string();
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a UserConnectAction message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof UserConnectAction
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {UserConnectAction} UserConnectAction
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   UserConnectAction.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a UserConnectAction message.
//    * @function verify
//    * @memberof UserConnectAction
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   UserConnectAction.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.Id != null && message.hasOwnProperty("Id"))
//       if (!$util.isString(message.Id)) return "Id: string expected";
//     return null;
//   };

//   /**
//    * Creates a UserConnectAction message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof UserConnectAction
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {UserConnectAction} UserConnectAction
//    */
//   UserConnectAction.fromObject = function fromObject(object) {
//     if (object instanceof $root.UserConnectAction) return object;
//     var message = new $root.UserConnectAction();
//     if (object.Id != null) message.Id = String(object.Id);
//     return message;
//   };

//   /**
//    * Creates a plain object from a UserConnectAction message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof UserConnectAction
//    * @static
//    * @param {UserConnectAction} message UserConnectAction
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   UserConnectAction.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) object.Id = "";
//     if (message.Id != null && message.hasOwnProperty("Id")) object.Id = message.Id;
//     return object;
//   };

//   /**
//    * Converts this UserConnectAction to JSON.
//    * @function toJSON
//    * @memberof UserConnectAction
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   UserConnectAction.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return UserConnectAction;
// })();

// $root.UserDisconnectAction = (function() {
//   /**
//    * Properties of a UserDisconnectAction.
//    * @exports IUserDisconnectAction
//    * @interface IUserDisconnectAction
//    * @property {string|null} [Id] UserDisconnectAction Id
//    */

//   /**
//    * Constructs a new UserDisconnectAction.
//    * @exports UserDisconnectAction
//    * @classdesc Represents a UserDisconnectAction.
//    * @implements IUserDisconnectAction
//    * @constructor
//    * @param {IUserDisconnectAction=} [properties] Properties to set
//    */
//   function UserDisconnectAction(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * UserDisconnectAction Id.
//    * @member {string} Id
//    * @memberof UserDisconnectAction
//    * @instance
//    */
//   UserDisconnectAction.prototype.Id = "";

//   /**
//    * Creates a new UserDisconnectAction instance using the specified properties.
//    * @function create
//    * @memberof UserDisconnectAction
//    * @static
//    * @param {IUserDisconnectAction=} [properties] Properties to set
//    * @returns {UserDisconnectAction} UserDisconnectAction instance
//    */
//   UserDisconnectAction.create = function create(properties) {
//     return new UserDisconnectAction(properties);
//   };

//   /**
//    * Encodes the specified UserDisconnectAction message. Does not implicitly {@link UserDisconnectAction.verify|verify} messages.
//    * @function encode
//    * @memberof UserDisconnectAction
//    * @static
//    * @param {IUserDisconnectAction} message UserDisconnectAction message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   UserDisconnectAction.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.Id != null && message.hasOwnProperty("Id"))
//       writer.uint32(/* id 1, wireType 2 =*/ 10).string(message.Id);
//     return writer;
//   };

//   /**
//    * Encodes the specified UserDisconnectAction message, length delimited. Does not implicitly {@link UserDisconnectAction.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof UserDisconnectAction
//    * @static
//    * @param {IUserDisconnectAction} message UserDisconnectAction message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   UserDisconnectAction.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a UserDisconnectAction message from the specified reader or buffer.
//    * @function decode
//    * @memberof UserDisconnectAction
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {UserDisconnectAction} UserDisconnectAction
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   UserDisconnectAction.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.UserDisconnectAction();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.Id = reader.string();
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a UserDisconnectAction message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof UserDisconnectAction
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {UserDisconnectAction} UserDisconnectAction
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   UserDisconnectAction.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a UserDisconnectAction message.
//    * @function verify
//    * @memberof UserDisconnectAction
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   UserDisconnectAction.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.Id != null && message.hasOwnProperty("Id"))
//       if (!$util.isString(message.Id)) return "Id: string expected";
//     return null;
//   };

//   /**
//    * Creates a UserDisconnectAction message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof UserDisconnectAction
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {UserDisconnectAction} UserDisconnectAction
//    */
//   UserDisconnectAction.fromObject = function fromObject(object) {
//     if (object instanceof $root.UserDisconnectAction) return object;
//     var message = new $root.UserDisconnectAction();
//     if (object.Id != null) message.Id = String(object.Id);
//     return message;
//   };

//   /**
//    * Creates a plain object from a UserDisconnectAction message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof UserDisconnectAction
//    * @static
//    * @param {UserDisconnectAction} message UserDisconnectAction
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   UserDisconnectAction.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) object.Id = "";
//     if (message.Id != null && message.hasOwnProperty("Id")) object.Id = message.Id;
//     return object;
//   };

//   /**
//    * Converts this UserDisconnectAction to JSON.
//    * @function toJSON
//    * @memberof UserDisconnectAction
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   UserDisconnectAction.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return UserDisconnectAction;
// })();

// $root.Vector3 = (function() {
//   /**
//    * Properties of a Vector3.
//    * @exports IVector3
//    * @interface IVector3
//    * @property {number|null} [X] Vector3 X
//    * @property {number|null} [Y] Vector3 Y
//    * @property {number|null} [Z] Vector3 Z
//    */

//   /**
//    * Constructs a new Vector3.
//    * @exports Vector3
//    * @classdesc Represents a Vector3.
//    * @implements IVector3
//    * @constructor
//    * @param {IVector3=} [properties] Properties to set
//    */
//   function Vector3(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * Vector3 X.
//    * @member {number} X
//    * @memberof Vector3
//    * @instance
//    */
//   Vector3.prototype.X = 0;

//   /**
//    * Vector3 Y.
//    * @member {number} Y
//    * @memberof Vector3
//    * @instance
//    */
//   Vector3.prototype.Y = 0;

//   /**
//    * Vector3 Z.
//    * @member {number} Z
//    * @memberof Vector3
//    * @instance
//    */
//   Vector3.prototype.Z = 0;

//   /**
//    * Creates a new Vector3 instance using the specified properties.
//    * @function create
//    * @memberof Vector3
//    * @static
//    * @param {IVector3=} [properties] Properties to set
//    * @returns {Vector3} Vector3 instance
//    */
//   Vector3.create = function create(properties) {
//     return new Vector3(properties);
//   };

//   /**
//    * Encodes the specified Vector3 message. Does not implicitly {@link Vector3.verify|verify} messages.
//    * @function encode
//    * @memberof Vector3
//    * @static
//    * @param {IVector3} message Vector3 message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   Vector3.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.X != null && message.hasOwnProperty("X")) writer.uint32(/* id 1, wireType 1 =*/ 9).double(message.X);
//     if (message.Y != null && message.hasOwnProperty("Y")) writer.uint32(/* id 2, wireType 1 =*/ 17).double(message.Y);
//     if (message.Z != null && message.hasOwnProperty("Z")) writer.uint32(/* id 3, wireType 1 =*/ 25).double(message.Z);
//     return writer;
//   };

//   /**
//    * Encodes the specified Vector3 message, length delimited. Does not implicitly {@link Vector3.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof Vector3
//    * @static
//    * @param {IVector3} message Vector3 message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   Vector3.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a Vector3 message from the specified reader or buffer.
//    * @function decode
//    * @memberof Vector3
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {Vector3} Vector3
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   Vector3.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.Vector3();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.X = reader.double();
//           break;
//         case 2:
//           message.Y = reader.double();
//           break;
//         case 3:
//           message.Z = reader.double();
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a Vector3 message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof Vector3
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {Vector3} Vector3
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   Vector3.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a Vector3 message.
//    * @function verify
//    * @memberof Vector3
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   Vector3.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.X != null && message.hasOwnProperty("X"))
//       if (typeof message.X !== "number") return "X: number expected";
//     if (message.Y != null && message.hasOwnProperty("Y"))
//       if (typeof message.Y !== "number") return "Y: number expected";
//     if (message.Z != null && message.hasOwnProperty("Z"))
//       if (typeof message.Z !== "number") return "Z: number expected";
//     return null;
//   };

//   /**
//    * Creates a Vector3 message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof Vector3
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {Vector3} Vector3
//    */
//   Vector3.fromObject = function fromObject(object) {
//     if (object instanceof $root.Vector3) return object;
//     var message = new $root.Vector3();
//     if (object.X != null) message.X = Number(object.X);
//     if (object.Y != null) message.Y = Number(object.Y);
//     if (object.Z != null) message.Z = Number(object.Z);
//     return message;
//   };

//   /**
//    * Creates a plain object from a Vector3 message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof Vector3
//    * @static
//    * @param {Vector3} message Vector3
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   Vector3.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.X = 0;
//       object.Y = 0;
//       object.Z = 0;
//     }
//     if (message.X != null && message.hasOwnProperty("X"))
//       object.X = options.json && !isFinite(message.X) ? String(message.X) : message.X;
//     if (message.Y != null && message.hasOwnProperty("Y"))
//       object.Y = options.json && !isFinite(message.Y) ? String(message.Y) : message.Y;
//     if (message.Z != null && message.hasOwnProperty("Z"))
//       object.Z = options.json && !isFinite(message.Z) ? String(message.Z) : message.Z;
//     return object;
//   };

//   /**
//    * Converts this Vector3 to JSON.
//    * @function toJSON
//    * @memberof Vector3
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   Vector3.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return Vector3;
// })();

// $root.VehicleDto = (function() {
//   /**
//    * Properties of a VehicleDto.
//    * @exports IVehicleDto
//    * @interface IVehicleDto
//    * @property {number|null} [RootVehicleId] VehicleDto RootVehicleId
//    * @property {string|null} [RootVehicleTemplate] VehicleDto RootVehicleTemplate
//    */

//   /**
//    * Constructs a new VehicleDto.
//    * @exports VehicleDto
//    * @classdesc Represents a VehicleDto.
//    * @implements IVehicleDto
//    * @constructor
//    * @param {IVehicleDto=} [properties] Properties to set
//    */
//   function VehicleDto(properties) {
//     if (properties)
//       for (var keys = Object.keys(properties), i = 0; i < keys.length; ++i)
//         if (properties[keys[i]] != null) this[keys[i]] = properties[keys[i]];
//   }

//   /**
//    * VehicleDto RootVehicleId.
//    * @member {number} RootVehicleId
//    * @memberof VehicleDto
//    * @instance
//    */
//   VehicleDto.prototype.RootVehicleId = 0;

//   /**
//    * VehicleDto RootVehicleTemplate.
//    * @member {string} RootVehicleTemplate
//    * @memberof VehicleDto
//    * @instance
//    */
//   VehicleDto.prototype.RootVehicleTemplate = "";

//   /**
//    * Creates a new VehicleDto instance using the specified properties.
//    * @function create
//    * @memberof VehicleDto
//    * @static
//    * @param {IVehicleDto=} [properties] Properties to set
//    * @returns {VehicleDto} VehicleDto instance
//    */
//   VehicleDto.create = function create(properties) {
//     return new VehicleDto(properties);
//   };

//   /**
//    * Encodes the specified VehicleDto message. Does not implicitly {@link VehicleDto.verify|verify} messages.
//    * @function encode
//    * @memberof VehicleDto
//    * @static
//    * @param {IVehicleDto} message VehicleDto message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   VehicleDto.encode = function encode(message, writer) {
//     if (!writer) writer = $Writer.create();
//     if (message.RootVehicleId != null && message.hasOwnProperty("RootVehicleId"))
//       writer.uint32(/* id 1, wireType 0 =*/ 8).int32(message.RootVehicleId);
//     if (message.RootVehicleTemplate != null && message.hasOwnProperty("RootVehicleTemplate"))
//       writer.uint32(/* id 2, wireType 2 =*/ 18).string(message.RootVehicleTemplate);
//     return writer;
//   };

//   /**
//    * Encodes the specified VehicleDto message, length delimited. Does not implicitly {@link VehicleDto.verify|verify} messages.
//    * @function encodeDelimited
//    * @memberof VehicleDto
//    * @static
//    * @param {IVehicleDto} message VehicleDto message or plain object to encode
//    * @param {$protobuf.Writer} [writer] Writer to encode to
//    * @returns {$protobuf.Writer} Writer
//    */
//   VehicleDto.encodeDelimited = function encodeDelimited(message, writer) {
//     return this.encode(message, writer).ldelim();
//   };

//   /**
//    * Decodes a VehicleDto message from the specified reader or buffer.
//    * @function decode
//    * @memberof VehicleDto
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @param {number} [length] Message length if known beforehand
//    * @returns {VehicleDto} VehicleDto
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   VehicleDto.decode = function decode(reader, length) {
//     if (!(reader instanceof $Reader)) reader = $Reader.create(reader);
//     var end = length === undefined ? reader.len : reader.pos + length,
//       message = new $root.VehicleDto();
//     while (reader.pos < end) {
//       var tag = reader.uint32();
//       switch (tag >>> 3) {
//         case 1:
//           message.RootVehicleId = reader.int32();
//           break;
//         case 2:
//           message.RootVehicleTemplate = reader.string();
//           break;
//         default:
//           reader.skipType(tag & 7);
//           break;
//       }
//     }
//     return message;
//   };

//   /**
//    * Decodes a VehicleDto message from the specified reader or buffer, length delimited.
//    * @function decodeDelimited
//    * @memberof VehicleDto
//    * @static
//    * @param {$protobuf.Reader|Uint8Array} reader Reader or buffer to decode from
//    * @returns {VehicleDto} VehicleDto
//    * @throws {Error} If the payload is not a reader or valid buffer
//    * @throws {$protobuf.util.ProtocolError} If required fields are missing
//    */
//   VehicleDto.decodeDelimited = function decodeDelimited(reader) {
//     if (!(reader instanceof $Reader)) reader = new $Reader(reader);
//     return this.decode(reader, reader.uint32());
//   };

//   /**
//    * Verifies a VehicleDto message.
//    * @function verify
//    * @memberof VehicleDto
//    * @static
//    * @param {Object.<string,*>} message Plain object to verify
//    * @returns {string|null} `null` if valid, otherwise the reason why it is not
//    */
//   VehicleDto.verify = function verify(message) {
//     if (typeof message !== "object" || message === null) return "object expected";
//     if (message.RootVehicleId != null && message.hasOwnProperty("RootVehicleId"))
//       if (!$util.isInteger(message.RootVehicleId)) return "RootVehicleId: integer expected";
//     if (message.RootVehicleTemplate != null && message.hasOwnProperty("RootVehicleTemplate"))
//       if (!$util.isString(message.RootVehicleTemplate)) return "RootVehicleTemplate: string expected";
//     return null;
//   };

//   /**
//    * Creates a VehicleDto message from a plain object. Also converts values to their respective internal types.
//    * @function fromObject
//    * @memberof VehicleDto
//    * @static
//    * @param {Object.<string,*>} object Plain object
//    * @returns {VehicleDto} VehicleDto
//    */
//   VehicleDto.fromObject = function fromObject(object) {
//     if (object instanceof $root.VehicleDto) return object;
//     var message = new $root.VehicleDto();
//     if (object.RootVehicleId != null) message.RootVehicleId = object.RootVehicleId | 0;
//     if (object.RootVehicleTemplate != null) message.RootVehicleTemplate = String(object.RootVehicleTemplate);
//     return message;
//   };

//   /**
//    * Creates a plain object from a VehicleDto message. Also converts values to other types if specified.
//    * @function toObject
//    * @memberof VehicleDto
//    * @static
//    * @param {VehicleDto} message VehicleDto
//    * @param {$protobuf.IConversionOptions} [options] Conversion options
//    * @returns {Object.<string,*>} Plain object
//    */
//   VehicleDto.toObject = function toObject(message, options) {
//     if (!options) options = {};
//     var object = {};
//     if (options.defaults) {
//       object.RootVehicleId = 0;
//       object.RootVehicleTemplate = "";
//     }
//     if (message.RootVehicleId != null && message.hasOwnProperty("RootVehicleId"))
//       object.RootVehicleId = message.RootVehicleId;
//     if (message.RootVehicleTemplate != null && message.hasOwnProperty("RootVehicleTemplate"))
//       object.RootVehicleTemplate = message.RootVehicleTemplate;
//     return object;
//   };

//   /**
//    * Converts this VehicleDto to JSON.
//    * @function toJSON
//    * @memberof VehicleDto
//    * @instance
//    * @returns {Object.<string,*>} JSON object
//    */
//   VehicleDto.prototype.toJSON = function toJSON() {
//     return this.constructor.toObject(this, $protobuf.util.toJSONOptions);
//   };

//   return VehicleDto;
// })();

// module.exports = $root;
