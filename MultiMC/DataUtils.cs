﻿// 
//  Copyright 2012  Andrew Okin
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace MultiMC
{
	/// <summary>
	/// Provides many useful methods for handling / converting data.
	/// </summary>
	public static class DataUtils
	{
		private static readonly char[] HexLowerChars = new[] 
		{ 
			'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f'
		};
		
		/// <summary>
		/// Gets a string representation of <paramref name="rawbytes"/>
		/// </param>
		public static string HexEncode(byte[] rawbytes)
		{
			int length = rawbytes.Length;
			char[] chArray = new char[2 * length];
			int index = 0;
			int num3 = 0;

			while (index < length)
			{
				chArray[num3++] = HexLowerChars[rawbytes[index] >> 4];
				chArray[num3++] = HexLowerChars[rawbytes[index] & 15];
				index++;
			}
			return new string(chArray);
		}
		
		public static byte[] BytesFromString(string str)
		{
			return new UTF8Encoding().GetBytes(str);
		}
		
		public static string ArrayToString(IEnumerable array, string separator = ", ")
		{
			StringBuilder builder = new StringBuilder();
			foreach (object obj in array)
				builder.Append(string.Format("{0}{1}", obj.ToString(), separator));
			builder.Length--;
			return builder.ToString();
		}

		public static bool TryParse<T>(string valueStr, out T value)
		{
			// ಠ_ಠ
			Type parsingType = typeof(T);
			System.Reflection.MethodInfo parseMethod =
				parsingType.GetMethod("Parse", new Type[] { typeof(string) });

			if (parseMethod == null)
			{
				value = default(T);
				return false;
			}

			try
			{
				try
				{
					value = (T)parseMethod.Invoke(null, new object[] { valueStr });
				}
				catch (TargetInvocationException e)
				{
					throw e.InnerException;
				}
			}
			catch(FormatException)
			{
				value = default(T);
				return false;
			}
			catch (InvalidCastException)
			{
				value = default(T);
				return false;
			}
			return true;
		}

		public static IEnumerable<T> Where<T>(this IEnumerable list, Predicate<T> predicate)
		{
			List<T> results = new List<T>();
			foreach (T item in list)
			{
				if (predicate(item))
					results.Add(item);
			}
			return results;
		}

		public static string ConvertLineEndings(string text, string newLineFormat = null)
		{
			if (string.IsNullOrEmpty(newLineFormat))
			{
				newLineFormat = Environment.NewLine;
			}

			List<string> lines = new List<string>();

			for (int i = 0; i < text.Length; i++)
			{
				// If it's CRLF (\r\n)
				if (text.Length >= i + 1 && text[i] == '\r' && text[i + 1] == '\n')
				{
					// Move the line into the lines list and remove it from the string.
					lines.Add(text.Substring(0, i));
					text = text.Remove(0, i + 2);
					i = -1;
				}

				// If it's CR (\r)
				else if (text[i] == '\r')
				{
					// Move the line into the lines list and remove it from the string.
					lines.Add(text.Substring(0, i));
					text = text.Remove(0, i + 1);
					i = -1;
				}
				
				// If it's LF (\n)
				else if (text[i] == '\n')
				{
					// Move the line into the lines list and remove it from the string.
					lines.Add(text.Substring(0, i));
					text = text.Remove(0, i + 1);
					i = -1;
				}
			}
			lines.Add(text);

			return string.Join(newLineFormat, lines);
		}
	}
}

