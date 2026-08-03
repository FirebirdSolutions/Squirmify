using System;
using System.IO;

namespace Squirmify.Core;

public static class SquirmifyPaths
{
	public static string ResolveDataDirectory(string[]? args = null)
	{
		string environmentVariable = Environment.GetEnvironmentVariable("SQUIRMIFY_DATA");
		if (!string.IsNullOrWhiteSpace(environmentVariable))
		{
			return EnsureDirectory(environmentVariable);
		}
		if (args != null)
		{
			for (int i = 0; i < args.Length - 1; i++)
			{
				string text = args[i];
				if ((text == "--data-dir" || text == "-d") ? true : false)
				{
					return EnsureDirectory(args[i + 1]);
				}
			}
		}
		return EnsureDirectory(Path.Combine(Directory.GetCurrentDirectory(), "data"));
	}

	public static string GetConnectionString(string dataDirectory)
	{
		string text = Path.Combine(dataDirectory, "squirmify.db");
		return "Data Source=" + text;
	}

	public static string GetConnectionString(string[]? args = null)
	{
		string dataDirectory = ResolveDataDirectory(args);
		return GetConnectionString(dataDirectory);
	}

	private static string EnsureDirectory(string path)
	{
		string fullPath = Path.GetFullPath(path);
		Directory.CreateDirectory(fullPath);
		return fullPath;
	}
}
