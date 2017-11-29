using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/*

Using some ported 3rd party code from http://www.lost.org.uk/grimdawn.html to decrypt stash files.
Thanks to that author (whoever he is!) and Mamba for pointing me to it.
*/

namespace GDStashLib
{
	internal struct GDBlock
	{
		internal UInt32 len;
		internal UInt32 end;
	};

	public class GDStash
	{
		//internal Dictionary<string, string> _listCfgNames = new Dictionary<string, string>();
		internal UInt32 _key;

		internal UInt32[] _table = new UInt32[256];
		internal string mod;
		public string FileName { get; internal set; }
		public string FriendlyName { get; internal set; }

		internal BinaryReader _file;

		public List<GDStashBag> Bags { get; internal set; }

		private List<GDStashItem> _Items;

		public List<GDStashItem> Items
		{
			get
			{
				if (_Items == null)
				{
					int numItems = 0;

					foreach (var Bag in Bags)
					{
						numItems += Bag.Items.Count;
					}
					List<GDStashItem> stashItems = new List<GDStashItem>(numItems);

					foreach (var Bag in Bags)
					{
						stashItems.AddRange(Bag.Items);
					}
					_Items = stashItems;
				}
				return _Items;
			}
		}

		internal void read_key()
		{
			UInt32 k = _file.ReadUInt32();
			k ^= 0x55555555;
			_key = k;

			for (UInt32 i = 0; i < 256; i++)
			{
				k = (k >> 1) | (k << 31);
				k *= 39916801;
				_table[i] = k;
			}
		}

		internal void update_key(byte[] b)
		{
			for (int i = 0; i < b.Length; i++)
			{
				_key ^= _table[b[i]];
			}
		}

		private UInt32 next_int()
		{
			UInt32 ret = _file.ReadUInt32();
			ret ^= _key;

			return ret;
		}

		internal UInt32 read_int()
		{
			UInt32 val = _file.ReadUInt32();
			UInt32 ret = val ^ _key;
			update_key(BitConverter.GetBytes(val));
			return ret;
		}

		internal byte read_byte()
		{
			byte val = _file.ReadByte();
			byte ret = (byte)(val ^ _key);
			byte[] bytes = new byte[1];
			bytes[0] = val;
			update_key(bytes);
			return ret;
		}

		internal float read_float()
		{
			UInt32 i = read_int();
			byte[] bytes = BitConverter.GetBytes(i);
			float f = BitConverter.ToSingle(bytes, 0);
			return f;
		}

		internal string read_str()
		{
			UInt32 len = read_int();
			if (len == 0)
				return null;
			byte[] bytes = new byte[len];
			for (UInt32 i = 0; i < len; i++)
			{
				bytes[i] = read_byte();
			}
			string str = System.Text.Encoding.UTF8.GetString(bytes);
			return str;
		}

		internal UInt32 read_block_start(ref GDBlock b)
		{
			UInt32 ret = read_int();
			b.len = next_int();
			b.end = (UInt32)_file.BaseStream.Position + b.len;

			return ret;
		}

		internal void read_block_end(ref GDBlock b)
		{
			if ((UInt32)_file.BaseStream.Position != b.end)
				throw new IOException();

			if (next_int() != 0)
				throw new IOException();
		}

		public void Open(string filename)
		{
			using (_file = new BinaryReader(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read), ASCIIEncoding.ASCII))
			{
				FileName = filename;
				UInt32 n, ver;
				read_key();

				if (read_int() != 2)
					throw new IOException();

				GDBlock b = new GDBlock();

				if (read_block_start(ref b) != 18)
					throw new IOException();
				ver = read_int();
				if (ver < 4) // version
					throw new IOException("Version Mismatch <4");
				n = next_int();
				if (n != 0)
					throw new IOException();

				mod = read_str();
				if (ver >= 5)
					read_byte();
				n = read_int();
				Bags = new List<GDStashBag>((int)n);

				for (int i = 0; i < n; i++)
				{
					GDStashBag bag = new GDStashBag();
					bag.Read(this);
					bag.Index = i;
					Bags.Add(bag);
				}

				read_block_end(ref b);
			}
		}

		public Dictionary<string, string> ReadTags(string fileName, string tagPartialmatch)
		{
			Dictionary<string, string> tags = new Dictionary<string, string>();
			string line;
			string[] parts;
			string fieldName;
			using (StreamReader tagsFile = File.OpenText(fileName))
			{
				while (!tagsFile.EndOfStream)
				{
					line = tagsFile.ReadLine();
					if (line.Length > 0 && line[0] != '#')
					{
						parts = line.Split('=');
						if (parts.Length == 2)
						{
							fieldName = parts[0];
							if (String.IsNullOrEmpty(tagPartialmatch) || fieldName.Contains(tagPartialmatch))
								tags.Add(parts[0], parts[1]);
						}
					}
				}
				tagsFile.Close();
			}

			return tags;
		}

		public void UpdateItems(string extractBaseFolder, string ResourceFolder, string listCfgFile, bool shouldResolveAffixes, bool useAlternateAffixFormat)
		{
			string tagsItemsFile = Path.Combine(ResourceFolder, @"tags_items.txt");
			string tagsSkillsFile = Path.Combine(ResourceFolder, @"tags_skills.txt");
			Dictionary<string, string> tagsItems = ReadTags(tagsItemsFile, null);
			Dictionary<string, string> tagsSkillsNames = ReadTags(tagsSkillsFile, "SkillName");

			string tagsItemsGdx1File = Path.Combine(ResourceFolder, @"tagsgdx1_items.txt");
			if(File.Exists(tagsItemsGdx1File))
			{
				Dictionary<string, string> tagsGdx1Items = ReadTags(tagsItemsGdx1File, null);
				tagsItems = tagsItems.Union(tagsGdx1Items).ToDictionary(k => k.Key, v => v.Value);

			}
			string tagsSkillsGdx1File = Path.Combine(ResourceFolder, @"tagsgdx1_skills.txt");
			if (File.Exists(tagsSkillsGdx1File))
			{
				Dictionary<string, string> tagsGdx1SkillsNames = ReadTags(tagsSkillsGdx1File, "SkillName");
				tagsSkillsNames = tagsSkillsNames.Union(tagsGdx1SkillsNames).ToDictionary(k => k.Key, v => v.Value);
			}
			if (!String.IsNullOrEmpty(listCfgFile) && File.Exists(listCfgFile))
			{
				var friendlyNames = GetFriendlyNames(listCfgFile);
				string filenameShort = Path.GetFileName(FileName);
				if (friendlyNames.ContainsKey(filenameShort))
					FriendlyName = friendlyNames[filenameShort];
			}
			foreach (GDStashItem item in Items)
			{
				string itemNameTag = null;
				//item.IsEmpowered = false;
				//string itemClass = null;
				//StreamReader dbrFile = File.OpenText(filename);
				//while (!dbrFile.EndOfStream && (itemNameTag == null || itemClass == null || item.IsEmpowered || item.LevelRequirement == 0))
				//{
				//	line = dbrFile.ReadLine();
				//	if (line.Length > 0 && line[0] != '#')
				//	{
				//		parts = line.Split(',');
				//		if (parts.Length == 3)
				//		{
				//			string fieldName = parts[0];
				//			string fieldValue = parts[1];
				//			if (fieldName == "itemNameTag" || fieldName == "description")
				//			{
				//				itemNameTag = fieldValue;
				//			}

				//			if (fieldName == "itemStyleTag" && fieldValue == "tagStyleUniqueTier2")
				//			{
				//				item.IsEmpowered = true;
				//			}
				//			if (fieldName == "levelRequirement")
				//			{
				//				item.LevelRequirement = int.Parse(fieldValue);
				//			}
				//			if (fieldName == "itemClassification")
				//			{
				//				GDStashItem.ItemClassification itemClassification = GDStashItem.ItemClassification.Unknown;
				//				if (!Enum.TryParse<GDStashItem.ItemClassification>(fieldValue, true, out itemClassification))
				//					itemClassification = GDStashItem.ItemClassification.Unknown;
				//				item.Class = itemClassification;
				//			}
				//		}
				//	}
				//}
				string dbrFilename = Path.Combine(extractBaseFolder, item.baseName);
				if (!File.Exists(dbrFilename))
					break;
				Dictionary<string, string> fields = GetFieldValuesFromDbr(extractBaseFolder, item.baseName, new string[] { "itemNameTag", "description", "itemStyleTag", "levelRequirement", "itemClassification" }, false);
				if (fields["itemNameTag"] != null)
					itemNameTag = fields["itemNameTag"];
				else
					itemNameTag = fields["description"];
				if (fields["itemStyleTag"] == "tagStyleUniqueTier2")
					item.Tier= 2;
				if (fields["itemStyleTag"] == "tagStyleUniqueTier3")
					item.Tier = 3;
				int lev;
				if (int.TryParse(fields["levelRequirement"], out lev))
					item.LevelRequirement = lev;

				GDStashItem.ItemClassification itemClassification = GDStashItem.ItemClassification.Unknown;
				if (!Enum.TryParse<GDStashItem.ItemClassification>(fields["itemClassification"], true, out itemClassification))
					itemClassification = GDStashItem.ItemClassification.Unknown;
				item.Class = itemClassification;
				if (item.DbrFileName == "q000_torso")// Gazer Man!
				{
					item.Category = "item";
					item.SubCategory = "torso";
				}

				if (itemNameTag != null && tagsItems.ContainsKey(itemNameTag))
				{
					item.Name = tagsItems[itemNameTag].Replace("^k", String.Empty);
					item.Tag = itemNameTag;

					try //supress exceptions unless debugging
					{
						if (itemNameTag == "tagMedalD002")// Badge of Mastery, skill values are important for listing
						{
							int skillLevel1;
							int skillLevel2;
							string skillName1;
							string skillName2;
							string skillNameTag;
							GetAffixValues(extractBaseFolder, item.prefixName, out skillLevel1, out skillNameTag);
							skillName1 = tagsSkillsNames[skillNameTag];
							GetAffixValues(extractBaseFolder, item.suffixName, out skillLevel2, out skillNameTag);
							skillName2 = tagsSkillsNames[skillNameTag];
							if (skillName1 == skillName2)// super badge of mastery w/ same skills in prefix and suffix. Damn you TomoDaK for requesting this!
							{
								item.Name += String.Format(" +{0} {1}", skillLevel1 + skillLevel2, skillName1);
							}
							else
							{
								item.Name += String.Format(" +{0} {1}/+{2} {3}", skillLevel1, skillName1, skillLevel2, skillName2);
							}
						}
						else if (shouldResolveAffixes)
						{
							string prefix = null;
							string suffix = null;
							if (!string.IsNullOrEmpty(item.prefixName))
							{
								string nameTag = GetFieldValueFromDbr(extractBaseFolder, item.prefixName, "lootRandomizerName");
								if (tagsItems.ContainsKey(nameTag))
									prefix = tagsItems[nameTag];
							}
							if (!string.IsNullOrEmpty(item.suffixName))
							{
								string nameTag = GetFieldValueFromDbr(extractBaseFolder, item.suffixName, "lootRandomizerName");
								if (tagsItems.ContainsKey(nameTag))
									suffix = tagsItems[nameTag];
							}
							//if (useAlternateAffixFormat)
							//{
							//	item.Name = string.Format("{0},{1},{2}", item.Name, prefix, suffix);
							//}
							//	else

							if (!string.IsNullOrEmpty(prefix))
							{
								if (useAlternateAffixFormat)
									item.Name = item.Name + " ," + prefix;
								else
									item.Name = prefix + " " + item.Name;
							}
							if (!string.IsNullOrEmpty(suffix))
							{
								if (useAlternateAffixFormat)
									item.Name = item.Name + " ," + suffix;
								else
									item.Name = item.Name + " " + suffix;
							}

						}
					}
					catch (Exception exc)
					{
						if (System.Diagnostics.Debugger.IsAttached)
							System.Diagnostics.Debugger.Break();
						else
							item.Name += "<Err>";
					}
					finally
					{

					}

					item.ParentStash = this;
				}
			}
			//Items.Sort(delegate (GDStashItem item1, GDStashItem item2)
			//{
			//  return item1.Name.CompareTo(item2.Name);
			//});
		}

		public void GetAffixValues(string extractbaseFolder, string filename, out int skillLevel, out string skillNameTag)
		{
			var fields2 = GetFieldValuesFromDbr(extractbaseFolder, filename, new string[] { "augmentSkillLevel1", "augmentSkillName1" }, false);
			skillLevel = 0;
			skillNameTag = null;

			if (fields2.ContainsKey("augmentSkillLevel1") && fields2["augmentSkillLevel1"] != null && fields2["augmentSkillName1"] != null)
			{
				skillLevel = int.Parse(fields2["augmentSkillLevel1"]);
				string dbrFilename = fields2["augmentSkillName1"];
				skillNameTag = GetFieldValueFromDbr(extractbaseFolder, dbrFilename, "skillDisplayName");
				if (skillNameTag == null)
				{
					dbrFilename = GetFieldValueFromDbr(extractbaseFolder, dbrFilename, "buffSkillName");
					skillNameTag = GetFieldValueFromDbr(extractbaseFolder, dbrFilename, "skillDisplayName");
				}
				//string skillName = tagsSkillsNames[skillNameTag];
				//item.Name += String.Format(" +{0} {1}", skillLevel, skillName);
			}
		}
		public static string GetFieldValueFromDbr(string extractbaseFolder, string filename, string fieldName)
		{
			string fieldValue = null;
			Dictionary<string, string> fieldNamesAndValues = GetFieldValuesFromDbr(extractbaseFolder, filename, new string[] { fieldName }, false);
			fieldValue = fieldNamesAndValues[fieldName];
			//if(resolveDbr && (fieldValue!=null && fieldValue.EndsWith(".dbr",StringComparison.CurrentCultureIgnoreCase)))
			//{
			//	fieldValue = GetFieldValueFromDbr(extractBaseFolder, fieldValue, fieldName, resolveDbr);//recursive dig thru DBR refs
			//}
			return fieldValue;
		}

		public static Dictionary<string, string> GetFieldValuesFromDbr(string extractbaseFolder, string dbrFilename, string[] fieldNames, bool getAllValues)
		{
			Dictionary<string, string> fieldNamesAndValues = new Dictionary<string, string>();
			if (fieldNames != null && fieldNames.Length > 0)
			{
				fieldNamesAndValues = new Dictionary<string, string>(fieldNames.Length);
				foreach (string fieldName in fieldNames)
					fieldNamesAndValues.Add(fieldName, null);
			}

			using (StreamReader dbrFile = File.OpenText(Path.Combine(extractbaseFolder, dbrFilename)))
			{
				string line;
				string[] parts;
				int valuesFound = 0;
				while (!dbrFile.EndOfStream && ((getAllValues) || (valuesFound < fieldNamesAndValues.Keys.Count)))
				{
					line = dbrFile.ReadLine();
					if (line.Length > 0 && line[0] != '#')
					{
						parts = line.Split(',');
						if (parts.Length == 3)//trailing comma
						{
							string fieldName = parts[0];
							string fieldValue = parts[1];
							if (getAllValues)
							{
								fieldNamesAndValues.Add(fieldName, fieldValue);
							}
							else if (fieldNamesAndValues.ContainsKey(fieldName))
							{
								fieldNamesAndValues[fieldName] = fieldValue;
								valuesFound++;
							}
						}
					}
				}
				dbrFile.Close();
			}
			return fieldNamesAndValues;
		}

		public static Dictionary<string, string> GetFriendlyNames(string filename)
		{
			string listCfgFile = File.ReadAllText(filename);
			string[] cfgList = listCfgFile.Split('®');
			Dictionary<string, string> listCfgNames = new Dictionary<string, string>(cfgList.Length);
			foreach (string cfgEntry in cfgList)
			{
				string[] cfgEntryRecord = cfgEntry.Split('♂');
				if (cfgEntryRecord.Length > 2)
					listCfgNames.Add(cfgEntryRecord[2], cfgEntryRecord[0]);
			}
			return listCfgNames;
		}
	}
}