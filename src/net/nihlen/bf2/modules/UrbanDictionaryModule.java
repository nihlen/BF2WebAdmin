package net.nihlen.bf2.modules;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.UnsupportedEncodingException;
import java.net.URL;
import java.net.URLConnection;
import java.net.URLEncoder;
import java.util.ArrayList;

import com.owlike.genson.Genson;

import net.nihlen.bf2.BF2Module;
import net.nihlen.bf2.BF2SocketServer;
import net.nihlen.bf2.ModManager;
import net.nihlen.bf2.listeners.ChatListener;
import net.nihlen.bf2.objects.ChatEntry;
import net.nihlen.bf2.objects.GameServer;

public class UrbanDictionaryModule implements BF2Module, ChatListener {

	private final GameServer server;
	
	public UrbanDictionaryModule(GameServer server) {
		this.server = server;
	}

	public void load(ModManager mm) {
		mm.addChatListener(this);
	}

	public void onChatMessage(ChatEntry entry) {
		String msg = entry.text.trim();
		if (msg.startsWith("define: ")) {
			String word = msg.replaceFirst("define: ", "");
			word = word.substring(0, Math.min(word.length(), 50));
			try {
				String content = UrbanDictionaryModule.getWebPageContents("http://api.urbandictionary.com/v0/define?term=" + URLEncoder.encode(word, "UTF-8"));
				UDDefinition def = getFirstDefinition(content);
				String cmd = "rcon game.sayall \"§C1001" + def.word + "§C1001" + abbreviate(def.definition, 120) + "\"";
				BF2SocketServer.getInstance().send(server.getIpAddress(), cmd);
			} catch (UnsupportedEncodingException e) {
				e.printStackTrace();
			}
		}
	}
	
    public static String abbreviate(String str, int maxWidth) {
        if (null == str)
            return null;
        if (str.length() <= maxWidth)
            return str;
        return str.substring(0, maxWidth) + "...";
    }

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
			// TODO Auto-generated catch block
			e.printStackTrace();
		}
		return "";
	}
	
	public static UDDefinition getFirstDefinition(String content) {
		try {
			
			Genson genson = new Genson();
			UDResult result = genson.deserialize(content, UDResult.class);
			if (result.list != null && result.list.size() > 0) {
				UDDefinition definition = result.list.get(0);
				return definition;
			}
		} catch (Exception e) {
			e.printStackTrace();
		}
		return new UDDefinition("No definition found.");
	}
	
	/*public static void main (String[] args) {
		//String json = @"{"tags":["sorority","fraternity","alpha","kappa kappa gamma","phi"],"result_type":"exact","list":[{"defid":7927027,"word":"Kappa","author":"R4D1AT10N","permalink":"http://kappa.urbanup.com/7927027","definition":"The main symbol/emote of Twitch.tv. It represents sarcasm, irony, puns, jokes, and trolls alike. If you see this term used outside of Twitch.tv, then this is not the correct definition. Usually used at the end of an ironic or sarcastic sentence. Sentences that contain a Kappa should not be taken seriously. If you search \"Kappa Twitch.tv\" in Google you can see what the emote looks like, and why it is used as it is. Sentences that use Kappa do not always have to make sense.","example":"You hold the speedrun world record of this game, but I bet you'll die on level 1, Kappa.","thumbs_up":967,"thumbs_down":91,"current_vote":""},{"defid":1228068,"word":"kappa","author":"Eddybean","permalink":"http://kappa.urbanup.com/1228068","definition":"a kappa is a japanese water monster.\r\n\r\nthe kappa has a beak, webbed feet and a shell on its back and dwells under bridges, pouncing on any who attempt to cross the river. The kappa also has a bowl-like head, in which it keeps a small amount of water, and this is a key to apparently defeating this monster. when you are confronted by a kappa, your only hope is to make it bow to you, thus making the water fall out of its head and draining it of its power. strange, no?","example":"here we are in japan. oh look, there's a river we must cross. good lord! a kappa! quick! make it bow to us!","thumbs_up":560,"thumbs_down":377,"current_vote":""},{"defid":3249309,"word":"Kappa","author":"I Am Not a Hippie","permalink":"http://kappa.urbanup.com/3249309","definition":"A Japanese water spirit. It looks like a human/duck/turtle type of thing. They live in ponds and rivers and drag people in and drown them. They also pull your intestines out through your behind. A Kappa has a dent in it's head that is full of water, this allows it to go out on land. Also, Kappas are very polite. If you come a across a Kappa all you have to do is bow to it, the Kappa will have to bow back spilling the water on it's head and leaving it powerless. Kappas also love cucumbers, toss a cucumber into the water were a Kappa lives and it will not harm you. \r\n     People also portray Kappas as being very cute and innocent looking. These versions of Kappas are indeed irresistibly adorable, they make me want to squeeze their little Kappa heads off! \r\n     And yes, I am aware there is also the Kappa Kappa Gamma sorority who think they're so elite because they've been around for so long. Well the REAL Kappas have been around since ancient times, drowning you and feasting on your soul! \r\n\r\n\r\n","example":"Me: Omg! Lookit the ickle Kappa!!! Ah, wait! Omg no, not the colon!!!! *Tosses cucumber*\r\n\r\nSorority chick on Urban Dictionary: Omg! This definition says a Kappa is a Japanese duck thinger! Omg they're dishonoring our almighty elite-ness! *Gives thumbs down to definition*","thumbs_up":235,"thumbs_down":188,"current_vote":""},{"defid":8103878,"word":"Kappa","author":"SynysterMind","permalink":"http://kappa.urbanup.com/8103878","definition":"Tenth letter of the [Greek] alphabet.","example":"Kappa is the tenth letter of the [Greek] alphabet.","thumbs_up":8,"thumbs_down":2,"current_vote":""},{"defid":3274515,"word":"kappa","author":"darryl1204032","permalink":"http://kappa.urbanup.com/3274515","definition":"A japanese water monster that sucks your liver out of your anus. His second favourite food is children, behind only to cucumbers.","example":"Ah crap better check on the children on the lake, a kappa might be there.","thumbs_up":129,"thumbs_down":142,"current_vote":""},{"defid":1814627,"word":"kappa","author":"josh","permalink":"http://kappa.urbanup.com/1814627","definition":"a beach party ( KAPPA BEACH PARTY )in galveston, texas that used to be real crunk but the city started to crack down on all the partying and made it not AS crunk. \r\n\r\n- it is the subject of alot of swisha house albums IE: \"before da kappa \" mixtapes","example":"\"mannn , there were so much sticky green down at the kappa, that the whole island got smoked out\"\r\n\r\n\"i got drank by the pint , dro by the pound , headed to da kappa in my boss top down \" - slim thug , before da kappa 2k1 mixtape\r\n\r\n","thumbs_up":301,"thumbs_down":356,"current_vote":""},{"defid":2181924,"word":"Kappa","author":"Richalo","permalink":"http://kappa.urbanup.com/2181924","definition":"A cute little water dwelling monster-myth in Japan who loves cucumbers and bludgeoning people to death.\r\nIt is recognised by Cryptozoologists.","example":"A:aw, look at that cute little creature eating a cucumber.\r\nB: hey thats a-\r\n*kappa gets up and kills A and B*\r\n\r\nC: Aw, that was cute.","thumbs_up":84,"thumbs_down":139,"current_vote":""},{"defid":7833711,"word":"Kappa","author":"post_jp123","permalink":"http://kappa.urbanup.com/7833711","definition":"The 10th letter of the Greek Alphabet, but only fucking nerds use it for that. It really is a word that is synonymous with Just Kidding (jk)\n\nIt's most likely origin is from twitch.tv, where it is most commonly used.\n\nAnother origin that I know about was on a Minecraft Server Network called Project Ares (Now Overcast Network) back in mid-late 2012. The different servers in the network were based off of the Greek Alphabet, and kappa.oc.tc was the last server added to the network before it's revamp. Somehow, it caught on, and people started spamming it on the forums, and it was given a meaning.","example":"I'm going to jump off a bridge!\n\nKappa\n\nI just fucked my own dog!\n\nKappa\n\nI'm going to chop a watermelon in half and stick it up a woman's bootyhole!","thumbs_up":30,"thumbs_down":86,"current_vote":""},{"defid":3729663,"word":"Kappa","author":"Beatin_Gutz","permalink":"http://kappa.urbanup.com/3729663","definition":"v. The act of putting something on your father's credit card or having your daddy pay for something.\r\n\r\n*Reference to the women of Kappa Kappa Gamma\r\n","example":"Dude don't worry about buying that polo,  I'll just kappa it.","thumbs_up":105,"thumbs_down":161,"current_vote":""},{"defid":3478368,"word":"kappa","author":"inf0saurus","permalink":"http://kappa.urbanup.com/3478368","definition":"A group of \"bitch ass ho's\" with an exaggerated sense of self importance and entitlement.","example":"Friend: \"She was hot, why'd you dump her?\"\r\nMe: \"She's a kappa.\"\r\nFriend: \"Oh.\"","thumbs_up":126,"thumbs_down":185,"current_vote":""}],"sounds":["http://wav.urbandictionary.com/kappa-23659.wav","http://wav.urbandictionary.com/kappa-25653.wav","http://wav.urbandictionary.com/kappa-26553.wav"]}";
		String content1 = UrbanDictionaryModule.getWebPageContents("http://api.urbandictionary.com/v0/define?term=russia");
		System.out.println(content1);
		UDDefinition def1 = getFirstDefinition(content1);
		String content2 = UrbanDictionaryModule.getWebPageContents("http://api.urbandictionary.com/v0/define?term=eeeeeeeeeee");
		System.out.println(content2);
		UDDefinition def2 = getFirstDefinition(content2);
		System.out.println(abbreviate(def1.definition.replace("\r\n", ""), 120));
		System.out.println(abbreviate(def2.definition.replace("\n", ""), 120));
	}*/
	
	static class UDResult {
		public String result_type;
		public ArrayList<UDDefinition> list;
		
		public UDResult() {}
	}
	
	static class UDDefinition {
		public int defid;
		public String word;
		public String author;
		public String definition;
		public String example;
		public int thumbsUp;
		public int thumbsDown;
		
		public UDDefinition() {}
		public UDDefinition(String def) { this.definition = def; }
	}

}
