import { useEffect, useRef, useState, useCallback, useMemo } from "react";

interface Subscriber {
  setLastMessage: any;
  setReadyState: any;
  options: Options;
}

export interface Options {
  share: boolean;
  onOpen: (e: Event) => any;
  onClose: (e: CloseEvent) => any;
  onMessage: (e: MessageEvent) => any;
  onError: (e: Event) => any;
}

const sharedWebSockets: { [url: string]: WebSocket | undefined } = {};
const subscribers: { [url: string]: Subscriber[] | undefined } = {};
const defaultOptions: Options = {
  share: true,
  onOpen: () => {},
  onClose: () => {},
  onMessage: () => {},
  onError: () => {}
};

const READY_STATE_CONNECTING = 0;
const READY_STATE_OPEN = 1;
const READY_STATE_CLOSING = 2;
const READY_STATE_CLOSED = 3;

const attachListeners = (webSocketInstance: WebSocket, url: string, setters: any, options: Options) => {
  const { setLastMessage, setReadyState } = setters;

  if (options.share) {
    const removeSubscriber = addSubscriber(
      webSocketInstance,
      url,
      {
        setLastMessage,
        setReadyState
      },
      options
    );

    return removeSubscriber;
  }

  webSocketInstance.onmessage = message => {
    options.onMessage && options.onMessage(message);
    setLastMessage(message);
  };
  webSocketInstance.onopen = event => {
    options.onOpen && options.onOpen(event);
    setReadyState((prev: any) => Object.assign({}, prev, { [url]: READY_STATE_OPEN }));
  };
  webSocketInstance.onclose = event => {
    options.onClose && options.onClose(event);
    setReadyState((prev: any) => Object.assign({}, prev, { [url]: READY_STATE_CLOSED }));
  };
  webSocketInstance.onerror = error => {
    options.onError && options.onError(error);
  };

  return () => {
    setReadyState((prev: any) => Object.assign({}, prev, { [url]: READY_STATE_CLOSING }));
    webSocketInstance.close();
  };
};

const createOrJoinSocket = (webSocketRef: React.MutableRefObject<WebSocket | null>, url: string, options: Options) => {
  if (options.share) {
    if (sharedWebSockets[url] === undefined) {
      sharedWebSockets[url] = new WebSocket(url);
    }
    webSocketRef.current = sharedWebSockets[url]!;
  } else {
    webSocketRef.current = new WebSocket(url);
  }
};

const addSubscriber = (webSocketInstance: WebSocket, url: string, setters: any, options = defaultOptions) => {
  const { setLastMessage, setReadyState } = setters;

  if (subscribers[url] === undefined) {
    subscribers[url] = [];

    webSocketInstance.onmessage = message => {
      subscribers[url]!.forEach(subscriber => {
        subscriber.setLastMessage(message);

        if (subscriber.options.onMessage) {
          subscriber.options.onMessage(message);
        }
      });
    };

    webSocketInstance.onclose = event => {
      subscribers[url]!.forEach(subscriber => {
        subscriber.setReadyState((prev: any) => Object.assign({}, prev, { [url]: READY_STATE_CLOSED }));
        if (subscriber.options.onClose) {
          subscriber.options.onClose(event);
        }
      });

      subscribers[url] = undefined;
      sharedWebSockets[url] = undefined;
    };

    webSocketInstance.onerror = error => {
      subscribers[url]!.forEach(subscriber => {
        if (subscriber.options.onError) {
          subscriber.options.onError(error);
        }
      });
    };

    webSocketInstance.onopen = event => {
      subscribers[url]!.forEach(subscriber => {
        subscriber.setReadyState((prev: any) => Object.assign({}, prev, { [url]: READY_STATE_OPEN }));
        if (subscriber.options.onOpen) {
          subscriber.options.onOpen(event);
        }
      });
    };
  } else {
    setReadyState((prev: any) => Object.assign({}, prev, { [url]: sharedWebSockets[url]!.readyState }));
  }

  const subscriber: Subscriber = {
    setLastMessage,
    setReadyState,
    options
  };
  subscribers[url]!.push(subscriber);

  return () => {
    if (subscribers[url] !== undefined) {
      const index = subscribers[url]!.indexOf(subscriber);
      if (index === -1) {
        throw new Error("A subscriber that is no longer registered has attempted to unsubscribe");
      }
      if (subscribers[url]!.length === 1) {
        subscribers[url]![0].setReadyState((prev: any) => Object.assign({}, prev, { [url]: READY_STATE_CLOSING }));
        webSocketInstance.close();
      } else {
        subscribers[url]!.splice(index, 1);
      }
    }
  };
};

export const useWebSocket = (url: string, options = defaultOptions) => {
  const webSocketRef = useRef<WebSocket | null>(null);
  const [lastMessage, setLastMessage] = useState(null);
  const [readyState, setReadyState] = useState(url ? { [url]: READY_STATE_CONNECTING } : null);
  const staticOptionsCheck = useRef<boolean | null>(null);

  const sendMessage = useCallback(message => {
    webSocketRef.current && webSocketRef.current.send(message);
  }, []);

  useEffect(() => {
    createOrJoinSocket(webSocketRef, url, options);

    // const removeListeners = attachListeners(
    //   webSocketRef.current!,
    //   url,
    //   {
    //     setLastMessage,
    //     setReadyState
    //   },
    //   options
    // );

    // return removeListeners;

    webSocketRef.current!.onmessage = message => {
      options.onMessage && options.onMessage(message);
    };

    return () => webSocketRef.current!.close();
  }, [url]);

  useEffect(() => {
    if (staticOptionsCheck.current) throw new Error("The options object you pass must be static");

    staticOptionsCheck.current = true;
  }, [options]);

  const readyStateFromUrl = useMemo(() => {
    return readyState && readyState[url] !== undefined ? readyState[url] : null;
  }, [readyState, url]);

  return [sendMessage, lastMessage, readyStateFromUrl];
};

export default useWebSocket;
