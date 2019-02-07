using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDStashLib
{
	public struct GDBlock
	{
		public UInt32 len;
		public UInt32 end;
	};

	public class GDBlockReader
	{


		public UInt32 _key;
		public BinaryReader File { get; set; }

		public UInt32[] _table = new UInt32[256];

		public void read_key()
		{
			UInt32 k = File.ReadUInt32();
			k ^= 0x55555555;
			_key = k;

			for (UInt32 i = 0; i < 256; i++)
			{
				k = (k >> 1) | (k << 31);
				k *= 39916801;
				_table[i] = k;
			}
		}

		public void update_key(byte[] b)
		{
			for (int i = 0; i < b.Length; i++)
			{
				_key ^= _table[b[i]];
			}
		}

		public UInt32 next_int()
		{
			UInt32 ret = File.ReadUInt32();
			ret ^= _key;

			return ret;
		}

		public UInt32 read_int()
		{
			UInt32 val = File.ReadUInt32();
			UInt32 ret = val ^ _key;
			update_key(BitConverter.GetBytes(val));
			return ret;
		}

		public byte read_byte()
		{
			byte val = File.ReadByte();
			byte ret = (byte)(val ^ _key);
			byte[] bytes = new byte[1];
			bytes[0] = val;
			update_key(bytes);
			return ret;
		}

		public float read_float()
		{
			UInt32 i = read_int();
			byte[] bytes = BitConverter.GetBytes(i);
			float f = BitConverter.ToSingle(bytes, 0);
			return f;
		}

		public string read_str()
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

		public string read_wide_str()
		{
			uint len = read_int();
			if (len == 0)
			{
				return null;
			}
			StringBuilder sb = new StringBuilder((int)len);

			len = 2 * len;
			for (int i = 0; i < len; i += 2)
			{
				byte b1 = read_byte();
				byte b2 = read_byte();

				short s = b2;
				s = (short)(s << 8);

				char c = (char)(b1 | s);

				sb.Append(c);
			}
			String str = sb.ToString();

			return str;
		}

		public UInt32 read_block_start(ref GDBlock b)
		{
			UInt32 ret = read_int();
			b.len = next_int();
			b.end = (UInt32)File.BaseStream.Position + b.len;

			return ret;
		}

		public void read_block_end(ref GDBlock b)
		{
			if ((UInt32)File.BaseStream.Position != b.end)
				throw new IOException();

			if (next_int() != 0)
				throw new IOException();
		}
	}
}
