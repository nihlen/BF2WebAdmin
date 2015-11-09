package net.nihlen.bf2;

import com.mysql.jdbc.jdbc2.optional.MysqlDataSource;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import java.io.FileInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.util.ArrayList;
import java.util.Properties;
/**
 * MySQL database connection.
 *
 * @author Alex
 */
public class Database {

	private static final Logger log = LogManager.getLogger();
	private MysqlDataSource dataSource;

	// Singleton
	private static Database instance = null;

	protected Database() {
		connect();
	}

	public static Database getInstance() {
		if (instance == null) {
			instance = new Database();
		}
		return instance;
	}

	private void connect() {
		dataSource = getMySQLDataSource();
	}

	private MysqlDataSource getMySQLDataSource() {

		FileInputStream fis = null;
		MysqlDataSource mysqlDS = null;

		try {
			Properties props = new Properties();
			InputStream is = this.getClass().getResourceAsStream("/db.properties");
			props.load(is);

			mysqlDS = new MysqlDataSource();
			mysqlDS.setURL(props.getProperty("MYSQL_DB_URL"));
			mysqlDS.setUser(props.getProperty("MYSQL_DB_USERNAME"));
			mysqlDS.setPassword(props.getProperty("MYSQL_DB_PASSWORD"));

		} catch (IOException e) {
			log.error(e);

		} catch (Exception e) {
			log.error("Database: Missing file db.properties.", e);
		}

		return mysqlDS;
	}

	public String[] getTopPlayerMatch(String ip) {

		ArrayList<String> names = new ArrayList<>();
		Connection conn = null;
		PreparedStatement pstmt = null;

		try {
			if (dataSource != null) {

				conn = dataSource.getConnection();
				pstmt = conn.prepareStatement("SELECT name, ip, COUNT(ip) AS count " +
						"FROM bf2_playerjoin " +
						"WHERE ip = ? " +
						"GROUP BY name, ip " +
						"ORDER BY count DESC " +
						"LIMIT 5");

				pstmt.setString(1, ip);
				ResultSet result = pstmt.executeQuery();
				while (result.next()) {
					names.add(result.getString("name"));
				}
			}

		} catch (SQLException e) {
			log.error(e);

		} finally {
			try {
				if (conn != null) conn.close();
				if (pstmt != null) pstmt.close();
			} catch (SQLException e) {
				log.error(e);
			}
		}

		String[] namesArr = new String[names.size()];
		return names.toArray(namesArr);
	}

	public boolean authorize(String key) {

		Connection conn = null;
		PreparedStatement pstmt = null;
		String username = null;

		try {
			if (dataSource != null) {

				conn = dataSource.getConnection();
				pstmt = conn.prepareStatement("SELECT username FROM wa_user WHERE socket_key = ? LIMIT 1");
				pstmt.setString(1, key);
				ResultSet result = pstmt.executeQuery();
				if (result.next()) {
					username = result.getString("username");
					log.info("WebSocket user " + username + " authorized");
				}
			}

		} catch (SQLException e) {
			log.error(e);

		} finally {
			try {
				if (conn != null) conn.close();
				if (pstmt != null) pstmt.close();
			} catch (SQLException e) {
				log.error(e);
			}
		}

		return (username != null);
	}

}
