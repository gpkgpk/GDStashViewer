using GDStashViewer.Properties;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Data;

namespace GDStashLib
{
	internal static class GDExporter
	{
		public static void ExportToTextFile(string filename, CollectionViewSource collectionViewSource)
		{
			using (StreamWriter exportFile = File.CreateText(filename))
			{
				if (!Settings.Default.ExportShouldIgnoreGrouping && collectionViewSource.View.Groups != null && collectionViewSource.View.Groups.Count > 1)
				{
					foreach (CollectionViewGroup group in collectionViewSource.View.Groups)
					{
						ExportGroup(exportFile, group, 0);
					}
				}
				else // no groups or ignored, export flat list
				{
					List<GDStashItem> itemList = itemList = collectionViewSource.View.OfType<GDStashItem>().ToList();
					ExportItems(exportFile, itemList, 0);
				}
			}
		}

		public static void ExportItems(StreamWriter exportFile, IEnumerable<GDStashItem> itemList, int indentLevel)
		{
			if (!Settings.Default.ExportShouldIgnoreDontExport)
			{
				itemList = itemList.Where(i => !i.DontExport);// drop items that are marked as DontExport
			}

			string duplicateCountStr = string.Empty;
			//ignore dupes, so get distinct items and update duplicate count prop on 1st item of dupes
			if (Settings.Default.ExportShouldIgnoreDuplicates)
			{
				var itemListCount = itemList.GroupBy(i => i, new GDStashItemComparer())
										.Select(g => new { Item = g.Key, Count = g.Count() });

				itemList = itemList.Distinct(new GDStashItemComparer()).ToList();
				foreach (var ic in itemListCount)
				{
					ic.Item.Count = ic.Count;
				}
			}
			foreach (GDStashItem item in itemList)
			{
				if (string.IsNullOrEmpty(item.Name))
				{
					break;
				}

				duplicateCountStr = string.Empty;
				if (Settings.Default.ExportShouldIgnoreDuplicates && item.Count > 1)
				{
					duplicateCountStr = "*" + item.Count;
				}

				ExportSingleItem(exportFile, item, indentLevel, duplicateCountStr);
			}
		}

		public static void ExportSingleItem(StreamWriter exportFile, GDStashItem item, int indentLevel, string duplicateCount)
		{
			indentLevel++;
			string itemName;
			if (item.Tier == 1)
			{
				itemName = item.Name + " - Empowered";
			}

			if (item.Tier == 2)
			{
				itemName = item.Name + " - Mythical";
			}
			else
			{
				itemName = item.Name;
			}

			if (Settings.Default.ExportUseIndent)
			{
				for (int i = 0; i < indentLevel; i++)
				{
					exportFile.Write(" ");
				}
			}

			exportFile.WriteLine(string.Format(Settings.Default.ExportFormat, itemName, item.Category, item.SubCategory, item.LevelRequirement, duplicateCount, item.Url));
		}

		//sloppy-ish recursion, but meh
		public static void ExportGroup(StreamWriter exportFile, CollectionViewGroup exportGroup, int indentLevel)
		{
			if (Settings.Default.ExportUseIndent)
			{
				for (int i = 0; i < indentLevel; i++)
				{
					exportFile.Write(" ");
				}
			}

			exportFile.WriteLine(string.Format(Settings.Default.ExportGroupFormat, exportGroup.Name, exportGroup.ItemCount, "\r\n"));
			string lastItemName = string.Empty;
			bool hasOnlyItems = exportGroup.Items.Where(i => i is CollectionViewGroup).Count() == 0; // LINQ Any has issues when casting to CVG so use where and count

			if (hasOnlyItems)
			{
				List<GDStashItem> itemList = exportGroup.Items.Cast<GDStashItem>().ToList();
				ExportItems(exportFile, itemList, indentLevel);
			}
			else
			{
				foreach (object obj in exportGroup.Items)
				{
					CollectionViewGroup group = obj as CollectionViewGroup;
					if (group != null)
					{
						ExportGroup(exportFile, group, indentLevel + 1);
					}
					else// edge case for ungrouped items; this should never be possible
					{
						GDStashItem item = obj as GDStashItem;
						if (item != null && !string.IsNullOrEmpty(item.Name))
						{
							string itemName;
							if (item.Tier == 1)
							{
								itemName = item.Name + " - Empowered";
							}

							if (item.Tier == 2)
							{
								itemName = item.Name + " - Mythical";
							}
							else
							{
								itemName = item.Name;
							}

							if ((!Settings.Default.ExportShouldIgnoreDuplicates) || (Settings.Default.ExportShouldIgnoreDuplicates && lastItemName != itemName))
							{
								ExportSingleItem(exportFile, item, indentLevel, string.Empty);
							}
							lastItemName = itemName;
						}
					}
				}
			}
			if (!string.IsNullOrEmpty(Settings.Default.ExportGroupFooterFormat))
			{
				if (Settings.Default.ExportUseIndent)
				{
					for (int i = 0; i < indentLevel; i++)
					{
						exportFile.Write(" ");
					}
				}

				exportFile.WriteLine(string.Format(Settings.Default.ExportGroupFooterFormat, exportGroup.Name, exportGroup.ItemCount, "\r\n"));
			}
		}

	}
}
