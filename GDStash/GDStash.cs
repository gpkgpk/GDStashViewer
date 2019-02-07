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

	public class GDStash
	{
		//internal Dictionary<string, string> _listCfgNames = new Dictionary<string, string>();
		internal string mod;
		public string FileName { get; set; }
		public string FriendlyName { get; set; }
		public GDBlock _GDBlock=new GDBlock();
		public GDBlockReader BlockReader { get; set; }
		public List<GDStashBag> Bags { get; set; }

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

		

		public void Open(string filename)
		{
		BlockReader = new GDBlockReader();
			using (BlockReader.File = new BinaryReader(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read), ASCIIEncoding.ASCII))
			{
				FileName = filename;
				UInt32 n, ver;
				BlockReader.read_key();

				if (BlockReader.read_int() != 2)
					throw new IOException();

				GDBlock b = new GDBlock();

				if (BlockReader.read_block_start(ref b) != 18)
					throw new IOException();
				ver = BlockReader.read_int();
				if (ver < 4) // version
					throw new IOException("Version Mismatch <4");
				n = BlockReader.next_int();
				if (n != 0)
					throw new IOException();

				mod = BlockReader.read_str();
				if (ver >= 5)
					BlockReader.read_byte();
				n = BlockReader.read_int();
				Bags = new List<GDStashBag>((int)n);

				for (int i = 0; i < n; i++)
				{
					GDStashBag bag = new GDStashBag();
					bag.Read(this.BlockReader,this);
					bag.Index = i;
					Bags.Add(bag);
				}

				BlockReader.read_block_end(ref b);
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

		internal Dictionary<string, string> _tagsItems;
		internal Dictionary<string, string> _tagsSkillsNames;
		internal Dictionary<string, string> _fields;

		internal struct ItemFields
		{
			public string itemNameTag;
			public string description;
			public string itemStyleTag;
			public string levelRequirement;
			public string itemClassification;
		}

		internal Dictionary<string, ItemFields> _itemFieldsCache = new Dictionary<string, ItemFields>();

		public void Initialize(string extractBaseFolder, string ResourceFolder)
		{
			string tagsItemsFile = Path.Combine(ResourceFolder, @"tags_items.txt");
			string tagsSkillsFile = Path.Combine(ResourceFolder, @"tags_skills.txt");
			_tagsItems = ReadTags(tagsItemsFile, null);
			_tagsSkillsNames = ReadTags(tagsSkillsFile, "SkillName");

			string tagsItemsGdx1File = Path.Combine(ResourceFolder, @"tagsgdx1_items.txt");
			if (File.Exists(tagsItemsGdx1File))
			{
				Dictionary<string, string> tagsGdx1Items = ReadTags(tagsItemsGdx1File, null);
				_tagsItems = _tagsItems.Union(tagsGdx1Items).ToDictionary(k => k.Key, v => v.Value);

			}
			string tagsSkillsGdx1File = Path.Combine(ResourceFolder, @"tagsgdx1_skills.txt");
			if (File.Exists(tagsSkillsGdx1File))
			{
				Dictionary<string, string> tagsGdx1SkillsNames = ReadTags(tagsSkillsGdx1File, "SkillName");
				_tagsSkillsNames = _tagsSkillsNames.Union(tagsGdx1SkillsNames).ToDictionary(k => k.Key, v => v.Value);
			}

		}

		public void UpdateItems(string extractBaseFolder, string ResourceFolder, string listCfgFile, bool shouldResolveAffixes, bool useAlternateAffixFormat)
		{
			if (_tagsItems == null)
				Initialize(extractBaseFolder, ResourceFolder);
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
				string dbrFilename = Path.Combine(extractBaseFolder, item.baseName);
				string itemClassificationStr;
				ItemFields itemFields;

				if (!File.Exists(dbrFilename))
					break;
				if (!_itemFieldsCache.ContainsKey(item.baseName))
				{
					Dictionary<string, string> fields = GetFieldValuesFromDbr(extractBaseFolder, item.baseName, new string[] { "itemNameTag", "description", "itemStyleTag", "levelRequirement", "itemClassification" }, false);

					itemFields.itemNameTag = fields["itemNameTag"];
					itemFields.description = fields["description"];
					itemFields.itemStyleTag = fields["itemStyleTag"];
					itemFields.levelRequirement = fields["levelRequirement"];
					itemFields.itemClassification = fields["itemClassification"];
					_itemFieldsCache.Add(item.baseName, itemFields);

				}
				else
				{
					itemFields = _itemFieldsCache[item.baseName];
				}

				if (itemFields.itemNameTag != null)
					itemNameTag = itemFields.itemNameTag;
				else
					itemNameTag = itemFields.description;
				if (itemFields.itemStyleTag == "tagStyleUniqueTier2")
					item.Tier = 2;
				if (itemFields.itemStyleTag == "tagStyleUniqueTier3")
					item.Tier = 3;
				itemClassificationStr = itemFields.itemClassification;
				int lev;
				if (int.TryParse(itemFields.levelRequirement, out lev))
					item.LevelRequirement = lev;


				GDStashItem.ItemClassification itemClassification = GDStashItem.ItemClassification.Unknown;
				if (!Enum.TryParse<GDStashItem.ItemClassification>(itemClassificationStr, true, out itemClassification))
					itemClassification = GDStashItem.ItemClassification.Unknown;
				item.Class = itemClassification;
				if (item.DbrFileName == "q000_torso")// Gazer Man!
				{
					item.Category = "item";
					item.SubCategory = "torso";
				}

				if (itemNameTag != null && _tagsItems.ContainsKey(itemNameTag))
				{
					item.Name = _tagsItems[itemNameTag].Replace("^k", String.Empty);
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
							skillName1 = _tagsSkillsNames[skillNameTag];
							GetAffixValues(extractBaseFolder, item.suffixName, out skillLevel2, out skillNameTag);
							skillName2 = _tagsSkillsNames[skillNameTag];
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
								if (_tagsItems.ContainsKey(nameTag))
									prefix = _tagsItems[nameTag];
							}
							if (!string.IsNullOrEmpty(item.suffixName))
							{
								string nameTag = GetFieldValueFromDbr(extractBaseFolder, item.suffixName, "lootRandomizerName");
								if (_tagsItems.ContainsKey(nameTag))
									suffix = _tagsItems[nameTag];
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