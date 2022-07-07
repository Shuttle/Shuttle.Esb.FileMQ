using System;
using System.IO;

namespace Shuttle.Esb.FileMQ.Tests
{
	public static class FileMQExtensions
	{
		public static string FileUri()
		{
			return
				$@"filemq://{Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."))}\test-queues\{{0}}";
		}
	}
}