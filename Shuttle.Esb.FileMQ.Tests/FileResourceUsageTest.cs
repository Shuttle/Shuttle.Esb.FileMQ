using NUnit.Framework;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.FileMQ.Tests
{
	public class FileResourceUsageTest : ResourceUsageFixture
	{
		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public void Should_not_exceeed_normal_resource_usage(bool isTransactionalEndpoint)
		{
			TestResourceUsage(FileMQExtensions.FileUri(), isTransactionalEndpoint);
		}
	}
}