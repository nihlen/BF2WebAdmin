package net.nihlen.bf2.objects;

/**
 * A Battlefield 2 map.
 *
 * @author Alex
 */
public class Map {

	public final String name;
	public int index;
	public int size;

	public Map(String name, int index, int size) {
		this.name = name;
		this.index = index;
		this.size = size;
	}

	@Override
	public String toString() {
		return name;
	}

	@Override
	public boolean equals(Object obj) {
		if (obj instanceof Map) {
			Map map2 = (Map)obj;
			if (this.name.equals(map2.name) && (this.size == map2.size)) {
				return true;
			}
		}
		return false;
	}

}
