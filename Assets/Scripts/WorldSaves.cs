using System;
using System.IO;
using UnityEngine;

// Reading and writing the three save slots. Each world is one small file:
// a header with when it was saved and where the player stood, then every
// block in the world. Animals aren't saved - fresh ones arrive when you load.
//
// Each slot has two places: home (place 0) and its Magic Land (place 1),
// reached through a magic portal. Each place has its own file.
public static class WorldSaves
{
    public const int Slots = 3;

    public const int Home = 0;
    public const int MagicLand = 1;
    public const int Nether = 2;     // the land of fire and dragons, through a fire portal

    // A short name for each place, used in file names and remembered settings
    public static string PlaceName(int place)
    {
        return place == MagicLand ? "magic" : place == Nether ? "nether" : "home";
    }

    // Bump this if the file layout ever changes, so old saves are spotted
    // instead of being read wrongly
    const int FormatVersion = 1;

    static string PathFor(int slot, int place)
    {
        string name = "world" + (slot + 1) + (place == Home ? "" : "-" + PlaceName(place));
        return Path.Combine(Application.persistentDataPath, name + ".save");
    }

    // Has this world been started? (Its home is always made first.)
    public static bool Exists(int slot, int place = Home)
    {
        return File.Exists(PathFor(slot, place));
    }

    // Which place you were in when you last left this world
    public static int LastPlace(int slot)
    {
        int place = PlayerPrefs.GetInt("world" + (slot + 1) + "-place", Home);
        return place != Home && Exists(slot, place) ? place : Home;
    }

    public static void SetLastPlace(int slot, int place)
    {
        PlayerPrefs.SetInt("world" + (slot + 1) + "-place", place);
        PlayerPrefs.Save();
    }

    // When the world was last saved - in whichever of its places was saved
    // last - or null if it can't be read
    public static DateTime? WhenSaved(int slot)
    {
        DateTime? latest = WhenSaved(slot, Home);
        if (!latest.HasValue) return null;

        foreach (int place in new[] { MagicLand, Nether })
        {
            DateTime? other = Exists(slot, place) ? WhenSaved(slot, place) : null;
            if (other.HasValue && other.Value > latest.Value) latest = other;
        }
        return latest;
    }

    static DateTime? WhenSaved(int slot, int place)
    {
        try
        {
            using (BinaryReader reader = new BinaryReader(File.OpenRead(PathFor(slot, place))))
            {
                if (reader.ReadInt32() != FormatVersion) return null;
                return DateTime.FromBinary(reader.ReadInt64());
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void Save(int slot, int place, BlockWorld world, FirstPersonPlayer player)
    {
        string path = PathFor(slot, place);
        string temp = path + ".tmp";

        // Write to a spare file first, so if the game crashes halfway
        // through saving, the old save is still there in one piece
        using (BinaryWriter writer = new BinaryWriter(File.Create(temp)))
        {
            writer.Write(FormatVersion);
            writer.Write(DateTime.Now.ToBinary());

            Vector3 position = player.transform.position;
            writer.Write(position.x);
            writer.Write(position.y);
            writer.Write(position.z);
            writer.Write(player.transform.eulerAngles.y);
            writer.Write(player.Pitch);

            world.WriteTo(writer);
        }

        if (File.Exists(path))
            File.Replace(temp, path, null);
        else
            File.Move(temp, path);
    }

    public static bool Load(int slot, int place, BlockWorld world, out Vector3 position, out float yaw, out float pitch)
    {
        position = Vector3.zero;
        yaw = 0f;
        pitch = 0f;

        try
        {
            using (BinaryReader reader = new BinaryReader(File.OpenRead(PathFor(slot, place))))
            {
                if (reader.ReadInt32() != FormatVersion) return false;
                reader.ReadInt64();   // when it was saved - only the menu needs that

                position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                yaw = reader.ReadSingle();
                pitch = reader.ReadSingle();

                return world.ReadFrom(reader);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Couldn't load world " + (slot + 1) + ": " + e.Message);
            return false;
        }
    }

    // Survival worlds: you start with nothing, collect every block you break,
    // and have hearts and food to look after. Creative worlds (the way the
    // game always worked) have everything. Old worlds are creative.
    public static bool IsSurvival(int slot)
    {
        return PlayerPrefs.GetString("world" + (slot + 1) + "-mode", "creative") == "survival";
    }

    public static void SetSurvival(int slot, bool survival)
    {
        PlayerPrefs.SetString("world" + (slot + 1) + "-mode", survival ? "survival" : "creative");
        PlayerPrefs.Save();
    }

    // Deleting a world deletes its Magic Land too
    public static void Delete(int slot)
    {
        File.Delete(PathFor(slot, Home));
        File.Delete(PathFor(slot, MagicLand));   // (does nothing if there isn't one)
        File.Delete(PathFor(slot, Nether));
        SetLastPlace(slot, Home);

        // ...and forgets its magic villagers, so a new world gets new ones
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-magicfolk");
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-visitors");
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-travellers");
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-unicorns-home");
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-unicorns-magic");
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-unicorns-arriving-home");
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-unicorns-arriving-magic");
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-dragons-home");
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-dragons-magic");
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-dragons-nether");
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-firefolk");

        // ...and, for a survival world, your blocks, hearts and food
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-mode");
        StarShrine.Forget(slot);
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-bed");
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-armour");
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-pets");
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-time");
        foreach (string place in new[] { "home", "magic", "nether" })
            PlayerPrefs.DeleteKey("world" + (slot + 1) + "-vehicles-" + place);
        Structures.Forget(slot);   // a new world gets a new shrine
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-stash");
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-survival-hotbar");
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-health");
        PlayerPrefs.DeleteKey("world" + (slot + 1) + "-food");
        PlayerPrefs.Save();
    }
}
