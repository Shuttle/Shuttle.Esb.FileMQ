using NUnit.Framework;
using Shuttle.ESB.Tests;

namespace Shuttle.ESB.FileMQ.Tests
{
	public class FileDistributorTest : DistributorFixture
	{
		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public void Should_be_able_to_distribute_messages(bool isTransactionalEndpoint)
		{
			TestDistributor(FileMQExtensions.FileUri(), isTransactionalEndpoint);
		}
	}
}