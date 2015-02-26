package net.nihlen.bf2.util;

import java.text.SimpleDateFormat;
import java.util.Date;

public class Log {

	public static void write(String str) {
		String timestamp = new SimpleDateFormat("yyyy-MM-dd HH:mm:ss").format(new Date());
		String message = String.format("[%s] %s", timestamp, str);
		System.out.println(message);
	}
	
	
	public static void error(String str) {
		String timestamp = new SimpleDateFormat("yyyy-MM-dd HH:mm:ss").format(new Date());
		String message = String.format("[%s] Error: %s", timestamp, str);
		System.out.println(message);
	}
	
	/*public static void info(String str) {
		String timestamp = new SimpleDateFormat("yyyy-MM-dd HH:mm:ss").format(new Date());
		String message = String.format("[%s] Info: %s", timestamp, str);
		System.out.println(message);
	}*/
}
