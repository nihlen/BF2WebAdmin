package net.nihlen.bf2.util;

import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import java.io.*;
import java.net.*;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;

/**
 * A helper class for sending RCon messages to a Battlefield 2 server.
 * A socket connection is created and closed with each send request.
 * Use sparingly.
 * 
 * Created by DeadEd (?)
 * 
 * @author Alex
 */
public class BF2Rcon {

	private static final Logger log = LogManager.getLogger();

	private static final int RESPONSE_TIMEOUT = 2000;
	private static Socket rconSocket = null;
	private static InputStream in = null;
	private static OutputStream out = null;

	public static String send(String ipStr, int port, String password, String command) throws SocketTimeoutException, Exception {
		//return send(ipStr, port, password, command, 0);
		return send(ipStr, port, password, command, null, 0);
	}

	/*public static String send(String ipStr, int port, String password, String command, int localPort) throws SocketTimeoutException, Exception {
		return send(ipStr, port, password, command, null, localPort);
	}*/

	public static String send(String ipStr, int port, String password, String command, InetAddress localhost, int localPort) throws SocketTimeoutException, Exception {
		
		StringBuffer response = new StringBuffer();
		
		try {
			rconSocket = new Socket();

			rconSocket.bind(new InetSocketAddress(localhost, localPort));
			rconSocket.connect(new InetSocketAddress(ipStr, port), 2000);

			out = rconSocket.getOutputStream();
			in = rconSocket.getInputStream();
			BufferedReader buffRead = new BufferedReader(new InputStreamReader(in));

			rconSocket.setSoTimeout(2000);

			String digestSeed = "";
			boolean loggedIn = false;
			boolean keepGoing = true;
			
			while (keepGoing) {
				
				String receivedContent = buffRead.readLine();
				if (receivedContent.startsWith("### Digest seed: ")) {
					
					digestSeed = receivedContent.substring(17, receivedContent.length());
					
					try {
						MessageDigest md5 = MessageDigest.getInstance("MD5");
						md5.update(digestSeed.getBytes());
						md5.update(password.getBytes());
						String digestStr = "login " + digestedToHex(md5.digest()) + "\n";
						out.write(digestStr.getBytes());
						
					} catch (NoSuchAlgorithmException e1) {
						response.append("MD5 algorithm not available - unable to complete RCON request.");
						keepGoing = false;
					}
					
				} else {
					
					if (receivedContent.startsWith("error: not authenticated: you can only invoke 'login'")) {
						throw new Exception("Bad rcon.");
					}
					
					if (receivedContent.startsWith("Authentication failed.")) {
						throw new Exception("Bad rcon.");
					}
					
					if (receivedContent.startsWith("Authentication successful, rcon ready.")) {
						keepGoing = false;
						loggedIn = true;
					}
				}
			}
			
			if (loggedIn) {
				
				//String cmd = "\002exec " + command + "\n";
				String cmd = "\002" + command + "\n";
				out.write(cmd.getBytes());

				readResponse(buffRead, response);
				if (response.length() == 0) {
					throw new Exception("Response empty.");
				}
			}
			
		} catch (SocketTimeoutException timeout) {
			throw timeout;
			
		} catch (UnknownHostException e) {
			response.append("UnknownHostException: " + e.getMessage());
			
		} catch (IOException e) {
			response.append("Couldn't get I/O for the connection: " + e.getMessage());
			//e.printStackTrace();
			
		} finally {
			try {
				if (out != null) {
					out.close();
				}
				if (in != null) {
					in.close();
				}
				if (rconSocket != null) {
					rconSocket.close();
				}
			} catch (IOException e) {
				log.error(e);
			}
		}
		return response.toString();
	}

	private static void readResponse(BufferedReader buffRead, StringBuffer sb) throws IOException {
		for (;;) {
			int ch = buffRead.read();
			if ((ch == -1) || (ch == 4)) {
				return;
			}
			sb.append((char) ch);
		}
	}

	private static String digestedToHex(byte[] digest) {
		
		StringBuffer store = new StringBuffer();

		for (byte bite : digest) {

			String val = Integer.toHexString(bite & 0xFF);
			if (val.length() == 1) {
				store.append("0");
			}
			store.append(val);
		}
		return store.toString();
	}
}
