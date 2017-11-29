using System;
using System.Collections.Generic;

namespace GDStashLib
{
	public class GDStashBag

	{
		public List<GDStashItem> Items { get; internal set; }
		internal GDStash ParentStash { get; set; }
		public int Index { get; internal set; }

		public UInt32 width;
		public UInt32 height;

		internal void Read(GDStash stash)
		{
			GDBlock b = new GDBlock();
			stash.read_block_start(ref b);

			width = stash.read_int();
			height = stash.read_int();

			UInt32 numItems = stash.read_int();
			ParentStash = stash;
			Items = new List<GDStashItem>((int)numItems);

			for (int i = 0; i < numItems; i++)
			{
				GDStashItem item = new GDStashItem();
				item.Read(stash);
				item.ParentStashBag = this;
				Items.Add(item);
			}

			stash.read_block_end(ref b);
		}
	}
}