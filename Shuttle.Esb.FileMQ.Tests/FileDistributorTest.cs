using NUnit.Framework;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.FileMQ.Tests
{
	public class FileDistributorTest : DistributorFixture
	{
		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public void Should_be_able_to_distribute_messages(bool isTransactionalEndpoint)
		{
			TestDistributor(FileMQFixture.GetServiceCollection(), FileMQFixture.GetServiceCollection(), FileMQExtensions.FileUri(), isTransactionalEndpoint, 300);
		}
	}
}