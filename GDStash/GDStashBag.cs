using System.Collections.Generic;

namespace GDStashLib
{
	public class GDStashBag

	{
		public List<GDStashItem> Items { get; set; }
		internal GDStash ParentStash { get; set; }
		public int Index { get; internal set; }

		public uint width;
		public uint height;

		internal void Read(GDBlockReader gdbr, GDStash parentStash = null, bool isCharacterBag = false)
		{
			GDBlock b = new GDBlock();
			gdbr.read_block_start(ref b);
			if (!isCharacterBag)
			{
				width = gdbr.read_int();
				height = gdbr.read_int();
			}
			else
			{
				gdbr.read_byte();
			}
			uint numItems = gdbr.read_int();
			ParentStash = parentStash;
			Items = new List<GDStashItem>((int)numItems);

			for (int i = 0; i < numItems; i++)
			{
				GDStashItem item = new GDStashItem();
				item.Read(gdbr);
				item.ParentStashBag = this;
				if (!string.IsNullOrEmpty(item.SubCategory))
				{
					string subcat = item.SubCategory.ToLower();
					if(!(subcat.Contains("potion") || subcat.Contains("consumable") || subcat.Contains("questitem")))
						Items.Add(item);
				}
				else if (string.IsNullOrEmpty(item.SubCategory))
					Items.Add(item);

			}

			gdbr.read_block_end(ref b);
		}
	}
}