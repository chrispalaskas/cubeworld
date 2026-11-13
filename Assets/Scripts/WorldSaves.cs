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

    // Bump this if the file layout ever changes, so old saves are spotted
    // instead of being read wrongly
    const int FormatVersion = 1;

    static string PathFor(int slot, int place)
    {
        string name = "world" + (slot + 1) + (place == MagicLand ? "-magic" : "");
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
        return place == MagicLand && Exists(slot, MagicLand) ? MagicLand : Home;
    }

    public static void SetLastPlace(int slot, int place)
    {
        PlayerPrefs.SetInt("world" + (slot + 1) + "-place", place);
        PlayerPrefs.Save();
    }

    // When the world was last saved - home or Magic Land, whichever was
    // later - or null if it can't be read
    public static DateTime? WhenSaved(int slot)
    {
        DateTime? home = WhenSaved(slot, Home);
        DateTime? magic = Exists(slot, MagicLand) ? WhenSaved(slot, MagicLand) : null;
        if (home.HasValue && magic.HasValue && magic.Value > home.Value) return magic;
        return home;
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

    // Deleting a world deletes its Magic Land too
    public static void Delete(int slot)
    {
        File.Delete(PathFor(slot, Home));
        File.Delete(PathFor(slot, MagicLand));   // (does nothing if there isn't one)
        SetLastPlace(slot, Home);
    }
}
