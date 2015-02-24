//   Program.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri
//
// This work is licensed under the Creative Commons Attribution 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/ 
// or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

using System;
using System.Reflection;
using System.IO;
using System.Text;

namespace DecodeResources
{
	class MainClass
	{
		internal static byte[] read_stream(Stream stream)
		{
			byte b = (byte)stream.ReadByte();
			b = (byte)~b;
			for (int i = 1; i < 2; i++)
				stream.ReadByte();
			var array = new byte[stream.Length - stream.Position];
			stream.Read(array, 0, array.Length);
			if((b & 32) != 0)
			{
				for (int j = 0; j < array.Length; j++)
					array[j] = (byte)~array[j];
			}
			return array;
		}

		internal static string get_string(byte[] res, int str)
		{
			int num;
			if ((res[str] & 128) == 0)
			{
				num = (int)res[str];
				str++;
			}
			else
			{
				if ((res[str] & 64) == 0)
				{
					num = ((int)res[str] & -129) << 8;
					num |= (int)res[str + 1];
					str += 2;
				}
				else
				{
					num = ((int)res[str] & -193) << 24;
					num |= (int)res[str + 1] << 16;
					num |= (int)res[str + 2] << 8;
					num |= (int)res[str + 3];
					str += 4;
				}
			}
			if (num < 1) return string.Empty;
			string @string = Encoding.Unicode.GetString(res, str, num);
			return string.Intern(@string);
		}

		public static void Main(string[] args)
		{
			int str = 0;
			if(args.Length > 0) 
				int.TryParse(args[0], out str);
			var assembly = Assembly.GetAssembly(typeof(Vessel));
			using(Stream stream = assembly.GetManifestResourceStream("Assembly-CSharpAssembly-CSharp"))
			{
				var res = read_stream(stream);
				Console.WriteLine(get_string(res, str));
			}
		}
	}
}
