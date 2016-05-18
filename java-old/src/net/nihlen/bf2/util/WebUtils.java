package net.nihlen.bf2.util;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.net.URL;
import java.net.URLConnection;

/**
 * A class containing helper methods related to web data.
 * 
 * @author Alex
 */
public class WebUtils {

	public static String getWebPageContents(String urlString) {
		
		try {
			
			URL url = new URL(urlString);
			URLConnection con;
			con = url.openConnection();
			InputStream in = con.getInputStream();
			String encoding = con.getContentEncoding();
			encoding = encoding == null ? "UTF-8" : encoding;
			ByteArrayOutputStream baos = new ByteArrayOutputStream();
			byte[] buf = new byte[8192];
			int len = 0;
			while ((len = in.read(buf)) != -1) {
				baos.write(buf, 0, len);
			}
			return new String(baos.toByteArray(), encoding);
			
		} catch (IOException e) {
			e.printStackTrace();
		}
		return "";
	}

}
