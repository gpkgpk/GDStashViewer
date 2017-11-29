using System;
using System.Collections.Generic;
using System.IO;

namespace GDStashLib
{
	public class GDStashItem
	{
		public enum ItemClassification { Unknown, Common, Rare, Epic, Legendary }
		public bool DontExport
		{
			get;
			set;
		}

		private string _Name;
		public string Name
		{
			get { return _Name ?? ""; }
			internal set { _Name = value; }
		}
		public ItemClassification Class { get; internal set; }

		private string _SubCategory;
		public string SubCategory
		{
			get
			{
				return _SubCategory;
			}
			set { _SubCategory = value; }
		}

		private string _Category;
		public string Category
		{
			get
			{
				return _Category;
			}
			set { _Category = value; }
		}

		public int LevelRequirement { get; internal set; }
		//public bool IsEmpowered { get; internal set; }
		//public bool IsEmpowered { get; internal set; }
		public string TierName
		{
			get
			{
				if (Tier == 2)
					return "Empowered";
				else if (Tier == 3)
					return "Mythical";
				return null;
			}
		}

		public int Tier { get; internal set; }
		public int Bag
		{
			get
			{
				if (ParentStashBag == null)
					return 0;
				else
					return ParentStashBag.Index + 1;
			}
		}

		public String StashFile
		{
			get
			{
				if (ParentStash == null)
					return null;
				else
					return ParentStash.FileName ?? null;
			}
		}

		public String FriendlyName
		{
			get
			{
				if (ParentStash == null)
					return null;
				else
					return ParentStash.FriendlyName ?? String.Empty;
			}
		}

		private String _DbrFileName;

		public String DbrFileName
		{
			get
			{
				return _DbrFileName;
			}
		}

		public String Tag { get; internal set; }
		public String baseName { get; internal set; }
		public UInt32 stackCount { get; internal set; }
		public String prefixName { get; internal set; }
		public String suffixName { get; internal set; }
		public String modifierName { get; internal set; }
		public String transmuteName { get; internal set; }
		public UInt32 seed { get; internal set; }
		public String relicName { get; internal set; }
		public String relicBonus { get; internal set; }
		public UInt32 relicSeed { get; internal set; }
		public String augmentName { get; internal set; }
		public UInt32 unknown { get; internal set; }
		public UInt32 augmentSeed { get; internal set; }
		public UInt32 var1 { get; internal set; }
		public float xOffset { get; internal set; }
		public float yOffset { get; internal set; }
		internal GDStash ParentStash { get; set; }
		internal GDStashBag ParentStashBag { get; set; }
		private string _EmpoweredName;
		public string EmpoweredName
		{
			get
			{
				if (_EmpoweredName == null)
				{
					string itemName;
					if (Tier == 1)
						itemName = Name + " - Empowered";
					if (Tier == 2)
						itemName = Name + " - Mythical";
					else
						itemName = Name;
					_EmpoweredName = itemName;
				}

				return _EmpoweredName;
			}
		}
		public int Count { get; set; }
		public String Url { get; set; }

		internal void Read(GDStash stash)
		{
			baseName = stash.read_str();
			string folder = Path.GetDirectoryName(baseName);
			_DbrFileName = Path.GetFileNameWithoutExtension(baseName);
			_SubCategory = folder.Substring(folder.LastIndexOf('\\') + 1).Replace("gear", String.Empty);
			folder = folder.Substring(0, folder.LastIndexOf('\\') - 1);
			_Category = folder.Substring(folder.LastIndexOf('\\') + 1).Replace("gear", String.Empty);

			prefixName = stash.read_str();
			suffixName = stash.read_str();
			modifierName = stash.read_str();
			transmuteName = stash.read_str();
			seed = stash.read_int();
			relicName = stash.read_str();
			relicBonus = stash.read_str();
			relicSeed = stash.read_int();
			augmentName = stash.read_str();
			unknown = stash.read_int();
			augmentSeed = stash.read_int();
			var1 = stash.read_int();
			stackCount = stash.read_int();
			xOffset = stash.read_float();
			yOffset = stash.read_float();
		}

		public override string ToString()
		{
			return this.Name;
		}
	};

	// Custom comparer for the GDStashItem class
	public class GDStashItemComparer : IEqualityComparer<GDStashItem>
	{
		public bool Equals(GDStashItem x, GDStashItem y)
		{
			if (Object.ReferenceEquals(x, y))
				return true;

			//Check whether any of the compared objects is null.
			if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
				return false;

			//Check whether the GDStashItems' properties are equal.
			return x.Name == y.Name && x.Tier == y.Tier;
		}

		// If Equals() returns true for a pair of objects
		// then GetHashCode() must return the same value for these objects.
		public int GetHashCode(GDStashItem item)
		{
			//Check whether the object is null
			if (Object.ReferenceEquals(item, null))
				return 0;

			//Get hash code for the Name field if it is not null.
			int hashGDStashItemName = item.Name == null ? 0 : item.Name.GetHashCode();

			//Calculate the hash code for the GDStashItem.
			return hashGDStashItemName ^ item.Tier.GetHashCode();
		}
	}
}