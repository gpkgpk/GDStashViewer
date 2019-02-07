using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;


namespace GDStashLib
{



	public class header
	{
		public string name;
		public string tag;
		public uint level;
		public byte sex;
		public byte hardcore;
		public void read(ref GDBlockReader gdbr)
		{
			name = gdbr.read_wide_str();
			sex = gdbr.read_byte();
			tag = gdbr.read_str();
			level = gdbr.read_int();
			hardcore = gdbr.read_byte();
		}

	}
	public class character_info
	{
		public string texture;
		public uint money;
		public uint lootMode;
		public byte isInMainQuest;
		public byte hasBeenInGame;
		public byte difficulty;
		public byte greatestDifficulty;
		public byte compassState;
		public byte skillWindowShowHelp;
		public byte alternateConfig;
		public byte alternateConfigEnabled;
		public void read(ref GDBlockReader gdc)
		{
			GDBlock b = new GDBlock();

			if (gdc.read_block_start(ref b) != 1)
			{
				throw new Exception();
			}
			uint ver = gdc.read_int();
			if (ver < 3) // version
			{
				throw new Exception();
			}

			isInMainQuest = gdc.read_byte();
			hasBeenInGame = gdc.read_byte();
			difficulty = gdc.read_byte();
			greatestDifficulty = gdc.read_byte();
			money = gdc.read_int();

			if (ver >= 4)
			{
				gdc.read_byte();
				gdc.read_int();
			}
			compassState = gdc.read_byte();
			lootMode = gdc.read_int();
			skillWindowShowHelp = gdc.read_byte();
			alternateConfig = gdc.read_byte();
			alternateConfigEnabled = gdc.read_byte();
			texture = gdc.read_str();

			gdc.read_block_end(ref b);
		}

	}

	public class character_bio
	{
		public uint level;
		public uint experience;
		public uint modifierPoints;
		public uint skillPoints;
		public uint devotionPoints;
		public uint totalDevotion;
		public float physique;
		public float cunning;
		public float spirit;
		public float health;
		public float energy;

		public void read(ref GDBlockReader gdc)
		{
			GDBlock b = new GDBlock();

			gdc.read_block_start(ref b);
			uint ver = gdc.read_int();
			if (ver != 8) // version
			{
				throw new Exception();
			}

			level = gdc.read_int();
			experience = gdc.read_int();
			modifierPoints = gdc.read_int();
			skillPoints = gdc.read_int();
			devotionPoints = gdc.read_int();
			totalDevotion = gdc.read_int();
			physique = gdc.read_float();
			cunning = gdc.read_float();
			spirit = gdc.read_float();
			health = gdc.read_float();
			energy = gdc.read_float();

			gdc.read_block_end(ref b);
		}

	}
	/*
	public class inventory_item : item
	{
		public uint x;
		public uint y;



	}

	public class inventory_sack
	{
		public List<GDStashItem> Items;
		public byte tempBool;

		public void read(ref GDBlockReader gdbr)
		{
			GDBlock b = new GDBlock();

			if (gdbr.read_block_start(ref b) != 0)
			{
				throw new IOException();
			}

			tempBool = gdbr.read_byte();
			//items.read(ref gdbr);
			//width = gdc.read_int();
			//height = gdc.read_int();

			uint numItems = gdbr.read_int();

			Items = new List<GDStashItem>((int)numItems);

			for (int i = 0; i < numItems; i++)
			{
				GDStashItem item = new GDStashItem();
				item.Read(gdbr);

				Items.Add(item);
			}


			gdbr.read_block_end(ref b);
		}

	}

	public class item
	{
		public string baseName;
		public string prefixName;
		public string suffixName;
		public string modifierName;
		public string transmuteName;
		public string relicName;
		public string relicBonus;
		public string augmentName;
		public uint stackCount;
		public uint seed;
		public uint relicSeed;
		public uint unknown;
		public uint augmentSeed;
		public uint var1;

		public void read(ref GDBlockReader gdc)
		{
			baseName = gdc.read_str();
			prefixName = gdc.read_str();
			suffixName = gdc.read_str();
			modifierName = gdc.read_str();
			transmuteName = gdc.read_str();
			seed = gdc.read_int();
			relicName = gdc.read_str();
			relicBonus = gdc.read_str();
			relicSeed = gdc.read_int();
			augmentName = gdc.read_str();
			unknown = gdc.read_int();
			augmentSeed = gdc.read_int();
			var1 = gdc.read_int();
			stackCount = gdc.read_int();
		}
	}

	public class inventory_equipment : item
	{
		public byte attached;

		public void read(ref GDBlockReader gdc)
		{
			base.read(ref gdc);
			attached = gdc.read_byte();
		}
	}
	*/
	public class inventory
	{
		public List<GDStashBag> Bags { get; internal set; }

		private List<GDStashItem> _Items;
		public List<GDStashItem> Items
		{
			get
			{
				if (_Items == null)
				{
					int numItems = 0;

					foreach (GDStashBag Bag in Bags)
					{
						numItems += Bag.Items.Count;
					}
					List<GDStashItem> stashItems = new List<GDStashItem>(numItems);

					foreach (GDStashBag Bag in Bags)
					{
						stashItems.AddRange(Bag.Items);
					}

					stashItems.AddRange(equipment);
					stashItems.AddRange(weapon1);
					stashItems.AddRange(weapon2);
					_Items = stashItems;
				}
				return _Items;
			}
		}


		public GDStashItem[] equipment = Arrays.InitializeWithDefaultInstances<GDStashItem>(12);
		public GDStashItem[] weapon1 = Arrays.InitializeWithDefaultInstances<GDStashItem>(2);
		public GDStashItem[] weapon2 = Arrays.InitializeWithDefaultInstances<GDStashItem>(2);
		public uint focused;
		public uint selected;
		public byte flag;
		public byte useAlternate;
		public byte alternate1;
		public byte alternate2;

		public void read(ref GDBlockReader gdbr)
		{
			GDBlock b = new GDBlock();
			uint ver;
			if (gdbr.read_block_start(ref b) != 3)
			{
				new IOException();
			}
			ver = gdbr.read_int();
			if (ver < 4) // version
			{
				new IOException();
			}
			flag = gdbr.read_byte();
			if (flag != 0)
			{
				uint numBags = gdbr.read_int();
				focused = gdbr.read_int();
				selected = gdbr.read_int();

				Bags = new List<GDStashBag>((int)numBags);

				for (int i = 0; i < numBags; i++)
				{
					GDStashBag bag = new GDStashBag();
					bag.Read(gdbr, null, true);
					bag.Index = i;
					Bags.Add(bag);

				}

				useAlternate = gdbr.read_byte();

				for (uint i = 0; i < 12; i++)
				{
					equipment[i].Read(gdbr, true);
					gdbr.read_byte();
				}

				alternate1 = gdbr.read_byte();

				for (uint i = 0; i < 2; i++)
				{
					weapon1[i].Read(gdbr, true);
					gdbr.read_byte();
				}

				alternate2 = gdbr.read_byte();

				for (uint i = 0; i < 2; i++)
				{
					weapon2[i].Read(gdbr, true);
					gdbr.read_byte();
				}
			}

			gdbr.read_block_end(ref b);
		}
	}

	//public class stash_item : item
	//{
	//	public float x;
	//	public float y;

	//	public void read(ref GDBlockReader gdc)
	//	{
	//		base.read(ref gdc);
	//		x = gdc.read_float();
	//		y = gdc.read_float();
	//	}
	//}

	//public void List<T>.read<T>(ref GDBlockReader gdbr)
	//{
	//	uint n = gdc.read_int();

	//	this.resize(n);
	//	T[] ptr = this.data();

	//	for (uint i = 0; i < n; i++)
	//	{
	//		ptr[i].read(gdc);
	//	}
	//}

	public class character_stash
	{

		public uint width;
		public uint height;

		public List<GDStashBag> Bags { get; internal set; }

		private List<GDStashItem> _Items;
		public List<GDStashItem> Items
		{
			get
			{
				if (_Items == null)
				{
					int numItems = 0;

					foreach (GDStashBag Bag in Bags)
					{
						numItems += Bag.Items.Count;
					}
					List<GDStashItem> stashItems = new List<GDStashItem>(numItems);

					foreach (GDStashBag Bag in Bags)
					{
						stashItems.AddRange(Bag.Items);
					}
					_Items = stashItems;
				}
				return _Items;
			}
		}

		public void read(ref GDBlockReader gdbr)
		{
			GDBlock b = new GDBlock();


			if (gdbr.read_block_start(ref b) != 4)
			{
				throw new Exception();
			}
			uint ver = gdbr.read_int();
			if (ver != 5 && ver != 6) // version
			{
				throw new Exception();
			}

			uint numBags;
			if (ver >= 6)
			{
				numBags = numBags = gdbr.read_int();

			}
			else
			{
				numBags = 1;
			}

			Bags = new List<GDStashBag>((int)numBags);

			for (int i = 0; i < numBags; i++)
			{
				GDStashBag bag = new GDStashBag();
				bag.Read(gdbr);
				bag.Index = i;
				Bags.Add(bag);
			}

			gdbr.read_block_end(ref b);
		}


	}


	internal static class Arrays
	{
		public static T[] InitializeWithDefaultInstances<T>(int length) where T : new()
		{
			T[] array = new T[length];
			for (int i = 0; i < length; i++)
			{
				array[i] = new T();
			}
			return array;
		}

		public static void DeleteArray<T>(T[] array) where T : System.IDisposable
		{
			foreach (T element in array)
			{
				if (element != null)
				{
					element.Dispose();
				}
			}
		}
	}
	public class uid
	{
		public byte[] id = new byte[16];

		public void read(ref GDBlockReader gdbr)
		{
			for (uint i = 0; i < 16; i++)
			{
				id[i] = gdbr.read_byte();
			}
		}
	}

	public class GDPlayer
	{
		[DllImport("Shell32.dll")]
		private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)]Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

		public static string GetSavedGamesDir()
		{
			int result = SHGetKnownFolderPath(new Guid("4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4"), 0, new IntPtr(0), out IntPtr path);
			if (result >= 0)
			{
				return Marshal.PtrToStringUni(path);
			}
			else
			{
				throw new ExternalException("Failed to find the saved games directory.", result);
			}
		}

		//public GDBlockReader BlockReader;
		public header hdr = new header();
		public uid id = new uid();
		public character_info info = new character_info();
		public character_bio bio = new character_bio();
		public inventory inv = new inventory();
		public character_stash stash = new character_stash();

		//respawn_list respawns;
		//teleport_list teleports;
		//marker_list markers;
		//shrine_list shrines;
		//character_skills skills;
		//lore_notes notes;
		//faction_pack factions;
		//ui_settings ui;
		//tutorial_pages tutorials;
		//play_stats stats;

		//void read(const char*);
		//void write(const char*);

		public void read(string filename)
		{
			GDBlockReader gdbr = new GDBlockReader();

			using (gdbr.File = new BinaryReader(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read), ASCIIEncoding.ASCII))
			{
				uint n, ver;
				gdbr.read_key();

				if (gdbr.read_int() != 0x58434447)
				{
					throw new IOException();
				}

				ver = gdbr.read_int();
				if (ver != 1 && ver != 2)
				{
					throw new IOException("Unsupported Version");
				}
				//read Header
				hdr.read(ref gdbr);
				if (ver == 2)
				{
					gdbr.read_byte();
				}

				n = gdbr.next_int();
				if (n != 0)
				{
					throw new IOException();
				}

				ver = gdbr.read_int();
				if (ver < 6) // version
				{
					throw new IOException("Version Mismatch reading Player file, ver<6");
				}

				id.read(ref gdbr);

				info.read(ref gdbr);
				bio.read(ref gdbr);
				inv.read(ref gdbr);

				//foreach (GDStashItem item in inv.Items)
				//{
				//	item.FriendlyName = hdr.name;
				//}

				stash.read(ref gdbr);

				//foreach (GDStashItem item in stash.Items)
				//{
				//	item.FriendlyName = hdr.name;
				//}
			
			//respawns.read(this);
			//teleports.read(this);
			//markers.read(this);
			//shrines.read(this);
			//skills.read(this);
			//notes.read(this);
			//factions.read(this);
			//ui.read(this);
			//tutorials.read(this);
			//stats.read(this);

			gdbr.File.Close();
		}


	}
}
}
